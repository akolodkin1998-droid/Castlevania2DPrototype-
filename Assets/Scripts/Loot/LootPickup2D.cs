using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Physics drop that pops out, lands on terrain, and is collected on player contact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LootPickup2D : MonoBehaviour
    {
        private static readonly Collider2D[] MagnetOverlapBuffer = new Collider2D[16];

        [SerializeField] private LootItemId itemId = LootItemId.Common;
        [SerializeField] private float collectDelay = 0.2f;
        [SerializeField] private float magnetRadius = 1.6f;
        [SerializeField] private float magnetSpeed = 10f;
        [SerializeField] private float lifetime = 45f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private float spawnTime;
        private bool collected;
        private Transform magnetTarget;
        private bool magnetActive;
        private ContactFilter2D magnetOverlapFilter;

        public LootItemId ItemId => itemId;

        public void Configure(LootItemId id, Sprite sprite, float scale, Vector2 popVelocity)
        {
            itemId = id;
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null && sprite != null)
            {
                renderer.sprite = sprite;
            }

            transform.localScale = new Vector3(scale, scale, 1f);
            EnsureBody();
            body.linearVelocity = popVelocity;
        }

        private void Awake()
        {
            EnsureBody();
            magnetOverlapFilter = new ContactFilter2D
            {
                useTriggers = Physics2D.queriesHitTriggers,
                useLayerMask = false,
                useDepth = false
            };
            spawnTime = Time.time;
        }

        private void EnsureBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 2.2f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (bodyCollider != null)
            {
                bodyCollider.isTrigger = false;
            }
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            if (lifetime > 0f && Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (!CanCollect())
            {
                return;
            }

            if (!magnetActive)
            {
                TryFindMagnetTarget();
            }

            if (magnetActive && magnetTarget != null)
            {
                Vector2 toPlayer = (Vector2)magnetTarget.position - body.position;
                float distance = toPlayer.magnitude;
                if (distance <= 0.18f)
                {
                    TryCollect(magnetTarget.gameObject);
                    return;
                }

                body.gravityScale = 0f;
                body.linearVelocity = toPlayer.normalized * magnetSpeed;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collected || !CanCollect())
            {
                return;
            }

            if (IsPlayer(collision.collider))
            {
                TryCollect(collision.collider.gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !CanCollect())
            {
                return;
            }

            if (IsPlayer(other))
            {
                TryCollect(other.gameObject);
            }
        }

        private bool CanCollect()
        {
            return Time.time - spawnTime >= collectDelay;
        }

        private void TryFindMagnetTarget()
        {
            int hitCount = Physics2D.OverlapCircle(
                body.position,
                magnetRadius,
                magnetOverlapFilter,
                MagnetOverlapBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = MagnetOverlapBuffer[i];
                if (!IsPlayer(hit))
                {
                    continue;
                }

                magnetTarget = hit.attachedRigidbody != null
                    ? hit.attachedRigidbody.transform
                    : hit.transform.root;
                magnetActive = true;
                return;
            }
        }

        private static bool IsPlayer(Collider2D other)
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

            string name = root.name;
            return name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryCollect(GameObject playerObject)
        {
            if (collected || playerObject == null)
            {
                return;
            }

            GameObject player = ResolvePlayer(playerObject);
            if (player == null)
            {
                return;
            }

            if (itemId == LootItemId.Potion)
            {
                PlayerQuickAccessInventory quickAccess = player.GetComponent<PlayerQuickAccessInventory>();
                if (quickAccess == null)
                {
                    quickAccess = player.AddComponent<PlayerQuickAccessInventory>();
                }

                collected = true;
                quickAccess.AddHealingPotion(1);
                Destroy(gameObject);
                return;
            }

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                inventory = player.GetComponentInParent<PlayerLootInventory>();
            }

            if (inventory == null)
            {
                return;
            }

            collected = true;
            inventory.Add(itemId, 1);
            Destroy(gameObject);
        }

        private static GameObject ResolvePlayer(GameObject playerObject)
        {
            if (playerObject != null)
            {
                PlayerLootInventory inventory = playerObject.GetComponentInParent<PlayerLootInventory>();
                if (inventory != null)
                {
                    return inventory.gameObject;
                }

                PlayerQuickAccessInventory quickAccess =
                    playerObject.GetComponentInParent<PlayerQuickAccessInventory>();
                if (quickAccess != null)
                {
                    return quickAccess.gameObject;
                }
            }

            return GameObject.Find("Player_HeroKnight");
        }
    }
}
