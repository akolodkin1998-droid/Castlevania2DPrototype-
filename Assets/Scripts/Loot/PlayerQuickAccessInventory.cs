using System;
using UnityEngine;
using PlayerHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// First quick-access slot: healing potions. Not shown in hold-I inventory.
    /// </summary>
    public sealed class PlayerQuickAccessInventory : MonoBehaviour
    {
        [SerializeField] private int healingPotionCount;
        [SerializeField] private int healAmount = 25;

        public event Action<int> HealingPotionCountChanged;

        public int HealingPotionCount => healingPotionCount;

        public void AddHealingPotion(int amount = 1)
        {
            if (amount <= 0)
            {
                return;
            }

            healingPotionCount += amount;
            HealingPotionCountChanged?.Invoke(healingPotionCount);
        }

        public void SetHealingPotionCount(int count)
        {
            healingPotionCount = Mathf.Max(0, count);
            HealingPotionCountChanged?.Invoke(healingPotionCount);
        }

        public bool TryUseHealingPotion()
        {
            if (healingPotionCount <= 0)
            {
                return false;
            }

            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null || !health.IsAlive || health.CurrentHealth >= health.MaxHealth)
            {
                return false;
            }

            healingPotionCount--;
            health.Restore(healAmount);
            HealingPotionCountChanged?.Invoke(healingPotionCount);
            return true;
        }
    }
}
