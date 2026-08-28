using Castlevania2D.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.Minigames.Hnefatafl
{
    /// <summary>
    /// Builds the Hnefatafl UI at runtime from Resources sprites.
    /// </summary>
    public static class HnefataflRuntimeFactory
    {
        private const string BoardPath = "Minigames/Hnefatafl/Board/Board_9x9";
        private const string AttackerAPath = "Minigames/Hnefatafl/Pieces/Attacker_A";
        private const string AttackerBPath = "Minigames/Hnefatafl/Pieces/Attacker_B";
        private const string DefenderAPath = "Minigames/Hnefatafl/Pieces/Defender_A";
        private const string DefenderBPath = "Minigames/Hnefatafl/Pieces/Defender_B";
        private const string KingPath = "Minigames/Hnefatafl/Pieces/King";
        private const string SlotButtonPath = "UI/SaveSlotButton";

        public static HnefataflGameController Create()
        {
            Sprite boardSprite = LoadSprite(BoardPath);
            Sprite attackerA = LoadSprite(AttackerAPath);
            Sprite attackerB = LoadSprite(AttackerBPath);
            Sprite defenderA = LoadSprite(DefenderAPath);
            Sprite defenderB = LoadSprite(DefenderBPath);
            Sprite king = LoadSprite(KingPath);
            Sprite slotSprite = LoadSprite(SlotButtonPath);

            var root = new GameObject(
                "Hnefatafl_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(HnefataflGameController),
                typeof(HnefataflBoardView));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image backdrop = CreateImage("Backdrop", root.transform);
            Stretch(backdrop.rectTransform);
            backdrop.color = new Color(0.03f, 0.02f, 0.025f, 1f);
            backdrop.raycastTarget = false;

            Image boardImage = CreateImage("Board", root.transform);
            RectTransform boardRect = boardImage.rectTransform;
            boardRect.anchorMin = new Vector2(0.5f, 0.5f);
            boardRect.anchorMax = new Vector2(0.5f, 0.5f);
            boardRect.pivot = new Vector2(0.5f, 0.5f);
            boardRect.sizeDelta = new Vector2(860f, 860f);
            boardRect.anchoredPosition = new Vector2(0f, 20f);
            boardImage.preserveAspect = true;
            boardImage.raycastTarget = true;
            boardImage.type = Image.Type.Simple;
            boardImage.useSpriteMesh = false;

            var piecesRootGo = new GameObject("Pieces", typeof(RectTransform));
            piecesRootGo.transform.SetParent(boardRect, false);
            RectTransform piecesRoot = piecesRootGo.GetComponent<RectTransform>();
            piecesRoot.anchorMin = new Vector2(0.5f, 0.5f);
            piecesRoot.anchorMax = new Vector2(0.5f, 0.5f);
            piecesRoot.pivot = new Vector2(0.5f, 0.5f);

            Text status = CreateText("Status", root.transform);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(1200f, 70f);
            statusRect.anchoredPosition = new Vector2(0f, -24f);
            status.fontSize = 28;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = new Color(0.92f, 0.84f, 0.68f, 1f);

            GameObject endMatchGroup = CreateEndMatchGroup(
                root.transform,
                slotSprite,
                out Button restart,
                out Button leave);

            EnsureCamera();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            HnefataflBoardView view = root.GetComponent<HnefataflBoardView>();
            HnefataflGameController controller = root.GetComponent<HnefataflGameController>();
            view.Wire(boardRect, boardImage, piecesRoot);
            controller.Wire(view, status, restart, leave, endMatchGroup);
            view.Configure(
                boardSprite,
                attackerA,
                attackerB,
                defenderA,
                defenderB,
                king,
                controller.HandlePlayerMove);
            Canvas.ForceUpdateCanvases();
            return controller;
        }

        private static GameObject CreateEndMatchGroup(
            Transform parent,
            Sprite slotSprite,
            out Button restart,
            out Button leave)
        {
            var group = new GameObject("EndMatchGroup", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform groupRect = group.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0f);
            groupRect.anchorMax = new Vector2(0.5f, 0f);
            groupRect.pivot = new Vector2(0.5f, 0f);
            groupRect.sizeDelta = new Vector2(420f, 220f);
            groupRect.anchoredPosition = new Vector2(0f, 24f);

            leave = CreateMatchSlot(group.transform, slotSprite, "В ЛАВКУ", new Vector2(0f, 108f));
            restart = CreateMatchSlot(group.transform, slotSprite, "ЗАНОВО", new Vector2(0f, 0f));

            group.SetActive(false);
            return group;
        }

        private static Button CreateMatchSlot(
            Transform parent,
            Sprite slotSprite,
            string label,
            Vector2 anchoredPosition)
        {
            var slot = new GameObject(label, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0f);
            slotRect.sizeDelta = new Vector2(380f, 96f);
            slotRect.anchoredPosition = anchoredPosition;

            Image visual = CreateImage("Visual", slot.transform);
            Stretch(visual.rectTransform);
            visual.preserveAspect = true;
            visual.raycastTarget = false;
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
            hotspot.transform.SetParent(slot.transform, false);
            Stretch(hotspot.GetComponent<RectTransform>());
            Image hotspotImage = hotspot.GetComponent<Image>();
            hotspotImage.color = new Color(1f, 1f, 1f, 0f);
            hotspotImage.raycastTarget = true;
            Button button = hotspot.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            hotspot.GetComponent<MenuButtonSlideFeedback>().AssignVisual(visual.rectTransform);

            Text text = CreateText("Label", visual.transform);
            Stretch(text.rectTransform);
            text.text = label;
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.88f, 0.7f, 1f);
            text.raycastTarget = false;
            return button;
        }

        private static void EnsureCamera()
        {
            if (Object.FindFirstObjectByType<Camera>() != null)
            {
                return;
            }

            var cameraObject = new GameObject("HnefataflCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.02f, 0.025f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            cameraObject.tag = "MainCamera";
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
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

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"HnefataflRuntimeFactory: missing sprite '{path}'.");
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
