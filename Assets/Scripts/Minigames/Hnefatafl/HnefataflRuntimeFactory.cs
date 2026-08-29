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
        private const string TablePath = "Minigames/Hnefatafl/Table/WoodenTable";
        private const string AttackerAPath = "Minigames/Hnefatafl/Pieces/Attacker_A";
        private const string AttackerBPath = "Minigames/Hnefatafl/Pieces/Attacker_B";
        private const string DefenderAPath = "Minigames/Hnefatafl/Pieces/Defender_A";
        private const string DefenderBPath = "Minigames/Hnefatafl/Pieces/Defender_B";
        private const string KingPath = "Minigames/Hnefatafl/Pieces/King";
        private static readonly Color RoomColor = new Color(0.07f, 0.035f, 0.02f, 1f);
        private static readonly Color ButtonIdle = new Color(0.96f, 0.88f, 0.7f, 1f);
        private static readonly Color ButtonHover = new Color(1f, 0.95f, 0.55f, 1f);

        public static HnefataflGameController Create()
        {
            Sprite boardSprite = LoadSprite(BoardPath);
            Sprite tableSprite = LoadSprite(TablePath);
            Sprite attackerA = LoadSprite(AttackerAPath);
            Sprite attackerB = LoadSprite(AttackerBPath);
            Sprite defenderA = LoadSprite(DefenderAPath);
            Sprite defenderB = LoadSprite(DefenderBPath);
            Sprite king = LoadSprite(KingPath);

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

            Image room = CreateImage("Room", root.transform);
            Stretch(room.rectTransform);
            room.color = RoomColor;
            room.raycastTarget = false;

            Image table = CreateImage("TableBackground", root.transform);
            table.raycastTarget = false;
            PlaceCoverBackground(table.rectTransform, tableSprite);
            if (tableSprite != null)
            {
                table.sprite = tableSprite;
                table.preserveAspect = true;
                table.color = Color.white;
                table.type = Image.Type.Simple;
                table.useSpriteMesh = false;
            }
            else
            {
                Stretch(table.rectTransform);
                table.color = RoomColor;
            }

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
            out Button restart,
            out Button leave)
        {
            var group = new GameObject("EndMatchGroup", typeof(RectTransform));
            group.transform.SetParent(parent, false);
            RectTransform groupRect = group.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0f);
            groupRect.anchorMax = new Vector2(0.5f, 0f);
            groupRect.pivot = new Vector2(0.5f, 0f);
            groupRect.sizeDelta = new Vector2(900f, 72f);
            groupRect.anchoredPosition = new Vector2(0f, 28f);

            restart = CreateTextButton(group.transform, "ЗАНОВО", new Vector2(-220f, 0f));
            leave = CreateTextButton(group.transform, "ЗАВЕРШИТЬ", new Vector2(220f, 0f));

            return group;
        }

        private static Button CreateTextButton(
            Transform parent,
            string label,
            Vector2 anchoredPosition)
        {
            var slot = new GameObject(label, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.5f, 0f);
            slotRect.anchorMax = new Vector2(0.5f, 0f);
            slotRect.pivot = new Vector2(0.5f, 0f);
            slotRect.sizeDelta = new Vector2(380f, 72f);
            slotRect.anchoredPosition = anchoredPosition;

            var hotspot = new GameObject(
                "Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            hotspot.transform.SetParent(slot.transform, false);
            Stretch(hotspot.GetComponent<RectTransform>());
            Image hotspotImage = hotspot.GetComponent<Image>();
            hotspotImage.color = new Color(1f, 1f, 1f, 0f);
            hotspotImage.raycastTarget = true;

            Text text = CreateText("Label", hotspot.transform);
            Stretch(text.rectTransform);
            text.text = label;
            text.fontSize = 32;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            Button button = hotspot.GetComponent<Button>();
            button.targetGraphic = text;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonIdle;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonHover;
            colors.selectedColor = ButtonHover;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
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
            camera.backgroundColor = RoomColor;
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

        private static void PlaceCoverBackground(RectTransform rect, Sprite sprite)
        {
            const float ReferenceWidth = 1920f;
            const float ReferenceHeight = 1080f;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            if (sprite == null)
            {
                rect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
                return;
            }

            float spriteWidth = sprite.rect.width;
            float spriteHeight = sprite.rect.height;
            if (spriteWidth < 1f || spriteHeight < 1f)
            {
                rect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
                return;
            }

            float scale = Mathf.Max(ReferenceWidth / spriteWidth, ReferenceHeight / spriteHeight);
            rect.sizeDelta = new Vector2(spriteWidth * scale, spriteHeight * scale);
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
