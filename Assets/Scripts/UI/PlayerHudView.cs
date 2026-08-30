using Castlevania2D.Combat;
using UnityEngine;
using UnityEngine.UI;
using PlayerHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.UI
{
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private MonoBehaviour blockDurabilitySource;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image blockFill;
        [SerializeField] private bool resolveBlockSourceFromHealth = true;
        [SerializeField] private float healthFillSmoothTime = 0.12f;

        private IBlockDurability blockDurability;
        private float displayedHealthFill = 1f;
        private float targetHealthFill = 1f;
        private bool subscribedToHealth;

        private void Awake()
        {
            UiFillImage.Configure(healthFill);
            UiFillImage.Configure(blockFill);
            blockDurability = blockDurabilitySource as IBlockDurability;
        }

        private void OnEnable()
        {
            TryResolveReferences();
            SubscribeToHealth();
            RefreshHealthImmediate();
            RefreshBlock();
        }

        private void Start()
        {
            TryResolveReferences();
            SubscribeToHealth();
            RefreshHealthImmediate();
            RefreshBlock();
        }

        private void Update()
        {
            if (health != null)
            {
                targetHealthFill = CalculateFill(health.CurrentHealth, health.MaxHealth);
            }

            UpdateHealthFillAnimation();
            RefreshBlock();
        }

        private void OnDisable()
        {
            UnsubscribeFromHealth();
        }

        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            targetHealthFill = CalculateFill(currentHealth, maxHealth);
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (health == null)
            {
                return;
            }

            targetHealthFill = CalculateFill(health.CurrentHealth, health.MaxHealth);
            RefreshBlock();
        }

        private void UpdateHealthFillAnimation()
        {
            if (healthFill == null)
            {
                return;
            }

            if (healthFillSmoothTime <= 0f)
            {
                displayedHealthFill = targetHealthFill;
            }
            else
            {
                float maxDelta = Time.deltaTime / healthFillSmoothTime;
                displayedHealthFill = Mathf.MoveTowards(displayedHealthFill, targetHealthFill, maxDelta);
            }

            UiFillImage.SetFillAmount(healthFill, displayedHealthFill);
        }

        private void RefreshHealthImmediate()
        {
            if (health == null)
            {
                targetHealthFill = 0f;
                displayedHealthFill = 0f;
                return;
            }

            targetHealthFill = CalculateFill(health.CurrentHealth, health.MaxHealth);
            displayedHealthFill = targetHealthFill;
        }

        private void RefreshBlock()
        {
            if (blockDurability == null)
            {
                SetFill(blockFill, 0, 1);
                return;
            }

            SetFill(blockFill, blockDurability.BlockDurability, blockDurability.MaxBlockDurability);
        }

        private void TryResolveReferences()
        {
            if (!resolveBlockSourceFromHealth || blockDurabilitySource != null || health == null)
            {
                blockDurability = blockDurabilitySource as IBlockDurability;
                return;
            }

            MonoBehaviour[] behaviours = health.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IBlockDurability durabilitySource)
                {
                    blockDurabilitySource = behaviours[i];
                    blockDurability = durabilitySource;
                    return;
                }
            }

            blockDurability = blockDurabilitySource as IBlockDurability;
        }

        private void SubscribeToHealth()
        {
            if (health == null || subscribedToHealth)
            {
                return;
            }

            health.HealthChanged += OnHealthChanged;
            health.Damaged += OnDamaged;
            subscribedToHealth = true;
        }

        private void UnsubscribeFromHealth()
        {
            if (health == null || !subscribedToHealth)
            {
                return;
            }

            health.HealthChanged -= OnHealthChanged;
            health.Damaged -= OnDamaged;
            subscribedToHealth = false;
        }

        private static void SetFill(Image image, int currentValue, int maxValue)
        {
            UiFillImage.SetFillAmount(image, CalculateFill(currentValue, maxValue));
        }

        private static float CalculateFill(int currentValue, int maxValue)
        {
            return maxValue > 0 ? Mathf.Clamp01((float)currentValue / maxValue) : 0f;
        }
    }
}
