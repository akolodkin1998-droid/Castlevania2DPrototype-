using Castlevania2D.Combat;
using Castlevania2D.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Bottom-left hotbar. Slot 1 = healing potions, slot 2 = spore bags.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public sealed class QuickAccessPanelUI : MonoBehaviour
    {
        private const string FrameResourcePath = "Items/Inventory/1785135476_6a670174d99df-Photoroom";
        private const string PotionResourcePath = "Items/Drop_Potion";
        private const string SporeBagResourcePath = "Items/Drop_SporeBag";
        private const string PlayerObjectName = "Player_HeroKnight";

        // Source PNG 1024x768 — user-measured first cell (Y from top).
        private const float TextureWidth = 1024f;
        private const float TextureHeight = 768f;
        private const float CellLeft = 48f;
        private const float CellTop = 294f;
        private const float CellRight = 225f;
        private const float CellBottom = 476f;
        private const float CellStrideX = 200f;

        [SerializeField] private Sprite frameSprite;
        [SerializeField] private Sprite potionSprite;
        [SerializeField] private Sprite sporeBagSprite;
        [SerializeField] private Vector2 panelSize = new Vector2(240f, 180f);
        [SerializeField] private Vector2 bottomLeftMargin = new Vector2(24f, -160.5508f);
        [SerializeField] private Vector3 panelScale = new Vector3(3f, 3f, 1f);
        [SerializeField] private KeyCode usePotionKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode useSporeBagKey = KeyCode.Alpha2;
        [SerializeField] private float iconInsetNormalized = 0.12f;
        [SerializeField] private int countFontSize = 7;

        private RectTransform rectTransform;
        private Image frameImage;
        private RectTransform potionSlotRoot;
        private Image potionIcon;
        private Text potionCountText;
        private RectTransform sporeBagSlotRoot;
        private Image sporeBagIcon;
        private Text sporeBagCountText;
        private PlayerQuickAccessInventory quickAccess;
        private PlayerLootInventory lootInventory;
        private SporeBagActivator2D sporeBagActivator;
        private float nextBindAttemptTime;
        private Font uiFont;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Sprite loadedFrame = LoadSprite(FrameResourcePath);
            if (loadedFrame != null)
            {
                frameSprite = loadedFrame;
            }

            Sprite loadedPotion = LoadSprite(PotionResourcePath);
            if (loadedPotion != null)
            {
                potionSprite = loadedPotion;
            }

            Sprite loadedSporeBag = LoadSprite(SporeBagResourcePath);
            if (loadedSporeBag != null)
            {
                sporeBagSprite = loadedSporeBag;
            }

            CacheComponents();
            EnsurePotionSlot();
            EnsureSporeBagSlot();
            ApplyLayout();
            ApplyFrameSprite();
            RefreshSlots();
        }

        private void Start()
        {
            ApplyLayout();
            ApplyFrameSprite();
            TryBindPlayer(force: true);
            RefreshSlots();
            transform.SetAsLastSibling();
        }

        private void OnEnable()
        {
            ApplyLayout();
            ApplyFrameSprite();
            TryBindPlayer(force: true);
            RefreshSlots();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if ((quickAccess == null || lootInventory == null) && Time.time >= nextBindAttemptTime)
            {
                TryBindPlayer(force: false);
            }

            if (UnityEngine.Input.GetKeyDown(usePotionKey) && quickAccess != null)
            {
                quickAccess.TryUseHealingPotion();
            }

            if (UnityEngine.Input.GetKeyDown(useSporeBagKey))
            {
                if (sporeBagActivator == null)
                {
                    TryBindPlayer(force: true);
                }

                sporeBagActivator?.TryActivate();
            }
        }

        private void CacheComponents()
        {
            frameImage = GetComponent<Image>();
            if (frameImage != null)
            {
                frameImage.raycastTarget = false;
                frameImage.preserveAspect = true;
                frameImage.enabled = true;
                frameImage.color = Color.white;
            }
        }

        private void ApplyLayout()
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.sizeDelta = panelSize;
            rectTransform.anchoredPosition = bottomLeftMargin;
            rectTransform.localScale = panelScale;

            ApplySlotLayout(potionSlotRoot, 0);
            ApplySlotLayout(sporeBagSlotRoot, 1);
            ApplyCountTextSharpness(potionCountText);
            ApplyCountTextSharpness(sporeBagCountText);
        }

        private void ApplyFrameSprite()
        {
            if (frameImage == null || frameSprite == null)
            {
                return;
            }

            frameImage.sprite = frameSprite;
        }

        private void EnsurePotionSlot()
        {
            EnsureItemSlot(
                "PotionSlot",
                out potionSlotRoot,
                out potionIcon,
                out potionCountText);
        }

        private void EnsureSporeBagSlot()
        {
            EnsureItemSlot(
                "SporeBagSlot",
                out sporeBagSlotRoot,
                out sporeBagIcon,
                out sporeBagCountText);
        }

        private void EnsureItemSlot(
            string slotName,
            out RectTransform slotRoot,
            out Image icon,
            out Text countText)
        {
            Transform existing = transform.Find(slotName);
            GameObject slotGo;
            if (existing != null)
            {
                slotGo = existing.gameObject;
            }
            else
            {
                slotGo = new GameObject(slotName, typeof(RectTransform));
                slotGo.transform.SetParent(transform, false);
            }

            slotRoot = slotGo.GetComponent<RectTransform>();

            Transform iconTransform = slotGo.transform.Find("Icon");
            if (iconTransform == null)
            {
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(slotGo.transform, false);
                iconTransform = iconGo.transform;
            }

            icon = iconTransform.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.color = Color.white;

            RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(iconInsetNormalized, iconInsetNormalized);
            iconRect.anchorMax = new Vector2(1f - iconInsetNormalized, 1f - iconInsetNormalized);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            Transform countTransform = slotGo.transform.Find("Count");
            if (countTransform == null)
            {
                GameObject countGo = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                countGo.transform.SetParent(slotGo.transform, false);
                countTransform = countGo.transform;
            }

            countText = countTransform.GetComponent<Text>();
            countText.font = uiFont;
            countText.fontStyle = FontStyle.Bold;
            countText.color = Color.white;
            countText.alignment = TextAnchor.LowerRight;
            countText.raycastTarget = false;
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Overflow;
            countText.supportRichText = false;

            Outline outline = countTransform.GetComponent<Outline>();
            if (outline == null)
            {
                outline = countTransform.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            RectTransform countRect = countTransform.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.35f, 0f);
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(0f, 2f);
            countRect.offsetMax = new Vector2(-4f, -2f);
            countRect.pivot = new Vector2(1f, 0f);
        }

        private void ApplyCountTextSharpness(Text countText)
        {
            if (countText == null)
            {
                return;
            }

            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(panelScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(panelScale.y));
            int rasterFontSize = Mathf.Max(1, Mathf.RoundToInt(countFontSize * scaleY));
            countText.fontSize = rasterFontSize;
            countText.rectTransform.localScale = new Vector3(1f / scaleX, 1f / scaleY, 1f);
        }

        private void ApplySlotLayout(RectTransform slotRoot, int cellIndex)
        {
            if (slotRoot == null)
            {
                return;
            }

            GetSquaredCellNormalized(cellIndex, out Vector2 anchorMin, out Vector2 anchorMax);
            slotRoot.anchorMin = anchorMin;
            slotRoot.anchorMax = anchorMax;
            slotRoot.offsetMin = Vector2.zero;
            slotRoot.offsetMax = Vector2.zero;
            slotRoot.pivot = new Vector2(0.5f, 0.5f);
            slotRoot.localScale = Vector3.one;
        }

        private static void GetSquaredCellNormalized(
            int cellIndex,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            float width = CellRight - CellLeft;
            float height = CellBottom - CellTop;
            float side = Mathf.Min(width, height);
            float centerX = (CellLeft + CellRight) * 0.5f + cellIndex * CellStrideX;
            float centerYFromTop = (CellTop + CellBottom) * 0.5f;

            float left = centerX - side * 0.5f;
            float right = centerX + side * 0.5f;
            float top = centerYFromTop - side * 0.5f;
            float bottom = centerYFromTop + side * 0.5f;

            float xMin = left / TextureWidth;
            float xMax = right / TextureWidth;
            float yMin = (TextureHeight - bottom) / TextureHeight;
            float yMax = (TextureHeight - top) / TextureHeight;

            anchorMin = new Vector2(xMin, yMin);
            anchorMax = new Vector2(xMax, yMax);
        }

        private void TryBindPlayer(bool force)
        {
            if (!force && Time.time < nextBindAttemptTime)
            {
                return;
            }

            nextBindAttemptTime = Time.time + 0.5f;
            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                return;
            }

            PlayerQuickAccessInventory foundQuick = player.GetComponent<PlayerQuickAccessInventory>();
            if (foundQuick == null)
            {
                foundQuick = player.AddComponent<PlayerQuickAccessInventory>();
            }

            PlayerLootInventory foundLoot = player.GetComponent<PlayerLootInventory>();
            if (foundLoot == null)
            {
                foundLoot = player.AddComponent<PlayerLootInventory>();
            }

            SporeBagActivator2D foundActivator = player.GetComponent<SporeBagActivator2D>();
            if (foundActivator == null)
            {
                foundActivator = player.AddComponent<SporeBagActivator2D>();
            }

            sporeBagActivator = foundActivator;

            if (foundQuick != quickAccess || foundLoot != lootInventory)
            {
                Unsubscribe();
                quickAccess = foundQuick;
                lootInventory = foundLoot;
                quickAccess.HealingPotionCountChanged += OnPotionCountChanged;
                lootInventory.Changed += OnLootChanged;
            }

            RefreshSlots();
        }

        private void Unsubscribe()
        {
            if (quickAccess != null)
            {
                quickAccess.HealingPotionCountChanged -= OnPotionCountChanged;
            }

            if (lootInventory != null)
            {
                lootInventory.Changed -= OnLootChanged;
            }
        }

        private void OnPotionCountChanged(int _)
        {
            RefreshSlots();
        }

        private void OnLootChanged()
        {
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            RefreshSlot(
                potionSlotRoot,
                potionIcon,
                potionCountText,
                potionSprite,
                quickAccess != null ? quickAccess.HealingPotionCount : 0);

            RefreshSlot(
                sporeBagSlotRoot,
                sporeBagIcon,
                sporeBagCountText,
                sporeBagSprite,
                lootInventory != null ? lootInventory.SporeBagCount : 0);
        }

        private static void RefreshSlot(
            RectTransform slotRoot,
            Image icon,
            Text countText,
            Sprite sprite,
            int count)
        {
            if (slotRoot == null || icon == null || countText == null)
            {
                return;
            }

            bool visible = count > 0;
            slotRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            icon.enabled = sprite != null;
            icon.sprite = sprite;
            countText.text = count.ToString();
        }

        private static Sprite LoadSprite(string resourcePath)
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
