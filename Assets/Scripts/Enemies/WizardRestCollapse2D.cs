using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// After lever second strike arms this component: hide Ground 3 + Lever once ALL of
    /// basketsFallen ∧ portalsSealed ∧ first rest-idle cycle complete.
    /// Does nothing until <see cref="ArmAfterLeverActivation"/>.
    /// </summary>
    public sealed class WizardRestCollapse2D : MonoBehaviour
    {
        [Header("Targets (SetActive false)")]
        [SerializeField] private GameObject ground3;
        [SerializeField] private GameObject lever;

        [Header("Fallback names")]
        [SerializeField] private string ground3ObjectName = "Ground 3";
        [SerializeField] private string leverObjectName = "Lever";

        [Header("Refs")]
        [SerializeField] private WizardIdleEnemy2D idleEnemy;

        [Header("Idle cycle fallback")]
        [Tooltip("If IdleCycleCompleted is missed, hide after this many cycle-durations once armed.")]
        [SerializeField] private float restCycleFallbackMultiplier = 1.05f;

        private bool armed;
        private bool basketsFallen;
        private bool portalsSealed;
        private bool firstRestIdleCycleCompleted;
        private bool collapsed;
        private bool subscribedToIdle;
        private float armedElapsed;
        private float restCycleFallbackDuration = 1f;

        public bool IsArmed => armed;
        public bool AreBasketsFallen => basketsFallen;
        public bool ArePortalsSealed => portalsSealed;
        public bool IsFirstRestIdleCycleCompleted => firstRestIdleCycleCompleted;
        public bool IsCollapsed => collapsed;

        /// <summary>
        /// Called from lever second strike only. Starts listening for rest-idle cycle
        /// and accepts basketsFallen / portalsSealed flags. First-pull must not call this.
        /// </summary>
        public void ArmAfterLeverActivation()
        {
            if (armed || collapsed)
            {
                return;
            }

            armed = true;
            armedElapsed = 0f;
            restCycleFallbackDuration = idleEnemy != null
                ? Mathf.Max(0.25f, idleEnemy.RestCycleDurationSeconds * Mathf.Max(1f, restCycleFallbackMultiplier))
                : 1f;

            ResolveTargets();
            EnsureIdleSubscription();
            Debug.Log(
                $"[WizardRestCollapse2D] Armed. ground3={(ground3 != null ? ground3.name : "NULL")} " +
                $"lever={(lever != null ? lever.name : "NULL")} " +
                $"fallback={restCycleFallbackDuration:0.###}s " +
                $"flags baskets={basketsFallen} portals={portalsSealed} idleCycle={firstRestIdleCycleCompleted}",
                this);
            TryCollapse();
        }

        /// <summary>Mark baskets fall started (same second-strike path).</summary>
        public void NotifyBasketsFallen()
        {
            if (basketsFallen)
            {
                return;
            }

            basketsFallen = true;
            Debug.Log("[WizardRestCollapse2D] Flag set: basketsFallen=true", this);
            TryCollapse();
        }

        /// <summary>Mark portals sealed/hidden (same second-strike path).</summary>
        public void NotifyPortalsSealed()
        {
            if (portalsSealed)
            {
                return;
            }

            portalsSealed = true;
            Debug.Log("[WizardRestCollapse2D] Flag set: portalsSealed=true", this);
            TryCollapse();
        }

        public void ApplySavedState(
            bool savedArmed,
            bool savedBasketsFallen,
            bool savedPortalsSealed,
            bool savedFirstRestIdleCycleCompleted,
            bool savedCollapsed)
        {
            collapsed = false;
            armed = savedArmed || savedCollapsed;
            basketsFallen = savedBasketsFallen || savedCollapsed;
            portalsSealed = savedPortalsSealed || savedCollapsed;
            firstRestIdleCycleCompleted = savedFirstRestIdleCycleCompleted || savedCollapsed;
            armedElapsed = 0f;
            ResolveTargets();

            if (armed && !savedCollapsed)
            {
                EnsureIdleSubscription();
            }

            TryCollapse();
        }

        private void Awake()
        {
            if (idleEnemy == null)
            {
                idleEnemy = GetComponent<WizardIdleEnemy2D>();
            }

            ResolveTargets();
        }

        private void OnEnable()
        {
            if (idleEnemy == null)
            {
                idleEnemy = GetComponent<WizardIdleEnemy2D>();
            }

            // Subscribe only after arm — OnEnable alone must not listen pre-strike.
            if (armed)
            {
                EnsureIdleSubscription();
            }
        }

        private void OnDisable()
        {
            UnsubscribeIdle();
        }

        private void Update()
        {
            if (!armed || collapsed || firstRestIdleCycleCompleted)
            {
                return;
            }

            armedElapsed += Time.deltaTime;
            if (armedElapsed < restCycleFallbackDuration)
            {
                return;
            }

            firstRestIdleCycleCompleted = true;
            Debug.Log(
                $"[WizardRestCollapse2D] Flag set: firstRestIdleCycleCompleted=true (duration fallback {armedElapsed:0.###}s)",
                this);
            TryCollapse();
        }

        private void EnsureIdleSubscription()
        {
            if (subscribedToIdle || idleEnemy == null)
            {
                return;
            }

            idleEnemy.IdleCycleCompleted -= OnIdleCycleCompleted;
            idleEnemy.IdleCycleCompleted += OnIdleCycleCompleted;
            subscribedToIdle = true;
        }

        private void UnsubscribeIdle()
        {
            if (!subscribedToIdle || idleEnemy == null)
            {
                return;
            }

            idleEnemy.IdleCycleCompleted -= OnIdleCycleCompleted;
            subscribedToIdle = false;
        }

        private void OnIdleCycleCompleted()
        {
            if (!armed || firstRestIdleCycleCompleted)
            {
                return;
            }

            firstRestIdleCycleCompleted = true;
            Debug.Log("[WizardRestCollapse2D] Flag set: firstRestIdleCycleCompleted=true (IdleCycleCompleted)", this);
            TryCollapse();
        }

        private void TryCollapse()
        {
            if (!armed || collapsed)
            {
                return;
            }

            if (!basketsFallen || !portalsSealed || !firstRestIdleCycleCompleted)
            {
                return;
            }

            collapsed = true;
            UnsubscribeIdle();
            ResolveTargets();

            Debug.Log(
                $"[WizardRestCollapse2D] Hiding targets. ground3={(ground3 != null ? ground3.name : "NULL")} " +
                $"lever={(lever != null ? lever.name : "NULL")}",
                this);

            if (ground3 != null && ground3.activeSelf)
            {
                ground3.SetActive(false);
            }
            else if (ground3 == null)
            {
                Debug.LogWarning(
                    $"[WizardRestCollapse2D] Cannot hide Ground 3 — ref null (name '{ground3ObjectName}').",
                    this);
            }

            if (lever != null && lever.activeSelf)
            {
                lever.SetActive(false);
            }
            else if (lever == null)
            {
                Debug.LogWarning(
                    $"[WizardRestCollapse2D] Cannot hide Lever — ref null (name '{leverObjectName}').",
                    this);
            }
        }

        private void ResolveTargets()
        {
            if (ground3 == null && !string.IsNullOrEmpty(ground3ObjectName))
            {
                ground3 = FindSceneObjectByName(ground3ObjectName);
            }

            if (lever == null && !string.IsNullOrEmpty(leverObjectName))
            {
                lever = FindSceneObjectByName(leverObjectName);
            }
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            GameObject active = GameObject.Find(objectName);
            if (active != null)
            {
                return active;
            }

            // Include inactive (e.g. if something else already hid the object).
            Transform[] transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t != null && t.name == objectName && t.gameObject.scene.IsValid())
                {
                    return t.gameObject;
                }
            }

            return null;
        }
    }
}
