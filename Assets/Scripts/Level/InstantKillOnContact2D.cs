using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Solid kill floor: builds composite colliders from sprite children (optional),
    /// then calls <see cref="Castlevania2D.Health.Health.Kill"/> on the player on first contact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstantKillOnContact2D : MonoBehaviour
    {
        [SerializeField] private string playerObjectName = "Player_HeroKnight";
        [SerializeField] private bool autoBuildSolidSurfaces = true;
        [SerializeField] private bool killOnCollision = true;
        [SerializeField] private bool killOnTrigger = true;

        private bool hasKilled;

        private void Awake()
        {
            if (autoBuildSolidSurfaces)
            {
                EnsureSolidSurfaces();
            }
        }

        /// <summary>
        /// Static Rigidbody2D + CompositeCollider2D on this object;
        /// BoxCollider2D (Merge) on every SpriteRenderer in the hierarchy.
        /// </summary>
        public void EnsureSolidSurfaces()
        {
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;

            CompositeCollider2D composite = GetComponent<CompositeCollider2D>();
            if (composite == null)
            {
                composite = gameObject.AddComponent<CompositeCollider2D>();
            }

            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            composite.isTrigger = false;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                BoxCollider2D box = renderer.GetComponent<BoxCollider2D>();
                if (box == null)
                {
                    box = renderer.gameObject.AddComponent<BoxCollider2D>();
                }

                Bounds localBounds = renderer.sprite.bounds;
                box.isTrigger = false;
                box.size = localBounds.size;
                box.offset = localBounds.center;
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            composite.GenerateGeometry();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!killOnCollision)
            {
                return;
            }

            TryKill(collision != null ? collision.collider : null);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!killOnCollision)
            {
                return;
            }

            TryKill(collision != null ? collision.collider : null);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!killOnTrigger)
            {
                return;
            }

            TryKill(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!killOnTrigger)
            {
                return;
            }

            TryKill(other);
        }

        private void TryKill(Collider2D other)
        {
            if (hasKilled || other == null)
            {
                return;
            }

            Castlevania2D.Health.Health health = ResolvePlayerHealth(other);
            if (health == null || !health.IsAlive)
            {
                return;
            }

            hasKilled = true;
            health.Kill();
        }

        private Castlevania2D.Health.Health ResolvePlayerHealth(Collider2D other)
        {
            Castlevania2D.Health.Health health = other.GetComponentInParent<Castlevania2D.Health.Health>();
            if (health == null)
            {
                return null;
            }

            Transform root = health.transform;
            if (root.CompareTag("Player"))
            {
                return health;
            }

            if (!string.IsNullOrEmpty(playerObjectName) &&
                root.name.Equals(playerObjectName, System.StringComparison.Ordinal))
            {
                return health;
            }

            string name = root.name;
            if (name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return health;
            }

            return null;
        }
    }
}
