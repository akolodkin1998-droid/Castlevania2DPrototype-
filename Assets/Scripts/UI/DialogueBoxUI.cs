using System;
using Castlevania2D.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DialogueBoxUI : MonoBehaviour
    {
        private const string ResourcePath = "UI/Dialogue/DialoguePanel";
        private const int MaxChoices = 3;

        private static readonly Vector2 PanelSize = new Vector2(720f, 402f);
        private static readonly Color NameColor = new Color(0.93f, 0.82f, 0.62f, 1f);
        private static readonly Color BodyColor = new Color(0.18f, 0.09f, 0.05f, 1f);
        private const int TabInsetPixels = 48;
        private static readonly RectInt NamePixels = new RectInt(131, 73, 126, 22);
        private static readonly RectInt BodyPixels = new RectInt(100 + TabInsetPixels, 103, 423 - TabInsetPixels, 108);
        private static readonly RectInt FirstChoicePixels = new RectInt(100 + TabInsetPixels, 215, 423 - TabInsetPixels, 24);
        private const int ChoiceStrideY = 25;
        private const float SpriteWidth = 688f;
        private const float SpriteHeight = 384f;

        [SerializeField] private Image panelImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text bodyText;
        [SerializeField] private DialogueChoiceLine[] choiceLines;

        private static DialogueBoxUI instance;

        private Action<int> onChosen;
        private int choiceCount;
        private int selectedIndex;
        private bool open;

        public static bool IsOpen { get; private set; }

        public static DialogueBoxUI Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            EnsureEventSystem();

            var root = new GameObject(
                "DialogueBox_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject(
                "DialogueBox",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(DialogueBoxUI));
            panel.transform.SetParent(root.transform, false);

            DialogueBoxUI box = panel.GetComponent<DialogueBoxUI>();
            instance = box;
            box.Build();
            box.HideImmediate();
            return box;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                IsOpen = false;
                GameplayInputLock.IsLocked = false;
            }
        }

        public void Open(string speakerName, string body, string[] choices, Action<int> chosen)
        {
            if (panelImage == null)
            {
                Build();
            }
            else
            {
                PlacePanel();
            }

            onChosen = chosen;
            nameText.text = speakerName ?? string.Empty;
            bodyText.text = body ?? string.Empty;
            choiceCount = choices == null ? 0 : Mathf.Min(MaxChoices, choices.Length);
            selectedIndex = choiceCount > 0 ? 0 : -1;

            for (int i = 0; i < MaxChoices; i++)
            {
                bool visible = i < choiceCount;
                choiceLines[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                choiceLines[i].SetText(choices[i]);
                choiceLines[i].SetSelected(i == selectedIndex);
            }

            open = true;
            IsOpen = true;
            GameplayInputLock.IsLocked = true;
            gameObject.SetActive(true);
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(true);
            }
        }

        public void Close()
        {
            onChosen = null;
            HideImmediate();
        }

        public void HoverChoice(int index)
        {
            if (!open || index < 0 || index >= choiceCount)
            {
                return;
            }

            selectedIndex = index;
            RefreshSelection();
        }

        public void ConfirmChoice(int index)
        {
            if (!open || index < 0 || index >= choiceCount)
            {
                return;
            }

            Action<int> callback = onChosen;
            HideImmediate();
            callback?.Invoke(index);
        }

        private void Update()
        {
            if (!open)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveSelection(-1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveSelection(1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.F) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                ConfirmChoice(selectedIndex);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void MoveSelection(int delta)
        {
            if (choiceCount <= 0)
            {
                return;
            }

            selectedIndex = (selectedIndex + delta + choiceCount) % choiceCount;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < choiceCount; i++)
            {
                choiceLines[i].SetSelected(i == selectedIndex);
            }
        }

        private void HideImmediate()
        {
            open = false;
            IsOpen = false;
            GameplayInputLock.IsLocked = false;
            gameObject.SetActive(false);
            if (transform.parent != null)
            {
                transform.parent.gameObject.SetActive(false);
            }
        }

        private void PlacePanel()
        {
            RectTransform panelRect = GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = PanelSize;
            panelRect.anchoredPosition = new Vector2(0f, 20f);
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
                panelImage.color = new Color(0.22f, 0.12f, 0.08f, 0.96f);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText = CreateLabel("SpeakerName", transform, font, 18, FontStyle.Bold, NameColor, TextAnchor.MiddleLeft);
            PlaceOnPanel(nameText.rectTransform, NamePixels);

            bodyText = CreateLabel("Body", transform, font, 20, FontStyle.Normal, BodyColor, TextAnchor.UpperLeft);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceOnPanel(bodyText.rectTransform, BodyPixels);

            choiceLines = new DialogueChoiceLine[MaxChoices];
            for (int i = 0; i < MaxChoices; i++)
            {
                var choiceRect = new RectInt(
                    FirstChoicePixels.x,
                    FirstChoicePixels.y + i * ChoiceStrideY,
                    FirstChoicePixels.width,
                    FirstChoicePixels.height);
                choiceLines[i] = CreateChoice(i, font, choiceRect);
            }
        }

        private DialogueChoiceLine CreateChoice(int index, Font font, RectInt pixels)
        {
            var go = new GameObject("Choice_" + index, typeof(RectTransform), typeof(DialogueChoiceLine));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            PlaceOnPanel(rect, pixels);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            Text label = CreateLabel("Label", go.transform, font, 20, FontStyle.Bold, BodyColor, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform);

            DialogueChoiceLine line = go.GetComponent<DialogueChoiceLine>();
            line.Bind(this, index, label);
            return line;
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

        private static void PlaceOnPanel(RectTransform rect, RectInt pixels)
        {
            float x0 = pixels.x / SpriteWidth;
            float x1 = (pixels.x + pixels.width) / SpriteWidth;
            float yTop = pixels.y / SpriteHeight;
            float yBot = (pixels.y + pixels.height) / SpriteHeight;
            rect.anchorMin = new Vector2(x0, 1f - yBot);
            rect.anchorMax = new Vector2(x1, 1f - yTop);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
