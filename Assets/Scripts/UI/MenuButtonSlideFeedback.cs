using UnityEngine;
using UnityEngine.EventSystems;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuButtonSlideFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform visual;
        [SerializeField] private float slideDistance = 18f;
        [SerializeField] private float slideSpeed = 240f;

        private RectTransform hitArea;
        private Vector2 visualRestPosition;
        private Vector2 hitAreaRestPosition;
        private bool isHovered;
        private bool isSelected;

        private void Awake()
        {
            hitArea = GetComponent<RectTransform>();
            CacheRestPositions();
        }

        private void OnEnable()
        {
            isHovered = false;
            ApplyPosition(immediate: true);
        }

        private void Update()
        {
            ApplyPosition(immediate: false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
        }

        public void SetVisible(bool visible)
        {
            if (visual != null)
            {
                visual.gameObject.SetActive(visible);
            }

            gameObject.SetActive(visible);
        }

        private void CacheRestPositions()
        {
            if (hitArea != null)
            {
                hitAreaRestPosition = hitArea.anchoredPosition;
            }

            if (visual != null)
            {
                visualRestPosition = visual.anchoredPosition;
            }
        }

        private void ApplyPosition(bool immediate)
        {
            if (hitArea == null)
            {
                return;
            }

            float offset = isHovered || isSelected ? slideDistance : 0f;
            Vector2 hitAreaTarget = hitAreaRestPosition + new Vector2(offset, 0f);
            Vector2 visualTarget = visualRestPosition + new Vector2(offset, 0f);

            if (immediate)
            {
                hitArea.anchoredPosition = hitAreaTarget;
                if (visual != null)
                {
                    visual.anchoredPosition = visualTarget;
                }

                return;
            }

            float maxDistance = Mathf.Max(1f, slideSpeed) * Time.unscaledDeltaTime;
            hitArea.anchoredPosition = Vector2.MoveTowards(
                hitArea.anchoredPosition,
                hitAreaTarget,
                maxDistance);

            if (visual != null)
            {
                visual.anchoredPosition = Vector2.MoveTowards(
                    visual.anchoredPosition,
                    visualTarget,
                    maxDistance);
            }
        }

        public void AssignVisual(RectTransform newVisual)
        {
            visual = newVisual;
            CacheRestPositions();
        }

#if UNITY_EDITOR
        public void EditorAssignVisual(RectTransform newVisual)
        {
            AssignVisual(newVisual);
        }
#endif
    }
}
