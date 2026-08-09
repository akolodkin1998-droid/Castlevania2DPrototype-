using System.Collections.Generic;
using Castlevania2D.Combat;
using EnemyHealth = Castlevania2D.Health.Health;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Stationary Likho: sleep → rise (aggro) → kick → rise reversed → sleep.
    /// Kick launches the player far forward.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LikhoEnemy2D : MonoBehaviour
    {
        private enum State
        {
            Sleep,
            Rising,
            Attacking,
            Settling,
        }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 aggroArea = new Vector2(6f, 3.5f);

        [Header("Animations")]
        [SerializeField] private Sprite[] sleepFrames;
        [SerializeField] private Sprite[] riseFrames;
        [SerializeField] private Sprite[] kickFrames;
        [SerializeField] private float sleepFrameRate = 8f;
        [SerializeField] private float actionFrameRate = 12f;
        [SerializeField] private bool sleepPingPong = true;
        [Tooltip("Relative size for kick frames and the last rise/settle frames (0.85 = 15% smaller).")]
        [SerializeField] private float attackAndFinalRiseScale = 0.85f;
        [Tooltip("How many trailing rise frames use the reduced scale.")]
        [SerializeField] private int finalRiseSmallFrameCount = 3;

        [Header("Kick")]
        [SerializeField] private int kickDamage = 12;
        [SerializeField] private float attackCooldown = 1.4f;
        [SerializeField] private int hitActiveStartFrame = 6;
        [SerializeField] private int hitActiveEndFrame = 10;
        [SerializeField] private float hitboxWidth = 2.4f;
        [SerializeField] private float hitboxHeight = 1.6f;
        [SerializeField] private float hitboxXOffset = 1.1f;
        [SerializeField] private float hitboxYOffset = 0.9f;
        [SerializeField] private float kickLaunchSpeed = 30f;
        [SerializeField] private float kickLaunchUpward = 6.5f;
        [SerializeField] private float kickLaunchDuration = 0.7f;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private EnemyHealth health;
        private IDamageable targetDamageable;
        private CombatKnockbackReceiver2D targetKnockback;
        private State state;
        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private float cooldownRemaining;
        private bool isDead;
        private bool facingRight = true;
        private Vector3 baseScale;
        private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private ContactFilter2D overlapFilter;

        public Transform Target => target;

#if UNITY_EDITOR
        public void EditorAssignFrames(Sprite[] sleep, Sprite[] rise, Sprite[] kick)
        {
            sleepFrames = sleep ?? System.Array.Empty<Sprite>();
            riseFrames = rise ?? System.Array.Empty<Sprite>();
            kickFrames = kick ?? System.Array.Empty<Sprite>();
        }
#endif

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<EnemyHealth>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            baseScale = transform.localScale;

            overlapFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false,
                useDepth = false,
            };

            EnterSleep();
        }

        private void Start()
        {
            CacheTarget();
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
            if (isDead)
            {
                return;
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
            }

            switch (state)
            {
                case State.Sleep:
                    AdvanceSleepAnimation(Time.deltaTime);
                    if (cooldownRemaining <= 0f && IsTargetInAggroArea())
                    {
                        FaceTarget();
                        EnterRising();
                    }

                    break;
                case State.Rising:
                    AdvanceOneShot(riseFrames, actionFrameRate, Time.deltaTime, EnterAttacking);
                    break;
                case State.Attacking:
                    AdvanceAttack(Time.deltaTime);
                    break;
                case State.Settling:
                    AdvanceReverse(riseFrames, actionFrameRate, Time.deltaTime, FinishSettle);
                    break;
            }
        }

        private void EnterSleep()
        {
            state = State.Sleep;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            hitThisSwing.Clear();
            ApplyFrame(sleepFrames);
        }

        private void EnterRising()
        {
            state = State.Rising;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame(riseFrames);
        }

        private void EnterAttacking()
        {
            state = State.Attacking;
            frameIndex = 0;
            frameTimer = 0f;
            hitThisSwing.Clear();
            ApplyFrame(kickFrames);
        }

        private void EnterSettling()
        {
            state = State.Settling;
            frameIndex = riseFrames != null && riseFrames.Length > 0 ? riseFrames.Length - 1 : 0;
            frameTimer = 0f;
            ApplyFrame(riseFrames);
        }

        private void FinishSettle()
        {
            cooldownRemaining = attackCooldown;
            EnterSleep();
        }

        private void AdvanceSleepAnimation(float deltaTime)
        {
            if (sleepFrames == null || sleepFrames.Length == 0)
            {
                return;
            }

            if (sleepFrames.Length == 1)
            {
                ApplyFrame(sleepFrames);
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, sleepFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (sleepPingPong)
                {
                    frameIndex += frameDirection;
                    if (frameIndex >= sleepFrames.Length - 1)
                    {
                        frameIndex = sleepFrames.Length - 1;
                        frameDirection = -1;
                    }
                    else if (frameIndex <= 0)
                    {
                        frameIndex = 0;
                        frameDirection = 1;
                    }
                }
                else
                {
                    frameIndex = (frameIndex + 1) % sleepFrames.Length;
                }

                ApplyFrame(sleepFrames);
            }
        }

        private void AdvanceOneShot(Sprite[] frames, float frameRate, float deltaTime, System.Action onComplete)
        {
            if (frames == null || frames.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= frames.Length - 1)
                {
                    onComplete?.Invoke();
                    return;
                }

                frameIndex++;
                ApplyFrame(frames);
            }
        }

        private void AdvanceReverse(Sprite[] frames, float frameRate, float deltaTime, System.Action onComplete)
        {
            if (frames == null || frames.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex <= 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                frameIndex--;
                ApplyFrame(frames);
            }
        }

        private void AdvanceAttack(float deltaTime)
        {
            if (kickFrames == null || kickFrames.Length == 0)
            {
                EnterSettling();
                return;
            }

            TryDealKickDamage();

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, actionFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= kickFrames.Length - 1)
                {
                    EnterSettling();
                    return;
                }

                frameIndex++;
                ApplyFrame(kickFrames);
                TryDealKickDamage();
            }
        }

        private void TryDealKickDamage()
        {
            if (frameIndex < hitActiveStartFrame || frameIndex > hitActiveEndFrame)
            {
                return;
            }

            if (targetDamageable == null)
            {
                CacheTarget();
                if (targetDamageable == null)
                {
                    return;
                }
            }

            float facing = facingRight ? 1f : -1f;
            Vector2 center = (Vector2)transform.position
                             + new Vector2(hitboxXOffset * facing, hitboxYOffset);
            var hitboxSize = new Vector2(hitboxWidth, hitboxHeight);
            int hitCount = Physics2D.OverlapBox(center, hitboxSize, 0f, overlapFilter, overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
                if (damageable == null
                    || damageable == (IDamageable)health
                    || !damageable.CanReceiveDamage
                    || hitThisSwing.Contains(damageable))
                {
                    continue;
                }

                Vector2 hitPoint = hitCollider.ClosestPoint(center);
                Vector2 direction = new Vector2(facing, 0.15f).normalized;
                DamageResult result = damageable.ReceiveDamage(
                    new DamageInfo(kickDamage, gameObject, hitPoint, direction));

                if (result == DamageResult.Ignored)
                {
                    continue;
                }

                hitThisSwing.Add(damageable);

                if (result == DamageResult.Applied)
                {
                    ApplyKickLaunch(damageable, facing);
                }
            }
        }

        private void ApplyKickLaunch(IDamageable damageable, float facing)
        {
            CombatKnockbackReceiver2D knockback = targetKnockback;
            if (knockback == null && damageable is Component component)
            {
                knockback = component.GetComponentInParent<CombatKnockbackReceiver2D>();
            }

            if (knockback == null)
            {
                return;
            }

            var launch = new Vector2(kickLaunchSpeed * facing, kickLaunchUpward);
            knockback.ApplyKnockback(launch, kickLaunchDuration);
        }

        private bool IsTargetInAggroArea()
        {
            if (target == null)
            {
                CacheTarget();
                if (target == null)
                {
                    return false;
                }
            }

            Vector2 difference = (Vector2)target.position - (Vector2)transform.position;
            return Mathf.Abs(difference.x) <= aggroArea.x * 0.5f
                   && Mathf.Abs(difference.y) <= aggroArea.y * 0.5f;
        }

        private void FaceTarget()
        {
            if (target == null)
            {
                return;
            }

            float deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.05f)
            {
                facingRight = deltaX > 0f;
                // Source art faces right; flip when looking left.
                spriteRenderer.flipX = !facingRight;
            }
        }

        private void ApplyFrame(Sprite[] frames)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }

            ApplyVisualScale();
        }

        private void ApplyVisualScale()
        {
            bool shrink = state == State.Attacking || IsFinalRiseFrame(frameIndex);
            float multiplier = shrink ? attackAndFinalRiseScale : 1f;
            transform.localScale = new Vector3(
                baseScale.x * multiplier,
                baseScale.y * multiplier,
                baseScale.z);
        }

        private bool IsFinalRiseFrame(int riseFrameIndex)
        {
            if (state != State.Rising && state != State.Settling)
            {
                return false;
            }

            if (riseFrames == null || riseFrames.Length == 0)
            {
                return false;
            }

            int smallCount = Mathf.Clamp(finalRiseSmallFrameCount, 1, riseFrames.Length);
            int firstSmallIndex = riseFrames.Length - smallCount;
            return riseFrameIndex >= firstSmallIndex;
        }

        private void CacheTarget()
        {
            if (target == null)
            {
                GameObject player = GameObject.Find("Player_HeroKnight");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (target != null)
            {
                targetDamageable = target.GetComponent<IDamageable>();
                targetKnockback = target.GetComponent<CombatKnockbackReceiver2D>();
            }
        }

        private void OnDied()
        {
            isDead = true;
            enabled = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, aggroArea);

            float facing = facingRight ? 1f : -1f;
            Vector2 center = (Vector2)transform.position
                             + new Vector2(hitboxXOffset * facing, hitboxYOffset);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(center, new Vector3(hitboxWidth, hitboxHeight, 0f));
        }
#endif
    }
}
