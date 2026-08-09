using EnemyHealth = Castlevania2D.Health.Health;
using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Spawns loot pickups when Health.Died fires.
    /// 15% chance for healing potion; fewer coins (0..2); optional Ent bonus log.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class LootDropOnDeath : MonoBehaviour
    {
        private static PhysicsMaterial2D sharedPickupMaterial;

        [Header("Coins (reduced)")]
        [SerializeField] private int commonMinCount = 0;
        [SerializeField] private int commonMaxCount = 2;

        [Header("Healing potion (all mobs)")]
        [SerializeField] [Range(0f, 1f)] private float potionDropChance = 0.15f;

        [Header("Bonus (e.g. Ent only)")]
        [SerializeField] private bool dropBonusLoot;
        [SerializeField] private int bonusCount = 1;

        [Header("Pop")]
        [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.4f);
        [SerializeField] private float popSpeedMin = 2.5f;
        [SerializeField] private float popSpeedMax = 5f;
        [SerializeField] private float popAngleSpread = 55f;
        [SerializeField] private float commonPickupScale = 0.08f / 7f;
        [SerializeField] private float potionPickupScale = 0.08f * 6f / 10f;
        [SerializeField] private float bonusPickupScale = 0.064f;
        [SerializeField] private int sortingOrder = 6;
        [SerializeField] private float colliderRadius = 0.35f;
        [SerializeField] private float gravityScale = 2.2f;
        [SerializeField] private float bounciness = 0.35f;
        [SerializeField] private float friction = 0.35f;

        private EnemyHealth health;
        private bool dropped;
        private Sprite commonSprite;
        private Sprite potionSprite;
        private Sprite bonusSprite;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            EnsureSpritesLoaded();
        }

        public void Configure(
            Sprite newCommonSprite,
            Sprite newPotionSprite,
            Sprite newBonusSprite,
            bool shouldDropBonusLoot)
        {
            if (newCommonSprite != null)
            {
                commonSprite = newCommonSprite;
            }

            if (newPotionSprite != null)
            {
                potionSprite = newPotionSprite;
            }

            bonusSprite = newBonusSprite;
            dropBonusLoot = shouldDropBonusLoot;
            EnsureSpritesLoaded();
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

        private void OnDied()
        {
            if (dropped)
            {
                return;
            }

            dropped = true;
            EnsureSpritesLoaded();
            Vector3 origin = transform.position + (Vector3)spawnOffset;

            int commonCount = Random.Range(commonMinCount, commonMaxCount + 1);
            for (int i = 0; i < commonCount; i++)
            {
                SpawnOne(LootItemId.Common, commonSprite, origin, i, commonCount);
            }

            if (Random.value < potionDropChance)
            {
                SpawnOne(LootItemId.Potion, potionSprite, origin, 0, 1);
            }

            if (dropBonusLoot && bonusSprite != null)
            {
                for (int i = 0; i < Mathf.Max(0, bonusCount); i++)
                {
                    SpawnOne(LootItemId.Ent, bonusSprite, origin, i, bonusCount);
                }
            }
        }

        private void EnsureSpritesLoaded()
        {
            if (commonSprite == null)
            {
                commonSprite = LootDropSprites.Common;
            }

            if (potionSprite == null)
            {
                potionSprite = LootDropSprites.Potion;
            }

            if (dropBonusLoot && bonusSprite == null)
            {
                bonusSprite = LootDropSprites.Ent;
            }
        }

        private void SpawnOne(LootItemId itemId, Sprite sprite, Vector3 origin, int index, int total)
        {
            if (sprite == null)
            {
                return;
            }

            float t = total <= 1 ? 0.5f : index / (float)(total - 1);
            float angle = Mathf.Lerp(-popAngleSpread, popAngleSpread, t);
            float speed = Random.Range(popSpeedMin, popSpeedMax);
            Vector2 velocity = Quaternion.Euler(0f, 0f, angle) * Vector2.up * speed;
            velocity.x += Random.Range(-0.6f, 0.6f);

            var instance = new GameObject($"Loot_{itemId}");
            instance.transform.position = origin;
            instance.transform.rotation = Quaternion.identity;

            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            var body = instance.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sharedMaterial = GetSharedPhysicsMaterial();
            body.linearVelocity = velocity;

            float scale = itemId switch
            {
                LootItemId.Ent => bonusPickupScale,
                LootItemId.Potion => potionPickupScale,
                _ => commonPickupScale,
            };

            var circle = instance.AddComponent<CircleCollider2D>();
            circle.isTrigger = false;
            circle.radius = colliderRadius;

            var pickup = instance.AddComponent<LootPickup2D>();
            pickup.Configure(itemId, sprite, scale, velocity);
        }

        private PhysicsMaterial2D GetSharedPhysicsMaterial()
        {
            if (sharedPickupMaterial == null)
            {
                sharedPickupMaterial = new PhysicsMaterial2D("LootBounce (Runtime)")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            sharedPickupMaterial.bounciness = bounciness;
            sharedPickupMaterial.friction = friction;
            return sharedPickupMaterial;
        }
    }
}
