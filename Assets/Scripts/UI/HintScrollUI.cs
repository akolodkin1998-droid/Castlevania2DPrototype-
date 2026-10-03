using System.Collections.Generic;
using System.Text;
using Castlevania2D.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Screen parchment for a first-time hint and for the hint journal (ё).
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HintScrollUI : MonoBehaviour
    {
        private const string ResourcePath = "UI/Hints/HintScroll";
        private const string CloseButtonResourcePath = "UI/Hints/HintCloseButton";
        private const float SpriteWidth = 384f;
        private const float SpriteHeight = 216f;
        private const float CloseButtonSpriteWidth = 32f;
        private const float CloseButtonSpriteHeight = 32f;
        private const float DismissDelay = 0.2f;

        private static readonly Vector2 PanelSize = new Vector2(768f, 432f);
        private static readonly RectInt TextPixels = new RectInt(72, 46, 240, 119);
        private static readonly RectInt CloseButtonPixels = new RectInt(360, 0, 24, 24);
        private static readonly RectInt CloseMarkPixels = new RectInt(6, 6, 20, 16);
        private static readonly Color BodyColor = new Color(0.22f, 0.12f, 0.07f, 1f);
        private static readonly Color CloseMarkColor = new Color(0.18f, 0.08f, 0.04f, 1f);

        [SerializeField] private Image panelImage;
        [SerializeField] private Text bodyText;
        [SerializeField] private ScrollRect bodyScroll;
        [SerializeField] private RectTransform bodyContent;
        [SerializeField] private HintScrollCloseButton closeButton;

        private static HintScrollUI instance;
        private bool open;
        private bool journalMode;
        private bool pausedGameplay;
        private float openedAt;

        public static bool IsOpen { get; private set; }
        public static bool IsJournalOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ResetAfterSceneLoad()
        {
            CloseIfOpen();
        }

        public static void CloseIfOpen()
        {
            IsOpen = false;
            IsJournalOpen = false;
            if (instance != null)
            {
                instance.HideImmediate();
            }
        }

        public static HintScrollUI Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            EnsureEventSystem();

            var root = new GameObject(
                "HintScroll_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject(
                "HintScroll",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(HintScrollUI));
            panel.transform.SetParent(root.transform, false);

            HintScrollUI box = panel.GetComponent<HintScrollUI>();
            instance = box;
            box.Build();
            box.HideImmediate();
            return box;
        }

        public static bool TryShow(string id, string title, string body)
        {
            if (IsOpen || DialogueBoxUI.IsOpen || HintJournal.Contains(id))
            {
                return false;
            }

            HintJournal.TryAdd(id, title, body);
            Ensure().Open(body ?? string.Empty, asJournal: false);
            return true;
        }

        public static void ToggleJournal()
        {
            HintScrollUI box = Ensure();
            if (box.open && box.journalMode)
            {
                box.Close();
                return;
            }

            if (box.open)
            {
                return;
            }

            box.Open(FormatJournalBody(), asJournal: true);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                IsOpen = false;
                IsJournalOpen = false;
                ReleasePauseAndLock();
            }
        }

        public void Close()
        {
            if (!open)
            {
                return;
            }

            if (Time.unscaledTime - openedAt < DismissDelay)
            {
                return;
            }

            HideImmediate();
        }

        private void Open(string body, bool asJournal)
        {
            if (panelImage == null)
            {
                Build();
            }
            else
            {
                PlacePanel();
            }

            journalMode = asJournal;
            bodyText.text = body ?? string.Empty;
            closeButton?.ResetVisual();
            open = true;
            IsOpen = true;
            IsJournalOpen = asJournal;
            openedAt = Time.unscaledTime;
            GameplayInputLock.IsLocked = true;
            if (!pausedGameplay)
            {
                GameplayPause.Hold();
                pausedGameplay = true;
            }

            gameObject.SetActive(true);
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(true);
            }

            RefreshScroll();
        }

        private void HideImmediate()
        {
            open = false;
            journalMode = false;
            IsOpen = false;
            IsJournalOpen = false;
            ReleasePauseAndLock();
            gameObject.SetActive(false);
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(false);
            }
        }

        private void ReleasePauseAndLock()
        {
            GameplayInputLock.IsLocked = false;
            if (!pausedGameplay)
            {
                return;
            }

            GameplayPause.Release();
            pausedGameplay = false;
        }

        private void PlacePanel()
        {
            RectTransform panelRect = GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = PanelSize;
            panelRect.anchoredPosition = Vector2.zero;
        }

        private void Build()
        {
            PlacePanel();

            panelImage = GetComponent<Image>();
            panelImage.raycastTarget = true;
            Sprite sprite = Resources.Load<Sprite>(ResourcePath);
            if (sprite != null)
            {
                panelImage.sprite = sprite;
                panelImage.preserveAspect = true;
                panelImage.color = Color.white;
            }
            else
            {
                panelImage.color = new Color(0.91f, 0.82f, 0.62f, 0.96f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildBody(font);
            BuildCloseButton(font);
        }

        private void BuildBody(Font font)
        {
            var viewportObject = new GameObject(
                "TextViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask),
                typeof(ScrollRect));
            viewportObject.transform.SetParent(transform, false);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.004f);
            viewportImage.raycastTarget = true;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;
            PlaceOnSprite(viewportObject.GetComponent<RectTransform>(), TextPixels, SpriteWidth, SpriteHeight);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            bodyContent = contentObject.GetComponent<RectTransform>();
            bodyContent.anchorMin = new Vector2(0f, 1f);
            bodyContent.anchorMax = new Vector2(1f, 1f);
            bodyContent.pivot = new Vector2(0.5f, 1f);
            bodyContent.anchoredPosition = Vector2.zero;
            bodyContent.offsetMin = new Vector2(0f, -119f);
            bodyContent.offsetMax = Vector2.zero;

            bodyText = CreateLabel("Body", contentObject.transform, font, 20, FontStyle.Normal, BodyColor, TextAnchor.UpperLeft);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.lineSpacing = 1.15f;
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            bodyRect.pivot = new Vector2(0.5f, 1f);

            bodyScroll = viewportObject.GetComponent<ScrollRect>();
            bodyScroll.horizontal = false;
            bodyScroll.vertical = true;
            bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            bodyScroll.scrollSensitivity = 24f;
            bodyScroll.viewport = viewportObject.GetComponent<RectTransform>();
            bodyScroll.content = bodyContent;
        }

        private void RefreshScroll()
        {
            if (bodyText == null || bodyContent == null || bodyScroll == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(119f, bodyText.preferredHeight + 4f);
            bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            bodyScroll.verticalNormalizedPosition = 1f;
        }

        private void BuildCloseButton(Font font)
        {
            var buttonObject = new GameObject(
                "CloseButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(HintScrollCloseButton));
            buttonObject.transform.SetParent(transform, false);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.raycastTarget = true;
            Sprite buttonSprite = Resources.Load<Sprite>(CloseButtonResourcePath);
            if (buttonSprite != null)
            {
                buttonImage.sprite = buttonSprite;
                buttonImage.preserveAspect = true;
                buttonImage.color = Color.white;
            }
            else
            {
                buttonImage.color = new Color(0.55f, 0.38f, 0.22f, 1f);
            }

            PlaceOnSprite(buttonObject.GetComponent<RectTransform>(), CloseButtonPixels, SpriteWidth, SpriteHeight);

            Text mark = CreateLabel("Mark", buttonObject.transform, font, 10, FontStyle.Bold, CloseMarkColor, TextAnchor.MiddleCenter);
            mark.text = "X";
            mark.horizontalOverflow = HorizontalWrapMode.Overflow;
            mark.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceOnSprite(mark.rectTransform, CloseMarkPixels, CloseButtonSpriteWidth, CloseButtonSpriteHeight);

            closeButton = buttonObject.GetComponent<HintScrollCloseButton>();
            closeButton.Bind(Close, buttonImage);
        }

        private static string FormatJournalBody()
        {
            var builder = new StringBuilder();
            builder.Append("Журнал подсказок");
            IReadOnlyList<HintJournal.Entry> entries = HintJournal.Entries;
            if (entries.Count == 0)
            {
                builder.Append("\n\nПока пусто. Новые подсказки появятся сами и останутся здесь.");
                return builder.ToString();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                builder.Append("\n\n");
                if (!string.IsNullOrEmpty(entries[i].Title))
                {
                    builder.Append(entries[i].Title);
                    builder.Append('\n');
                }

                builder.Append(entries[i].Body);
            }

            return builder.ToString();
        }

        private static Text CreateLabel(
            string objectName,
            Transform parent,
            Font font,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor align)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void PlaceOnSprite(RectTransform rect, RectInt pixels, float spriteWidth, float spriteHeight)
        {
            float x0 = pixels.x / spriteWidth;
            float x1 = (pixels.x + pixels.width) / spriteWidth;
            float yTop = pixels.y / spriteHeight;
            float yBot = (pixels.y + pixels.height) / spriteHeight;
            rect.anchorMin = new Vector2(x0, 1f - yBot);
            rect.anchorMax = new Vector2(x1, 1f - yTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
