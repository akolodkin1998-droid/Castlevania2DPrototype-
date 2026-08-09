using UnityEngine;
using UnityEngine.UI;

namespace Castlevania2D.UI
{
    internal static class UiFillImage
    {
        private static Sprite whiteSprite;

        public static void Configure(Image image, bool filled = true)
        {
            if (image == null)
            {
                return;
            }

            if (image.sprite == null)
            {
                image.sprite = GetWhiteSprite();
            }

            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            else
            {
                image.type = Image.Type.Simple;
            }
        }

        public static void SetFillAmount(Image image, float normalized)
        {
            if (image == null)
            {
                return;
            }

            Configure(image);
            image.fillAmount = Mathf.Clamp01(normalized);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            Texture2D texture = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            whiteSprite.name = "UiWhiteSprite";

            return whiteSprite;
        }
    }
}
