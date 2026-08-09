using Castlevania2D.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Bottom-left hotbar. Slot 1 (leftmost) shows healing potions with a corner count.
    /// Cell bounds come from source texture pixels; the cell is squared to fit inside.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public sealed class QuickAccessPanelUI : MonoBehaviour
    {
        private const string FrameResourcePath = "Items/Inventory/1785135476_6a670174d99df-Photoroom";
        private const string PotionResourcePath = "Items/Drop_Potion";
        private const string PlayerObjectName = "Player_HeroKnight";

        // Source PNG 1024x768 — user-measured first cell (Y from top).
        private const float TextureWidth = 1024f;
        private const float TextureHeight = 768f;
        private const float CellLeft = 48f;
        private const float CellTop = 294f;
        private const float CellRight = 225f;
        private const float CellBottom = 476f;

        [SerializeField] private Sprite frameSprite;
        [SerializeField] private Sprite potionSprite;
        [SerializeField] private Vector2 panelSize = new Vector2(240f, 180f);
        [SerializeField] private Vector2 bottomLeftMargin = new Vector2(24f, -160.5508f);
        [SerializeField] private Vector3 panelScale = new Vector3(3f, 3f, 1f);
        [SerializeField] private KeyCode usePotionKey = KeyCode.Alpha1;
        [SerializeField] private float iconInsetNormalized = 0.12f;
        [SerializeField] private int countFontSize = 7;

        private RectTransform rectTransform;
        private Image frameImage;
        private RectTransform potionSlotRoot;
        private Image potionIcon;
        private Text potionCountText;
        private PlayerQuickAccessInventory quickAccess;
        private float nextBindAttemptTime;
        private Font uiFont;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Важно: принудительно берём спрайты из Resources, чтобы исключить
            // ситуацию, когда в сцене сериализовано старое значение.
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

            CacheComponents();
            EnsurePotionSlot();
            ApplyLayout();
            ApplyFrameSprite();
            RefreshPotionSlot();
        }

        private void Start()
        {
            ApplyLayout();
            ApplyFrameSprite();
            TryBindQuickAccess(force: true);
            RefreshPotionSlot();
            transform.SetAsLastSibling();
        }

        private void OnEnable()
        {
            ApplyLayout();
            ApplyFrameSprite();
            TryBindQuickAccess(force: true);
            RefreshPotionSlot();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (quickAccess == null && Time.time >= nextBindAttemptTime)
            {
                TryBindQuickAccess(force: false);
            }

            if (UnityEngine.Input.GetKeyDown(usePotionKey) && quickAccess != null)
            {
                quickAccess.TryUseHealingPotion();
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

            ApplyPotionSlotLayout();
            ApplyCountTextSharpness();
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
            Transform existing = transform.Find("PotionSlot");
            GameObject slotGo;
            if (existing != null)
            {
                slotGo = existing.gameObject;
            }
            else
            {
                slotGo = new GameObject("PotionSlot", typeof(RectTransform));
                slotGo.transform.SetParent(transform, false);
            }

            potionSlotRoot = slotGo.GetComponent<RectTransform>();

            Transform iconTransform = slotGo.transform.Find("Icon");
            if (iconTransform == null)
            {
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(slotGo.transform, false);
                iconTransform = iconGo.transform;
            }

            potionIcon = iconTransform.GetComponent<Image>();
            potionIcon.raycastTarget = false;
            potionIcon.preserveAspect = true;
            potionIcon.color = Color.white;

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

            potionCountText = countTransform.GetComponent<Text>();
            potionCountText.font = uiFont;
            potionCountText.fontStyle = FontStyle.Bold;
            potionCountText.color = Color.white;
            potionCountText.alignment = TextAnchor.LowerRight;
            potionCountText.raycastTarget = false;
            potionCountText.horizontalOverflow = HorizontalWrapMode.Overflow;
            potionCountText.verticalOverflow = VerticalWrapMode.Overflow;
            potionCountText.supportRichText = false;

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

            ApplyCountTextSharpness();
            ApplyPotionSlotLayout();
        }

        private void ApplyCountTextSharpness()
        {
            if (potionCountText == null)
            {
                return;
            }

            // Parent panel is scaled up; counter that so glyphs aren't rasterized tiny then upscaled blurry.
            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(panelScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(panelScale.y));
            int rasterFontSize = Mathf.Max(1, Mathf.RoundToInt(countFontSize * scaleY));
            potionCountText.fontSize = rasterFontSize;

            RectTransform countRect = potionCountText.rectTransform;
            countRect.localScale = new Vector3(1f / scaleX, 1f / scaleY, 1f);
        }

        private void ApplyPotionSlotLayout()
        {
            if (potionSlotRoot == null)
            {
                return;
            }

            GetSquaredCellNormalized(out Vector2 anchorMin, out Vector2 anchorMax);
            potionSlotRoot.anchorMin = anchorMin;
            potionSlotRoot.anchorMax = anchorMax;
            potionSlotRoot.offsetMin = Vector2.zero;
            potionSlotRoot.offsetMax = Vector2.zero;
            potionSlotRoot.pivot = new Vector2(0.5f, 0.5f);
            potionSlotRoot.localScale = Vector3.one;
        }

        /// <summary>
        /// Converts user pixel rect (Y from top) into a square cell in Unity anchors (Y from bottom).
        /// </summary>
        private static void GetSquaredCellNormalized(out Vector2 anchorMin, out Vector2 anchorMax)
        {
            float width = CellRight - CellLeft;
            float height = CellBottom - CellTop;
            float side = Mathf.Min(width, height);
            float centerX = (CellLeft + CellRight) * 0.5f;
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

        private void TryBindQuickAccess(bool force)
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

            PlayerQuickAccessInventory found = player.GetComponent<PlayerQuickAccessInventory>();
            if (found == null)
            {
                found = player.AddComponent<PlayerQuickAccessInventory>();
            }

            if (found == quickAccess)
            {
                return;
            }

            Unsubscribe();
            quickAccess = found;
            quickAccess.HealingPotionCountChanged += OnPotionCountChanged;
            RefreshPotionSlot();
        }

        private void Unsubscribe()
        {
            if (quickAccess != null)
            {
                quickAccess.HealingPotionCountChanged -= OnPotionCountChanged;
            }
        }

        private void OnPotionCountChanged(int _)
        {
            RefreshPotionSlot();
        }

        private void RefreshPotionSlot()
        {
            if (potionSlotRoot == null || potionIcon == null || potionCountText == null)
            {
                return;
            }

            int count = quickAccess != null ? quickAccess.HealingPotionCount : 0;
            bool visible = count > 0;

            potionSlotRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            potionIcon.enabled = potionSprite != null;
            potionIcon.sprite = potionSprite;
            potionCountText.text = count.ToString();
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
