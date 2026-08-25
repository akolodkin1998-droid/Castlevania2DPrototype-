using Castlevania2D.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Hold-I inventory: birch frame sprite with 6x4 pixel-mapped cells.
    /// Loot stacks fill slots in inventory order; counts appear in the corner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventoryHudView : MonoBehaviour
    {
        private const string FrameResourcePath = "Items/Inventory/1785145649_6a672931ce114-Photoroom";
        private const int GridColumns = 6;
        private const int GridRows = 4;

        // Source PNG 1024x768 — user-measured cells (Y from top).
        private const float TextureWidth = 1024f;
        private const float TextureHeight = 768f;
        private const float RowTopStart = 98f;
        private const float RowBottomStart = 236f;
        private const float RowStride = 154f;

        private static readonly float[] ColumnLeft = { 42f, 200f, 363f, 522f, 680f, 839f };
        private static readonly float[] ColumnRight = { 180f, 342f, 503f, 659f, 820f, 980f };

        [Header("Input")]
        [SerializeField] private KeyCode holdKey = KeyCode.I;

        [Header("Player binding (by name)")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Item sprites (Resources)")]
        [SerializeField] private string commonItemResourcePath = "Items/Drop_Common";
        [SerializeField] private string entItemResourcePath = "Items/Drop_Ent";
        [SerializeField] private string maraTearItemResourcePath = "Items/Drop_MaraTear";
        [SerializeField] private string sporeBagItemResourcePath = "Items/Drop_SporeBag";

        [Header("Layout")]
        [SerializeField] [Range(0.35f, 0.85f)] private float panelScreenFraction = 0.62f;
        [SerializeField] private float iconInsetNormalized = 0.12f;
        [SerializeField] private int countFontSize = 14;

        private RectTransform canvasRect;
        private Rect lastPixelRect = new Rect(-1f, -1f, -1f, -1f);

        private GameObject panelRoot;
        private RectTransform panelRect;
        private Image frameImage;
        private SlotView[] slots;

        private Sprite frameSprite;
        private Sprite commonSprite;
        private Sprite entSprite;
        private Sprite maraTearSprite;
        private Sprite sporeBagSprite;
        private Font uiFont;

        private PlayerLootInventory inventory;
        private float nextBindAttemptTime;

        private sealed class SlotView
        {
            public GameObject Root;
            public Image Icon;
            public Text CountText;
        }

        private void Awake()
        {
            canvasRect = GetComponent<RectTransform>();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            frameSprite = LoadItemSprite(FrameResourcePath);
            commonSprite = LoadItemSprite(commonItemResourcePath);
            entSprite = LoadItemSprite(entItemResourcePath);
            maraTearSprite = LoadItemSprite(maraTearItemResourcePath);
            sporeBagSprite = LoadItemSprite(sporeBagItemResourcePath);
            CreateUiIfNeeded();
            SetVisible(false);
        }

        private void Start()
        {
            TryBindInventory(force: true);
            ApplyPanelLayout();
            RefreshSlots();
        }

        private void Update()
        {
            bool held = UnityEngine.Input.GetKey(holdKey);
            SetVisible(held);

            if (inventory == null && Time.time >= nextBindAttemptTime)
            {
                TryBindInventory(force: false);
            }
        }

        private void LateUpdate()
        {
            if (canvasRect == null || panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            Camera camera = ResolveViewportCamera();
            if (camera == null)
            {
                return;
            }

            if (camera.pixelRect == lastPixelRect)
            {
                return;
            }

            ApplyPanelLayout(camera);
            lastPixelRect = camera.pixelRect;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void TryBindInventory(bool force)
        {
            if (!force && inventory != null)
            {
                return;
            }

            nextBindAttemptTime = Time.time + 1f;

            GameObject player = GameObject.Find(playerObjectName);
            if (player == null)
            {
                return;
            }

            PlayerLootInventory found = player.GetComponent<PlayerLootInventory>();
            if (found == null)
            {
                return;
            }

            if (inventory == found)
            {
                return;
            }

            Unsubscribe();
            inventory = found;
            inventory.Changed += OnInventoryChanged;
            RefreshSlots();
        }

        private void Unsubscribe()
        {
            if (inventory == null)
            {
                return;
            }

            inventory.Changed -= OnInventoryChanged;
            inventory = null;
        }

        private void OnInventoryChanged()
        {
            if (panelRoot != null && panelRoot.activeSelf)
            {
                RefreshSlots();
            }
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot == null)
            {
                return;
            }

            if (panelRoot.activeSelf == visible)
            {
                return;
            }

            panelRoot.SetActive(visible);
            if (visible)
            {
                panelRoot.transform.SetAsLastSibling();
                ApplyPanelLayout();
                RefreshSlots();
            }
        }

        private void CreateUiIfNeeded()
        {
            if (panelRoot != null)
            {
                return;
            }

            panelRoot = new GameObject("InventoryPanel");
            panelRoot.transform.SetParent(transform, worldPositionStays: false);

            panelRect = panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale = Vector3.one;

            CanvasGroup canvasGroup = panelRoot.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(panelRoot.transform, false);
            RectTransform frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            frameImage = frameGo.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = false;
            frameImage.color = Color.white;

            int slotCount = GridColumns * GridRows;
            slots = new SlotView[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                slots[i] = CreateSlot(i);
            }
        }

        private SlotView CreateSlot(int index)
        {
            GameObject root = new GameObject($"Slot_{index:D2}");
            root.transform.SetParent(panelRoot.transform, worldPositionStays: false);

            RectTransform rect = root.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(iconInsetNormalized, iconInsetNormalized);
            iconRect.anchorMax = new Vector2(1f - iconInsetNormalized, 1f - iconInsetNormalized);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconRect.localScale = Vector3.one;

            Image icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;

            GameObject countGo = new GameObject("Count");
            countGo.transform.SetParent(root.transform, worldPositionStays: false);
            RectTransform countRect = countGo.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(1f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-3f, 2f);
            countRect.sizeDelta = new Vector2(40f, 22f);
            countRect.localScale = Vector3.one;

            Text countText = countGo.AddComponent<Text>();
            countText.font = uiFont;
            countText.fontSize = countFontSize;
            countText.fontStyle = FontStyle.Bold;
            countText.color = Color.white;
            countText.alignment = TextAnchor.LowerRight;
            countText.raycastTarget = false;
            countText.supportRichText = false;
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Overflow;
            countText.text = string.Empty;
            countGo.SetActive(false);

            return new SlotView
            {
                Root = root,
                Icon = icon,
                CountText = countText,
            };
        }

        private void RefreshSlots()
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                SetEmptySlot(slots[i]);
            }

            if (inventory == null)
            {
                return;
            }

            int stackCount = inventory.StackCount;
            for (int i = 0; i < stackCount && i < slots.Length; i++)
            {
                LootItemId itemId = inventory.GetItemIdAtSlot(i);
                int count = inventory.GetCountAtSlot(i);
                if (count <= 0)
                {
                    continue;
                }

                Sprite sprite = GetSpriteForItem(itemId);
                if (sprite == null)
                {
                    continue;
                }

                SetFilledSlot(slots[i], sprite, count);
            }
        }

        private Sprite GetSpriteForItem(LootItemId itemId)
        {
            return itemId switch
            {
                LootItemId.Common => commonSprite,
                LootItemId.Ent => entSprite,
                LootItemId.MaraTear => maraTearSprite,
                LootItemId.SporeBag => sporeBagSprite,
                _ => null,
            };
        }

        private static void SetFilledSlot(SlotView slot, Sprite sprite, int count)
        {
            slot.Root.SetActive(true);
            slot.Icon.enabled = sprite != null;
            slot.Icon.sprite = sprite;
            slot.Icon.color = Color.white;
            slot.CountText.text = count.ToString();
            slot.CountText.gameObject.SetActive(true);
        }

        private static void SetEmptySlot(SlotView slot)
        {
            slot.Root.SetActive(false);
            slot.Icon.enabled = false;
            slot.Icon.sprite = null;
            slot.CountText.text = string.Empty;
            slot.CountText.gameObject.SetActive(false);
        }

        private void ApplyPanelLayout()
        {
            ApplyPanelLayout(ResolveViewportCamera());
        }

        private void ApplyPanelLayout(Camera viewportCamera)
        {
            if (panelRect == null || canvasRect == null)
            {
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            if (canvasSize.x < 1f || canvasSize.y < 1f)
            {
                canvasSize = new Vector2(1920f, 1080f);
            }

            float aspect = TextureWidth / TextureHeight;
            float maxHeight = canvasSize.y * panelScreenFraction;
            float maxWidth = canvasSize.x * panelScreenFraction;
            float panelHeight = Mathf.Min(maxHeight, maxWidth / aspect);
            float panelWidth = panelHeight * aspect;

            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localScale = Vector3.one;

            if (frameImage != null)
            {
                frameImage.sprite = frameSprite;
            }

            LayoutSlots();

            if (viewportCamera != null)
            {
                lastPixelRect = viewportCamera.pixelRect;
            }
        }

        private void LayoutSlots()
        {
            if (slots == null)
            {
                return;
            }

            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridColumns; col++)
                {
                    int index = (row * GridColumns) + col;
                    GetCellNormalized(col, row, out Vector2 anchorMin, out Vector2 anchorMax);

                    RectTransform rect = slots[index].Root.GetComponent<RectTransform>();
                    rect.anchorMin = anchorMin;
                    rect.anchorMax = anchorMax;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Converts user pixel rect (Y from top) into a square cell in Unity anchors (Y from bottom).
        /// </summary>
        private static void GetCellNormalized(int column, int row, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            float left = ColumnLeft[column];
            float right = ColumnRight[column];
            float top = RowTopStart + (row * RowStride);
            float bottom = RowBottomStart + (row * RowStride);

            float width = right - left;
            float height = bottom - top;
            float side = Mathf.Min(width, height);
            float centerX = (left + right) * 0.5f;
            float centerYFromTop = (top + bottom) * 0.5f;

            left = centerX - (side * 0.5f);
            right = centerX + (side * 0.5f);
            top = centerYFromTop - (side * 0.5f);
            bottom = centerYFromTop + (side * 0.5f);

            anchorMin = new Vector2(left / TextureWidth, (TextureHeight - bottom) / TextureHeight);
            anchorMax = new Vector2(right / TextureWidth, (TextureHeight - top) / TextureHeight);
        }

        private Camera ResolveViewportCamera()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null &&
                canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            return Camera.main;
        }

        private static Sprite LoadItemSprite(string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
