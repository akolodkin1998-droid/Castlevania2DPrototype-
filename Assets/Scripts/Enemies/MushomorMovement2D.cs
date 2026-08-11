using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Mushomor chase on X toward the player. Dynamic 2D physics keeps it on floors and outside walls.
    /// Idle when standing still / no chase; Walk while moving. Default art faces right; flipX when facing left.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MushomorMovement2D : MonoBehaviour
    {
        private const float StandardColliderWidthRatio = 0.55f;
        private const float StandardColliderHeightRatio = 0.76171875f;
        private const float StandardColliderOffsetHeightRatio = 0.45f;

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float chaseRange = 18f;
        [SerializeField] private float stopDistance = 0.2f;
        [SerializeField, Min(0f)] private float gravityScale = 3f;
        [SerializeField, Min(0.5f)] private float spawnGroundProbeDistance = 30f;

        [Header("Idle Animation")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float idleFrameRate = 12f;
        [SerializeField] private bool pingPongIdle;

        [Header("Walk Animation")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool pingPongWalk;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private MushomorMeleeAttack2D attack;
        private MushomorHurtAnimator2D hurt;

        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private bool playingWalk;
        private bool isMoving;
        private bool dead;
        private bool hasPlacedOnGround;
        private static PhysicsMaterial2D zeroFrictionMaterial;

        public bool IsMoving => isMoving;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Places the collider bottom on the nearest static floor below the spawn point.
        /// Used by scene instances and portal summons before normal physics takes over.
        /// </summary>
        public void PlaceOnGroundFromSpawn()
        {
            if (hasPlacedOnGround || body == null || bodyCollider == null)
            {
                return;
            }

            Physics2D.SyncTransforms();
            body.linearVelocity = Vector2.zero;
            TryPlaceColliderBottomOnGround(spawnGroundProbeDistance);
            hasPlacedOnGround = true;
        }

        public void SetDead(bool value)
        {
            dead = value;
            if (dead && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>Restores the first idle (or walk) frame after hurt/attack yields the SpriteRenderer.</summary>
        public void RestoreIdleSprite()
        {
            ResetAnimState(playingWalk: false);
            ApplyCurrentLocomotionSprite();
        }

#if UNITY_EDITOR
        public void EditorAssignIdleFrames(Sprite[] frames)
        {
            idleFrames = frames ?? System.Array.Empty<Sprite>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyCurrentLocomotionSprite();
        }

        public void EditorAssignWalkFrames(Sprite[] frames)
        {
            walkFrames = frames ?? System.Array.Empty<Sprite>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if ((idleFrames == null || idleFrames.Length == 0) &&
                walkFrames.Length > 0 &&
                walkFrames[0] != null &&
                spriteRenderer != null)
            {
                spriteRenderer.sprite = walkFrames[0];
            }
        }
#endif

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            attack = GetComponent<MushomorMeleeAttack2D>();
            hurt = GetComponent<MushomorHurtAnimator2D>();
            ApplyStandardBodyCollider();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            ApplyZeroFrictionMaterial();

            ResetAnimState(playingWalk: false);
            ApplyCurrentLocomotionSprite();
        }

        private void ApplyStandardBodyCollider()
        {
            if (bodyCollider is not BoxCollider2D box ||
                spriteRenderer == null ||
                spriteRenderer.sprite == null)
            {
                return;
            }

            Bounds spriteBounds = spriteRenderer.sprite.bounds;
            box.isTrigger = false;
            box.size = new Vector2(
                Mathf.Max(0.4f, spriteBounds.size.x * StandardColliderWidthRatio),
                Mathf.Max(0.5f, spriteBounds.size.y * StandardColliderHeightRatio));
            box.offset = new Vector2(
                spriteBounds.center.x,
                spriteBounds.min.y + spriteBounds.size.y * StandardColliderOffsetHeightRatio);
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
            PlaceOnGroundFromSpawn();
        }

        private void Update()
        {
            if (dead || OwnsSpriteBlocked())
            {
                return;
            }

            if (isMoving)
            {
                if (!playingWalk)
                {
                    ResetAnimState(playingWalk: true);
                }

                AdvanceFrames(walkFrames, frameRate, pingPongWalk, Time.deltaTime);
                return;
            }

            if (playingWalk)
            {
                ResetAnimState(playingWalk: false);
            }

            Sprite[] frames = HasIdleFrames() ? idleFrames : walkFrames;
            float rate = HasIdleFrames() ? idleFrameRate : frameRate;
            bool pingPong = HasIdleFrames() ? pingPongIdle : pingPongWalk;
            AdvanceFrames(frames, rate, pingPong, Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (dead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (OwnsSpriteBlocked())
            {
                isMoving = false;
                StopHorizontalMotion();
                return;
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                isMoving = false;
                StopHorizontalMotion();
                return;
            }

            float deltaX = target.position.x - transform.position.x;
            float distanceX = Mathf.Abs(deltaX);
            float direction = Mathf.Sign(deltaX);
            float holdRange = attack != null
                ? Mathf.Max(stopDistance, attack.AttackTriggerRange)
                : stopDistance;

            // Hold ground inside melee range so attack can play; chase only while farther.
            if (distanceX > chaseRange || distanceX <= holdRange)
            {
                isMoving = false;
                StopHorizontalMotion();
                if (distanceX > 0.01f)
                {
                    FaceDirection(direction);
                }

                return;
            }

            FaceDirection(direction);
            Vector2 velocity = body.linearVelocity;
            velocity.x = direction * moveSpeed;
            body.linearVelocity = velocity;
            isMoving = true;
        }

        private void StopHorizontalMotion()
        {
            if (body == null)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }

        private void ApplyZeroFrictionMaterial()
        {
            if (bodyCollider == null)
            {
                return;
            }

            if (zeroFrictionMaterial == null)
            {
                zeroFrictionMaterial = new PhysicsMaterial2D("MushomorZeroFriction")
                {
                    friction = 0f,
                    bounciness = 0f,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            bodyCollider.sharedMaterial = zeroFrictionMaterial;
        }

        private bool TryPlaceColliderBottomOnGround(float probeDistance)
        {
            Bounds bounds = bodyCollider.bounds;
            float startBottomY = bounds.min.y;
            float castTop = Mathf.Max(bounds.max.y + 2f, body.position.y + 6f);
            float castDistance = Mathf.Max(0.5f, probeDistance) + (castTop - startBottomY);
            const float castHeight = 0.02f;
            const float groundSkin = 0.01f;
            Vector2 castOrigin = new Vector2(bounds.center.x, castTop);
            Vector2 castSize = new Vector2(
                Mathf.Max(0.14f, bounds.size.x * 0.35f),
                castHeight);

            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                castOrigin,
                castSize,
                0f,
                Vector2.down,
                castDistance);

            float bestSurfaceY = float.NegativeInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (!IsValidSpawnFloor(hit))
                {
                    continue;
                }

                float surfaceY = castOrigin.y - hit.distance - castHeight * 0.5f;
                // Never snap upward onto a platform above the spawn point.
                if (surfaceY > startBottomY + 0.25f)
                {
                    continue;
                }

                if (surfaceY > bestSurfaceY)
                {
                    bestSurfaceY = surfaceY;
                }
            }

            if (float.IsNegativeInfinity(bestSurfaceY))
            {
                return false;
            }

            float targetBottomY = bestSurfaceY + groundSkin;
            body.position += new Vector2(0f, targetBottomY - startBottomY);
            Physics2D.SyncTransforms();
            return true;
        }

        private bool IsValidSpawnFloor(RaycastHit2D hit)
        {
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null ||
                hitCollider.isTrigger ||
                hit.normal.y < 0.6f ||
                hitCollider == bodyCollider ||
                hitCollider.transform == transform ||
                hitCollider.transform.IsChildOf(transform))
            {
                return false;
            }

            if (hitCollider.GetComponentInParent<WizardPortal2D>() != null)
            {
                return false;
            }

            Rigidbody2D hitBody = hit.rigidbody;
            return hitBody == null ||
                   hitBody.bodyType == RigidbodyType2D.Static ||
                   hitCollider is CompositeCollider2D ||
                   hitCollider.compositeOperation != Collider2D.CompositeOperation.None;
        }

        private bool OwnsSpriteBlocked()
        {
            return (attack != null && attack.IsAttacking) ||
                   (hurt != null && (hurt.IsPlayingHurt || hurt.IsPlayingDeath));
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null || string.IsNullOrEmpty(playerObjectName))
            {
                return;
            }

            GameObject player = GameObject.Find(playerObjectName);
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void FaceDirection(float horizontalSign)
        {
            if (spriteRenderer == null || Mathf.Abs(horizontalSign) <= 0.01f)
            {
                return;
            }

            // Walk/attack art faces right by default; flip when facing left.
            spriteRenderer.flipX = horizontalSign < 0f;
        }

        private bool HasIdleFrames()
        {
            return idleFrames != null && idleFrames.Length > 0;
        }

        private void ResetAnimState(bool playingWalk)
        {
            this.playingWalk = playingWalk;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
        }

        private void ApplyCurrentLocomotionSprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Sprite[] frames = playingWalk
                ? walkFrames
                : (HasIdleFrames() ? idleFrames : walkFrames);

            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                return;
            }

            spriteRenderer.sprite = frames[0];
        }

        private void AdvanceFrames(Sprite[] frames, float rate, bool pingPong, float deltaTime)
        {
            if (frames == null || frames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            if (frames.Length == 1)
            {
                if (frames[0] != null)
                {
                    spriteRenderer.sprite = frames[0];
                }

                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, rate);
            frameTimer += deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                StepFrame(frames.Length, pingPong);
            }

            if (frameIndex >= 0 && frameIndex < frames.Length && frames[frameIndex] != null)
            {
                spriteRenderer.sprite = frames[frameIndex];
            }
        }

        private void StepFrame(int frameCount, bool pingPong)
        {
            if (pingPong)
            {
                frameIndex += frameDirection;
                if (frameIndex >= frameCount - 1)
                {
                    frameIndex = frameCount - 1;
                    frameDirection = -1;
                }
                else if (frameIndex <= 0)
                {
                    frameIndex = 0;
                    frameDirection = 1;
                }

                return;
            }

            frameIndex++;
            if (frameIndex >= frameCount)
            {
                frameIndex = 0;
            }
        }
    }
}
