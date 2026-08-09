using System.Collections;
using Castlevania2D.Combat;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Animator))]
    public sealed class SimpleHostileEnemy2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float chaseRange = 8f;
        [SerializeField] private float attackRange = 6f;
        [SerializeField] private float attackCooldown = 1f;
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private float projectileSpeed = 6f;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.7f, 0.7f);
        [SerializeField] private Sprite[] deathSprites;
        [SerializeField] private float deathFrameRate = 12f;
        [SerializeField] private float destroyAfterDeathDelay = 1.5f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");

        private Rigidbody2D body;
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private EnemyHealth health;
        private float attackTimer;
        private bool dead;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (attackTimer > 0f)
            {
                attackTimer -= Time.deltaTime;
            }
        }

        private void FixedUpdate()
        {
            if (dead || target == null)
            {
                StopMoving();
                return;
            }

            Vector2 toTarget = target.position - transform.position;
            float distanceToTarget = Mathf.Abs(toTarget.x);

            if (distanceToTarget > chaseRange)
            {
                StopMoving();
                return;
            }

            FaceTarget(toTarget.x);

            if (distanceToTarget <= attackRange)
            {
                StopMoving();
                TryAttack(toTarget);
                return;
            }

            float direction = Mathf.Sign(toTarget.x);
            body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
            animator.SetFloat(SpeedHash, Mathf.Abs(body.linearVelocity.x));
        }

        private void StopMoving()
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            animator.SetFloat(SpeedHash, 0f);
        }

        private void FaceTarget(float horizontalDelta)
        {
            if (spriteRenderer == null || Mathf.Abs(horizontalDelta) <= 0.01f)
            {
                return;
            }

            spriteRenderer.flipX = horizontalDelta < 0f;
        }

        private void TryAttack(Vector2 toTarget)
        {
            if (attackTimer > 0f)
            {
                return;
            }

            animator.SetTrigger(AttackHash);
            SpawnProjectile(toTarget);
            attackTimer = attackCooldown;
        }

        private void SpawnProjectile(Vector2 toTarget)
        {
            Vector2 direction = toTarget.sqrMagnitude > 0f ? toTarget.normalized : Vector2.right;
            float facingSign = direction.x >= 0f ? 1f : -1f;
            Vector3 spawnPosition = transform.position + new Vector3(projectileSpawnOffset.x * facingSign, projectileSpawnOffset.y, 0f);

            GameObject projectileObject = new GameObject("EnemyProjectile");
            projectileObject.transform.position = spawnPosition;

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            projectileRenderer.sprite = projectileSprite;
            projectileRenderer.flipX = direction.x < 0f;

            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = 0.18f;

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            projectileBody.freezeRotation = true;

            EnemyProjectile2D projectile = projectileObject.AddComponent<EnemyProjectile2D>();
            projectile.Launch(gameObject, direction, projectileDamage, projectileSpeed);
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (!dead)
            {
                animator.SetTrigger(HurtHash);
            }
        }

        private void OnDied()
        {
            dead = true;
            FreezeCorpseOnPlatform();
            animator.ResetTrigger(HurtHash);
            animator.ResetTrigger(AttackHash);

            if (deathSprites != null && deathSprites.Length > 0)
            {
                animator.enabled = false;
                StartCoroutine(PlayDeathSprites());
            }
            else
            {
                animator.CrossFadeInFixedTime("Death", 0f, 0, 0f);
            }

            Destroy(gameObject, destroyAfterDeathDelay);

            // Physics is disabled after death, so the corpse remains visually on the platform.
        }

        private void FreezeCorpseOnPlatform()
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            body.simulated = false;
        }

        private IEnumerator PlayDeathSprites()
        {
            float frameDelay = deathFrameRate > 0f ? 1f / deathFrameRate : 0.083333336f;

            for (int i = 0; i < deathSprites.Length; i++)
            {
                spriteRenderer.sprite = deathSprites[i];
                yield return new WaitForSeconds(frameDelay);
            }
        }
    }
}
