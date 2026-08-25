using Castlevania2D.Combat;
using EnemyHealth = Castlevania2D.Health.Health;
using System;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>Controls a ceiling mouse that winds up, dives at the player, and recoils from a block.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MouseDiveAttack2D : MonoBehaviour
    {
        private enum State
        {
            Idle,
            AttackWindup,
            Dive,
            ContactRecoil,
            Knockback,
        }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] [Min(0.1f)] private float aggroRadius = 7f;

        public Transform Target => target;

        [Header("Animations")]
        [SerializeField] private Sprite[] attackWindupFrames;
        [SerializeField] private Sprite[] diveFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private Sprite[] knockbackFrames;
        [SerializeField] private float knockbackFrameRate = 12f;

        [Header("Dive")]
        [SerializeField] private float diveSpeed = 5.5f;
        [Tooltip("Distance to push away after a successful hit before diving again.")]
        [SerializeField] private float contactRecoilDistance = 1.25f;
        [SerializeField] private float contactRecoilSpeed = 6f;
        [Tooltip("Height above the player root used only when no valid body collider is available.")]
        [SerializeField] private float targetBodyAimHeight = 0.662f;

        [Header("Contact damage")]
        [SerializeField] private int contactDamage = 10;
        [SerializeField] private float damageCooldown = 0.6f;

        [Header("Blocked knockback")]
        [SerializeField] private float knockbackDistance = 4f;
        [SerializeField] private float knockbackSpeed = 7f;
        [SerializeField] private float knockbackDuration = 0.6f;
        [SerializeField] private float blockedKnockbackUpward = 0.5f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private MouseIdleEnemy2D idleAnimation;
        private EnemyHealth health;
        private IDamageable targetDamageable;
        private Collider2D targetBodyCollider;
        private State state;
        private int frameIndex;
        private float frameTimer;
        private float nextDamageTime;
        private float knockbackTimer;
        private float knockbackTravelled;
        private float contactRecoilTravelled;
        private Vector2 knockbackDirection;
        private Vector2 contactRecoilDirection;
        private Vector2 attack1AnchorPosition;
        private Vector2 lastDiveDirection = Vector2.down;
        private bool isDead;
        private bool isHurtPaused;
        private bool lockedFacingRight;

        public void EditorAssignSprites(Sprite[] windup, Sprite[] dive, Sprite[] knockback)
        {
            attackWindupFrames = windup;
            diveFrames = dive;
            knockbackFrames = knockback;
        }

        /// <summary>Stops all attack activity while the hurt animation owns the renderer.</summary>
        public void PauseForHurt()
        {
            if (isDead)
            {
                return;
            }

            isHurtPaused = true;
            state = State.Idle;
            frameIndex = 0;
            frameTimer = 0f;
            body.linearVelocity = Vector2.zero;
        }

        /// <summary>Returns to windup when the player remains in aggro, otherwise to idle.</summary>
        public void ResumeAfterHurt()
        {
            if (isDead)
            {
                return;
            }

            isHurtPaused = false;
            if (IsTargetInAggroArea())
            {
                EnterWindup();
                return;
            }

            state = State.Idle;
            idleAnimation?.SetAnimationPlaying(true);
        }

        /// <summary>Disables combat control so the death animation can own physics and rendering.</summary>
        public void SetDead()
        {
            isDead = true;
            isHurtPaused = true;
            state = State.Idle;
            nextDamageTime = float.PositiveInfinity;
            body.linearVelocity = Vector2.zero;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            idleAnimation = GetComponent<MouseIdleEnemy2D>();
            health = GetComponent<EnemyHealth>();
            CacheTargetDamageable();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            bodyCollider.isTrigger = true;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (isDead || isHurtPaused || state == State.Idle || state == State.ContactRecoil)
            {
                return;
            }

            // Hold the current dive pose while overlapping the player so wing-frame
            // size changes do not look like the sprite is tearing on contact.
            if (state == State.Dive && IsOverlappingTargetBody())
            {
                return;
            }

            AdvanceAnimation(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (isDead || isHurtPaused)
            {
                return;
            }

            if (state == State.Idle)
            {
                if (IsTargetInAggroArea())
                {
                    EnterWindup();
                }

                return;
            }

            if (state == State.Dive)
            {
                MoveTowardTarget(Time.fixedDeltaTime);
            }
            else if (state == State.AttackWindup)
            {
                HoldAttack1Position();
            }
            else if (state == State.ContactRecoil)
            {
                MoveContactRecoil(Time.fixedDeltaTime);
            }
            else if (state == State.Knockback)
            {
                MoveKnockback(Time.fixedDeltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDealContactDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealContactDamage(other);
        }

        private void EnterWindup()
        {
            state = State.AttackWindup;
            frameIndex = 0;
            frameTimer = 0f;
            attack1AnchorPosition = body.position;
            body.linearVelocity = Vector2.zero;
            idleAnimation?.SetAnimationPlaying(false);
            LockFacingTowardTarget();
            ApplyAnimationFrame(attackWindupFrames);
        }

        private void EnterDive()
        {
            state = State.Dive;
            frameIndex = 0;
            frameTimer = 0f;
            body.linearVelocity = Vector2.zero;
            LockFacingTowardTarget();
            ApplyAnimationFrame(diveFrames);
        }

        private void EnterContactRecoil(Vector2 direction)
        {
            state = State.ContactRecoil;
            contactRecoilTravelled = 0f;
            contactRecoilDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            body.linearVelocity = Vector2.zero;
            // Keep the current dive frame frozen and facing locked — no flip, no frame swap.
            ApplyLockedFacing();
        }

        private void EnterKnockback(Vector2 direction)
        {
            state = State.Knockback;
            frameIndex = 0;
            frameTimer = 0f;
            knockbackTimer = 0f;
            knockbackTravelled = 0f;
            knockbackDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            body.linearVelocity = Vector2.zero;
            // Keep dive facing so the knockback clip does not mirror-flip on contact.
            ApplyLockedFacing();
            ApplyAnimationFrame(knockbackFrames);
        }

        private void AdvanceAnimation(float deltaTime)
        {
            Sprite[] frames = GetFramesForCurrentState();
            if (frames == null || frames.Length == 0)
            {
                if (state == State.AttackWindup)
                {
                    EnterDive();
                }

                return;
            }

            frameTimer += deltaTime;
            float animationFrameRate = state == State.Knockback ? knockbackFrameRate : frameRate;
            float frameDuration = 1f / Mathf.Max(1f, animationFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (state == State.AttackWindup)
                {
                    if (frameIndex >= frames.Length - 1)
                    {
                        EnterDive();
                        return;
                    }

                    frameIndex++;
                }
                else if (state == State.Knockback)
                {
                    if (frameIndex >= frames.Length - 1)
                    {
                        frameIndex = frames.Length - 1;
                        ApplyAnimationFrame(frames);
                        return;
                    }

                    frameIndex++;
                }
                else
                {
                    frameIndex = (frameIndex + 1) % frames.Length;
                }

                ApplyAnimationFrame(frames);
            }
        }

        private Sprite[] GetFramesForCurrentState()
        {
            return state switch
            {
                State.AttackWindup => attackWindupFrames,
                State.Dive => diveFrames,
                State.Knockback => knockbackFrames,
                _ => null,
            };
        }

        private void MoveTowardTarget(float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            Vector2 aimPoint = GetTargetAimPoint();
            Vector2 direction = aimPoint - body.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = lastDiveDirection.sqrMagnitude > 0.0001f ? lastDiveDirection : Vector2.left;
            }

            MoveDive(direction.normalized, deltaTime);
            ApplyLockedFacing();
        }

        private void MoveDive(Vector2 direction, float deltaTime)
        {
            lastDiveDirection = direction;
            body.linearVelocity = Vector2.zero;
            body.MovePosition(body.position + direction * diveSpeed * deltaTime);
        }

        private void MoveContactRecoil(float deltaTime)
        {
            float step = contactRecoilSpeed * deltaTime;
            body.linearVelocity = Vector2.zero;
            body.MovePosition(body.position + contactRecoilDirection * step);
            contactRecoilTravelled += step;
            ApplyLockedFacing();

            bool finishedDistance = contactRecoilTravelled >= contactRecoilDistance;
            bool clearOfPlayer = !IsOverlappingTargetBody();
            bool timedOut = contactRecoilTravelled >= contactRecoilDistance * 2.5f;
            if ((finishedDistance && clearOfPlayer) || timedOut)
            {
                EnterDive();
            }
        }

        private void HoldAttack1Position()
        {
            body.linearVelocity = Vector2.zero;
            body.position = attack1AnchorPosition;
        }

        private void MoveKnockback(float deltaTime)
        {
            knockbackTimer += deltaTime;
            float remainingDistance = knockbackDistance - knockbackTravelled;
            float step = Mathf.Min(knockbackSpeed * deltaTime, Mathf.Max(0f, remainingDistance));
            body.linearVelocity = Vector2.zero;
            body.MovePosition(body.position + knockbackDirection * step);
            knockbackTravelled += step;
            ApplyLockedFacing();

            if (knockbackTimer >= knockbackDuration || knockbackTravelled >= knockbackDistance)
            {
                EnterDive();
            }
        }

        private void TryDealContactDamage(Collider2D other)
        {
            if (isHurtPaused
                || state != State.Dive
                || targetDamageable == null
                || Time.time < nextDamageTime
                || !IsDamageTargetCollider(other))
            {
                return;
            }

            Vector2 hitPoint = other.ClosestPoint(bodyCollider != null ? bodyCollider.bounds.center : transform.position);
            Vector2 direction = GetTargetAimPoint() - body.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = lastDiveDirection;
            }

            direction = direction.normalized;
            DamageResult result = targetDamageable.ReceiveDamage(
                new DamageInfo(contactDamage, gameObject, hitPoint, direction));

            if (result == DamageResult.Blocked)
            {
                nextDamageTime = Time.time + damageCooldown;
                EnterKnockback(GetBlockedKnockbackDirection(direction));
            }
            else if (result == DamageResult.Applied)
            {
                nextDamageTime = Time.time + damageCooldown;
                EnterContactRecoil(GetBlockedKnockbackDirection(direction));
            }
        }

        private bool IsOverlappingTargetBody()
        {
            if (bodyCollider == null || targetBodyCollider == null || !targetBodyCollider.enabled)
            {
                return false;
            }

            ColliderDistance2D distance = Physics2D.Distance(bodyCollider, targetBodyCollider);
            return distance.isOverlapped || distance.distance <= 0.02f;
        }

        private bool IsTargetInAggroArea()
        {
            if (target == null)
            {
                return false;
            }

            Vector2 difference = (Vector2)target.position - body.position;
            return difference.sqrMagnitude <= aggroRadius * aggroRadius;
        }

        private bool IsDamageTargetCollider(Collider2D other)
        {
            if (other == null || target == null || IsKnownSensor(other.gameObject.name))
            {
                return false;
            }

            return other.transform == target
                   || other.transform.IsChildOf(target)
                   || target.IsChildOf(other.transform);
        }

        private void ApplyAnimationFrame(Sprite[] frames)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }

        private void LockFacingTowardTarget()
        {
            if (target == null || spriteRenderer == null)
            {
                return;
            }

            float deltaX = GetTargetAimPoint().x - body.position.x;
            if (Mathf.Abs(deltaX) > 0.05f)
            {
                lockedFacingRight = deltaX > 0f;
            }

            ApplyLockedFacing();
        }

        private void ApplyLockedFacing()
        {
            if (spriteRenderer != null)
            {
                // Dive source art faces left; flip when locked facing is to the right.
                spriteRenderer.flipX = lockedFacingRight;
            }
        }

        private void CacheTargetDamageable()
        {
            targetDamageable = target != null ? target.GetComponent<IDamageable>() : null;
            targetBodyCollider = ResolveTargetBodyCollider();
        }

        private Collider2D ResolveTargetBodyCollider()
        {
            if (target == null)
            {
                return null;
            }

            Collider2D rootCollider = target.GetComponent<Collider2D>();
            if (IsBodyCollider(rootCollider))
            {
                return rootCollider;
            }

            Collider2D[] childColliders = target.GetComponentsInChildren<Collider2D>(true);
            Collider2D bestCollider = null;
            float largestArea = 0f;
            float closestDistanceSquared = float.PositiveInfinity;
            Vector2 targetPosition = target.position;

            foreach (Collider2D collider in childColliders)
            {
                if (!IsBodyCollider(collider) || collider.transform == target)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float area = bounds.size.x * bounds.size.y;
                float distanceSquared = ((Vector2)bounds.center - targetPosition).sqrMagnitude;
                if (area > largestArea
                    || (Mathf.Approximately(area, largestArea) && distanceSquared < closestDistanceSquared))
                {
                    bestCollider = collider;
                    largestArea = area;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return bestCollider;
        }

        private static bool IsBodyCollider(Collider2D collider)
        {
            return collider != null
                   && collider.enabled
                   && !collider.isTrigger
                   && !IsKnownSensor(collider.gameObject.name);
        }

        private static bool IsKnownSensor(string objectName)
        {
            return objectName.Equals("GroundSensor", StringComparison.Ordinal)
                   || objectName.StartsWith("WallSensor", StringComparison.Ordinal)
                   || objectName.Equals("AttackHitbox", StringComparison.Ordinal);
        }

        private Vector2 GetTargetAimPoint()
        {
            if (targetBodyCollider != null && targetBodyCollider.enabled)
            {
                return targetBodyCollider.bounds.center;
            }

            return target != null
                ? (Vector2)target.position + Vector2.up * targetBodyAimHeight
                : body.position;
        }

        private Vector2 GetBlockedKnockbackDirection(Vector2 attackDirection)
        {
            Vector2 awayFromPlayer = -attackDirection;
            if (awayFromPlayer.sqrMagnitude <= 0.0001f)
            {
                awayFromPlayer = body.position - GetTargetAimPoint();
            }

            awayFromPlayer.y = Mathf.Max(awayFromPlayer.y, blockedKnockbackUpward);
            return awayFromPlayer;
        }

        private void OnDied()
        {
            SetDead();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, aggroRadius);
        }
#endif
    }
}
