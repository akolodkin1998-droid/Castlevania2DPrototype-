using Castlevania2D.Environment;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.Hub
{
    public sealed class HubMerchantTalk2D : MonoBehaviour
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string PromptResourcePath = "UI/InteractPrompt_F";
        private const string SlotButtonPath = "UI/SaveSlotButton";
        private const string ExteriorSpawnName = "ExteriorSpawn";

        [SerializeField] private HubRoomTransition2D transition;
        [SerializeField] private Transform leaveShopDestination;
        [SerializeField] [Min(0.1f)] private float interactionDistance = 2.4f;
        [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 3.1f, 0f);
        [SerializeField] private Vector3 promptScale = new Vector3(0.48f, 0.48f, 1f);
        [SerializeField] [Min(0.01f)] private float promptPopDuration = 0.22f;
        [SerializeField] [Min(0f)] private float promptPopOffset = 0.2f;
        [SerializeField] private Sprite promptSprite;

        private Transform player;
        private SpriteRenderer promptRenderer;
        private float nextPlayerSearchTime;
        private bool promptWanted;
        private float promptPop;
        private bool offerOpen;
        private GameObject offerRoot;

        public static bool BlocksHubInteract { get; private set; }

        private void Awake()
        {
            CreatePrompt();
        }

        private void OnDisable()
        {
            CloseOffer();
        }

        private void Update()
        {
            CacheRefsIfNeeded();
            if (player == null || promptRenderer == null)
            {
                return;
            }

            bool isNear = ((Vector2)(player.position - transform.position)).sqrMagnitude
                          <= interactionDistance * interactionDistance;
            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            bool canInteract = isNear
                               && (transition == null || !transition.IsBusy)
                               && !SceneWarpTotem2D.IsBusy
                               && (session == null || session.CanInteract);

            if (offerOpen && !isNear)
            {
                CloseOffer();
            }

            promptWanted = canInteract && !offerOpen;
            TickPromptPop();

            if (!canInteract || offerOpen || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            OpenOffer();
        }

        private void OpenOffer()
        {
            if (offerRoot == null)
            {
                offerRoot = BuildOfferUi();
            }

            offerOpen = true;
            BlocksHubInteract = true;
            if (offerRoot != null)
            {
                offerRoot.SetActive(true);
            }
        }

        private void CloseOffer()
        {
            offerOpen = false;
            BlocksHubInteract = false;
            if (offerRoot != null)
            {
                offerRoot.SetActive(false);
            }
        }

        private void OnPlayTafl()
        {
            CloseOffer();
            HubSceneFadeLoad.Load(HubTaflSession.TaflSceneName);
        }

        private void OnLeaveShop()
        {
            CloseOffer();
            if (transition == null || leaveShopDestination == null)
            {
                return;
            }

            transition.TravelTo(leaveShopDestination.position, null, false);
        }

        private void CacheRefsIfNeeded()
        {
            if (player == null && Time.unscaledTime >= nextPlayerSearchTime)
            {
                nextPlayerSearchTime = Time.unscaledTime + 1f;
                GameObject playerObject = GameObject.Find(PlayerObjectName);
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (transition == null)
            {
                transition = FindFirstObjectByType<HubRoomTransition2D>();
            }

            if (leaveShopDestination == null)
            {
                GameObject spawn = GameObject.Find(ExteriorSpawnName);
                if (spawn != null)
                {
                    leaveShopDestination = spawn.transform;
                }
            }
        }

        private void CreatePrompt()
        {
            Sprite sprite = promptSprite != null
                ? promptSprite
                : Resources.Load<Sprite>(PromptResourcePath);

            var promptObject = new GameObject("InteractionPrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = promptLocalPosition;

            promptRenderer = promptObject.AddComponent<SpriteRenderer>();
            promptRenderer.sprite = sprite;
            promptRenderer.sortingOrder = 20;
            ApplyPromptVisual(0f);
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

            Transform promptTransform = promptRenderer.transform;
            promptTransform.localScale = promptScale * eased;
            promptTransform.localPosition = promptLocalPosition
                                           + new Vector3(0f, (eased - 1f) * promptPopOffset, 0f);

            Color color = Color.white;
            color.a = eased;
            promptRenderer.color = color;
            promptRenderer.enabled = eased > 0.001f && promptRenderer.sprite != null;
        }

        private GameObject BuildOfferUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            Sprite slotSprite = Resources.Load<Sprite>(SlotButtonPath);
            var root = new GameObject(
                "MerchantOfferCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Text title = CreateText("Title", root.transform);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(900f, 70f);
            titleRect.anchoredPosition = new Vector2(0f, 140f);
            title.fontSize = 30;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.92f, 0.84f, 0.68f, 1f);
            title.text = "Варяг предлагает сыграть в тафл";

            CreateSlotButton(
                root.transform,
                slotSprite,
                "Сыграть в тафл",
                new Vector2(0f, 20f),
                OnPlayTafl);
            CreateSlotButton(
                root.transform,
                slotSprite,
                "Выйти из лавки",
                new Vector2(0f, -90f),
                OnLeaveShop);

            root.SetActive(false);
            return root;
        }

        private static void CreateSlotButton(
            Transform parent,
            Sprite slotSprite,
            string label,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick)
        {
            var group = new GameObject(label, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform groupRect = group.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.sizeDelta = new Vector2(420f, 96f);
            groupRect.anchoredPosition = anchoredPosition;

            var visualGo = new GameObject("Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            visualGo.transform.SetParent(group.transform, false);
            Stretch(visualGo.GetComponent<RectTransform>());
            Image visual = visualGo.GetComponent<Image>();
            visual.raycastTarget = false;
            visual.preserveAspect = true;
            if (slotSprite != null)
            {
                visual.sprite = slotSprite;
                visual.color = Color.white;
            }
            else
            {
                visual.color = new Color(0.28f, 0.07f, 0.06f, 0.96f);
            }

            var hotspot = new GameObject(
                "Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(MenuButtonSlideFeedback));
            hotspot.transform.SetParent(group.transform, false);
            Stretch(hotspot.GetComponent<RectTransform>());
            Image hotspotImage = hotspot.GetComponent<Image>();
            hotspotImage.color = new Color(1f, 1f, 1f, 0f);
            hotspotImage.raycastTarget = true;
            Button button = hotspot.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            hotspot.GetComponent<MenuButtonSlideFeedback>().AssignVisual(visual.rectTransform);

            Text text = CreateText("Label", visualGo.transform);
            Stretch(text.rectTransform);
            text.text = label;
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.88f, 0.7f, 1f);
        }

        private static Text CreateText(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        public void EditorAssign(
            HubRoomTransition2D roomTransition,
            Transform leaveDestination,
            Sprite sprite)
        {
            transition = roomTransition;
            leaveShopDestination = leaveDestination;
            promptSprite = sprite;
        }
#endif
    }
}
