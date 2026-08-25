using Castlevania2D.CameraFx;
using Castlevania2D.Combat;
using Castlevania2D.Movement;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class GiantLikhoEnemy2D : MonoBehaviour
    {
        private enum State
        {
            Patrol,
            AttackStartup,
            Charging,
            WallImpact,
            JumpPrepare,
            Jumping,
            MeleeAttack,
        }

        private enum ImpactPhase
        {
            Intro,
            Tail,
        }

        private const string PlayerObjectName = "Player_HeroKnight";
        private const int WalkLoopStartIndex = 4;
        private const int WalkLoopEndIndex = 13;
        private const int ImpactTailStartIndex = 6;
        private const int ImpactTailRepeatCount = 3;
        private static readonly RaycastHit2D[] ProbeBuffer = new RaycastHit2D[8];
        private static readonly Collider2D[] AttackOverlapBuffer = new Collider2D[16];

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] [Min(0.1f)] private float aggroRadius = 8f;

        [Header("Movement")]
        [SerializeField] [Min(0f)] private float patrolSpeed = 1.8f;
        [SerializeField] [Min(0f)] private float chaseSpeed = 3f;
        [SerializeField] [Min(0.01f)] private float obstacleProbeDistance = 0.3f;
        [SerializeField] [Min(0.01f)] private float ledgeProbeDepth = 0.65f;

        [Header("Animation")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] [Min(1f)] private float walkFrameRate = 10f;

        [Header("Attack 1 — Charge")]
        [SerializeField] private Sprite[] attackStartupFrames;
        [SerializeField] private Sprite[] chargeLoopFrames;
        [SerializeField] private Sprite[] wallImpactFrames;
        [SerializeField] [Min(1f)] private float attackFrameRate = 12f;
        [SerializeField] [Min(0f)] private float chargeSpeed = 7f;
        [SerializeField] [Min(0f)] private float attackCooldown = 3f;
        [SerializeField] [Min(1)] private int chargeDamage = 25;
        [SerializeField] [Min(0f)] private float knockbackHorizontalSpeed = 18f;
        [SerializeField] [Min(0f)] private float knockbackUpwardSpeed = 6f;
        [SerializeField] [Min(0f)] private float knockbackDuration = 0.55f;

        [Header("Attack 2 — Jump")]
        [SerializeField] private Sprite[] jumpPrepareFrames;
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] [Min(1)] private int jumpLandFrame = 8;
        [SerializeField] [Min(1)] private int jumpDamage = 25;
        [SerializeField] [Min(0f)] private float slamShakeAmplitude = 0.28f;
        [SerializeField] [Min(0.01f)] private float slamShakeDuration = 0.32f;

        [Header("Attack 3 — Melee")]
        [SerializeField] private Sprite[] meleeFrames;
        [SerializeField] [Min(0.1f)] private float meleeTriggerRange = 3.2f;
        [SerializeField] [Min(0.1f)] private float meleeHitRange = 3.2f;
        [SerializeField] [Min(0.2f)] private float meleeHitboxHeight = 2.4f;
        [SerializeField] private float meleeHitboxYOffset = 1.1f;
        [SerializeField] [Min(0)] private int meleeHitStartFrame = 7;
        [SerializeField] [Min(0)] private int meleeHitEndFrame = 8;
        [SerializeField] [Min(1)] private int minMeleePerCycle = 1;
        [SerializeField] [Min(1)] private int maxMeleePerCycle = 2;
        [SerializeField] [Range(0f, 1f)] private float rareTripleMeleeChance = 0.12f;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private BoxCollider2D boxCollider;
        private EnemyHealth health;
        private ContactFilter2D probeFilter;
        private ContactFilter2D attackOverlapFilter;
        private State state;
        private int patrolDirection = 1;
        private int movementDirection = 1;
        private int chargeDirection = 1;
        private int frameIndex;
        private int impactTailLoopsCompleted;
        private ImpactPhase impactPhase;
        private float frameTimer;
        private float cooldownRemaining;
        private float meleeCooldownRemaining;
        private int meleeAttacksThisCycle;
        private int meleeAttacksPlanned = 2;
        private bool isDead;
        private bool hitPlayerThisCharge;
        private bool jumpSlamDealt;
        private bool meleeHitDealt;
        private bool ignoredPlayerCollision;
        private Vector2 authoredColliderOffset;

        public float AggroRadius => aggroRadius;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            boxCollider = bodyCollider as BoxCollider2D;
            health = GetComponent<EnemyHealth>();
            if (boxCollider != null)
            {
                authoredColliderOffset = boxCollider.offset;
            }

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            probeFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false,
                useDepth = false,
            };
            attackOverlapFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false,
                useDepth = false,
            };

            state = State.Patrol;
            RollMeleeQuota();
            ApplyFrame();
        }

        private void Start()
        {
            CacheTarget();
            SnapBodyOntoGround();
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

            if (meleeCooldownRemaining > 0f)
            {
                meleeCooldownRemaining -= Time.deltaTime;
            }

            switch (state)
            {
                case State.Patrol:
                    if (Mathf.Abs(body.linearVelocity.x) > 0.05f)
                    {
                        AdvanceWalkAnimation(Time.deltaTime);
                    }

                    break;
                case State.AttackStartup:
                    AdvanceAttackStartup(Time.deltaTime);
                    break;
                case State.Charging:
                    AdvanceChargeLoop(Time.deltaTime);
                    break;
                case State.WallImpact:
                    AdvanceWallImpact(Time.deltaTime);
                    break;
                case State.JumpPrepare:
                    AdvanceJumpPrepare(Time.deltaTime);
                    break;
                case State.Jumping:
                    AdvanceJump(Time.deltaTime);
                    break;
                case State.MeleeAttack:
                    AdvanceMelee(Time.deltaTime);
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (isDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            CacheTarget();
            IgnorePlayerBodyCollision();
            if (state == State.JumpPrepare || state == State.Jumping)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (state == State.AttackStartup
                || state == State.WallImpact
                || state == State.MeleeAttack)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            if (state == State.Charging)
            {
                TickCharge();
                return;
            }

            bool targetInAggro = IsTargetInAggroRadius();
            bool inMeleeRange = IsTargetInMeleeRange();
            bool meleeQuotaRemaining = HasMeleeQuotaRemaining();
            if (targetInAggro && HasMeleeFrames() && inMeleeRange && meleeQuotaRemaining)
            {
                if (meleeCooldownRemaining <= 0f)
                {
                    BeginMeleeAttack();
                    return;
                }

                HoldInPlaceFacingTarget();
                return;
            }

            bool readyToCharge = targetInAggro
                && cooldownRemaining <= 0f
                && HasAttackFrames()
                && !HasObstacleAhead(GetChargeDirectionOrFacing());
            if (readyToCharge && (!HasMeleeFrames() || !meleeQuotaRemaining))
            {
                BeginAttackStartup();
                return;
            }

            if (targetInAggro && HasMeleeFrames() && inMeleeRange && !meleeQuotaRemaining)
            {
                HoldInPlaceFacingTarget();
                return;
            }

            int desiredDirection = targetInAggro
                ? GetDirectionToTarget()
                : patrolDirection;

            if (desiredDirection == 0)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            bool obstacleAhead = HasObstacleAhead(desiredDirection);
            bool groundAhead = HasGroundAhead(desiredDirection);

            if (!targetInAggro && (obstacleAhead || !groundAhead))
            {
                patrolDirection *= -1;
                desiredDirection = patrolDirection;
            }
            else if (targetInAggro && (obstacleAhead || !groundAhead))
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                Face(desiredDirection);
                return;
            }

            movementDirection = desiredDirection;
            Face(movementDirection);
            float speed = targetInAggro ? chaseSpeed : patrolSpeed;
            body.linearVelocity = new Vector2(movementDirection * speed, body.linearVelocity.y);
        }

        private void BeginAttackStartup()
        {
            chargeDirection = GetChargeDirectionOrFacing();
            state = State.AttackStartup;
            frameIndex = 0;
            frameTimer = 0f;
            hitPlayerThisCharge = false;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            Face(chargeDirection);
            ApplyAnimationFrame(attackStartupFrames);
        }

        private int GetChargeDirectionOrFacing()
        {
            int direction = GetDirectionToTarget();
            return direction != 0 ? direction : movementDirection;
        }

        private bool HasAttackFrames()
        {
            return attackStartupFrames != null && attackStartupFrames.Length > 0
                   && chargeLoopFrames != null && chargeLoopFrames.Length > 0
                   && wallImpactFrames != null && wallImpactFrames.Length > 0;
        }

        private void EnterCharge()
        {
            state = State.Charging;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyAnimationFrame(chargeLoopFrames);
        }

        private void TickCharge()
        {
            Face(chargeDirection);
            if (TryHitPlayer() || HasObstacleAhead(chargeDirection))
            {
                EnterWallImpact();
                return;
            }

            body.linearVelocity =
                new Vector2(chargeDirection * chargeSpeed, body.linearVelocity.y);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryImpactFromCollider(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryImpactFromCollider(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryImpactFromCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryImpactFromCollider(other);
        }

        private void TryImpactFromCollider(Collider2D other)
        {
            if (isDead || state != State.Charging || hitPlayerThisCharge)
            {
                return;
            }

            if (!IsTargetCollider(other))
            {
                return;
            }

            hitPlayerThisCharge = true;
            ApplyChargeHit(other, bodyCollider.bounds.center);
            EnterWallImpact();
        }

        private void EnterWallImpact()
        {
            state = State.WallImpact;
            frameIndex = 0;
            frameTimer = 0f;
            impactTailLoopsCompleted = 0;
            impactPhase = ImpactPhase.Intro;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            ApplyAnimationFrame(wallImpactFrames);
        }

        private void FinishWallImpact()
        {
            if (HasJumpFrames())
            {
                BeginJumpPrepare();
                return;
            }

            ReturnToPatrol();
        }

        private bool HasJumpFrames()
        {
            return jumpPrepareFrames != null && jumpPrepareFrames.Length > 0
                   && jumpFrames != null && jumpFrames.Length > 0;
        }

        private void BeginJumpPrepare()
        {
            state = State.JumpPrepare;
            frameIndex = 0;
            frameTimer = 0f;
            jumpSlamDealt = false;
            body.linearVelocity = Vector2.zero;
            ApplyAnimationFrame(jumpPrepareFrames);
        }

        private void EnterJump()
        {
            state = State.Jumping;
            frameIndex = 0;
            frameTimer = 0f;
            jumpSlamDealt = false;
            ApplyAnimationFrame(jumpFrames);
        }

        private void AdvanceJumpPrepare(float deltaTime)
        {
            if (AdvanceOneShot(jumpPrepareFrames, deltaTime))
            {
                EnterJump();
            }
        }

        private void AdvanceJump(float deltaTime)
        {
            if (jumpFrames == null || jumpFrames.Length == 0)
            {
                FinishJump();
                return;
            }

            int landIndex = GetJumpLandIndex();
            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= jumpFrames.Length - 1)
                {
                    FinishJump();
                    return;
                }

                frameIndex++;
                ApplyAnimationFrame(jumpFrames);
                if (!jumpSlamDealt && frameIndex >= landIndex)
                {
                    TryJumpSlam();
                }
            }
        }

        private int GetJumpLandIndex()
        {
            return Mathf.Clamp(jumpLandFrame - 1, 0, jumpFrames.Length - 1);
        }

        private void TryJumpSlam()
        {
            if (jumpSlamDealt)
            {
                return;
            }

            jumpSlamDealt = true;
            SnapBodyOntoGround();
            CameraShake2D.Play(slamShakeAmplitude, slamShakeDuration);

            CacheTarget();
            if (target == null || IsPlayerAirborne())
            {
                return;
            }

            IDamageable damageable = ResolveTargetDamageable();
            if (damageable == null || !damageable.CanReceiveDamage)
            {
                return;
            }

            Vector2 hitPoint = target.position;
            DamageResult result = damageable.ReceiveDamage(
                new DamageInfo(jumpDamage, gameObject, hitPoint, Vector2.up));
            if (result != DamageResult.Applied)
            {
                return;
            }

            CombatKnockbackReceiver2D knockback =
                target.GetComponentInParent<CombatKnockbackReceiver2D>();
            if (knockback != null)
            {
                knockback.ApplyKnockback(
                    new Vector2(0f, knockbackUpwardSpeed),
                    knockbackDuration);
            }
        }

        private IDamageable ResolveTargetDamageable()
        {
            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                return damageable;
            }

            damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                return damageable;
            }

            return target.GetComponentInChildren<IDamageable>();
        }

        private bool IsPlayerAirborne()
        {
            if (target == null)
            {
                return true;
            }

            if (HasGroundUnderTarget())
            {
                return false;
            }

            Animator animator = target.GetComponent<Animator>();
            if (animator == null)
            {
                animator = target.GetComponentInChildren<Animator>();
            }

            if (animator != null && HasAnimatorBool(animator, "Grounded") && animator.GetBool("Grounded"))
            {
                return false;
            }

            if (animator != null && HasAnimatorBool(animator, "IsGrounded") && animator.GetBool("IsGrounded"))
            {
                return false;
            }

            CharacterMotor2D motor = target.GetComponent<CharacterMotor2D>();
            if (motor != null && motor.IsGrounded)
            {
                return false;
            }

            if (animator != null && HasAnimatorBool(animator, "Grounded"))
            {
                return !animator.GetBool("Grounded");
            }

            if (animator != null && HasAnimatorBool(animator, "IsGrounded"))
            {
                return !animator.GetBool("IsGrounded");
            }

            if (motor != null)
            {
                return !motor.IsGrounded;
            }

            return false;
        }

        private static bool HasAnimatorBool(Animator animator, string parameterName)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool
                    && parameters[i].name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasGroundUnderTarget()
        {
            Collider2D playerCollider = target.GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                playerCollider = target.GetComponentInChildren<Collider2D>();
            }

            if (playerCollider == null)
            {
                return false;
            }

            Bounds bounds = playerCollider.bounds;
            Vector2 size = new Vector2(Mathf.Max(0.25f, bounds.size.x * 0.7f), 0.28f);
            Vector2 center = new Vector2(bounds.center.x, bounds.min.y - 0.04f);
            int count = Physics2D.OverlapBox(
                center,
                size,
                0f,
                probeFilter,
                AttackOverlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = AttackOverlapBuffer[i];
                if (hit == null
                    || hit == playerCollider
                    || hit.transform.IsChildOf(target)
                    || hit == bodyCollider)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void FinishJump()
        {
            SnapBodyOntoGround();
            ReturnToPatrol();
        }

        private bool HasMeleeFrames()
        {
            return meleeFrames != null && meleeFrames.Length > 0;
        }

        private bool IsTargetInMeleeRange()
        {
            if (target == null || !EnemyAggroLimits.IsWithinVerticalRange(transform, target))
            {
                return false;
            }

            return Mathf.Abs(target.position.x - transform.position.x) <= meleeTriggerRange;
        }

        private bool HasMeleeQuotaRemaining()
        {
            return meleeAttacksThisCycle < meleeAttacksPlanned;
        }

        private void RollMeleeQuota()
        {
            int min = Mathf.Min(minMeleePerCycle, maxMeleePerCycle);
            int max = Mathf.Max(minMeleePerCycle, maxMeleePerCycle);
            if (Random.value < rareTripleMeleeChance)
            {
                meleeAttacksPlanned = 3;
            }
            else
            {
                meleeAttacksPlanned = Random.Range(min, max + 1);
            }

            meleeAttacksThisCycle = 0;
        }

        private void HoldInPlaceFacingTarget()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            int holdDirection = GetDirectionToTarget();
            Face(holdDirection != 0 ? holdDirection : movementDirection);
            ApplyFrame();
        }

        private void BeginMeleeAttack()
        {
            chargeDirection = GetChargeDirectionOrFacing();
            state = State.MeleeAttack;
            frameIndex = 0;
            frameTimer = 0f;
            meleeHitDealt = false;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            Face(chargeDirection);
            ApplyAnimationFrame(meleeFrames);
            TryMeleeHitIfActiveFrame();
        }

        private void AdvanceMelee(float deltaTime)
        {
            if (meleeFrames == null || meleeFrames.Length == 0)
            {
                FinishMelee();
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= meleeFrames.Length - 1)
                {
                    FinishMelee();
                    return;
                }

                frameIndex++;
                ApplyAnimationFrame(meleeFrames);
                TryMeleeHitIfActiveFrame();
            }
        }

        private void TryMeleeHitIfActiveFrame()
        {
            if (meleeHitDealt)
            {
                return;
            }

            int start = Mathf.Min(meleeHitStartFrame, meleeHitEndFrame);
            int end = Mathf.Max(meleeHitStartFrame, meleeHitEndFrame);
            if (frameIndex < start || frameIndex > end)
            {
                return;
            }

            float facing = chargeDirection != 0 ? chargeDirection : (spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f);
            Vector2 origin = (Vector2)transform.position + new Vector2(0f, meleeHitboxYOffset);
            Vector2 size = new Vector2(Mathf.Max(0.1f, meleeHitRange), Mathf.Max(0.2f, meleeHitboxHeight));
            Vector2 center = origin + new Vector2(facing * size.x * 0.5f, 0f);
            int count = Physics2D.OverlapBox(
                center,
                size,
                0f,
                attackOverlapFilter,
                AttackOverlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = AttackOverlapBuffer[i];
                if (hit == null || !IsTargetCollider(hit))
                {
                    continue;
                }

                meleeHitDealt = true;
                ApplyMeleeHit(hit, center);
                return;
            }
        }

        private void ApplyMeleeHit(Collider2D hit, Vector2 origin)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.CanReceiveDamage)
            {
                return;
            }

            Vector2 hitPoint = hit.ClosestPoint(origin);
            DamageResult result = damageable.ReceiveDamage(
                new DamageInfo(jumpDamage, gameObject, hitPoint, Vector2.up));
            if (result != DamageResult.Applied)
            {
                return;
            }

            CombatKnockbackReceiver2D knockback =
                hit.GetComponentInParent<CombatKnockbackReceiver2D>();
            if (knockback != null)
            {
                knockback.ApplyKnockback(
                    new Vector2(0f, knockbackUpwardSpeed),
                    knockbackDuration);
            }
        }

        private void FinishMelee()
        {
            state = State.Patrol;
            meleeAttacksThisCycle++;
            meleeCooldownRemaining = 0.8f;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame();
        }

        private void ReturnToPatrol()
        {
            state = State.Patrol;
            patrolDirection = -chargeDirection;
            movementDirection = patrolDirection;
            cooldownRemaining = attackCooldown;
            RollMeleeQuota();
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame();
        }

        private void AdvanceAttackStartup(float deltaTime)
        {
            if (AdvanceOneShot(attackStartupFrames, deltaTime))
            {
                EnterCharge();
            }
        }

        private void AdvanceChargeLoop(float deltaTime)
        {
            if (chargeLoopFrames == null || chargeLoopFrames.Length == 0)
            {
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % chargeLoopFrames.Length;
                ApplyAnimationFrame(chargeLoopFrames);
            }
        }

        private void AdvanceWallImpact(float deltaTime)
        {
            if (wallImpactFrames == null || wallImpactFrames.Length == 0)
            {
                FinishWallImpact();
                return;
            }

            int lastIndex = wallImpactFrames.Length - 1;
            int tailStart = Mathf.Min(ImpactTailStartIndex, lastIndex);
            int introLast = Mathf.Max(0, tailStart - 1);

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (impactPhase == ImpactPhase.Intro)
                {
                    if (frameIndex < introLast)
                    {
                        frameIndex++;
                    }
                    else
                    {
                        impactPhase = ImpactPhase.Tail;
                        frameIndex = tailStart;
                    }

                    ApplyAnimationFrame(wallImpactFrames);
                    continue;
                }

                if (frameIndex < lastIndex)
                {
                    frameIndex++;
                    ApplyAnimationFrame(wallImpactFrames);
                    continue;
                }

                impactTailLoopsCompleted++;
                if (impactTailLoopsCompleted >= ImpactTailRepeatCount)
                {
                    FinishWallImpact();
                    return;
                }

                frameIndex = tailStart;
                ApplyAnimationFrame(wallImpactFrames);
            }
        }

        private bool AdvanceOneShot(Sprite[] frames, float deltaTime)
        {
            if (frames == null || frames.Length == 0)
            {
                return true;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, attackFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= frames.Length - 1)
                {
                    return true;
                }

                frameIndex++;
                ApplyAnimationFrame(frames);
            }

            return false;
        }

        private bool TryHitPlayer()
        {
            if (hitPlayerThisCharge || target == null)
            {
                return hitPlayerThisCharge;
            }

            Bounds bounds = bodyCollider.bounds;
            Vector2 size = (Vector2)bounds.size + new Vector2(0.25f, 0.15f);
            int count = Physics2D.OverlapBox(
                bounds.center,
                size,
                0f,
                attackOverlapFilter,
                AttackOverlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = AttackOverlapBuffer[i];
                if (hit == null || !IsTargetCollider(hit))
                {
                    continue;
                }

                hitPlayerThisCharge = true;
                ApplyChargeHit(hit, bounds.center);
                return true;
            }

            return false;
        }

        private void ApplyChargeHit(Collider2D hit, Vector2 origin)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.CanReceiveDamage)
            {
                return;
            }

            Vector2 hitPoint = hit.ClosestPoint(origin);
            Vector2 direction = new Vector2(chargeDirection, 0.15f).normalized;
            DamageResult result = damageable.ReceiveDamage(
                new DamageInfo(chargeDamage, gameObject, hitPoint, direction));
            if (result != DamageResult.Applied)
            {
                return;
            }

            CombatKnockbackReceiver2D knockback =
                hit.GetComponentInParent<CombatKnockbackReceiver2D>();
            if (knockback != null)
            {
                knockback.ApplyKnockback(
                    new Vector2(
                        chargeDirection * knockbackHorizontalSpeed,
                        knockbackUpwardSpeed),
                    knockbackDuration);
            }
        }

        private bool IsTargetInAggroRadius()
        {
            if (target == null || !EnemyAggroLimits.IsWithinVerticalRange(transform, target))
            {
                return false;
            }

            return ((Vector2)(target.position - transform.position)).sqrMagnitude
                   <= aggroRadius * aggroRadius;
        }

        private int GetDirectionToTarget()
        {
            if (target == null)
            {
                return 0;
            }

            float deltaX = target.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.1f)
            {
                return 0;
            }

            return deltaX > 0f ? 1 : -1;
        }

        private void SnapBodyOntoGround()
        {
            if (boxCollider == null || body == null)
            {
                return;
            }

            boxCollider.offset = authoredColliderOffset;
            Physics2D.SyncTransforms();
            if (!TryFindGroundSurfaceY(out float surfaceY))
            {
                return;
            }

            const float groundSkin = 0.02f;
            float deltaWorld = surfaceY + groundSkin - boxCollider.bounds.min.y;
            if (Mathf.Abs(deltaWorld) < 0.001f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
                return;
            }

            body.position += new Vector2(0f, deltaWorld);
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            Physics2D.SyncTransforms();
        }

        private bool TryFindGroundSurfaceY(out float surfaceY)
        {
            surfaceY = 0f;
            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.max.y + 0.5f);
            float distance = bounds.size.y + 12f;
            int count = Physics2D.Raycast(
                origin,
                Vector2.down,
                probeFilter,
                ProbeBuffer,
                distance);

            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = ProbeBuffer[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null
                    || hitCollider == bodyCollider
                    || hitCollider.transform.IsChildOf(transform)
                    || IsTargetCollider(hitCollider)
                    || EnemyCollisionPassThrough2D.IsEnemyBody(hitCollider))
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    surfaceY = hit.point.y;
                    found = true;
                }
            }

            return found;
        }

        private bool HasObstacleAhead(int direction)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(
                direction > 0 ? bounds.max.x + 0.02f : bounds.min.x - 0.02f,
                bounds.center.y);
            int count = Physics2D.Raycast(
                origin,
                direction > 0 ? Vector2.right : Vector2.left,
                probeFilter,
                ProbeBuffer,
                obstacleProbeDistance);
            return HasBlockingHit(count);
        }

        private bool HasGroundAhead(int direction)
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 origin = new Vector2(
                direction > 0 ? bounds.max.x + 0.08f : bounds.min.x - 0.08f,
                bounds.min.y + 0.08f);
            int count = Physics2D.Raycast(
                origin,
                Vector2.down,
                probeFilter,
                ProbeBuffer,
                ledgeProbeDepth);
            return HasBlockingHit(count);
        }

        private bool HasBlockingHit(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = ProbeBuffer[i].collider;
                if (hit == null
                    || hit == bodyCollider
                    || hit.transform.IsChildOf(transform)
                    || IsTargetCollider(hit)
                    || EnemyCollisionPassThrough2D.IsEnemyBody(hit))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsTargetCollider(Collider2D collider)
        {
            if (collider == null || target == null)
            {
                return false;
            }

            Transform root = collider.attachedRigidbody != null
                ? collider.attachedRigidbody.transform
                : collider.transform.root;
            return root == target || root.IsChildOf(target);
        }

        private void AdvanceWalkAnimation(float deltaTime)
        {
            if (walkFrames == null || walkFrames.Length == 0)
            {
                return;
            }

            frameTimer += deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, walkFrameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                int loopEnd = Mathf.Min(WalkLoopEndIndex, walkFrames.Length - 1);
                int loopStart = Mathf.Min(WalkLoopStartIndex, loopEnd);
                frameIndex++;
                if (frameIndex > loopEnd)
                {
                    frameIndex = loopStart;
                }

                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || walkFrames == null || walkFrames.Length == 0)
            {
                return;
            }

            Sprite frame = walkFrames[Mathf.Clamp(frameIndex, 0, walkFrames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void ApplyAnimationFrame(Sprite[] frames)
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
        }

        private void Face(int direction)
        {
            if (direction == 0 || spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.flipX = direction < 0;
        }

        private void CacheTarget()
        {
            if (target != null)
            {
                return;
            }

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void IgnorePlayerBodyCollision()
        {
            if (ignoredPlayerCollision || bodyCollider == null || target == null)
            {
                return;
            }

            Collider2D[] playerColliders = target.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || playerCollider.isTrigger)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(bodyCollider, playerCollider, true);
            }

            ignoredPlayerCollision = true;
        }

        private void OnDied()
        {
            isDead = true;
            body.linearVelocity = Vector2.zero;
            enabled = false;
        }

#if UNITY_EDITOR
        public void EditorAssignFrames(
            Sprite[] walk,
            Sprite[] startup,
            Sprite[] charge,
            Sprite[] impact,
            Sprite[] jumpPrepare = null,
            Sprite[] jump = null,
            Sprite[] melee = null)
        {
            walkFrames = walk ?? System.Array.Empty<Sprite>();
            attackStartupFrames = startup ?? System.Array.Empty<Sprite>();
            chargeLoopFrames = charge ?? System.Array.Empty<Sprite>();
            wallImpactFrames = impact ?? System.Array.Empty<Sprite>();
            jumpPrepareFrames = jumpPrepare ?? System.Array.Empty<Sprite>();
            jumpFrames = jump ?? System.Array.Empty<Sprite>();
            if (melee != null)
            {
                meleeFrames = melee;
            }
        }

        public void EditorAssignMeleeFrames(Sprite[] melee)
        {
            meleeFrames = melee ?? System.Array.Empty<Sprite>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.15f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, aggroRadius);
        }
#endif
    }
}
