using System;
using Castlevania2D.Combat;
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

        public bool CanUseHealingPotion()
        {
            if (healingPotionCount <= 0)
            {
                return false;
            }

            PlayerHealth health = GetComponent<PlayerHealth>();
            return health != null && health.IsAlive && health.CurrentHealth < health.MaxHealth;
        }

        public bool TryConsumeHealingPotion()
        {
            if (!CanUseHealingPotion())
            {
                return false;
            }

            healingPotionCount--;
            HealingPotionCountChanged?.Invoke(healingPotionCount);
            return true;
        }

        public void ApplyHealingPotionRestore()
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health == null || !health.IsAlive)
            {
                return;
            }

            health.Restore(healAmount);
        }

        public bool TryUseHealingPotion()
        {
            HeroPotionDrink2D drinker = GetComponent<HeroPotionDrink2D>();
            if (drinker != null)
            {
                return drinker.TryBegin();
            }

            if (!TryConsumeHealingPotion())
            {
                return false;
            }

            ApplyHealingPotionRestore();
            return true;
        }
    }
}
