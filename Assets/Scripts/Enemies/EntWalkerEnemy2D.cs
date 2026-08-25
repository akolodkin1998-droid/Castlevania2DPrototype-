using System.Text.RegularExpressions;
using Castlevania2D.Combat;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Ent (Древень): chase on X while far; in attack range stop and play attack
    /// frames 1→19 fast, then 19→32 at normal FPS (fire on 32), then loop 19→32.
    /// On death: play death frames once forward, then Destroy (Health.destroyOnDeath false).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EntWalkerEnemy2D : MonoBehaviour
    {
        private enum AnimMode
        {
            IdleOrWalk,
            AttackWindup,
            AttackLoop,
            Death,
        }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.6f;
        [SerializeField] private float chaseRange = 24f;
        [Tooltip("Maximum vertical distance from the target for horizontal aggro/chase.")]
        [SerializeField] private float chaseVerticalRange = 5f;
        [SerializeField] private float attackRange = 5.5f;
        [Tooltip("Maximum vertical distance from the target for starting or firing an attack.")]
        [SerializeField] private float attackVerticalRange = 3f;
        [SerializeField] private float stopDistance = 0.35f;
        [SerializeField] private float gravityScale = 2.5f;
        /// <summary>Shared local capsule size for scene and portal-summoned Ents.</summary>
        public static readonly Vector2 StandardBodyColliderSize = new Vector2(5.625f, 8f);
        public static readonly Vector2 StandardBodyColliderOffset = new Vector2(0f, 5.5f);

        [Header("Body Collider")]
        [Tooltip("Fixed local capsule size for every Ent.")]
        [SerializeField] private Vector2 bodyColliderSize = new Vector2(5.625f, 8f);
        [SerializeField] private Vector2 bodyColliderOffset = new Vector2(0f, 5.5f);
        [Tooltip("Legacy: unused when fixed bodyColliderSize is applied.")]
        [SerializeField] private float physicsFeetPaddingLocal = 1f;
        [Tooltip("Legacy: unused when fixed bodyColliderSize is applied.")]
        [SerializeField] private float feetGroundSinkLocal = 0f;
        [Tooltip("Legacy: unused when fixed bodyColliderSize is applied.")]
        [SerializeField] private float physicsFeetWidthScale = 0.85f;
        [Tooltip("Legacy: unused when fixed bodyColliderSize is applied.")]
        [SerializeField] private float physicsMinFeetWidthWorld = 1.25f;
        [Tooltip("Downward box-cast distance (world units) to catch / unstick from ground when contacts miss tile seams.")]
        [SerializeField] private float groundProbeDistanceWorld = 0.22f;
        [Tooltip("Legacy serialized flag. Ent now always preserves the exact scene-authored foot line during normal grounded life.")]
        [SerializeField] private bool lockWalkHeightToScenePlacement;
        [Tooltip("How far down to look for platform before releasing the walk-Y lock (ledge drop).")]
        [SerializeField] private float walkHeightLockProbeDistanceWorld = 0.55f;
        [Tooltip("Awake/Start cast distance to find ground and plant opaque feet before locking walk Y.")]
        [SerializeField] private float snapToGroundProbeDistanceWorld = 4f;
        [Tooltip("How long wall-collision recovery can push the Ent away before chase takes over again.")]
        [SerializeField] private float ledgeTurnDuration = 0.35f;

        [Header("Walk Animation")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool pingPongWalk;

        [Header("Attack Animation")]
        [SerializeField] private Sprite[] attackFrames;
        [Tooltip("FPS for frames before the loop start (1→19). From frame 19 onward uses frameRate.")]
        [SerializeField] private float attackWindupFrameRate = 20f;
        [Tooltip("Source frame number that fires the projectile (Ent_Attack_032).")]
        [SerializeField] private int projectileFireFrameNumber = 32;
        [Tooltip("Source frame number where the attack loop restarts (Ent_Attack_019).")]
        [SerializeField] private int attackLoopStartFrameNumber = 19;

        [Header("Hit Feedback")]
        [SerializeField] private Sprite hurtSprite;
        [SerializeField] private float damageFlashDuration = 0.12f;
        [Tooltip("Unused (kept for serialized compatibility). Hurt flash no longer height-matches via localScale — that caused a vertical jump.")]
        [SerializeField] private float hurtFlashScaleMultiplier = 1f;
        [Tooltip("Optional fine-tune added to locked feet world Y during hurt. Keep 0; bounds lock handles Attack/Walk vs Hurt canvas padding.")]
        [SerializeField] private float hurtFlashYOffset = 0f;

        [Header("Death Animation")]
        [SerializeField] private Sprite[] deathSprites;

        [Header("Projectile")]
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private float projectileSpeed = 6f;
        [SerializeField] private float projectileScale = 0.35f / 3f;
        [SerializeField] private float projectileSpinDegreesPerSecond = 360f;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.55f, 1.1f);
        [Tooltip("Height above the target pivot used when no target Collider2D is available.")]
        [SerializeField] private float targetAimHeight = 0.9f;
        [SerializeField] private float projectileColliderRadius = 0.18f;
        [SerializeField] private int projectileSortingOrder = 4;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Collider2D bodyCollider;
        private EnemyHealth health;
        private ContactDamage2D contactDamage;
        private Transform colliderTarget;
        private Collider2D targetCollider;

        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private bool dead;
        private bool isMoving;
        private AnimMode animMode = AnimMode.IdleOrWalk;
        private int fireFrameIndex = -1;
        private int loopStartFrameIndex = -1;
        private int lastAttackFrameShown = -1;
        private bool firedOnCurrentVisit;
        private float flashTimerRemaining;
        private Vector3 localScaleBeforeFlash;
        private float positionYBeforeFlash;
        private float lockedFeetWorldY;
        private bool flashAlteredScale;
        private bool flashLockedFeet;
        private bool hasDisappeared;
        private float lockedWalkWorldY;
        private bool hasLockedWalkWorldY;
        private bool capsuleGroundPlanted;
        private float sceneAuthoredFeetWorldY;
        private bool hasSceneAuthoredFeetWorldY;
        private float wallRecoveryTimerRemaining;
        private float wallRecoveryDirection;
        private const float WallRecoveryDuration = 0.25f;
        private const float HorizontalCastSkin = 0.02f;
        private const float CapsuleGroundSkin = 0.02f;

        private static PhysicsMaterial2D zeroFrictionMaterial;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            CacheTargetCollider();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            bodyCollider = GetComponent<Collider2D>();
            health = GetComponent<EnemyHealth>();
            contactDamage = GetComponent<ContactDamage2D>();
            CacheTargetCollider();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            EnsureCapsuleBodyCollider();
            ApplyZeroFrictionMaterial();

            if (bodyCollider != null)
            {
                bodyCollider.isTrigger = false;
            }

            ResolveAttackFrameIndices();

            if (walkFrames != null && walkFrames.Length > 0 && spriteRenderer != null)
            {
                spriteRenderer.sprite = walkFrames[0];
            }

            // Capsule only — plant onto ground in Start / PlaceOnGroundFromSpawn (Physics2D ready).
            ApplyStandardBodyCollider();
            Physics2D.SyncTransforms();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        /// <summary>Forces the shared capsule size used by scene Ents and portal summons.</summary>
        public void ApplyStandardBodyCollider()
        {
            bodyColliderSize = StandardBodyColliderSize;
            bodyColliderOffset = StandardBodyColliderOffset;
            if (bodyCollider == null)
            {
                EnsureCapsuleBodyCollider();
            }

            ApplyFixedBodyCollider();
        }

        /// <summary>
        /// Portal spawn: shared capsule, plant capsule bottom on floor under the portal, lock walk Y.
        /// </summary>
        public void PlaceOnGroundFromSpawn()
        {
            ApplyStandardBodyCollider();
            Physics2D.SyncTransforms();
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            PlantCapsuleOnGroundAndLock();
        }

        /// <summary>Obsolete alias kept for portal call sites / older compiles.</summary>
        public void BeginCapsuleGroundSettle()
        {
            PlaceOnGroundFromSpawn();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            EndDamageFlash();
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
            // Scene Ents plant here; portal Ents already planted in PlaceOnGroundFromSpawn.
            PlantCapsuleOnGroundAndLock();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (dead)
            {
                if (animMode == AnimMode.Death)
                {
                    AdvanceDeathAnimation(deltaTime);
                }

                return;
            }

            if (animMode == AnimMode.AttackWindup || animMode == AnimMode.AttackLoop)
            {
                AdvanceAttackAnimation(deltaTime);
                return;
            }

            if (!isMoving || walkFrames == null || walkFrames.Length == 0)
            {
                return;
            }

            AdvanceWalkAnimation(deltaTime);
        }

        private void LateUpdate()
        {
            if (dead || spriteRenderer == null || flashTimerRemaining <= 0f)
            {
                return;
            }

            flashTimerRemaining -= Time.deltaTime;
            if (flashTimerRemaining > 0f)
            {
                // Keep anim clocks advancing in Update; force hurt sprite and re-lock feet each frame.
                if (hurtSprite != null)
                {
                    spriteRenderer.sprite = hurtSprite;
                    spriteRenderer.color = Color.white;
                }

                LockHurtFeetToRecordedY();
                return;
            }

            EndDamageFlash();
        }

        private void FixedUpdate()
        {
            if (dead)
            {
                return;
            }

            if (wallRecoveryTimerRemaining > 0f)
            {
                wallRecoveryTimerRemaining = Mathf.Max(0f, wallRecoveryTimerRemaining - Time.fixedDeltaTime);
                if (wallRecoveryTimerRemaining <= 0f)
                {
                    wallRecoveryDirection = 0f;
                }
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                if (animMode != AnimMode.IdleOrWalk)
                {
                    ExitAttackToWalk();
                }

                MaintainWalkHeight();
                return;
            }

            float deltaX = target.position.x - transform.position.x;
            float deltaY = target.position.y - transform.position.y;
            float distanceX = Mathf.Abs(deltaX);
            float distanceY = Mathf.Abs(deltaY);
            float maxChaseY = EnemyAggroLimits.CapVerticalRange(chaseVerticalRange);
            float maxAttackY = EnemyAggroLimits.CapVerticalRange(attackVerticalRange);
            float direction = Mathf.Sign(deltaX);
            bool inChase = distanceX <= chaseRange &&
                           distanceY <= maxChaseY &&
                           distanceX > stopDistance;
            bool inAttack = HasAttackFrames() &&
                            distanceX <= attackRange &&
                            distanceY <= maxAttackY;

            if (inAttack)
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                if (animMode == AnimMode.IdleOrWalk)
                {
                    BeginAttack();
                }

                MaintainWalkHeight();
                return;
            }

            if (animMode != AnimMode.IdleOrWalk)
            {
                ExitAttackToWalk();
            }

            if (!inChase)
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                MaintainWalkHeight();
                return;
            }

            float moveDirection = GetMoveDirection(direction);
            FaceDirection(moveDirection);

            if (Mathf.Abs(moveDirection) <= 0.01f)
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                MaintainWalkHeight();
                return;
            }

            float stepDistance = moveSpeed * Time.fixedDeltaTime;
            if (!TryMoveHorizontally(moveDirection, stepDistance))
            {
                isMoving = false;
                body.linearVelocity = Vector2.zero;
                MaintainWalkHeight();
                return;
            }

            isMoving = true;
            MaintainWalkHeight();
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (dead || body == null)
            {
                return;
            }

            float velocityX = body.linearVelocity.x;
            if (Mathf.Abs(velocityX) <= 0.001f)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector2 normal = collision.GetContact(i).normal;
                if (normal.y > 0.4f || Mathf.Abs(normal.x) < 0.4f)
                {
                    continue;
                }

                if (Mathf.Sign(velocityX) == -Mathf.Sign(normal.x))
                {
                    TriggerWallRecovery(normal.x);
                    return;
                }
            }
        }

        private bool HasAttackFrames()
        {
            return attackFrames != null && attackFrames.Length > 0 && fireFrameIndex >= 0;
        }

        private void BeginAttack()
        {
            animMode = AnimMode.AttackWindup;
            frameIndex = 0;
            frameTimer = 0f;
            lastAttackFrameShown = -1;
            firedOnCurrentVisit = false;
            ApplyAttackFrame(0);
        }

        private void ExitAttackToWalk()
        {
            animMode = AnimMode.IdleOrWalk;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            lastAttackFrameShown = -1;
            firedOnCurrentVisit = false;

            if (walkFrames != null && walkFrames.Length > 0 && spriteRenderer != null)
            {
                spriteRenderer.sprite = walkFrames[0];
            }
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null)
            {
                if (colliderTarget != target)
                {
                    CacheTargetCollider();
                }

                return;
            }

            ClearTargetCollider();
            if (string.IsNullOrEmpty(playerObjectName))
            {
                return;
            }

            GameObject player = GameObject.Find(playerObjectName);
            if (player != null)
            {
                SetTarget(player.transform);
            }
        }

        private void CacheTargetCollider()
        {
            colliderTarget = target;
            targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
            if (targetCollider == null && target != null)
            {
                targetCollider = target.GetComponentInChildren<Collider2D>();
            }
        }

        private void ClearTargetCollider()
        {
            colliderTarget = null;
            targetCollider = null;
        }

        private void FaceDirection(float horizontalSign)
        {
            if (spriteRenderer == null || Mathf.Abs(horizontalSign) <= 0.01f)
            {
                return;
            }

            // Walk/attack art faces left by default; flip when facing right.
            spriteRenderer.flipX = horizontalSign > 0f;
        }

        private void AdvanceWalkAnimation(float deltaTime)
        {
            if (walkFrames.Length == 1)
            {
                spriteRenderer.sprite = walkFrames[0];
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                StepWalkFrame();
            }

            spriteRenderer.sprite = walkFrames[frameIndex];
        }

        private void StepWalkFrame()
        {
            if (pingPongWalk)
            {
                frameIndex += frameDirection;
                if (frameIndex >= walkFrames.Length - 1)
                {
                    frameIndex = walkFrames.Length - 1;
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
            if (frameIndex >= walkFrames.Length)
            {
                frameIndex = 0;
            }
        }

        private void AdvanceAttackAnimation(float deltaTime)
        {
            if (!HasAttackFrames() || spriteRenderer == null)
            {
                return;
            }

            frameTimer += deltaTime;
            // Fast rate only before loop-start (frames 1→19); 19→32 and the loop use frameRate.
            while (true)
            {
                bool inPreLoopWindup = loopStartFrameIndex >= 0 && frameIndex < loopStartFrameIndex;
                float rate = inPreLoopWindup ? attackWindupFrameRate : frameRate;
                float frameDuration = 1f / Mathf.Max(1f, rate);
                if (frameTimer < frameDuration)
                {
                    break;
                }

                frameTimer -= frameDuration;
                StepAttackFrame();
                ApplyAttackFrame(frameIndex);
            }
        }

        private void StepAttackFrame()
        {
            if (animMode == AnimMode.AttackWindup)
            {
                if (frameIndex >= fireFrameIndex)
                {
                    // Reached fire frame on windup; next step enters loop from loop-start.
                    animMode = AnimMode.AttackLoop;
                    frameIndex = loopStartFrameIndex;
                    firedOnCurrentVisit = false;
                    return;
                }

                frameIndex++;
                return;
            }

            // AttackLoop: 19 → 32 inclusive, then wrap to 19.
            if (frameIndex >= fireFrameIndex)
            {
                frameIndex = loopStartFrameIndex;
                firedOnCurrentVisit = false;
                return;
            }

            frameIndex++;
        }

        private void ApplyAttackFrame(int index)
        {
            if (attackFrames == null || index < 0 || index >= attackFrames.Length)
            {
                return;
            }

            spriteRenderer.sprite = attackFrames[index];

            if (index != lastAttackFrameShown)
            {
                lastAttackFrameShown = index;
                firedOnCurrentVisit = false;
            }

            if (index == fireFrameIndex && !firedOnCurrentVisit)
            {
                firedOnCurrentVisit = true;
                FireProjectileAtTarget();
            }
        }

        private void FireProjectileAtTarget()
        {
            if (projectileSprite == null ||
                projectileSpeed <= 0f ||
                target == null ||
                Mathf.Abs(target.position.y - transform.position.y) > attackVerticalRange ||
                Mathf.Abs(target.position.x - transform.position.x) > attackRange)
            {
                return;
            }

            float facingSign = spriteRenderer != null && spriteRenderer.flipX ? 1f : -1f;
            Vector3 spawnPosition = transform.position + new Vector3(
                projectileSpawnOffset.x * facingSign,
                projectileSpawnOffset.y,
                0f);

            Vector2 direction = new Vector2(facingSign, 0f);
            Vector2 toTarget = GetTargetAimPoint() - (Vector2)spawnPosition;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                direction = toTarget.normalized;
            }

            GameObject projectileObject = new GameObject("EntMudProjectile");
            projectileObject.transform.position = spawnPosition;
            float scale = Mathf.Max(0.01f, projectileScale);
            projectileObject.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            projectileRenderer.sprite = projectileSprite;
            projectileRenderer.sortingOrder = projectileSortingOrder;

            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = Mathf.Max(0.05f, projectileColliderRadius);

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            projectileBody.freezeRotation = projectileSpinDegreesPerSecond <= 0f;

            EnemyProjectile2D projectile = projectileObject.AddComponent<EnemyProjectile2D>();
            projectile.ConfigureSpin(projectileSpinDegreesPerSecond);
            projectile.Launch(gameObject, direction, projectileDamage, projectileSpeed);
        }

        private Vector2 GetTargetAimPoint()
        {
            if (targetCollider != null && targetCollider.enabled)
            {
                return (Vector2)targetCollider.bounds.center;
            }

            return (Vector2)target.position + Vector2.up * targetAimHeight;
        }

        private void ResolveAttackFrameIndices()
        {
            fireFrameIndex = -1;
            loopStartFrameIndex = -1;

            if (attackFrames == null || attackFrames.Length == 0)
            {
                return;
            }

            for (int i = 0; i < attackFrames.Length; i++)
            {
                Sprite sprite = attackFrames[i];
                if (sprite == null || !TryParseFrameNumber(sprite.name, out int number))
                {
                    continue;
                }

                if (number == projectileFireFrameNumber)
                {
                    fireFrameIndex = i;
                }

                if (number == attackLoopStartFrameNumber)
                {
                    loopStartFrameIndex = i;
                }
            }

            if (fireFrameIndex < 0)
            {
                fireFrameIndex = attackFrames.Length - 1;
            }

            if (loopStartFrameIndex < 0)
            {
                loopStartFrameIndex = 0;
            }

            if (loopStartFrameIndex > fireFrameIndex)
            {
                loopStartFrameIndex = 0;
            }
        }

        private static bool TryParseFrameNumber(string spriteName, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            Match match = Regex.Match(spriteName, @"(\d+)(?!.*\d)");
            return match.Success && int.TryParse(match.Groups[1].Value, out number);
        }

        private void OnDamaged(DamageInfo _)
        {
            // Skip flash when already dead or on the killing blow (HP already 0 before Died).
            if (dead || health == null || !health.IsAlive || spriteRenderer == null || hurtSprite == null)
            {
                return;
            }

            BeginDamageFlash();
        }

        private void OnDied()
        {
            if (dead)
            {
                return;
            }

            dead = true;
            EnemyDeathEvents.Raise(EnemyDeathKind.Ent);
            isMoving = false;
            EndDamageFlash();

            // Stop chase/attack/projectile spawning; keep facing from death moment.
            if (contactDamage != null)
            {
                contactDamage.enabled = false;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                body.simulated = false;
            }

            BeginDeathAnimation();
        }

        private void BeginDeathAnimation()
        {
            if (deathSprites == null || deathSprites.Length == 0)
            {
                DisappearAfterDeath();
                return;
            }

            animMode = AnimMode.Death;
            frameIndex = 0;
            frameTimer = 0f;
            if (spriteRenderer != null && deathSprites[0] != null)
            {
                spriteRenderer.sprite = deathSprites[0];
                spriteRenderer.color = Color.white;
            }
        }

        private void AdvanceDeathAnimation(float deltaTime)
        {
            if (hasDisappeared || deathSprites == null || deathSprites.Length == 0)
            {
                DisappearAfterDeath();
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= deathSprites.Length)
                {
                    DisappearAfterDeath();
                    return;
                }

                if (spriteRenderer != null && deathSprites[frameIndex] != null)
                {
                    spriteRenderer.sprite = deathSprites[frameIndex];
                }
            }
        }

        private void DisappearAfterDeath()
        {
            if (hasDisappeared)
            {
                return;
            }

            hasDisappeared = true;
            // Health.destroyOnDeath stays false so we control removal after the last death frame.
            Destroy(gameObject);
        }

        private void BeginDamageFlash()
        {
            flashTimerRemaining = Mathf.Max(0.01f, damageFlashDuration);

            // Do not change localScale during hurt — height-match amplified canvas padding and caused a jump.
            spriteRenderer.sprite = hurtSprite;
            spriteRenderer.color = Color.white;
        }

        /// <summary>
        /// Hurt canvas has feet flush to bottom (pad_below=0) while Attack/Walk leave ~100px empty
        /// below opaque feet. Bottom-center pivot alone therefore sinks hurt. Nudge Y so
        /// SpriteRenderer.bounds.min.y matches the pre-flash feet world Y (+ optional fine offset).
        /// </summary>
        private void LockHurtFeetToRecordedY()
        {
            if (!flashLockedFeet || spriteRenderer == null)
            {
                return;
            }

            float targetFeetY = lockedFeetWorldY + hurtFlashYOffset;
            float deltaY = targetFeetY - spriteRenderer.bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.0001f)
            {
                return;
            }

            if (body != null)
            {
                Vector2 p = body.position;
                p.y += deltaY;
                body.position = p;
            }
            else
            {
                Vector3 p = transform.position;
                p.y += deltaY;
                transform.position = p;
            }
        }

        private void RestoreFlashScale()
        {
            if (!flashAlteredScale)
            {
                return;
            }

            transform.localScale = localScaleBeforeFlash;
            flashAlteredScale = false;
        }

        private void RestoreFlashPositionY()
        {
            flashLockedFeet = false;
        }

        private void EndDamageFlash()
        {
            if (flashTimerRemaining <= 0f && !flashAlteredScale && !flashLockedFeet)
            {
                return;
            }

            flashTimerRemaining = 0f;
            RestoreFlashScale();
            RestoreFlashPositionY();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                ApplyCurrentAnimationSprite();
            }
        }

        private void ApplyCurrentAnimationSprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (animMode == AnimMode.Death)
            {
                if (deathSprites != null && frameIndex >= 0 && frameIndex < deathSprites.Length &&
                    deathSprites[frameIndex] != null)
                {
                    spriteRenderer.sprite = deathSprites[frameIndex];
                }

                return;
            }

            if (animMode == AnimMode.AttackWindup || animMode == AnimMode.AttackLoop)
            {
                if (attackFrames != null && frameIndex >= 0 && frameIndex < attackFrames.Length &&
                    attackFrames[frameIndex] != null)
                {
                    spriteRenderer.sprite = attackFrames[frameIndex];
                }

                return;
            }

            if (walkFrames != null && walkFrames.Length > 0)
            {
                int index = Mathf.Clamp(frameIndex, 0, walkFrames.Length - 1);
                if (walkFrames[index] != null)
                {
                    spriteRenderer.sprite = walkFrames[index];
                }
            }
        }

        private void EnsureCapsuleBodyCollider()
        {
            var capsule = GetComponent<CapsuleCollider2D>();
            if (capsule == null)
            {
                var box = GetComponent<BoxCollider2D>();
                if (box == null)
                {
                    capsule = gameObject.AddComponent<CapsuleCollider2D>();
                }
                else
                {
                    bool wasTrigger = box.isTrigger;
                    PhysicsMaterial2D material = box.sharedMaterial;
                    capsule = gameObject.AddComponent<CapsuleCollider2D>();
                    capsule.direction = CapsuleDirection2D.Vertical;
                    capsule.isTrigger = wasTrigger;
                    if (material != null)
                    {
                        capsule.sharedMaterial = material;
                    }

                    box.enabled = false;
                    Destroy(box);
                }
            }

            capsule.direction = CapsuleDirection2D.Vertical;
            bodyCollider = capsule;
        }

        private void InitializeGroundedWalkHeight()
        {
            PlantCapsuleOnGroundAndLock();
        }

        /// <summary>
        /// One-shot: move so CapsuleCollider2D bottom rests on the nearest floor, then lock walk Y.
        /// Used by scene Ents (Start) and portal summons (PlaceOnGroundFromSpawn).
        /// </summary>
        private void PlantCapsuleOnGroundAndLock()
        {
            if (capsuleGroundPlanted || body == null)
            {
                return;
            }

            ApplyStandardBodyCollider();
            Physics2D.SyncTransforms();
            hasLockedWalkWorldY = false;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;

            float spawnY = body.position.y;
            float probe = Mathf.Max(snapToGroundProbeDistanceWorld, 5f);
            if (!TryPlantCapsuleBottomOnGround(probe))
            {
                body.position = new Vector2(body.position.x, spawnY);
            }

            LockWalkHeightToCurrentScenePosition();
            capsuleGroundPlanted = true;
        }

        /// <summary>
        /// Thin center cast from above — full Capsule.Cast fails at the right portal where the
        /// wide capsule overlaps the cliff face (Cast distance 0 → Ent never lowers onto the floor).
        /// </summary>
        private bool TryPlantCapsuleBottomOnGround(float maxDistanceWorld)
        {
            if (body == null || bodyCollider == null || !bodyCollider.enabled)
            {
                return false;
            }

            Physics2D.SyncTransforms();
            Bounds bounds = bodyCollider.bounds;
            float startBottomY = bounds.min.y;
            // Start well above so we still find the floor when spawned inside / against a ledge.
            float castTop = Mathf.Max(bounds.max.y + 2f, body.position.y + 6f);
            float maxDistance = Mathf.Max(0.5f, maxDistanceWorld) + (castTop - startBottomY);
            const float castHeight = 0.02f;
            Vector2 castOrigin = new Vector2(bounds.center.x, castTop);
            Vector2 castSize = new Vector2(0.14f, castHeight);

            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                castOrigin,
                castSize,
                0f,
                Vector2.down,
                maxDistance);

            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D candidate = hits[i];
                if (!IsValidFloorGeometryHit(candidate))
                {
                    continue;
                }

                if (candidate.distance < nearestDistance)
                {
                    nearestDistance = candidate.distance;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            float surfaceY = castOrigin.y - nearestDistance - (castHeight * 0.5f);
            float targetBottomY = surfaceY + CapsuleGroundSkin;
            float deltaY = targetBottomY - startBottomY;
            if (Mathf.Abs(deltaY) <= 0.0001f)
            {
                return true;
            }

            body.position += new Vector2(0f, deltaY);
            Physics2D.SyncTransforms();
            return true;
        }

        /// <summary>Floor tilemap / composite only — ignores baskets, cliffsides-as-walls handled by thin cast.</summary>
        private bool IsValidFloorGeometryHit(RaycastHit2D hit)
        {
            if (!IsValidGroundHit(hit))
            {
                return false;
            }

            Collider2D col = hit.collider;
            if (col is CompositeCollider2D || col.usedByComposite)
            {
                return true;
            }

            if (col.GetComponentInParent<Castlevania2D.Environment.StoneBasketHitStages2D>() != null ||
                col.GetComponentInParent<WizardPortal2D>() != null)
            {
                return false;
            }

            Rigidbody2D hitBody = hit.rigidbody;
            return hitBody != null && hitBody.bodyType == RigidbodyType2D.Static;
        }

        private bool TrySnapOpaqueFeetToGround(float probeDistanceWorld)
        {
            return TryPlantCapsuleBottomOnGround(probeDistanceWorld);
        }

        private void MaintainWalkHeight()
        {
            if (body == null)
            {
                return;
            }

            if (hasLockedWalkWorldY)
            {
                Vector2 position = body.position;
                if (Mathf.Abs(position.y - lockedWalkWorldY) > 0.0001f)
                {
                    position.y = lockedWalkWorldY;
                    body.MovePosition(position);
                }
            }
        }

        private float GetMoveDirection(float desiredDirection)
        {
            if (Mathf.Abs(desiredDirection) <= 0.01f)
            {
                return 0f;
            }

            if (wallRecoveryTimerRemaining > 0f && Mathf.Abs(wallRecoveryDirection) > 0.01f)
            {
                if (CanMoveInDirection(wallRecoveryDirection))
                {
                    return wallRecoveryDirection;
                }

                wallRecoveryTimerRemaining = 0f;
                wallRecoveryDirection = 0f;
                return 0f;
            }

            if (CanMoveInDirection(desiredDirection))
            {
                return desiredDirection;
            }

            float alternateDirection = -desiredDirection;
            if (CanMoveInDirection(alternateDirection))
            {
                wallRecoveryDirection = alternateDirection;
                wallRecoveryTimerRemaining = Mathf.Max(WallRecoveryDuration, ledgeTurnDuration);
                return alternateDirection;
            }

            return 0f;
        }

        private bool CanMoveInDirection(float moveDirection)
        {
            if (Mathf.Abs(moveDirection) <= 0.01f || bodyCollider == null)
            {
                return true;
            }

            if (HasHorizontalObstacle(moveDirection, Mathf.Max(moveSpeed * Time.fixedDeltaTime, 0.05f)))
            {
                return false;
            }

            // Ledge stop only — never snap Y / never hover. Y stays scene-locked.
            float groundProbe = Mathf.Max(groundProbeDistanceWorld, walkHeightLockProbeDistanceWorld, 0.55f);
            return TryProbeGroundAhead(moveDirection, groundProbe, out _);
        }

        private bool TryProbeGround(float probeDistanceWorld, out RaycastHit2D hit)
        {
            hit = default;
            if (bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            // Start ABOVE the foot line so we never cast from inside the tilemap (that misses and freezes AI).
            const float lift = 0.18f;
            float probeDistance = Mathf.Max(0.02f, probeDistanceWorld) + lift;
            Vector2 castSize = new Vector2(
                Mathf.Max(bounds.size.x * 0.7f, physicsMinFeetWidthWorld * 0.55f),
                0.04f);
            Vector2 castOrigin = new Vector2(bounds.center.x, bounds.min.y + lift);

            hit = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, probeDistance);
            return IsValidGroundHit(hit);
        }

        private bool TryProbeGroundAhead(float moveDirection, float probeDistanceWorld, out RaycastHit2D hit)
        {
            hit = default;
            if (bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            float horizontalSign = Mathf.Sign(moveDirection);
            float forwardCheckDistance = Mathf.Max(
                0.1f,
                Mathf.Abs(moveSpeed) * Time.fixedDeltaTime + 0.06f);
            float toeX = bounds.center.x + (horizontalSign * (bounds.extents.x + forwardCheckDistance));
            float midX = bounds.center.x + (horizontalSign * (bounds.extents.x * 0.55f + forwardCheckDistance * 0.5f));

            // Two samples so a single tile-seam gap does not freeze the Ent mid-platform.
            if (TryProbeGroundAtX(toeX, probeDistanceWorld, out hit))
            {
                return true;
            }

            return TryProbeGroundAtX(midX, probeDistanceWorld, out hit);
        }

        private bool TryProbeGroundAtX(float worldX, float probeDistanceWorld, out RaycastHit2D hit)
        {
            hit = default;
            if (bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            const float lift = 0.18f;
            float probeDistance = Mathf.Max(0.02f, probeDistanceWorld) + lift;
            Vector2 castOrigin = new Vector2(worldX, bounds.min.y + lift);
            Vector2 castSize = new Vector2(Mathf.Max(0.14f, bounds.size.x * 0.22f), 0.04f);

            hit = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, probeDistance);
            return IsValidGroundHit(hit);
        }

        private void LockWalkHeightToCurrentScenePosition()
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            lockedWalkWorldY = body.position.y;
            hasLockedWalkWorldY = true;
        }

        private bool TryMoveHorizontally(float moveDirection, float stepDistance)
        {
            if (body == null)
            {
                return false;
            }

            float allowedDistance = GetAllowedHorizontalDistance(moveDirection, stepDistance);
            if (allowedDistance <= 0.0001f)
            {
                return false;
            }

            Vector2 position = body.position;
            position.x += moveDirection * allowedDistance;
            position.y = hasLockedWalkWorldY ? lockedWalkWorldY : position.y;
            body.MovePosition(position);
            body.linearVelocity = new Vector2(moveDirection * moveSpeed, 0f);
            return true;
        }

        private float GetAllowedHorizontalDistance(float moveDirection, float requestedDistance)
        {
            if (Mathf.Abs(moveDirection) <= 0.01f || requestedDistance <= 0f || bodyCollider == null)
            {
                return 0f;
            }

            Bounds bounds = bodyCollider.bounds;
            // Keep the cast off the floor so tile seams / flat tops are not treated as walls.
            float castHeight = Mathf.Max(0.08f, bounds.size.y * 0.62f);
            Vector2 castSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.7f),
                castHeight);
            Vector2 castOrigin = new Vector2(
                bounds.center.x,
                bounds.min.y + (castHeight * 0.5f) + 0.12f);

            RaycastHit2D hit = Physics2D.BoxCast(
                castOrigin,
                castSize,
                0f,
                new Vector2(Mathf.Sign(moveDirection), 0f),
                requestedDistance + HorizontalCastSkin);

            if (!IsBlockingHorizontalHit(hit))
            {
                return requestedDistance;
            }

            return Mathf.Clamp(hit.distance - HorizontalCastSkin, 0f, requestedDistance);
        }

        private bool HasHorizontalObstacle(float moveDirection, float checkDistance)
        {
            return GetAllowedHorizontalDistance(moveDirection, checkDistance) <= 0.0001f;
        }

        private void TriggerWallRecovery(float wallNormalX)
        {
            if (body == null)
            {
                return;
            }

            float recoveryDirection = Mathf.Abs(wallNormalX) > 0.01f
                ? Mathf.Sign(wallNormalX)
                : -(Mathf.Sign(body.linearVelocity.x) == 0f ? 1f : Mathf.Sign(body.linearVelocity.x));

            wallRecoveryDirection = recoveryDirection;
            wallRecoveryTimerRemaining = Mathf.Max(WallRecoveryDuration, ledgeTurnDuration);
            body.linearVelocity = new Vector2(recoveryDirection * moveSpeed, 0f);
        }

        private bool IsValidGroundHit(RaycastHit2D hit)
        {
            if (hit.collider == null ||
                hit.collider.isTrigger ||
                IsOwnCollider(hit.collider) ||
                hit.normal.y < 0.6f)
            {
                return false;
            }

            // Do not plant on other characters — only static/composite floor geometry.
            Rigidbody2D hitBody = hit.rigidbody;
            if (hitBody != null &&
                hitBody != body &&
                hitBody.bodyType != RigidbodyType2D.Static &&
                hit.collider is not CompositeCollider2D &&
                !hit.collider.usedByComposite)
            {
                return false;
            }

            return true;
        }

        private bool IsOwnCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return true;
            }

            if (collider == bodyCollider)
            {
                return true;
            }

            Transform hitTransform = collider.transform;
            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private bool IsBlockingHorizontalHit(RaycastHit2D hit)
        {
            if (hit.collider == null
                || hit.collider.isTrigger
                || IsOwnCollider(hit.collider)
                || EnemyCollisionPassThrough2D.IsEnemyBody(hit.collider))
            {
                return false;
            }

            // Floor / shallow slopes must never count as walls (that freezes chase on tile seams).
            if (hit.normal.y >= 0.4f)
            {
                return false;
            }

            return Mathf.Abs(hit.normal.x) >= 0.45f;
        }

        private void ApplyZeroFrictionMaterial()
        {
            if (zeroFrictionMaterial == null)
            {
                zeroFrictionMaterial = new PhysicsMaterial2D("EntZeroFriction")
                {
                    friction = 0f,
                    bounciness = 0f,
                };
            }

            if (body != null)
            {
                body.sharedMaterial = zeroFrictionMaterial;
            }

            if (bodyCollider != null)
            {
                bodyCollider.sharedMaterial = zeroFrictionMaterial;
            }
        }

        private void ApplyFixedBodyCollider()
        {
            if (bodyCollider == null)
            {
                return;
            }

            // Always force the shared constants so scene/prefab/portal Ents cannot drift apart.
            bodyColliderSize = StandardBodyColliderSize;
            bodyColliderOffset = StandardBodyColliderOffset;
            float width = Mathf.Max(0.05f, StandardBodyColliderSize.x);
            float height = Mathf.Max(width, StandardBodyColliderSize.y);
            Vector2 offset = StandardBodyColliderOffset;

            if (bodyCollider is CapsuleCollider2D capsule)
            {
                capsule.direction = CapsuleDirection2D.Vertical;
                capsule.size = new Vector2(width, height);
                capsule.offset = offset;
                capsule.isTrigger = false;
                return;
            }

            if (bodyCollider is BoxCollider2D box)
            {
                box.size = new Vector2(width, height);
                box.offset = offset;
                box.isTrigger = false;
            }
        }

#if UNITY_EDITOR
        public void EditorAssignWalkFrames(Sprite[] frames)
        {
            walkFrames = frames ?? System.Array.Empty<Sprite>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && walkFrames.Length > 0)
            {
                spriteRenderer.sprite = walkFrames[0];
            }

            ApplyStandardBodyCollider();
        }

        public void EditorAssignAttackFrames(Sprite[] frames)
        {
            attackFrames = frames ?? System.Array.Empty<Sprite>();
            ResolveAttackFrameIndices();
        }

        public void EditorAssignHurtSprite(Sprite sprite)
        {
            hurtSprite = sprite;
        }

        public void EditorAssignDeathSprites(Sprite[] frames)
        {
            deathSprites = frames ?? System.Array.Empty<Sprite>();
        }

        public void EditorAssignProjectile(
            Sprite sprite,
            int damage,
            float speed,
            float scale,
            float spinDegreesPerSecond,
            Vector2 spawnOffset,
            float aimHeight = 0.9f)
        {
            projectileSprite = sprite;
            projectileDamage = Mathf.Max(0, damage);
            projectileSpeed = Mathf.Max(0f, speed);
            projectileScale = Mathf.Max(0.01f, scale);
            projectileSpinDegreesPerSecond = spinDegreesPerSecond;
            projectileSpawnOffset = spawnOffset;
            targetAimHeight = aimHeight;
        }
#endif
    }
}
