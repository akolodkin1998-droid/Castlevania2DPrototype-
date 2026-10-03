using Castlevania2D.Hub;
using Castlevania2D.Input;
using Castlevania2D.Level;
using Castlevania2D.Loot;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    public sealed class CraneCrateInspect2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";
        private const string InteriorResourcePath = "Environment/GearBox/CrateInterior";
        private const string LeverResourcePath = "Environment/GearBox/CrateLever";
        private const string LeverDownResourcePath = "Environment/GearBox/CrateLeverDown";
        private const string LockedPromptText = "Нужен ключ";

        public static bool IsOpen { get; private set; }
        private static CraneCrateInspect2D current;

        [SerializeField] [Min(0.1f)] private float interactionDistance = 1.8f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] private Vector2 interiorScreenSize = new Vector2(720f, 405f);
        [SerializeField] private Vector2 leverScreenSize = new Vector2(336f, 336f);
        [SerializeField] [Min(0.01f)] private float interiorPopDuration = 0.18f;
        [SerializeField] private ElevatorMechanism2D hoist;

        private Transform player;
        private PlayerLootInventory inventory;
        private SpriteRenderer promptRenderer;
        private TextMesh lockedPromptText;
        private Canvas overlayCanvas;
        private RectTransform interiorRect;
        private Image interiorImage;
        private Image dimmerImage;
        private RectTransform leverRect;
        private Image leverImage;
        private Sprite interiorSprite;
        private Sprite leverSprite;
        private Sprite leverDownSprite;
        private float nextPlayerSearchTime;
        private bool promptWanted;
        private bool lockedPromptWanted;
        private float promptPop;
        private float lockedPromptPop;
        private bool overlayWanted;
        private float overlayPop;
        private bool lockedInput;
        private bool unlocked;
        private bool leverPulled;

        public void BindHoist(ElevatorMechanism2D mechanism)
        {
            hoist = mechanism;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetOpenFlag()
        {
            current = null;
            IsOpen = false;
        }

        public static void ForceClose()
        {
            if (current != null)
            {
                current.CloseOverlay();
            }

            IsOpen = false;
        }

        private void Awake()
        {
            current = this;
            GearBoxInspect2D strayLift = GetComponent<GearBoxInspect2D>();
            if (strayLift != null)
            {
                Destroy(strayLift);
            }

            interiorSprite = Resources.Load<Sprite>(InteriorResourcePath);
            leverSprite = Resources.Load<Sprite>(LeverResourcePath);
            leverDownSprite = Resources.Load<Sprite>(LeverDownResourcePath);
            if (hoist == null)
            {
                hoist = GetComponentInParent<ElevatorMechanism2D>();
            }

            CreatePrompt();
            CreateOverlay();
        }

        private void OnDisable()
        {
            CloseOverlay();
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }

            if (IsOpen)
            {
                UnlockInput();
                IsOpen = false;
            }
        }

        private void Update()
        {
            CachePlayerIfNeeded();
            if (player == null)
            {
                return;
            }

            bool isNear = ((Vector2)(player.position - transform.position)).sqrMagnitude
                          <= interactionDistance * interactionDistance;
            bool canInspect = isNear
                              && !DialogueBoxUI.IsOpen
                              && !HintScrollUI.IsOpen
                              && !NpcTalk2D.IsTalking
                              && CanUseSession();
            bool hasKey = HasCraneKey();

            if (overlayWanted)
            {
                promptWanted = false;
                lockedPromptWanted = false;
                if (!isNear || UnityEngine.Input.GetKeyDown(KeyCode.F)
                    || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseOverlay();
                }
            }
            else
            {
                promptWanted = canInspect && (hasKey || unlocked) && !leverPulled;
                lockedPromptWanted = canInspect && !hasKey && !unlocked && !leverPulled;
                if (canInspect && (hasKey || unlocked) && !leverPulled && UnityEngine.Input.GetKeyDown(KeyCode.F))
                {
                    OpenOverlay();
                }
            }

            TickPromptPop();
            TickLockedPromptPop();
            TickOverlayPop();
            TickLeverClick();
        }

        private static bool CanUseSession()
        {
            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            return session == null || session.CanInteract;
        }

        private void CachePlayerIfNeeded()
        {
            if (player != null || Time.unscaledTime < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.unscaledTime + 1f;
            GameObject playerObject = GameObject.Find(PlayerObjectName);
            if (playerObject != null)
            {
                player = playerObject.transform;
                inventory = playerObject.GetComponent<PlayerLootInventory>();
            }
        }

        private bool HasCraneKey()
        {
            if (inventory == null && player != null)
            {
                inventory = player.GetComponent<PlayerLootInventory>();
            }

            return inventory != null && inventory.GetCount(LootItemId.CraneKey) > 0;
        }

        private void OpenOverlay()
        {
            if (interiorImage == null || interiorImage.sprite == null)
            {
                return;
            }

            if (!unlocked && !ConsumeKey(LootItemId.CraneKey))
            {
                return;
            }

            unlocked = true;
            GearBoxInspect2D.ForceClose();
            overlayWanted = true;
            IsOpen = true;
            LockInput();
            if (overlayCanvas != null)
            {
                overlayCanvas.gameObject.SetActive(true);
            }

            ApplyLeverVisual();
        }

        private void CloseOverlay()
        {
            overlayWanted = false;
            IsOpen = false;
            UnlockInput();
        }

        private void LockInput()
        {
            if (lockedInput)
            {
                return;
            }

            lockedInput = true;
            GameplayInputLock.IsLocked = true;
        }

        private void UnlockInput()
        {
            if (!lockedInput)
            {
                return;
            }

            lockedInput = false;
            GameplayInputLock.IsLocked = false;
        }

        private void CreatePrompt()
        {
            Sprite sprite = Resources.Load<Sprite>(PromptResourcePath);
            var promptObject = new GameObject("InteractionPrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptLocalPosition;
            promptRenderer = promptObject.AddComponent<SpriteRenderer>();
            promptRenderer.sprite = sprite;
            promptRenderer.sortingOrder = 20;
            ApplyPromptVisual(0f);

            var lockedObject = new GameObject("LockedPrompt", typeof(TextMesh));
            lockedObject.transform.SetParent(transform, false);
            lockedObject.transform.localPosition = promptLocalPosition;
            lockedPromptText = lockedObject.GetComponent<TextMesh>();
            lockedPromptText.text = LockedPromptText;
            lockedPromptText.anchor = TextAnchor.MiddleCenter;
            lockedPromptText.alignment = TextAlignment.Center;
            lockedPromptText.fontSize = 32;
            lockedPromptText.characterSize = 0.08f;
            lockedPromptText.color = new Color(0.95f, 0.87f, 0.68f, 1f);
            lockedObject.GetComponent<MeshRenderer>().sortingOrder = 20;
            ApplyLockedPromptVisual(0f);
        }

        private void CreateOverlay()
        {
            var canvasObject = new GameObject(
                "CraneCrateInspectCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 85;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var dimmerObject = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dimmerObject.transform.SetParent(canvasObject.transform, false);
            RectTransform dimmerRect = dimmerObject.GetComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            dimmerImage = dimmerObject.GetComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.45f);
            dimmerImage.raycastTarget = false;

            var imageObject = new GameObject("CrateInterior", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            interiorRect = imageObject.GetComponent<RectTransform>();
            interiorRect.anchorMin = new Vector2(0.5f, 0.5f);
            interiorRect.anchorMax = new Vector2(0.5f, 0.5f);
            interiorRect.pivot = new Vector2(0.5f, 0.5f);
            interiorRect.sizeDelta = interiorScreenSize;
            interiorImage = imageObject.GetComponent<Image>();
            interiorImage.sprite = interiorSprite;
            interiorImage.preserveAspect = true;
            interiorImage.raycastTarget = false;

            if (leverSprite != null)
            {
                var leverObject = new GameObject(
                    "CrateLever",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                leverObject.transform.SetParent(interiorRect, false);
                leverRect = leverObject.GetComponent<RectTransform>();
                leverRect.anchorMin = new Vector2(0.5f, 0.5f);
                leverRect.anchorMax = new Vector2(0.5f, 0.5f);
                leverRect.pivot = new Vector2(0.5f, 0.5f);
                leverRect.anchoredPosition = new Vector2(0f, leverScreenSize.y * 0.08f);
                leverRect.sizeDelta = leverScreenSize;
                leverImage = leverObject.GetComponent<Image>();
                leverImage.sprite = leverSprite;
                leverImage.preserveAspect = true;
                leverImage.raycastTarget = true;
            }

            canvasObject.SetActive(false);
        }

        private void TickPromptPop()
        {
            float target = promptWanted ? 1f : 0f;
            float speed = promptPopDuration > 0.001f ? 1f / promptPopDuration : 1000f;
            promptPop = Mathf.MoveTowards(promptPop, target, speed * Time.unscaledDeltaTime);
            ApplyPromptVisual(promptPop * promptPop * (3f - 2f * promptPop));
        }

        private void ApplyPromptVisual(float eased)
        {
            if (promptRenderer == null)
            {
                return;
            }

            promptRenderer.transform.localScale = promptScale * eased;
            promptRenderer.transform.localPosition = promptLocalPosition
                                                   + new Vector3(0f, (eased - 1f) * promptPopOffset, 0f);
            Color color = Color.white;
            color.a = eased;
            promptRenderer.color = color;
            promptRenderer.enabled = eased > 0.001f && promptRenderer.sprite != null;
        }

        private void TickLockedPromptPop()
        {
            float target = lockedPromptWanted ? 1f : 0f;
            float speed = promptPopDuration > 0.001f ? 1f / promptPopDuration : 1000f;
            lockedPromptPop = Mathf.MoveTowards(lockedPromptPop, target, speed * Time.unscaledDeltaTime);
            ApplyLockedPromptVisual(lockedPromptPop * lockedPromptPop * (3f - 2f * lockedPromptPop));
        }

        private void ApplyLockedPromptVisual(float eased)
        {
            if (lockedPromptText == null)
            {
                return;
            }

            lockedPromptText.transform.localScale = Vector3.one * eased;
            lockedPromptText.transform.localPosition = promptLocalPosition
                                                     + new Vector3(0f, (eased - 1f) * promptPopOffset, 0f);
            Color color = lockedPromptText.color;
            color.a = eased;
            lockedPromptText.color = color;
            lockedPromptText.gameObject.SetActive(eased > 0.001f);
        }

        private void TickOverlayPop()
        {
            if (overlayCanvas == null || interiorRect == null)
            {
                return;
            }

            float target = overlayWanted ? 1f : 0f;
            float speed = interiorPopDuration > 0.001f ? 1f / interiorPopDuration : 1000f;
            overlayPop = Mathf.MoveTowards(overlayPop, target, speed * Time.unscaledDeltaTime);
            float eased = overlayPop * overlayPop * (3f - 2f * overlayPop);
            interiorRect.localScale = Vector3.one * eased;
            if (dimmerImage != null)
            {
                Color dimmer = dimmerImage.color;
                dimmer.a = 0.45f * eased;
                dimmerImage.color = dimmer;
            }

            if (!overlayWanted && overlayPop <= 0.001f && overlayCanvas.gameObject.activeSelf)
            {
                overlayCanvas.gameObject.SetActive(false);
            }
        }

        private void TickLeverClick()
        {
            if (!overlayWanted || leverPulled || leverRect == null || overlayPop < 0.95f)
            {
                return;
            }

            if (!UnityEngine.Input.GetMouseButtonDown(0))
            {
                return;
            }

            Vector2 mouse = UnityEngine.Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(leverRect, mouse, null))
            {
                CompleteLeverPull();
            }
        }

        private void CompleteLeverPull()
        {
            if (leverPulled)
            {
                return;
            }

            leverPulled = true;
            ApplyLeverVisual();
            if (hoist == null)
            {
                hoist = GetComponentInParent<ElevatorMechanism2D>();
            }

            hoist?.StartHoistFromLever();
            CloseOverlay();
        }

        private bool ConsumeKey(LootItemId keyId)
        {
            if (player != null)
            {
                inventory = player.GetComponent<PlayerLootInventory>();
            }

            if (inventory == null || !inventory.TryRemove(keyId))
            {
                return false;
            }

            PlayerInventorySession.CaptureFromScene();
            PlayerInventoryHudBootstrap.Refresh();
            return true;
        }

        private void ApplyLeverVisual()
        {
            if (leverImage == null)
            {
                return;
            }

            Sprite sprite = leverPulled && leverDownSprite != null ? leverDownSprite : leverSprite;
            if (sprite != null)
            {
                leverImage.sprite = sprite;
            }
        }
    }
}
