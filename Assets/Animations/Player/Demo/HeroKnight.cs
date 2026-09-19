using UnityEngine;
using System.Collections;
using Castlevania2D.Combat;
using Castlevania2D.Level;
using Castlevania2D.Player;
using PlayerHealth = Castlevania2D.Health.Health;

public class HeroKnight : MonoBehaviour, IDamageBlocker, IBlockDurability, IProjectileReflectSurface, IForcedJump, IRopeClimber {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 7.5f;
    [SerializeField] float      m_rollForce = 6.0f;
    [SerializeField] bool       m_noBlood = false;
    [SerializeField] GameObject m_slideDust;
    [SerializeField] int        m_attackDamage = 25;
    [SerializeField] float      m_attackHitboxActiveTime = 0.18f;
    [SerializeField] Hitbox2D   m_attackHitbox;
    [SerializeField] int        m_maxBlockedAttacks = 7;
    [SerializeField] private BoxCollider2D m_attackBox;
    [SerializeField] private Vector2 m_attackBoxSize = new Vector2(0.8f, 1.8f);
    [SerializeField] private Vector2 m_attackBoxOffset;
    [SerializeField] private Vector2 m_slideBodySize = new Vector2(1.8f, 0.8f);
    [SerializeField] private Vector2 m_slideBodyOffset = new Vector2(0f, 0.4f);
    [SerializeField] private Vector2 m_slideAttackBoxSize = new Vector2(1.8f, 0.8f);
    [SerializeField] private Vector2 m_slideAttackBoxOffset = new Vector2(0f, 0.4f);
    [SerializeField] private float m_climbSpeed = 2.4f;
    [SerializeField] private float m_climbRegrabDelay = 0.22f;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private SpriteRenderer      m_spriteRenderer;
    private CapsuleCollider2D   m_bodyCollider;
    private PlayerHealth        m_health;
    private CombatKnockbackReceiver2D m_knockbackReceiver;
    private Sensor_HeroKnight   m_groundSensor;
    private Sensor_HeroKnight   m_wallSensorR1;
    private Sensor_HeroKnight   m_wallSensorR2;
    private Sensor_HeroKnight   m_wallSensorL1;
    private Sensor_HeroKnight   m_wallSensorL2;
    private PhysicsMaterial2D   m_standingBodyMaterial;
    private PhysicsMaterial2D   m_standingRbMaterial;
    private bool                m_zeroFrictionOnWall;
    private static PhysicsMaterial2D s_wallSlideMaterial;
    private readonly ContactPoint2D[] m_contacts = new ContactPoint2D[16];
    private bool                m_isWallSliding = false;
    private bool                m_grounded = false;
    private bool                m_rolling = false;
    private bool                m_rollEntered;
    private bool                m_slideCollidersActive;
    private bool                m_standingCollidersCached;
    private Vector2             m_standingCapsuleSize;
    private Vector2             m_standingCapsuleOffset;
    private CapsuleDirection2D  m_standingCapsuleDirection;
    private Vector2             m_standingAttackSize;
    private Vector2             m_standingAttackOffset;
    private Vector3             m_standingWallSensorR2;
    private Vector3             m_standingWallSensorL2;
    private int                 m_facingDirection = 1;
    private int                 m_currentAttack = 0;
    private float               m_timeSinceAttack = 0.0f;
    private float               m_delayToIdle = 0.0f;
    private float               m_attackHitboxTimer;
    private bool                m_dead;
    private bool                m_blocking;
    private bool                m_overheadBlockHeld;
    private int                 m_blockedAttacksRemaining;
    private static readonly int RunStartState = Animator.StringToHash("RunStart");
    private static readonly int RollState = Animator.StringToHash("Roll");
    private static readonly int RollSlideState = Animator.StringToHash("Roll Slide");
    private static readonly int RollSlideLoopState = Animator.StringToHash("Roll Slide Loop");
    private static readonly int Attack1State = Animator.StringToHash("Attack1");
    private static readonly int Attack2State = Animator.StringToHash("Attack2");
    private static readonly int Attack3State = Animator.StringToHash("Attack3");
    private const int SlideLoopMaxCycles = 5;
    private bool m_queuedAttack;
    private bool m_climbing;
    private bool m_mustLeaveRope;
    private float m_climbRegrabUnlockTime;
    private float m_storedGravityScale = 1f;
    private ClimbableRope2D m_activeRope;
    private ClimbableRope2D m_touchingRope;
    private static readonly int ClimbState = Animator.StringToHash("Climb");

    public bool IsGrounded => m_grounded;
    public int FacingDirection => m_facingDirection;
    public bool IsBlockingProjectiles => m_blocking && !m_dead;

    int IBlockDurability.BlockDurability => m_blocking ? m_blockedAttacksRemaining : 0;
    int IBlockDurability.MaxBlockDurability => Mathf.Max(0, m_maxBlockedAttacks);
    bool IDamageBlocker.IsProjectileReflectActive => IsBlockingProjectiles;

    Vector2 IProjectileReflectSurface.GetReflectNormal(Vector2 hitPoint, Vector2 incomingDirection)
    {
        if (m_overheadBlockHeld)
        {
            return Vector2.up;
        }

        return new Vector2(m_facingDirection >= 0 ? 1f : -1f, 0f);
    }


    // Use this for initialization
    void Awake()
    {
        m_animator = GetComponent<Animator>();
        m_body2d = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_bodyCollider = GetComponent<CapsuleCollider2D>();
        m_health = GetComponent<PlayerHealth>();
        m_knockbackReceiver = GetComponent<CombatKnockbackReceiver2D>();
        if (m_knockbackReceiver == null)
        {
            m_knockbackReceiver = gameObject.AddComponent<CombatKnockbackReceiver2D>();
        }

        ApplyAttackBox();
    }

    private void OnValidate()
    {
        ApplyAttackBox();
    }

    private void ApplyAttackBox()
    {
        if (m_attackBox == null && m_attackHitbox != null)
        {
            m_attackBox = m_attackHitbox.GetComponent<BoxCollider2D>();
        }

        if (m_attackBox == null || m_slideCollidersActive)
        {
            return;
        }

        m_attackBox.size = m_attackBoxSize;
        m_attackBox.offset = m_attackBoxOffset;
        m_attackBox.isTrigger = true;
    }

    void Start ()
    {
        if (m_animator == null)
        {
            Debug.LogError("HeroKnight: Animator component is missing.", this);
            enabled = false;
            return;
        }

        if (m_animator.runtimeAnimatorController == null)
        {
            Debug.LogError("HeroKnight: Animator has no Runtime Animator Controller assigned.", this);
            enabled = false;
            return;
        }

        m_groundSensor = transform.Find("GroundSensor").GetComponent<Sensor_HeroKnight>();
        m_wallSensorR1 = transform.Find("WallSensor_R1").GetComponent<Sensor_HeroKnight>();
        m_wallSensorR2 = transform.Find("WallSensor_R2").GetComponent<Sensor_HeroKnight>();
        m_wallSensorL1 = transform.Find("WallSensor_L1").GetComponent<Sensor_HeroKnight>();
        m_wallSensorL2 = transform.Find("WallSensor_L2").GetComponent<Sensor_HeroKnight>();

        if (m_attackHitbox != null)
        {
            m_attackHitbox.Configure(gameObject, m_facingDirection, m_attackDamage);
            m_attackHitbox.EndSwing();
        }

        CacheStandingColliders();
    }

    private void OnEnable()
    {
        m_health = GetComponent<PlayerHealth>();

        if (m_health != null)
        {
            m_health.Damaged += OnDamaged;
            m_health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        if (m_health != null)
        {
            m_health.Damaged -= OnDamaged;
            m_health.Died -= OnDied;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (m_dead || m_climbing || m_rolling || m_body2d == null)
        {
            return;
        }

        float inputX = Input.GetAxisRaw("Horizontal");
        if (!IsAirborneAgainstWall(inputX))
        {
            return;
        }

        Vector2 velocity = m_body2d.linearVelocity;
        velocity.x = 0f;
        m_body2d.linearVelocity = velocity;
        SetWallSlideFriction(true);
    }

    void Update ()
    {
        if (m_dead)
        {
            StopAttackHitbox();
            return;
        }

        if (m_animator == null)
        {
            return;
        }

        // Increase timer that controls attack combo
        m_timeSinceAttack += Time.deltaTime;
        TickAttackHitbox();
        TickRoll();

        if (m_climbing)
        {
            TickClimb();
            return;
        }

        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        TryMountFromTouch();

        // -- Handle input and movement --
        float inputX = Input.GetAxisRaw("Horizontal");
        bool isOverheadBlocking = m_overheadBlockHeld;

        // Swap direction of sprite depending on walk direction
        if (!m_rolling)
        {
            if (inputX > 0)
            {
                m_spriteRenderer.flipX = false;
                m_facingDirection = 1;
            }
            else if (inputX < 0)
            {
                m_spriteRenderer.flipX = true;
                m_facingDirection = -1;
            }
        }

        // Strong enemy launches (Likho kick / Giant Likho) override walk and roll.
        if (m_knockbackReceiver != null
            && m_knockbackReceiver.TryGetKnockbackVelocity(out Vector2 knockbackVelocity))
        {
            if (m_rolling)
            {
                EndRoll();
            }

            if (m_climbing)
            {
                EndClimb();
            }

            Vector2 launched = m_body2d.linearVelocity;
            launched.x = knockbackVelocity.x;
            if (Mathf.Abs(knockbackVelocity.y) > 0.01f)
            {
                launched.y = knockbackVelocity.y;
            }

            m_body2d.linearVelocity = launched;
        }
        // Move (overhead block allows horizontal walk; uses IdleBlockWalk clip)
        else if (!m_rolling)
        {
            bool slideDownWall = IsAirborneAgainstWall(inputX);
            float walkSpeed = IsWalkStartupLocked() || slideDownWall ? 0f : inputX * m_speed;
            m_body2d.linearVelocity = new Vector2(walkSpeed, m_body2d.linearVelocity.y);
            SetWallSlideFriction(slideDownWall);
        }
        else
        {
            SetWallSlideFriction(false);
        }

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        // -- Handle Animations --
        //Wall Slide
        m_isWallSliding = false;
        m_animator.SetBool("WallSlide", false);

        bool inAttack = IsInAttack();
        if (m_queuedAttack && !inAttack)
        {
            m_queuedAttack = false;
            BeginNextAttack();
            inAttack = true;
        }

        bool overheadBlockPressed =
            !m_rolling &&
            ((Input.GetMouseButtonDown(1) && Input.GetKey(KeyCode.W)) ||
             (Input.GetKeyDown(KeyCode.W) && Input.GetMouseButton(1)));

        if (m_overheadBlockHeld && (!Input.GetMouseButton(1) || !Input.GetKey(KeyCode.W)))
        {
            EndBlock();
        }

        // Overhead block only: hold W + right mouse button.
        if (overheadBlockPressed)
        {
            BeginBlock();
            m_overheadBlockHeld = true;
            m_animator.ResetTrigger("Block");
        }
        //Attack
        else if (Input.GetMouseButtonDown(0) && !m_rolling && !isOverheadBlocking)
        {
            if (inAttack)
            {
                m_queuedAttack = true;
            }
            else
            {
                BeginNextAttack();
            }
        }
        // Normal block: right mouse button only (no W).
        else if (Input.GetMouseButtonDown(1) && !Input.GetKey(KeyCode.W) && !m_rolling && !isOverheadBlocking)
        {
            BeginBlock();
            m_overheadBlockHeld = false;
            m_animator.SetTrigger("Block");
        }
        else if (Input.GetMouseButtonUp(1) && !m_overheadBlockHeld)
        {
            EndBlock();
        }
        // Roll / belly slide. Hold Left Shift to keep sliding on frames 9-10.
        else if (Input.GetKeyDown(KeyCode.LeftShift) && !m_rolling && !m_isWallSliding && !isOverheadBlocking)
        {
            BeginRoll();
        }
        //Jump
        else if (Input.GetKeyDown("space") && m_grounded && !m_rolling && !isOverheadBlocking)
        {
            ForceJump();
        }

        // IdleBlock selects Idle Block / Idle Block Walk; false keeps normal Idle / Run.
        m_animator.SetBool("IdleBlock", m_overheadBlockHeld);
        m_animator.SetBool("FrontBlock", m_blocking && !m_overheadBlockHeld);

        // Run / overhead-block walk (AnimState 1). Always update (not gated by attack/block edges).
        if (Mathf.Abs(inputX) > Mathf.Epsilon)
        {
            m_delayToIdle = 0.05f;
            m_animator.SetInteger("AnimState", 1);
        }
        else
        {
            m_delayToIdle -= Time.deltaTime;
            if (m_delayToIdle < 0)
                m_animator.SetInteger("AnimState", 0);
        }
    }

    public void ForceJump(float forceMultiplier = 1f)
    {
        if (m_dead || m_body2d == null)
        {
            return;
        }

        if (m_climbing)
        {
            EndClimb();
        }

        if (m_animator != null)
        {
            m_queuedAttack = false;
            m_animator.SetTrigger("Jump");
            m_animator.SetBool("Grounded", false);
        }

        m_grounded = false;
        float jumpSpeed = m_jumpForce * Mathf.Max(0f, forceMultiplier);
        m_body2d.linearVelocity = new Vector2(m_body2d.linearVelocity.x, jumpSpeed);
        if (m_groundSensor != null)
        {
            m_groundSensor.Disable(0.2f);
        }

        TryMountFromTouch();
    }

    public void NotifyRopeTouch(ClimbableRope2D rope)
    {
        if (rope == null)
        {
            return;
        }

        m_touchingRope = rope;
        TryMountFromTouch();
    }

    public void NotifyRopeLeave(ClimbableRope2D rope)
    {
        if (m_touchingRope == rope)
        {
            m_touchingRope = null;
        }

        m_mustLeaveRope = false;
    }

    private void TryMountFromTouch()
    {
        if (m_climbing
            || m_dead
            || m_rolling
            || m_grounded
            || m_touchingRope == null
            || m_mustLeaveRope
            || Time.time < m_climbRegrabUnlockTime)
        {
            return;
        }

        BeginClimb(m_touchingRope);
    }

    private void BeginClimb(ClimbableRope2D rope)
    {
        if (rope == null || m_body2d == null)
        {
            return;
        }

        if (m_rolling)
        {
            EndRoll();
        }

        EndBlock();
        m_queuedAttack = false;
        m_climbing = true;
        m_activeRope = rope;
        m_storedGravityScale = m_body2d.gravityScale;
        m_body2d.gravityScale = 0f;
        m_body2d.linearVelocity = Vector2.zero;
        m_grounded = false;

        rope.GetClimbRange(out float bottomY, out float topY);
        Vector3 position = transform.position;
        position.x = rope.GrabX;
        position.y = Mathf.Clamp(position.y, bottomY, topY);
        transform.position = position;

        if (m_animator != null)
        {
            m_animator.ResetTrigger("Jump");
            m_animator.ResetTrigger("Attack1");
            m_animator.ResetTrigger("Attack2");
            m_animator.ResetTrigger("Attack3");
            m_animator.ResetTrigger("Roll");
            m_animator.SetBool("Grounded", false);
            m_animator.SetBool("Climbing", true);
            m_animator.SetFloat("ClimbSpeed", 0f);
            m_animator.Play(ClimbState, 0, 0f);
        }
    }

    private void TickClimb()
    {
        if (m_activeRope == null || !m_activeRope.isActiveAndEnabled)
        {
            EndClimb();
            return;
        }

        if (m_knockbackReceiver != null && m_knockbackReceiver.IsActive)
        {
            EndClimb();
            return;
        }

        if (Input.GetKeyDown("space"))
        {
            ForceJump();
            return;
        }

        float inputY = Input.GetAxisRaw("Vertical");
        m_activeRope.GetClimbRange(out float bottomY, out float topY);
        Vector3 position = transform.position;
        position.x = m_activeRope.GrabX;
        position.y = Mathf.Clamp(position.y + inputY * m_climbSpeed * Time.deltaTime, bottomY, topY);
        transform.position = position;
        m_body2d.linearVelocity = Vector2.zero;

        if (m_animator != null)
        {
            m_animator.SetBool("Climbing", true);
            m_animator.SetFloat("ClimbSpeed", inputY);
            m_animator.SetFloat("AirSpeedY", 0f);
        }

        if (inputY < -0.01f && position.y <= bottomY + 0.02f)
        {
            EndClimb();
        }
    }

    private void EndClimb()
    {
        if (!m_climbing && m_activeRope == null)
        {
            return;
        }

        m_climbing = false;
        m_activeRope = null;
        m_mustLeaveRope = m_touchingRope != null;
        m_climbRegrabUnlockTime = Time.time + m_climbRegrabDelay;

        if (m_body2d != null)
        {
            m_body2d.gravityScale = m_storedGravityScale;
        }

        if (m_animator != null)
        {
            m_animator.SetBool("Climbing", false);
            m_animator.SetFloat("ClimbSpeed", 0f);
        }
    }

    // Animation Events
    // Called in slide animation.
    void AE_SlideDust()
    {
        Vector3 spawnPosition;

        if (m_facingDirection == 1)
            spawnPosition = m_wallSensorR2.transform.position;
        else
            spawnPosition = m_wallSensorL2.transform.position;

        if (m_slideDust != null)
        {
            // Set correct arrow spawn position
            GameObject dust = Instantiate(m_slideDust, spawnPosition, gameObject.transform.localRotation) as GameObject;
            // Turn arrow in correct direction
            dust.transform.localScale = new Vector3(m_facingDirection, 1, 1);
        }
    }

    private bool IsWalkStartupLocked()
    {
        return m_animator != null
            && m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash == RunStartState;
    }

    private bool IsAirborneAgainstWall(float inputX)
    {
        return !HasFloorContact() && IsPressingIntoWall(inputX);
    }

    private bool HasFloorContact()
    {
        if (m_body2d == null)
        {
            return m_grounded;
        }

        int count = m_body2d.GetContacts(m_contacts);
        for (int i = 0; i < count; i++)
        {
            if (m_contacts[i].normal.y > 0.65f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPressingIntoWall(float inputX)
    {
        if (Mathf.Abs(inputX) < 0.01f)
        {
            return false;
        }

        float inputSign = Mathf.Sign(inputX);
        if (m_body2d != null)
        {
            int count = m_body2d.GetContacts(m_contacts);
            for (int i = 0; i < count; i++)
            {
                float normalX = m_contacts[i].normal.x;
                if (Mathf.Abs(normalX) >= 0.65f && inputSign * normalX < 0f)
                {
                    return true;
                }
            }
        }

        return CastHitsWall(inputSign);
    }

    private bool CastHitsWall(float inputSign)
    {
        if (m_bodyCollider == null)
        {
            return false;
        }

        Vector2 origin = (Vector2)transform.position + m_bodyCollider.offset;
        RaycastHit2D[] hits = Physics2D.CapsuleCastAll(
            origin,
            m_bodyCollider.size,
            m_bodyCollider.direction,
            0f,
            new Vector2(inputSign, 0f),
            0.08f);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.collider == m_bodyCollider)
            {
                continue;
            }

            if (hit.rigidbody == m_body2d)
            {
                continue;
            }

            if (Mathf.Abs(hit.normal.x) >= 0.65f && inputSign * hit.normal.x < 0f)
            {
                return true;
            }
        }

        return false;
    }

    private void SetWallSlideFriction(bool slidingDown)
    {
        if (slidingDown == m_zeroFrictionOnWall)
        {
            return;
        }

        if (!m_zeroFrictionOnWall)
        {
            if (m_bodyCollider != null)
            {
                m_standingBodyMaterial = m_bodyCollider.sharedMaterial;
            }

            if (m_body2d != null)
            {
                m_standingRbMaterial = m_body2d.sharedMaterial;
            }
        }

        if (slidingDown && s_wallSlideMaterial == null)
        {
            s_wallSlideMaterial = new PhysicsMaterial2D("HeroKnightWallSlide")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        m_zeroFrictionOnWall = slidingDown;
        PhysicsMaterial2D material = slidingDown ? s_wallSlideMaterial : m_standingBodyMaterial;
        if (m_bodyCollider != null)
        {
            m_bodyCollider.sharedMaterial = material;
        }

        if (m_body2d != null)
        {
            m_body2d.sharedMaterial = slidingDown ? s_wallSlideMaterial : m_standingRbMaterial;
        }
    }

    private void BeginNextAttack()
    {
        m_currentAttack++;
        if (m_currentAttack > 3)
        {
            m_currentAttack = 1;
        }

        if (m_timeSinceAttack > 1.0f)
        {
            m_currentAttack = 1;
        }

        m_animator.ResetTrigger("Attack1");
        m_animator.ResetTrigger("Attack2");
        m_animator.ResetTrigger("Attack3");
        m_animator.SetTrigger("Attack" + m_currentAttack);
        BeginAttackHitbox();
        m_timeSinceAttack = 0.0f;
    }

    private bool IsInAttack()
    {
        if (m_animator == null)
        {
            return false;
        }

        int current = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (IsAttackStateHash(current))
        {
            return true;
        }

        return m_animator.IsInTransition(0)
            && IsAttackStateHash(m_animator.GetNextAnimatorStateInfo(0).shortNameHash);
    }

    private static bool IsAttackStateHash(int hash)
    {
        return hash == Attack1State || hash == Attack2State || hash == Attack3State;
    }

    private void BeginRoll()
    {
        m_queuedAttack = false;
        m_rolling = true;
        m_rollEntered = false;
        m_animator.ResetTrigger("Roll");
        m_animator.SetTrigger("Roll");
        m_animator.SetBool("RollHeld", true);
        m_body2d.linearVelocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.linearVelocity.y);
    }

    private void TickRoll()
    {
        if (!m_rolling)
        {
            SetSlideColliders(false);
            return;
        }

        if (m_knockbackReceiver != null && m_knockbackReceiver.IsActive)
        {
            EndRoll();
            return;
        }

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) && !HasFinishedSlideLoopLimit();
        m_animator.SetBool("RollHeld", shiftHeld);

        bool inRollMove = IsInRollMove();
        if (inRollMove)
        {
            m_rollEntered = true;
        }
        else if (m_rollEntered)
        {
            EndRoll();
            return;
        }

        m_body2d.linearVelocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.linearVelocity.y);
        SetSlideColliders(IsInSlideMove());
    }

    private void EndRoll()
    {
        m_rolling = false;
        m_rollEntered = false;

        if (m_animator != null)
        {
            m_animator.SetBool("RollHeld", false);
        }

        SetSlideColliders(false);
    }

    private bool IsInRollMove()
    {
        if (m_animator == null)
        {
            return false;
        }

        int current = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (IsRollStateHash(current))
        {
            return true;
        }

        return m_animator.IsInTransition(0)
            && IsRollStateHash(m_animator.GetNextAnimatorStateInfo(0).shortNameHash);
    }

    private bool HasFinishedSlideLoopLimit()
    {
        if (m_animator == null)
        {
            return false;
        }

        AnimatorStateInfo current = m_animator.GetCurrentAnimatorStateInfo(0);
        return current.shortNameHash == RollSlideLoopState
            && current.normalizedTime >= SlideLoopMaxCycles;
    }

    private bool IsInSlideMove()
    {
        if (m_animator == null)
        {
            return false;
        }

        int current = m_animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (current == RollSlideState || current == RollSlideLoopState)
        {
            return true;
        }

        return m_animator.IsInTransition(0)
            && (m_animator.GetNextAnimatorStateInfo(0).shortNameHash == RollSlideState
                || m_animator.GetNextAnimatorStateInfo(0).shortNameHash == RollSlideLoopState);
    }

    private static bool IsRollStateHash(int hash)
    {
        return hash == RollState || hash == RollSlideState || hash == RollSlideLoopState;
    }

    private void CacheStandingColliders()
    {
        if (m_standingCollidersCached)
        {
            return;
        }

        if (m_bodyCollider == null)
        {
            m_bodyCollider = GetComponent<CapsuleCollider2D>();
        }

        if (m_bodyCollider != null)
        {
            m_standingCapsuleSize = m_bodyCollider.size;
            m_standingCapsuleOffset = m_bodyCollider.offset;
            m_standingCapsuleDirection = m_bodyCollider.direction;
        }

        if (m_attackBox != null)
        {
            m_standingAttackSize = m_attackBox.size;
            m_standingAttackOffset = m_attackBox.offset;
        }
        else
        {
            m_standingAttackSize = m_attackBoxSize;
            m_standingAttackOffset = m_attackBoxOffset;
        }

        if (m_wallSensorR2 != null)
        {
            m_standingWallSensorR2 = m_wallSensorR2.transform.localPosition;
        }

        if (m_wallSensorL2 != null)
        {
            m_standingWallSensorL2 = m_wallSensorL2.transform.localPosition;
        }

        m_standingCollidersCached = true;
    }

    private void SetSlideColliders(bool sliding)
    {
        if (sliding == m_slideCollidersActive)
        {
            return;
        }

        CacheStandingColliders();
        m_slideCollidersActive = sliding;

        if (m_bodyCollider != null)
        {
            if (sliding)
            {
                float standingBottom = m_standingCapsuleOffset.y - m_standingCapsuleSize.y * 0.5f;
                m_bodyCollider.direction = CapsuleDirection2D.Horizontal;
                m_bodyCollider.size = m_slideBodySize;
                m_bodyCollider.offset = new Vector2(
                    m_slideBodyOffset.x,
                    standingBottom + m_slideBodySize.y * 0.5f);
            }
            else
            {
                m_bodyCollider.direction = m_standingCapsuleDirection;
                m_bodyCollider.size = m_standingCapsuleSize;
                m_bodyCollider.offset = m_standingCapsuleOffset;
            }
        }

        if (m_attackBox != null)
        {
            if (sliding)
            {
                m_attackBox.size = m_slideAttackBoxSize;
                m_attackBox.offset = m_slideAttackBoxOffset;
            }
            else
            {
                m_attackBox.size = m_standingAttackSize;
                m_attackBox.offset = m_standingAttackOffset;
            }
        }

        float sensorY = sliding ? m_slideBodyOffset.y : 0f;
        if (m_wallSensorR2 != null)
        {
            Vector3 position = sliding
                ? new Vector3(m_standingWallSensorR2.x, sensorY, m_standingWallSensorR2.z)
                : m_standingWallSensorR2;
            m_wallSensorR2.transform.localPosition = position;
        }

        if (m_wallSensorL2 != null)
        {
            Vector3 position = sliding
                ? new Vector3(m_standingWallSensorL2.x, sensorY, m_standingWallSensorL2.z)
                : m_standingWallSensorL2;
            m_wallSensorL2.transform.localPosition = position;
        }
    }

    private void BeginAttackHitbox()
    {
        if (m_attackHitbox == null)
        {
            return;
        }

        m_attackHitbox.Configure(gameObject, m_facingDirection, m_attackDamage);
        m_attackHitbox.BeginSwing();
        m_attackHitboxTimer = m_attackHitboxActiveTime;
    }

    private void TickAttackHitbox()
    {
        if (m_attackHitbox == null || m_attackHitboxTimer <= 0f)
        {
            return;
        }

        m_attackHitboxTimer -= Time.deltaTime;

        if (m_attackHitboxTimer <= 0f)
        {
            StopAttackHitbox();
        }
    }

    private void StopAttackHitbox()
    {
        m_attackHitboxTimer = 0f;

        if (m_attackHitbox != null)
        {
            m_attackHitbox.EndSwing();
        }
    }

    private void OnDamaged(DamageInfo damage)
    {
        if (m_dead || m_rolling)
        {
            return;
        }

        if (m_animator == null)
        {
            m_animator = GetComponent<Animator>();
        }

        if (m_climbing)
        {
            EndClimb();
        }

        if (m_animator != null)
        {
            m_queuedAttack = false;
            m_animator.SetTrigger("Hurt");
        }
    }

    private void OnDied()
    {
        m_dead = true;
        m_queuedAttack = false;
        EndBlock();
        EndRoll();
        EndClimb();
        StopAttackHitbox();

        if (m_body2d == null)
        {
            m_body2d = GetComponent<Rigidbody2D>();
        }

        if (m_animator == null)
        {
            m_animator = GetComponent<Animator>();
        }

        if (m_body2d != null)
        {
            m_body2d.linearVelocity = Vector2.zero;
        }

        if (m_animator != null)
        {
            m_animator.SetBool("noBlood", m_noBlood);
            m_animator.SetTrigger("Death");
        }
    }

    public bool IsBlockingDamage(DamageInfo damage)
    {
        if (!m_blocking || m_dead || m_blockedAttacksRemaining <= 0)
        {
            return false;
        }

        m_blockedAttacksRemaining--;

        if (m_blockedAttacksRemaining <= 0)
        {
            EndBlock();
        }

        return true;
    }

    private void BeginBlock()
    {
        m_blocking = true;
        m_blockedAttacksRemaining = Mathf.Max(0, m_maxBlockedAttacks);
    }

    private void EndBlock()
    {
        m_blocking = false;
        m_overheadBlockHeld = false;

        if (m_animator == null)
        {
            m_animator = GetComponent<Animator>();
        }

        if (m_animator != null)
        {
            m_animator.SetBool("IdleBlock", false);
            m_animator.SetBool("FrontBlock", false);
        }
    }
}
