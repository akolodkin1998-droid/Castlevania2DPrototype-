using System.Collections.Generic;
using UnityEngine;

namespace Castlevania2D.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hitbox2D : MonoBehaviour
    {
        [SerializeField] private int damage = 1;
        [SerializeField] private LayerMask targetMask;

        private readonly HashSet<IDamageable> targetsHitThisSwing = new HashSet<IDamageable>();
        private Collider2D hitboxCollider;
        private GameObject owner;
        private int facingDirection = 1;
        private Vector3 initialLocalPosition;

        private void Awake()
        {
            hitboxCollider = GetComponent<Collider2D>();
            hitboxCollider.isTrigger = true;
            initialLocalPosition = transform.localPosition;
            SetActive(false);
        }

        public void Configure(GameObject newOwner, int newFacingDirection)
        {
            Configure(newOwner, newFacingDirection, damage);
        }

        public void Configure(GameObject newOwner, int newFacingDirection, int newDamage)
        {
            owner = newOwner;
            facingDirection = newFacingDirection >= 0 ? 1 : -1;
            damage = Mathf.Max(0, newDamage);

            transform.localPosition = new Vector3(
                Mathf.Abs(initialLocalPosition.x) * facingDirection,
                initialLocalPosition.y,
                initialLocalPosition.z);
        }

        public void BeginSwing()
        {
            targetsHitThisSwing.Clear();
            SetActive(true);
        }

        public void EndSwing()
        {
            SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return;
            }

            if (targetMask.value != 0 && (targetMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.CanReceiveDamage || targetsHitThisSwing.Contains(damageable))
            {
                return;
            }

            targetsHitThisSwing.Add(damageable);
            Vector2 direction = new Vector2(facingDirection, 0f);
            damageable.ReceiveDamage(new DamageInfo(damage, owner, other.ClosestPoint(transform.position), direction));
        }

        private void SetActive(bool isActive)
        {
            hitboxCollider.enabled = isActive;
        }
    }
}
