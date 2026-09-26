using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Physics drop that pops out and lands on terrain.
    /// Companions collect it; the player can also take it with F (no prompt).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LootPickup2D : MonoBehaviour
    {
        [SerializeField] private LootItemId itemId = LootItemId.Common;
        [SerializeField] private float collectDelay = 0.2f;
        [SerializeField] private float lifetime = 45f;

        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private float spawnTime;
        private bool collected;
        private Component reservedBy;

        public LootItemId ItemId => itemId;

        public bool IsAvailable => !collected && gameObject.activeInHierarchy;

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
            if (itemId == LootItemId.SporeBag)
            {
                SitSporeBagOnGround(renderer, sprite);
            }

            body.linearVelocity = popVelocity;
        }

        private void SitSporeBagOnGround(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer != null && sprite != null)
            {
                renderer.sprite = SpriteWithBottomPivot(sprite);
            }

            if (bodyCollider is CircleCollider2D circle)
            {
                const float feetRadius = 0.12f;
                circle.radius = feetRadius;
                circle.offset = new Vector2(0f, feetRadius);
            }
        }

        private static Sprite SpriteWithBottomPivot(Sprite source)
        {
            if (source == null)
            {
                return null;
            }

            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, 0.08f);
            Sprite shifted = Sprite.Create(
                source.texture,
                source.rect,
                pivot,
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            shifted.name = source.name;
            return shifted;
        }

        private void Awake()
        {
            EnsureBody();
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
                IgnorePlayerCollisions();
            }
        }

        private void IgnorePlayerCollisions()
        {
            if (bodyCollider == null)
            {
                return;
            }

            GameObject[] players;
            try
            {
                players = GameObject.FindGameObjectsWithTag("Player");
            }
            catch (UnityException)
            {
                players = System.Array.Empty<GameObject>();
            }

            for (int i = 0; i < players.Length; i++)
            {
                IgnoreCollisionsWith(players[i]);
            }

            GameObject namedPlayer = GameObject.Find("Player_HeroKnight");
            if (namedPlayer == null)
            {
                namedPlayer = GameObject.Find("HeroKnight");
            }

            IgnoreCollisionsWith(namedPlayer);
        }

        private void IgnoreCollisionsWith(GameObject root)
        {
            if (root == null || bodyCollider == null)
            {
                return;
            }

            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D other = colliders[i];
                if (other == null || other == bodyCollider || other.isTrigger)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(bodyCollider, other, true);
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
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider != null && IsPlayer(collision.collider))
            {
                Physics2D.IgnoreCollision(bodyCollider, collision.collider, true);
            }
        }

        public bool CanCollectNow()
        {
            return IsAvailable && Time.time - spawnTime >= collectDelay;
        }

        public bool IsReservedBy(Component collector)
        {
            return reservedBy != null && reservedBy && reservedBy == collector;
        }

        public bool TryReserve(Component collector)
        {
            if (!CanCollectNow() || collector == null)
            {
                return false;
            }

            if (reservedBy != null && reservedBy && reservedBy != collector)
            {
                return false;
            }

            reservedBy = collector;
            return true;
        }

        public void ReleaseReserve(Component collector)
        {
            if (reservedBy == collector)
            {
                reservedBy = null;
            }
        }

        public bool TryCollectToPlayer()
        {
            if (!CanCollectNow())
            {
                return false;
            }

            GameObject player = ResolvePlayer(null);
            if (player == null)
            {
                return false;
            }

            TryCollect(player);
            return collected;
        }

        public static LootPickup2D FindNearest(
            Vector2 origin,
            float radius,
            bool includeReserved,
            Component reservedFor = null)
        {
            LootPickup2D[] pickups = FindObjectsByType<LootPickup2D>(FindObjectsSortMode.None);
            LootPickup2D nearest = null;
            float best = radius * radius;
            for (int i = 0; i < pickups.Length; i++)
            {
                LootPickup2D pickup = pickups[i];
                if (pickup == null || !pickup.CanCollectNow())
                {
                    continue;
                }

                if (!includeReserved
                    && pickup.reservedBy != null
                    && pickup.reservedBy
                    && pickup.reservedBy != reservedFor)
                {
                    continue;
                }

                float sqr = ((Vector2)pickup.transform.position - origin).sqrMagnitude;
                if (sqr > best)
                {
                    continue;
                }

                best = sqr;
                nearest = pickup;
            }

            return nearest;
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
                reservedBy = null;
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
            reservedBy = null;
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
