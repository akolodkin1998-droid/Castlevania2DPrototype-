using Castlevania2D.Environment;
using Castlevania2D.Input;
using Castlevania2D.Movement;
using UnityEngine;

namespace Castlevania2D.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class EnemyProjectile2D : MonoBehaviour
    {
        [SerializeField] private int damage = 10;
        [SerializeField] private float speed = 6f;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private LayerMask targetMask;
        [Tooltip("When true, unreflected projectiles only damage the player (no friendly fire on other enemies). Cleared on shield reflect so ricochets can hit enemies.")]
        [SerializeField] private bool damagePlayersOnly = true;
        [Tooltip("Damage dealt after a shield ricochet. -1 uses the launch damage.")]
        [SerializeField] private int reflectedDamage = -1;
        [SerializeField] private float reflectSeparation = 0.2f;
        [SerializeField] private float reflectedLifetimeBonus = 3f;
        [Tooltip("Extra world units below the camera view before a downward reflected projectile is culled.")]
        [SerializeField] private float reflectedDownwardOffscreenPadding = 1f;
        [Tooltip("Speed multiplier applied on every shield ricochet (horizontal and arc).")]
        [SerializeField] private float reflectedSpeedMultiplier = 1.55f;
        [Tooltip("Gravity scale for overhead shield ricochets so the projectile arcs up then falls.")]
        [SerializeField] private float reflectedGravityScale = 1.2f;
        [Tooltip("Extra speed multiplier stacked on arc ricochets (overhead block).")]
        [SerializeField] private float reflectedUpwardSpeedMultiplier = 1.1f;
        [Tooltip("World units below the camera top edge used when capping reflected arc peak height.")]
        [SerializeField] private float reflectedArcTopMargin = 0.35f;
        [Tooltip("Continuous Z spin while flying. 0 = no spin (default for existing projectiles).")]
        [SerializeField] private float spinDegreesPerSecond;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Camera viewCamera;
        private GameObject owner;
        private GameObject reflectedBy;
        private Vector2 direction = Vector2.right;
        private float lifetimeTimer;
        private bool isPlayerReflected;
        private bool usesReflectedArc;
        /// <summary>Runtime copy of damagePlayersOnly; set false on shield reflect so ricochets hit enemies.</summary>
        private bool damagePlayersOnlyActive;

        public bool IsPlayerReflected => isPlayerReflected;

        public bool WasReflectedBy(GameObject entityRoot)
        {
            if (!isPlayerReflected || entityRoot == null || reflectedBy == null)
            {
                return false;
            }

            if (reflectedBy == entityRoot)
            {
                return true;
            }

            return reflectedBy.transform.IsChildOf(entityRoot.transform)
                   || entityRoot.transform.IsChildOf(reflectedBy.transform);
        }

        public void ConfigureSpin(float degreesPerSecond)
        {
            spinDegreesPerSecond = degreesPerSecond;
            ApplySpinConstraints();
        }

        public void Launch(GameObject newOwner, Vector2 newDirection, int newDamage, float newSpeed)
        {
            owner = newOwner;
            direction = newDirection.sqrMagnitude > 0f ? newDirection.normalized : Vector2.right;
            damage = Mathf.Max(0, newDamage);
            speed = Mathf.Max(0f, newSpeed);
            lifetimeTimer = lifetime;
            isPlayerReflected = false;
            reflectedBy = null;
            usesReflectedArc = false;
            damagePlayersOnlyActive = damagePlayersOnly;

            if (body != null)
            {
                body.gravityScale = 0f;
            }

            ApplySpinConstraints();
            ApplyVelocity();
            UpdateVisualFacing();
        }

        private void Awake()
        {
            Collider2D projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            damagePlayersOnlyActive = damagePlayersOnly;

            if (body != null)
            {
                body.gravityScale = 0f;
            }

            ApplySpinConstraints();
            ApplyVelocity();
            lifetimeTimer = lifetime;
        }

        private void Update()
        {
            if (body == null)
            {
                transform.position += (Vector3)(direction * speed * Time.deltaTime);
                if (Mathf.Abs(spinDegreesPerSecond) > 0.01f)
                {
                    transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime, Space.Self);
                }
            }

            if (isPlayerReflected)
            {
                SyncDirectionFromVelocity();

                if (TryDespawnDownwardReflectedProjectile())
                {
                    return;
                }
            }

            if (!ShouldExpireFromLifetime())
            {
                return;
            }

            lifetimeTimer -= Time.deltaTime;
            if (lifetimeTimer <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private bool TryDespawnDownwardReflectedProjectile()
        {
            if (GetVerticalSpeed() >= 0f)
            {
                return false;
            }

            if (!IsBelowCameraView())
            {
                return false;
            }

            Destroy(gameObject);
            return true;
        }

        private bool IsBelowCameraView()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (viewCamera == null || !viewCamera.orthographic)
            {
                return false;
            }

            float minY = viewCamera.transform.position.y - viewCamera.orthographicSize - reflectedDownwardOffscreenPadding;
            return transform.position.y < minY;
        }

        private void ApplySpinConstraints()
        {
            if (body == null)
            {
                return;
            }

            bool spinning = Mathf.Abs(spinDegreesPerSecond) > 0.01f;
            body.freezeRotation = !spinning;
            body.angularVelocity = spinning ? spinDegreesPerSecond : 0f;
        }

        private void UpdateVisualFacing()
        {
            // Spinning mud uses continuous Z rotation; flipX would fight the spin.
            if (spriteRenderer == null || Mathf.Abs(spinDegreesPerSecond) > 0.01f)
            {
                return;
            }

            spriteRenderer.flipX = direction.x < 0f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ShouldIgnoreCollider(other))
            {
                return;
            }

            // Eagle stones aimed at baskets: register hit before player/enemy damage checks.
            if (!isPlayerReflected)
            {
                StoneBasketHitStages2D basket = other.GetComponentInParent<StoneBasketHitStages2D>();
                if (basket != null)
                {
                    basket.RegisterHit();
                    Destroy(gameObject);
                    return;
                }
            }

            // Layer mask applies to enemy-fired shots only; reflected ricochets may hit any IDamageable (enemies).
            if (!isPlayerReflected
                && targetMask.value != 0
                && (targetMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.CanReceiveDamage)
            {
                return;
            }

            if (isPlayerReflected)
            {
                if (TryReReflectOffShield(other))
                {
                    return;
                }

                // Skip the reflecting player (shot flies away); still allow enemy Health hits.
                if (IsSameEntity(other, reflectedBy))
                {
                    return;
                }

                ApplyReflectedHit(other, damageable);
                return;
            }

            // Enemy-fired shots must not friendly-fire other enemies (Ent mud, Flower Boss, etc.).
            if (damagePlayersOnlyActive && !IsPlayerTarget(other))
            {
                return;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            DamageResult result = damageable.ReceiveDamage(new DamageInfo(damage, owner, hitPoint, direction));

            if (result == DamageResult.Blocked)
            {
                ReflectOffShield(other, hitPoint);
                return;
            }

            if (result == DamageResult.Applied)
            {
                Destroy(gameObject);
            }
        }

        private bool ShouldIgnoreCollider(Collider2D other)
        {
            return !isPlayerReflected && IsSameEntity(other, owner);
        }

        private static bool IsSameEntity(Collider2D other, GameObject root)
        {
            return root != null &&
                   (other.gameObject == root || other.transform.IsChildOf(root.transform));
        }

        private static bool IsPlayerTarget(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            Transform root = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;

            if (root.CompareTag("Player"))
            {
                return true;
            }

            // Demo hero lives outside this asmdef (HeroKnight); identify by name / shared player components.
            if (other.GetComponentInParent<PlayerInputReader>() != null
                || other.GetComponentInParent<CharacterMotor2D>() != null)
            {
                return true;
            }

            string name = root.name;
            return name.Equals("Player_HeroKnight", System.StringComparison.Ordinal)
                   || name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool TryReReflectOffShield(Collider2D other)
        {
            if (!isPlayerReflected || reflectedBy == null || !IsSameEntity(other, reflectedBy))
            {
                return false;
            }

            IDamageBlocker blocker = other.GetComponentInParent<IDamageBlocker>();
            if (blocker == null || !blocker.IsProjectileReflectActive)
            {
                return false;
            }

            Vector2 hitPoint = other.ClosestPoint(transform.position);
            ReflectOffShield(other, hitPoint);
            return true;
        }

        private void ApplyReflectedHit(Collider2D other, IDamageable damageable)
        {
            int hitDamage = GetReflectedDamage();
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            GameObject source = reflectedBy != null ? reflectedBy : owner;
            damageable.ReceiveDamage(new DamageInfo(hitDamage, source, hitPoint, direction));
            Destroy(gameObject);
        }

        private void ReflectOffShield(Collider2D other, Vector2 hitPoint)
        {
            IProjectileReflectSurface reflectSurface = other.GetComponentInParent<IProjectileReflectSurface>();
            Component blockerComponent = other.GetComponentInParent<IDamageBlocker>() as Component;

            reflectedBy = blockerComponent != null ? blockerComponent.gameObject : other.gameObject;
            owner = reflectedBy;
            isPlayerReflected = true;
            // Ricochet must be able to damage enemy IDamageable/Health (bypass players-only).
            damagePlayersOnlyActive = false;

            Vector2 normal = Vector2.right;
            if (reflectSurface != null)
            {
                normal = reflectSurface.GetReflectNormal(hitPoint, direction);
            }
            else if (direction.sqrMagnitude > 0.0001f)
            {
                normal = -direction.normalized;
            }

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector2.right;
            }
            else
            {
                normal.Normalize();
            }

            Vector2 reflected = Vector2.Reflect(direction, normal);
            if (reflected.sqrMagnitude < 0.0001f)
            {
                reflected = normal;
            }

            direction = reflected.normalized;
            usesReflectedArc = direction.y > 0.01f;

            if (body != null)
            {
                body.gravityScale = usesReflectedArc ? reflectedGravityScale : 0f;
            }

            lifetimeTimer = Mathf.Max(lifetimeTimer, reflectedLifetimeBonus);
            transform.position += (Vector3)(direction * Mathf.Max(0f, reflectSeparation));
            ApplyVelocity();
            UpdateVisualFacing();
        }

        private int GetReflectedDamage()
        {
            return reflectedDamage >= 0 ? reflectedDamage : damage;
        }

        private bool ShouldExpireFromLifetime()
        {
            // Overhead arc ricochets persist until they leave below the camera.
            if (usesReflectedArc)
            {
                return false;
            }

            // Other shield ricochets flying upward persist until they hit something.
            if (isPlayerReflected && GetVerticalSpeed() > 0f)
            {
                return false;
            }

            return true;
        }

        private float GetVerticalSpeed()
        {
            if (isPlayerReflected && body != null)
            {
                return body.linearVelocity.y;
            }

            return direction.y * speed;
        }

        private void SyncDirectionFromVelocity()
        {
            if (!usesReflectedArc || body == null)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            direction = velocity.normalized;
            UpdateVisualFacing();
        }

        private void ApplyVelocity()
        {
            if (body != null)
            {
                float launchSpeed = speed;
                if (isPlayerReflected)
                {
                    launchSpeed *= reflectedSpeedMultiplier;
                    if (usesReflectedArc)
                    {
                        launchSpeed *= reflectedUpwardSpeedMultiplier;
                    }
                }

                Vector2 velocity = direction * launchSpeed;
                if (usesReflectedArc)
                {
                    velocity = ClampReflectedArcVelocity(velocity);
                }

                body.linearVelocity = velocity;
            }
        }

        private Vector2 ClampReflectedArcVelocity(Vector2 velocity)
        {
            if (velocity.y <= 0f)
            {
                return velocity;
            }

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (viewCamera == null || !viewCamera.orthographic)
            {
                return velocity;
            }

            float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y) * reflectedGravityScale;
            if (gravityMagnitude <= 0.0001f)
            {
                return velocity;
            }

            float cameraTopY = viewCamera.transform.position.y + viewCamera.orthographicSize;
            float maxRise = cameraTopY - reflectedArcTopMargin - transform.position.y;
            if (maxRise <= 0f)
            {
                velocity.y = Mathf.Min(velocity.y, 0.5f);
                return velocity;
            }

            float maxVerticalSpeed = Mathf.Sqrt(2f * gravityMagnitude * maxRise);
            if (velocity.y > maxVerticalSpeed)
            {
                velocity.y = maxVerticalSpeed;
            }

            return velocity;
        }
    }
}
