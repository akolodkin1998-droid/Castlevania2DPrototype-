using UnityEngine;

namespace Castlevania2D.Loot
{
    public static class LootPickupSpawner
    {
        public static LootPickup2D SpawnRestored(LootItemId itemId, Vector3 position)
        {
            Sprite sprite = GetSprite(itemId);
            if (sprite == null)
            {
                return null;
            }

            var instance = new GameObject($"Loot_{itemId}");
            instance.transform.position = position;

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 6;

            Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 2.2f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D circle = instance.AddComponent<CircleCollider2D>();
            circle.isTrigger = false;
            circle.radius = 0.35f;

            LootPickup2D pickup = instance.AddComponent<LootPickup2D>();
            pickup.Configure(itemId, sprite, GetScale(itemId), Vector2.zero);
            return pickup;
        }

        private static Sprite GetSprite(LootItemId itemId)
        {
            return itemId switch
            {
                LootItemId.Ent => LootDropSprites.Ent,
                LootItemId.Potion => LootDropSprites.Potion,
                LootItemId.MaraTear => LootDropSprites.MaraTear,
                LootItemId.SporeBag => LootDropSprites.SporeBag,
                _ => LootDropSprites.Common,
            };
        }

        private static float GetScale(LootItemId itemId)
        {
            return itemId switch
            {
                LootItemId.Ent => 0.064f,
                LootItemId.Potion => 0.08f * 6f / 10f,
                LootItemId.MaraTear => 0.1f,
                LootItemId.SporeBag => 0.48f,
                _ => 0.08f / 7f,
            };
        }
    }
}
