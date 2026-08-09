using UnityEngine;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Keeps HUD canvas valid (scale, stretch). Bar position/size come from the scene — not overridden at runtime.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HudGameViewportFit : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private RectTransform canvasRect;
        private Canvas canvas;

        private void Awake()
        {
            CacheReferences();
            EnsureCanvasRect();
        }

        private void OnEnable()
        {
            EnsureCanvasRect();
        }

        /// <summary>Only fixes canvas rect — never moves or resizes HealthBar / BlockDurabilityBar.</summary>
        public void ApplyLayout()
        {
            CacheReferences();
            EnsureCanvasRect();
        }

        private void EnsureCanvasRect()
        {
            if (canvasRect == null)
            {
                return;
            }

            if (canvasRect.localScale.sqrMagnitude < 0.001f)
            {
                canvasRect.localScale = Vector3.one;
            }

            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
        }

        private void CacheReferences()
        {
            if (canvasRect == null)
            {
                canvasRect = GetComponent<RectTransform>();
            }

            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }
        }

        public Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            if (canvas != null && canvas.worldCamera != null)
            {
                targetCamera = canvas.worldCamera;
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
        }
    }
}
