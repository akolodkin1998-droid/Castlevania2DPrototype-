using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    public static class MainMenuRuntimeFactory
    {
        public static MainMenuView Create()
        {
            var canvasObject = new GameObject(
                "MainMenu_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(MainMenuView));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvasObject.transform);
            SetStretch(background.rectTransform);
            background.color = new Color(0.025f, 0.012f, 0.018f, 1f);
            background.raycastTarget = false;

            Text title = CreateText("Title", canvasObject.transform);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(1000f, 140f);
            titleRect.anchoredPosition = new Vector2(0f, 110f);
            title.text = "CASTLEVANIA 2D";
            title.fontSize = 72;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.84f, 0.72f, 0.52f, 1f);

            Button startButton = CreateStartButton(canvasObject.transform);
            MainMenuView view = canvasObject.GetComponent<MainMenuView>();
            view.Configure(startButton);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
            }

            return view;
        }

        private static Button CreateStartButton(Transform parent)
        {
            var buttonObject = new GameObject(
                "StartButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 92f);
            rect.anchoredPosition = new Vector2(0f, -70f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.24f, 0.055f, 0.045f, 0.96f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.82f, 0.68f, 1f);
            colors.pressedColor = new Color(0.72f, 0.58f, 0.48f, 1f);
            button.colors = colors;

            Text label = CreateText("Label", buttonObject.transform);
            SetStretch(label.rectTransform);
            label.text = "НАЧАТЬ ИГРУ";
            label.fontSize = 36;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.88f, 0.7f, 1f);
            label.raycastTarget = false;
            return button;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<Image>();
        }

        private static Text CreateText(string name, Transform parent)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            gameObject.transform.SetParent(parent, false);

            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
