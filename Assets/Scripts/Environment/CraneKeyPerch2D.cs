using Castlevania2D.Combat;
using Castlevania2D.Loot;
using UnityEngine;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Key sitting on the crane's right roller. A stone knocks it down to the player's feet.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CraneKeyPerch2D : MonoBehaviour
    {
        [SerializeField] private LootItemId keyItemId = LootItemId.CraneKey;
        [SerializeField] private float pickupScale = 1f / 3f;

        private bool knockedDown;

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryKnockDown(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryKnockDown(collision.collider);
        }

        private void TryKnockDown(Collider2D other)
        {
            if (knockedDown || other == null)
            {
                return;
            }

            EnemyProjectile2D stone = other.GetComponent<EnemyProjectile2D>()
                ?? other.GetComponentInParent<EnemyProjectile2D>();
            if (stone == null)
            {
                return;
            }

            knockedDown = true;
            Vector3 dropPosition = transform.position;
            Destroy(stone.gameObject);
            LootPickup2D pickup = LootPickupSpawner.SpawnRestored(keyItemId, dropPosition);
            if (pickup != null)
            {
                pickup.Configure(keyItemId, LootDropSprites.SpriteFor(keyItemId), pickupScale, new Vector2(0f, -1.5f));
                pickup.KeepUntilCollected();
            }

            SpriteRenderer keyRenderer = GetComponent<SpriteRenderer>();
            if (keyRenderer != null)
            {
                keyRenderer.enabled = false;
            }

            Collider2D keyCollider = GetComponent<Collider2D>();
            if (keyCollider != null)
            {
                keyCollider.enabled = false;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
