using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    public sealed class ScreenFadeOverlay : MonoBehaviour
    {
        private CanvasGroup canvasGroup;

        public static ScreenFadeOverlay Create(Transform parent)
        {
            var root = new GameObject(
                "ScreenFadeOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(ScreenFadeOverlay));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imageObject = new GameObject(
                "Black",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            imageObject.transform.SetParent(root.transform, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = Color.black;

            ScreenFadeOverlay overlay = root.GetComponent<ScreenFadeOverlay>();
            overlay.canvasGroup = imageObject.GetComponent<CanvasGroup>();
            overlay.canvasGroup.alpha = 0f;
            overlay.canvasGroup.blocksRaycasts = false;
            overlay.canvasGroup.interactable = false;
            return overlay;
        }

        public IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            canvasGroup.blocksRaycasts = true;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        }
    }
}
