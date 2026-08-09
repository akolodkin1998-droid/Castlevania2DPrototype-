using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>Owns the mouse death clip and physical fall after its health is depleted.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MouseDeathAnimator2D : MonoBehaviour
    {
        [Header("Death Animation")]
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField] private float frameRate = 12f;

        [Header("Fall")]
        [SerializeField] private float gravityScale = 3.5f;
        [SerializeField] private float minimumFallTime = 0.15f;
        [SerializeField] private float landingDelay = 0.35f;
        [SerializeField] private float maximumFallTime = 8f;
        [SerializeField] private float groundNormalThreshold = 0.5f;
        [SerializeField] private float settledVelocityThreshold = 0.15f;
        [Tooltip("Extra downward snap after landing so the death sprite sits on the floor.")]
        [SerializeField] private float landingSinkOffset = 0.08f;
        [SerializeField] private Vector2 deathColliderSize = new Vector2(0.55f, 0.22f);
        [SerializeField] private Vector2 deathColliderOffset = new Vector2(0f, 0.11f);

        private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[8];

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private BoxCollider2D boxCollider;
        private EnemyHealth health;
        private MouseIdleEnemy2D idleAnimation;
        private MouseDiveAttack2D diveAttack;
        private MouseHurtAnimator2D hurtAnimation;
        private bool isDead;
        private bool hasLanded;
        private bool animationFinished;
        private int frameIndex;
        private float frameTimer;
        private float deathStartTime;
        private float landedTime;
        private float lowestContactY = float.PositiveInfinity;

#if UNITY_EDITOR
        public void EditorAssignDeathFrames(Sprite[] frames)
        {
            deathFrames = frames ?? System.Array.Empty<Sprite>();
        }
#endif

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            boxCollider = bodyCollider as BoxCollider2D;
            health = GetComponent<EnemyHealth>();
            idleAnimation = GetComponent<MouseIdleEnemy2D>();
            diveAttack = GetComponent<MouseDiveAttack2D>();
            hurtAnimation = GetComponent<MouseHurtAnimator2D>();
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
            if (!isDead)
            {
                return;
            }

            AdvanceAnimation();
            TryFinishDeath();
        }

        private void FixedUpdate()
        {
            if (!isDead || hasLanded)
            {
                return;
            }

            if (Time.time - deathStartTime >= minimumFallTime && IsGrounded())
            {
                RegisterLanding();
            }
            else if (Time.time - deathStartTime >= maximumFallTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryRegisterLandingFromCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryRegisterLandingFromCollision(collision);
        }

        private void OnDied()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            deathStartTime = Time.time;
            lowestContactY = float.PositiveInfinity;
            idleAnimation?.SetAnimationPlaying(false);
            diveAttack?.SetDead();
            hurtAnimation?.SetDead();

            ConfigureDeathPhysics();
            IgnoreTargetCollisions();

            frameIndex = 0;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        private void ConfigureDeathPhysics()
        {
            if (boxCollider != null)
            {
                // Combat hitbox is tall; a small foot collider lets the corpse reach the floor.
                boxCollider.size = deathColliderSize;
                boxCollider.offset = deathColliderOffset;
            }

            bodyCollider.enabled = true;
            bodyCollider.isTrigger = false;
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = Vector2.zero;
        }

        private void IgnoreTargetCollisions()
        {
            Transform target = diveAttack != null ? diveAttack.Target : null;
            if (target == null || bodyCollider == null)
            {
                return;
            }

            Collider2D[] targetColliders = target.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider2D other = targetColliders[i];
                if (other != null && other != bodyCollider)
                {
                    Physics2D.IgnoreCollision(bodyCollider, other, true);
                }
            }
        }

        private void AdvanceAnimation()
        {
            if (animationFinished || deathFrames == null || deathFrames.Length == 0)
            {
                animationFinished = true;
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (frameIndex >= deathFrames.Length - 1)
                {
                    animationFinished = true;
                    return;
                }

                frameIndex++;
                ApplyCurrentFrame();
            }
        }

        private void TryRegisterLandingFromCollision(Collision2D collision)
        {
            if (!isDead || hasLanded || Time.time - deathStartTime < minimumFallTime)
            {
                return;
            }

            if (!HasGroundContact(collision))
            {
                return;
            }

            CacheLowestContact(collision);
            RegisterLanding();
        }

        private bool IsGrounded()
        {
            if (body.linearVelocity.y > settledVelocityThreshold)
            {
                return false;
            }

            int contactCount = body.GetContacts(contactBuffer);
            bool grounded = false;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = contactBuffer[i];
                if (contact.normal.y >= groundNormalThreshold)
                {
                    grounded = true;
                    if (contact.point.y < lowestContactY)
                    {
                        lowestContactY = contact.point.y;
                    }
                }
            }

            return grounded;
        }

        private bool HasGroundContact(Collision2D collision)
        {
            if (collision == null || body.linearVelocity.y > settledVelocityThreshold)
            {
                return false;
            }

            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= groundNormalThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheLowestContact(Collision2D collision)
        {
            int contactCount = collision.contactCount;
            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                if (contact.normal.y >= groundNormalThreshold && contact.point.y < lowestContactY)
                {
                    lowestContactY = contact.point.y;
                }
            }
        }

        private void RegisterLanding()
        {
            if (hasLanded)
            {
                return;
            }

            hasLanded = true;
            landedTime = Time.time;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            SnapSpriteToGround();
        }

        private void SnapSpriteToGround()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            float groundY = float.IsPositiveInfinity(lowestContactY)
                ? bodyCollider.bounds.min.y
                : lowestContactY;

            float spriteBottom = spriteRenderer.bounds.min.y;
            float sink = (spriteBottom - groundY) + Mathf.Max(0f, landingSinkOffset);
            if (sink > 0f)
            {
                body.position = new Vector2(body.position.x, body.position.y - sink);
            }
        }

        private void TryFinishDeath()
        {
            if (hasLanded && animationFinished && Time.time - landedTime >= landingDelay)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyCurrentFrame()
        {
            if (spriteRenderer != null
                && deathFrames != null
                && deathFrames.Length > 0
                && deathFrames[frameIndex] != null)
            {
                spriteRenderer.sprite = deathFrames[frameIndex];
                spriteRenderer.enabled = true;
            }
        }
    }
}
