using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DialogueChoiceLine : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Text label;
        [SerializeField] private Color idleColor = new Color(0.28f, 0.14f, 0.07f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.52f, 0.28f, 0.10f, 1f);

        private int index;
        private DialogueBoxUI owner;
        private bool hovered;
        private bool selected;

        public void Bind(DialogueBoxUI box, int choiceIndex, Text text)
        {
            owner = box;
            index = choiceIndex;
            label = text;
            ApplyColor();
        }

        public void SetText(string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        public void SetSelected(bool isSelected)
        {
            selected = isSelected;
            ApplyColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            owner?.HoverChoice(index);
            ApplyColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            ApplyColor();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.ConfirmChoice(index);
        }

        private void ApplyColor()
        {
            if (label == null)
            {
                return;
            }

            label.color = hovered || selected ? hoverColor : idleColor;
        }
    }
}
