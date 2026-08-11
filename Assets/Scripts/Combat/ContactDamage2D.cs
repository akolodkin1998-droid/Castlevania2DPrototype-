using System.Collections.Generic;
using UnityEngine;

namespace Castlevania2D.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class ContactDamage2D : MonoBehaviour
    {
        [SerializeField] private int damage = 10;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private float hitCooldown = 0.5f;
        [SerializeField] private bool onlyDamagePlayer;

        private readonly Dictionary<IDamageable, float> nextHitTimes = new Dictionary<IDamageable, float>();

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryDamage(collision.collider, GetContactPoint(collision));
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryDamage(collision.collider, GetContactPoint(collision));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other, other.ClosestPoint(transform.position));
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other, other.ClosestPoint(transform.position));
        }

        private void TryDamage(Collider2D other, Vector2 hitPoint)
        {
            if (targetMask.value != 0 && (targetMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null ||
                !damageable.CanReceiveDamage ||
                onlyDamagePlayer && !IsPlayer(damageable))
            {
                return;
            }

            if (nextHitTimes.TryGetValue(damageable, out float nextHitTime) && Time.time < nextHitTime)
            {
                return;
            }

            nextHitTimes[damageable] = Time.time + hitCooldown;

            Vector2 direction = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            damageable.ReceiveDamage(new DamageInfo(damage, gameObject, hitPoint, direction));
        }

        private static bool IsPlayer(IDamageable damageable)
        {
            if (damageable is not Component component)
            {
                return false;
            }

            Transform root = component.transform.root;
            return root.CompareTag("Player") ||
                   root.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   root.name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Vector2 GetContactPoint(Collision2D collision)
        {
            return collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(transform.position);
        }
    }
}
