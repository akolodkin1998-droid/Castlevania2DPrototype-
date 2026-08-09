using Castlevania2D.Combat;
using Castlevania2D.Enemies;
using UnityEngine;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Phase 1: both baskets full → play first lever anim.
    /// Phase 2: player can sword-hit the lever → raised-lever anim, baskets fall to serialized
    /// world targets (basket01 explicit; basket02 onto portal 1 by default), portals seal/hide,
    /// and the wizard returns to permanent idle/rest.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LeverWhenBasketsFull2D : MonoBehaviour, IDamageable
    {
        [Header("Baskets")]
        [SerializeField] private StoneBasketHitStages2D basket01;
        [SerializeField] private StoneBasketHitStages2D basket02;
        [SerializeField] private string basket01Name = "StoneBasket_01";
        [SerializeField] private string basket02Name = "StoneBasket_02";

        [Header("Fall targets (second strike)")]
        [SerializeField] private Vector2 basket01FallTarget = new Vector2(76.52f, -37.16f);
        [Tooltip("When true, basket02 falls onto portalSpawner.Portal1Position; otherwise basket02FallTarget.")]
        [SerializeField] private bool basket02FallOntoPortal1 = true;
        [SerializeField] private Vector2 basket02FallTarget = new Vector2(50.76f, -37.16f);

        [Header("First pull (baskets full)")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] activateFrames;
        [SerializeField] private float frameRate = 12f;

        [Header("Second strike (raised lever)")]
        [SerializeField] private Sprite[] raisedFrames;
        [SerializeField] private float raisedFrameRate = 12f;

        [Header("Portal collapse")]
        [SerializeField] private WizardPortalSpawner2D portalSpawner;
        [SerializeField] private WizardAttackAnimator2D wizardAttackAnimator;
        [SerializeField] private WizardRestCollapse2D wizardRestCollapse;
        [SerializeField] private string wizardObjectName = "Enemy_Wizard";

        private SpriteRenderer spriteRenderer;
        private Collider2D hitCollider;

        private bool firstAnimStarted;
        private bool firstAnimDone;
        private bool secondStrikeDone;
        private bool animating;
        private bool playingRaised;
        private Sprite[] activeFrames;
        private int frameIndex;
        private float frameTimer;
        private float activeFrameRate = 12f;

        public bool HasActivated => firstAnimStarted;
        public bool CanReceiveDamage => firstAnimDone && !secondStrikeDone && !animating;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            hitCollider = GetComponent<Collider2D>();
            if (hitCollider != null)
            {
                // Only accept sword hits after the first pull finishes.
                hitCollider.enabled = false;
            }

            ResolveRefs();
            if (idleSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        private void OnEnable()
        {
            ResolveRefs();
            Subscribe(basket01);
            Subscribe(basket02);
            TryStartFirstPull();
        }

        private void OnDisable()
        {
            Unsubscribe(basket01);
            Unsubscribe(basket02);
        }

        public DamageResult ReceiveDamage(DamageInfo damage)
        {
            if (!CanReceiveDamage || damage.Amount <= 0)
            {
                return DamageResult.Ignored;
            }

            BeginSecondStrike();
            return DamageResult.Applied;
        }

        private void Update()
        {
            if (!animating || activeFrames == null || activeFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            float duration = 1f / Mathf.Max(1f, activeFrameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                if (frameIndex >= activeFrames.Length - 1)
                {
                    Apply(activeFrames[activeFrames.Length - 1]);
                    animating = false;
                    OnAnimationFinished();
                    return;
                }

                frameIndex++;
                Apply(activeFrames[frameIndex]);
            }
        }

        private void OnBasketFilled()
        {
            TryStartFirstPull();
        }

        private void TryStartFirstPull()
        {
            if (firstAnimStarted)
            {
                return;
            }

            ResolveRefs();
            if (basket01 == null || basket02 == null)
            {
                return;
            }

            if (!basket01.IsFull || !basket02.IsFull)
            {
                return;
            }

            firstAnimStarted = true;
            PlayFrames(activateFrames, frameRate, raised: false);
        }

        private void BeginSecondStrike()
        {
            if (secondStrikeDone)
            {
                return;
            }

            secondStrikeDone = true;
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
            }

            // Same sword hit / same frame: fall start + seal portals + wizard rest.
            // Do not wait for raised anim end or basket landing.
            DropBasketsAndSealPortals();
            PlayFrames(raisedFrames, raisedFrameRate, raised: true);
        }

        private void DropBasketsAndSealPortals()
        {
            ResolveRefs();

            Vector2 portal1 = basket02FallTarget;
            if (portalSpawner != null)
            {
                portal1 = portalSpawner.Portal1Position;
                portalSpawner.SealPortals();
            }
            else
            {
                Debug.LogWarning(
                    "[LeverWhenBasketsFull2D] portalSpawner missing on second strike — sealing live portals by fallback.",
                    this);
                SealLivePortalsFallback();
            }

            // Order: seal → notify sealed → baskets fall → notify fallen → arm hide check → rest idle.
            // Arm before ReturnToIdle so IdleCycleCompleted subscription is live when watch starts.
            if (wizardRestCollapse != null)
            {
                wizardRestCollapse.NotifyPortalsSealed();
            }

            // Basket_01 → explicit serialized target; Basket_02 → portal 1 (or its own field).
            if (basket01 != null)
            {
                basket01.BeginFallOnto(basket01FallTarget);
            }

            if (basket02 != null)
            {
                Vector2 basket02Target = basket02FallOntoPortal1 ? portal1 : basket02FallTarget;
                basket02.BeginFallOnto(basket02Target);
            }

            if (wizardRestCollapse != null)
            {
                wizardRestCollapse.NotifyBasketsFallen();
                wizardRestCollapse.ArmAfterLeverActivation();
            }

            if (wizardAttackAnimator != null)
            {
                wizardAttackAnimator.ReturnToIdle();
            }
            else
            {
                Debug.LogWarning(
                    "[LeverWhenBasketsFull2D] wizardAttackAnimator missing on second strike — wizard may keep attacking.",
                    this);
            }
        }

        private void PlayFrames(Sprite[] frames, float fps, bool raised)
        {
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning(
                    raised
                        ? "[LeverWhenBasketsFull2D] No raised-lever frames assigned."
                        : "[LeverWhenBasketsFull2D] No activate frames assigned.",
                    this);
                if (!raised)
                {
                    firstAnimDone = true;
                    ArmHitCollider();
                }

                return;
            }

            playingRaised = raised;
            activeFrames = frames;
            activeFrameRate = Mathf.Max(1f, fps);
            frameIndex = 0;
            frameTimer = 0f;
            animating = true;
            Apply(activeFrames[0]);
        }

        private void OnAnimationFinished()
        {
            // Raised strike effects already ran in BeginSecondStrike — never defer seal/idle here.
            if (playingRaised)
            {
                return;
            }

            firstAnimDone = true;
            ArmHitCollider();
        }

        private void ArmHitCollider()
        {
            if (hitCollider == null)
            {
                hitCollider = gameObject.AddComponent<BoxCollider2D>();
                var box = hitCollider as BoxCollider2D;
                if (box != null && spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    Bounds b = spriteRenderer.sprite.bounds;
                    box.size = b.size;
                    box.offset = b.center;
                }
            }

            hitCollider.isTrigger = true;
            hitCollider.enabled = true;
        }

        private void Apply(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        private void ResolveRefs()
        {
            if (basket01 == null)
            {
                GameObject go = GameObject.Find(basket01Name);
                if (go != null)
                {
                    basket01 = go.GetComponent<StoneBasketHitStages2D>();
                }
            }

            if (basket02 == null)
            {
                GameObject go = GameObject.Find(basket02Name);
                if (go != null)
                {
                    basket02 = go.GetComponent<StoneBasketHitStages2D>();
                }
            }

            if (portalSpawner != null && wizardAttackAnimator != null && wizardRestCollapse != null)
            {
                return;
            }

            GameObject wizard = null;
            if (!string.IsNullOrEmpty(wizardObjectName))
            {
                wizard = GameObject.Find(wizardObjectName);
            }

            if (wizard != null)
            {
                if (portalSpawner == null)
                {
                    portalSpawner = wizard.GetComponent<WizardPortalSpawner2D>();
                    if (portalSpawner == null)
                    {
                        portalSpawner = wizard.GetComponentInChildren<WizardPortalSpawner2D>(true);
                    }
                }

                if (wizardAttackAnimator == null)
                {
                    wizardAttackAnimator = wizard.GetComponent<WizardAttackAnimator2D>();
                    if (wizardAttackAnimator == null)
                    {
                        wizardAttackAnimator = wizard.GetComponentInChildren<WizardAttackAnimator2D>(true);
                    }
                }

                if (wizardRestCollapse == null)
                {
                    wizardRestCollapse = wizard.GetComponent<WizardRestCollapse2D>();
                    if (wizardRestCollapse == null)
                    {
                        wizardRestCollapse = wizard.GetComponentInChildren<WizardRestCollapse2D>(true);
                    }
                }
            }

            // Prefab instance / rename fallback — cached, not per-frame.
            if (portalSpawner == null)
            {
                portalSpawner = FindFirstObjectByType<WizardPortalSpawner2D>(FindObjectsInactive.Include);
            }

            if (wizardAttackAnimator == null)
            {
                wizardAttackAnimator = FindFirstObjectByType<WizardAttackAnimator2D>(FindObjectsInactive.Include);
            }

            if (wizardRestCollapse == null && wizardAttackAnimator != null)
            {
                wizardRestCollapse = wizardAttackAnimator.GetComponent<WizardRestCollapse2D>();
            }

            if (wizardRestCollapse == null)
            {
                wizardRestCollapse = FindFirstObjectByType<WizardRestCollapse2D>(FindObjectsInactive.Include);
            }

            if (wizardAttackAnimator == null && portalSpawner != null)
            {
                wizardAttackAnimator = portalSpawner.GetComponent<WizardAttackAnimator2D>();
                if (wizardAttackAnimator == null)
                {
                    wizardAttackAnimator = portalSpawner.GetComponentInChildren<WizardAttackAnimator2D>(true);
                }
            }

            if (portalSpawner == null && wizardAttackAnimator != null)
            {
                portalSpawner = wizardAttackAnimator.GetComponent<WizardPortalSpawner2D>();
                if (portalSpawner == null)
                {
                    portalSpawner = wizardAttackAnimator.GetComponentInChildren<WizardPortalSpawner2D>(true);
                }
            }
        }

        private static void SealLivePortalsFallback()
        {
            WizardPortal2D[] portals = FindObjectsByType<WizardPortal2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < portals.Length; i++)
            {
                if (portals[i] != null)
                {
                    portals[i].SealAndHide();
                }
            }

            GameObject root = GameObject.Find("Wizard_Portals");
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        private void Subscribe(StoneBasketHitStages2D basket)
        {
            if (basket != null)
            {
                basket.Filled -= OnBasketFilled;
                basket.Filled += OnBasketFilled;
            }
        }

        private void Unsubscribe(StoneBasketHitStages2D basket)
        {
            if (basket != null)
            {
                basket.Filled -= OnBasketFilled;
            }
        }
    }
}
