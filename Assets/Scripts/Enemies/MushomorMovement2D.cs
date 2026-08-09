using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Mushomor chase on X toward the player. Locks authored scene Y. Stops while attack/hurt owns the sprite.
    /// Idle when standing still / no chase; Walk while moving. Default art faces right; flipX when facing left.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MushomorMovement2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.8f;
        [SerializeField] private float chaseRange = 18f;
        [SerializeField] private float stopDistance = 0.2f;

        [Header("Idle Animation")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float idleFrameRate = 12f;
        [SerializeField] private bool pingPongIdle;

        [Header("Walk Animation")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool pingPongWalk;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private MushomorMeleeAttack2D attack;
        private MushomorHurtAnimator2D hurt;

        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private bool playingWalk;
        private bool isMoving;
        private float lockedWalkWorldY;
        private bool hasLockedWalkWorldY;
        private bool dead;

        public bool IsMoving => isMoving;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
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
            spriteRenderer = GetComponent<SpriteRenderer>();
            attack = GetComponent<MushomorMeleeAttack2D>();
            hurt = GetComponent<MushomorHurtAnimator2D>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            lockedWalkWorldY = body.position.y;
            hasLockedWalkWorldY = true;

            ResetAnimState(playingWalk: false);
            ApplyCurrentLocomotionSprite();
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
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
                body.linearVelocity = Vector2.zero;
                MaintainWalkHeight();
                return;
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                MaintainWalkHeight();
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
                body.linearVelocity = Vector2.zero;
                if (distanceX > 0.01f)
                {
                    FaceDirection(direction);
                }

                MaintainWalkHeight();
                return;
            }

            FaceDirection(direction);
            Vector2 position = body.position;
            position.x += direction * moveSpeed * Time.fixedDeltaTime;
            if (hasLockedWalkWorldY)
            {
                position.y = lockedWalkWorldY;
            }

            body.MovePosition(position);
            body.linearVelocity = new Vector2(direction * moveSpeed, 0f);
            isMoving = true;
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

        private void MaintainWalkHeight()
        {
            if (body == null || !hasLockedWalkWorldY)
            {
                return;
            }

            Vector2 position = body.position;
            if (Mathf.Abs(position.y - lockedWalkWorldY) > 0.0001f)
            {
                position.y = lockedWalkWorldY;
                body.MovePosition(position);
            }
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
