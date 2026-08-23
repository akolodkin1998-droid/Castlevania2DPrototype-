using System;
using Castlevania2D.Combat;
using UnityEngine;

namespace Castlevania2D.Health
{
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private bool destroyOnDeath;

        private IDamageBlocker[] damageBlockers;

        public event Action<int, int> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsAlive => CurrentHealth > 0;
        public bool CanReceiveDamage => IsAlive;

        private void Awake()
        {
            damageBlockers = GetComponents<IDamageBlocker>();
            CurrentHealth = Mathf.Max(1, maxHealth);
        }

        private void Start()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public DamageResult ReceiveDamage(DamageInfo damage)
        {
            if (!CanReceiveDamage || damage.Amount <= 0)
            {
                return DamageResult.Ignored;
            }

            if (IsBlocked(damage))
            {
                return DamageResult.Blocked;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage.Amount);
            Damaged?.Invoke(damage);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth == 0)
            {
                Died?.Invoke();

                if (destroyOnDeath)
                {
                    Destroy(gameObject);
                }
            }

            return DamageResult.Applied;
        }

        public void Restore(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void SetCurrentHealthForLoad(int value)
        {
            CurrentHealth = Mathf.Clamp(value, 0, maxHealth);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        /// <summary>
        /// Instantly reduces HP to zero and fires Died (bypasses damage blockers).
        /// </summary>
        public void Kill()
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHealth = 0;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Died?.Invoke();

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        private bool IsBlocked(DamageInfo damage)
        {
            for (int i = 0; i < damageBlockers.Length; i++)
            {
                if (damageBlockers[i].IsBlockingDamage(damage))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
