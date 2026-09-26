using Castlevania2D.Loot;
using UnityEngine;

namespace Castlevania2D.Npcs
{
    /// <summary>
    /// Shared loot hunt for companion NPCs (Struchok and similar).
    /// Walks to grounded drops and reserves them so two carriers do not grab the same item.
    /// </summary>
    public sealed class CompanionLootCollector2D : MonoBehaviour
    {
        [SerializeField] [Min(0.5f)] private float searchRadius = 12f;
        [SerializeField] [Min(0.1f)] private float collectReach = 0.7f;
        [SerializeField] [Min(0.5f)] private float loseTargetDistance = 12f;

        private LootPickup2D target;

        private void Update()
        {
            if (GetComponent<StruchokNpc2D>() != null)
            {
                return;
            }

            TickAutoCollect(2.2f);
        }

        public bool HasTarget => target != null && target.IsAvailable;

        public bool HasAnyAvailable()
        {
            if (HasTarget)
            {
                return true;
            }

            return LootPickup2D.FindNearest(transform.position, searchRadius, false, this) != null;
        }

        public LootPickup2D Target => HasTarget ? target : null;

        public bool IsInReach
        {
            get
            {
                if (!HasTarget)
                {
                    return false;
                }

                return Mathf.Abs(transform.position.x - target.transform.position.x) <= collectReach;
            }
        }

        public void ClearTarget()
        {
            if (target != null)
            {
                target.ReleaseReserve(this);
            }

            target = null;
        }

        public void RefreshTarget()
        {
            if (HasTarget)
            {
                float distance = Vector2.Distance(transform.position, target.transform.position);
                if (distance <= loseTargetDistance && target.IsReservedBy(this))
                {
                    return;
                }

                ClearTarget();
            }

            LootPickup2D found = LootPickup2D.FindNearest(transform.position, searchRadius, false, this);
            if (found == null || !found.TryReserve(this))
            {
                return;
            }

            target = found;
        }

        public bool TryCollectReached()
        {
            if (!IsInReach || target == null)
            {
                return false;
            }

            bool collected = target.TryCollectToPlayer();
            ClearTarget();
            return collected;
        }

        public bool TickAutoCollect(float moveSpeed)
        {
            RefreshTarget();
            if (!HasTarget)
            {
                return false;
            }

            if (IsInReach)
            {
                TryCollectReached();
                return true;
            }

            Vector3 next = transform.position;
            next.x = Mathf.MoveTowards(next.x, target.transform.position.x, moveSpeed * Time.deltaTime);
            transform.position = next;
            return true;
        }
    }
}
