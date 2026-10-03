using UnityEngine;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    public sealed class DialogueBoxPlacement2D : MonoBehaviour
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float SpriteWidth = 688f;
        public const float SpriteHeight = 384f;

        public static DialogueBoxPlacement2D Current { get; private set; }

        [Header("Область на экране 0–1")]
        [SerializeField] [Range(0f, 1f)] [Tooltip("Левый край области")] private float left = 0f;
        [SerializeField] [Range(0f, 1f)] [Tooltip("Ширина области")] private float width = 1f;
        [SerializeField] [Range(0f, 1f)] [Tooltip("Нижний край области")] private float bottom = 0f;
        [SerializeField] [Range(0.02f, 1f)] [Tooltip("Высота области")] private float height = 1f;

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Apply(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            FitSprite(panel, left, width, bottom, height);
        }

        public static void FitSprite(
            RectTransform panel,
            float left,
            float width,
            float bottom,
            float height)
        {
            if (panel == null)
            {
                return;
            }

            float x0 = Mathf.Clamp01(left);
            float x1 = Mathf.Clamp01(left + width);
            float y0 = Mathf.Clamp01(bottom);
            float y1 = Mathf.Clamp01(bottom + height);
            if (x1 < x0)
            {
                float swap = x0;
                x0 = x1;
                x1 = swap;
            }

            if (y1 < y0)
            {
                float swap = y0;
                y0 = y1;
                y1 = swap;
            }

            float viewW = Mathf.Max(1f, (x1 - x0) * ReferenceWidth);
            float viewH = Mathf.Max(1f, (y1 - y0) * ReferenceHeight);
            float scale = Mathf.Min(viewW / SpriteWidth, viewH / SpriteHeight);
            float panelW = SpriteWidth * scale;
            float panelH = SpriteHeight * scale;

            panel.anchorMin = new Vector2((x0 + x1) * 0.5f, y0);
            panel.anchorMax = new Vector2((x0 + x1) * 0.5f, y0);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(panelW, panelH);
            panel.anchoredPosition = Vector2.zero;
        }
    }
}
