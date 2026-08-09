using System.Collections.Generic;
using Castlevania2D.Combat;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// When the player is within <see cref="attackTriggerRange"/>, stop chase and play attack frames once @ frameRate.
    /// Damage is applied via a directed OverlapBox up to <see cref="attackRange"/> during active swing frames
    /// (body <see cref="ContactDamage2D"/> alone is too short for that reach).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MushomorMeleeAttack2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Attack")]
        [SerializeField] private Sprite[] attackFrames;
        [SerializeField] private float frameRate = 12f;
        [Tooltip("Start attack anim / stop chase when |Δx| ≤ this.")]
        [SerializeField] private float attackTriggerRange = 1.0f;
        [Tooltip("Melee damage reach during active swing frames (OverlapBox toward facing).")]
        [SerializeField] private float attackRange = 4.0f;
        [SerializeField] private float attackCooldown = 1.1f;

        [Header("Melee Hit")]
        [SerializeField] private int attackDamage = 10;
        [SerializeField] private LayerMask targetMask;
        [Tooltip("Inclusive 0-based attackFrames indices that deal damage (once per target per swing).")]
        [SerializeField] private int hitActiveStartFrame = 3;
        [SerializeField] private int hitActiveEndFrame = 7;
        [SerializeField] private float hitboxHeight = 1.8f;
        [SerializeField] private float hitboxYOffset = 1.0f;

        private SpriteRenderer spriteRenderer;
        private MushomorHurtAnimator2D hurt;
        private MushomorMovement2D movement;

        private bool attacking;
        private bool dead;
        private int frameIndex;
        private float frameTimer;
        private float cooldownRemaining;

        private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private ContactFilter2D overlapFilter;

        public bool IsAttacking => attacking;

        /// <summary>Distance at which chase stops and the attack animation may begin.</summary>
        public float AttackTriggerRange => attackTriggerRange;

        /// <summary>Melee damage reach during active swing frames.</summary>
        public float AttackRange => attackRange;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetDead(bool value)
        {
            dead = value;
            if (dead)
            {
                attacking = false;
                hitThisSwing.Clear();
            }
        }

        /// <summary>Stops a mid-swing attack so hurt/death can own the SpriteRenderer.</summary>
        public void CancelAttack()
        {
            attacking = false;
            frameIndex = 0;
            frameTimer = 0f;
            hitThisSwing.Clear();
        }

#if UNITY_EDITOR
        public void EditorAssignAttackFrames(Sprite[] frames)
        {
            attackFrames = frames ?? System.Array.Empty<Sprite>();
        }
#endif

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hurt = GetComponent<MushomorHurtAnimator2D>();
            movement = GetComponent<MushomorMovement2D>();

            overlapFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false,
                useDepth = false
            };
        }

        private void Start()
        {
            ResolveTargetIfNeeded();
        }

        private void Update()
        {
            if (dead)
            {
                return;
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= Time.deltaTime;
            }

            if (hurt != null && (hurt.IsPlayingHurt || hurt.IsPlayingDeath))
            {
                return;
            }

            if (attacking)
            {
                AdvanceAttack(Time.deltaTime);
                return;
            }

            TryBeginAttack();
        }

        private void TryBeginAttack()
        {
            if (cooldownRemaining > 0f || attackFrames == null || attackFrames.Length == 0)
            {
                return;
            }

            ResolveTargetIfNeeded();
            if (target == null)
            {
                return;
            }

            float distanceX = Mathf.Abs(target.position.x - transform.position.x);
            if (distanceX > attackTriggerRange)
            {
                return;
            }

            FaceTarget();
            attacking = true;
            frameIndex = 0;
            frameTimer = 0f;
            hitThisSwing.Clear();
            if (spriteRenderer != null && attackFrames[0] != null)
            {
                spriteRenderer.sprite = attackFrames[0];
            }

            TryDealMeleeDamageIfActiveFrame();
        }

        private void AdvanceAttack(float deltaTime)
        {
            if (attackFrames == null || attackFrames.Length == 0 || spriteRenderer == null)
            {
                attacking = false;
                hitThisSwing.Clear();
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= attackFrames.Length)
                {
                    EndAttack();
                    return;
                }

                if (attackFrames[frameIndex] != null)
                {
                    spriteRenderer.sprite = attackFrames[frameIndex];
                }

                TryDealMeleeDamageIfActiveFrame();
            }
        }

        private void EndAttack()
        {
            attacking = false;
            cooldownRemaining = Mathf.Max(0f, attackCooldown);
            frameIndex = 0;
            frameTimer = 0f;
            hitThisSwing.Clear();

            if (movement != null)
            {
                movement.RestoreIdleSprite();
            }
        }

        private void TryDealMeleeDamageIfActiveFrame()
        {
            int start = Mathf.Min(hitActiveStartFrame, hitActiveEndFrame);
            int end = Mathf.Max(hitActiveStartFrame, hitActiveEndFrame);
            if (frameIndex < start || frameIndex > end)
            {
                return;
            }

            float range = Mathf.Max(0.1f, attackRange);
            float facing = GetFacingSign();
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, hitboxYOffset);
            Vector2 center = origin + new Vector2(facing * range * 0.5f, 0f);
            Vector2 size = new Vector2(range, Mathf.Max(0.2f, hitboxHeight));

            if (targetMask.value != 0)
            {
                overlapFilter.useLayerMask = true;
                overlapFilter.SetLayerMask(targetMask);
            }
            else
            {
                overlapFilter.useLayerMask = false;
            }

            int hitCount = Physics2D.OverlapBox(center, size, 0f, overlapFilter, overlapBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D other = overlapBuffer[i];
                if (other == null || other.transform == transform || other.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.CanReceiveDamage || hitThisSwing.Contains(damageable))
                {
                    continue;
                }

                // Do not damage self (Mushomor Health implements IDamageable).
                if (damageable is Component damageableComponent &&
                    damageableComponent.transform == transform)
                {
                    continue;
                }

                hitThisSwing.Add(damageable);
                Vector2 hitPoint = other.ClosestPoint(center);
                Vector2 direction = new Vector2(facing, 0f);
                damageable.ReceiveDamage(new DamageInfo(attackDamage, gameObject, hitPoint, direction));
            }
        }

        private float GetFacingSign()
        {
            if (spriteRenderer != null)
            {
                return spriteRenderer.flipX ? -1f : 1f;
            }

            if (target != null)
            {
                float deltaX = target.position.x - transform.position.x;
                if (Mathf.Abs(deltaX) > 0.01f)
                {
                    return Mathf.Sign(deltaX);
                }
            }

            return 1f;
        }

        private void FaceTarget()
        {
            if (target == null || spriteRenderer == null)
            {
                return;
            }

            float deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.01f)
            {
                return;
            }

            // Art faces right; flip when target is to the left.
            spriteRenderer.flipX = deltaX < 0f;
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
                if (movement != null)
                {
                    movement.SetTarget(target);
                }
            }
        }
    }
}
