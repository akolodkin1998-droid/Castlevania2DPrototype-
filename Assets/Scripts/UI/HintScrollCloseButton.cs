using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    /// <summary>
    /// Close control on the hint parchment. Darkens on hover, closes on click.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class HintScrollCloseButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private static readonly Color IdleColor = Color.white;
        private static readonly Color HoverColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [SerializeField] private Image targetImage;

        private Action onClicked;

        public void Bind(Action clicked, Image image)
        {
            onClicked = clicked;
            targetImage = image;
            ApplyColor(hovered: false);
        }

        public void ResetVisual()
        {
            ApplyColor(hovered: false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyColor(hovered: true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyColor(hovered: false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            onClicked?.Invoke();
        }

        private void ApplyColor(bool hovered)
        {
            if (targetImage == null)
            {
                return;
            }

            targetImage.color = hovered ? HoverColor : IdleColor;
        }
    }
}
