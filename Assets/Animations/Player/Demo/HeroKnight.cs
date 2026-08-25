using UnityEngine;
using System.Collections;
using Castlevania2D.Combat;
using Castlevania2D.Player;
using PlayerHealth = Castlevania2D.Health.Health;

public class HeroKnight : MonoBehaviour, IDamageBlocker, IBlockDurability, IProjectileReflectSurface, IForcedJump {

    [SerializeField] float      m_speed = 4.0f;
    [SerializeField] float      m_jumpForce = 7.5f;
    [SerializeField] float      m_rollForce = 6.0f;
    [SerializeField] bool       m_noBlood = false;
    [SerializeField] GameObject m_slideDust;
    [SerializeField] int        m_attackDamage = 25;
    [SerializeField] float      m_attackHitboxActiveTime = 0.18f;
    [SerializeField] Hitbox2D   m_attackHitbox;
    [SerializeField] int        m_maxBlockedAttacks = 7;

    private Animator            m_animator;
    private Rigidbody2D         m_body2d;
    private SpriteRenderer      m_spriteRenderer;
    private PlayerHealth        m_health;
    private CombatKnockbackReceiver2D m_knockbackReceiver;
    private Sensor_HeroKnight   m_groundSensor;
    private Sensor_HeroKnight   m_wallSensorR1;
    private Sensor_HeroKnight   m_wallSensorR2;
    private Sensor_HeroKnight   m_wallSensorL1;
    private Sensor_HeroKnight   m_wallSensorL2;
    private bool                m_isWallSliding = false;
    private bool                m_grounded = false;
    private bool                m_rolling = false;
    private int                 m_facingDirection = 1;
    private int                 m_currentAttack = 0;
    private float               m_timeSinceAttack = 0.0f;
    private float               m_delayToIdle = 0.0f;
    private float               m_rollDuration = 8.0f / 14.0f;
    private float               m_rollCurrentTime;
    private float               m_attackHitboxTimer;
    private bool                m_dead;
    private bool                m_blocking;
    private bool                m_overheadBlockHeld;
    private int                 m_blockedAttacksRemaining;

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
        m_health = GetComponent<PlayerHealth>();
        m_knockbackReceiver = GetComponent<CombatKnockbackReceiver2D>();
        if (m_knockbackReceiver == null)
        {
            m_knockbackReceiver = gameObject.AddComponent<CombatKnockbackReceiver2D>();
        }
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

        // Increase timer that checks roll duration
        if(m_rolling)
            m_rollCurrentTime += Time.deltaTime;

        // Disable rolling if timer extends duration
        if(m_rollCurrentTime > m_rollDuration)
            m_rolling = false;

        //Check if character just landed on the ground
        if (!m_grounded && m_groundSensor.State())
        {
            m_grounded = true;
            m_animator.SetBool("Grounded", m_grounded);
        }

        //Check if character just started falling
        if (m_grounded && !m_groundSensor.State())
        {
            m_grounded = false;
            m_animator.SetBool("Grounded", m_grounded);
        }

        // -- Handle input and movement --
        float inputX = Input.GetAxis("Horizontal");
        bool isOverheadBlocking = m_overheadBlockHeld;

        // Swap direction of sprite depending on walk direction
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

        // Strong enemy launches temporarily override walk input.
        if (m_knockbackReceiver != null
            && m_knockbackReceiver.TryGetKnockbackVelocity(out Vector2 knockbackVelocity))
        {
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
            m_body2d.linearVelocity = new Vector2(inputX * m_speed, m_body2d.linearVelocity.y);
        }

        //Set AirSpeed in animator
        m_animator.SetFloat("AirSpeedY", m_body2d.linearVelocity.y);

        // -- Handle Animations --
        //Wall Slide
        m_isWallSliding = (m_wallSensorR1.State() && m_wallSensorR2.State()) || (m_wallSensorL1.State() && m_wallSensorL2.State());
        m_animator.SetBool("WallSlide", m_isWallSliding);

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
        else if (Input.GetMouseButtonDown(0) && m_timeSinceAttack > 0.25f && !m_rolling && !isOverheadBlocking)
        {
            m_currentAttack++;

            // Loop back to one after third attack
            if (m_currentAttack > 3)
                m_currentAttack = 1;

            // Reset Attack combo if time since last attack is too large
            if (m_timeSinceAttack > 1.0f)
                m_currentAttack = 1;

            // Call one of three attack animations "Attack1", "Attack2", "Attack3"
            m_animator.SetTrigger("Attack" + m_currentAttack);
            BeginAttackHitbox();

            // Reset timer
            m_timeSinceAttack = 0.0f;
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
        // Roll
        else if (Input.GetKeyDown("left shift") && !m_rolling && !m_isWallSliding && !isOverheadBlocking)
        {
            m_rolling = true;
            m_animator.SetTrigger("Roll");
            m_body2d.linearVelocity = new Vector2(m_facingDirection * m_rollForce, m_body2d.linearVelocity.y);
        }
        //Jump
        else if (Input.GetKeyDown("space") && m_grounded && !m_rolling && !isOverheadBlocking)
        {
            ForceJump();
        }

        // IdleBlock selects Idle Block / Idle Block Walk; false keeps normal Idle / Run.
        m_animator.SetBool("IdleBlock", m_overheadBlockHeld);

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

        if (m_animator != null)
        {
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
        if (m_dead)
        {
            return;
        }

        if (m_animator == null)
        {
            m_animator = GetComponent<Animator>();
        }

        if (m_animator != null)
        {
            m_animator.SetTrigger("Hurt");
        }
    }

    private void OnDied()
    {
        m_dead = true;
        EndBlock();
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
        }
    }
}
