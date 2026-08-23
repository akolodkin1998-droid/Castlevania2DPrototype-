using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Cached Resources sprites for mob loot pickups.
    /// </summary>
    public static class LootDropSprites
    {
        private const string CommonPath = "Items/Drop_Common";
        private const string EntPath = "Items/Drop_Ent";
        private const string PotionPath = "Items/Drop_Potion";
        private const string MaraTearPath = "Items/Drop_MaraTear";

        private static Sprite common;
        private static Sprite ent;
        private static Sprite potion;
        private static Sprite maraTear;
        private static bool loadAttempted;

        public static Sprite Common
        {
            get
            {
                EnsureLoaded();
                return common;
            }
        }

        public static Sprite Ent
        {
            get
            {
                EnsureLoaded();
                return ent;
            }
        }

        public static Sprite Potion
        {
            get
            {
                EnsureLoaded();
                return potion;
            }
        }

        public static Sprite MaraTear
        {
            get
            {
                EnsureLoaded();
                return maraTear;
            }
        }

        public static bool AreReady
        {
            get
            {
                EnsureLoaded();
                return common != null && ent != null && potion != null && maraTear != null;
            }
        }

        private static void EnsureLoaded()
        {
            if (loadAttempted)
            {
                return;
            }

            loadAttempted = true;
            common = Resources.Load<Sprite>(CommonPath);
            ent = Resources.Load<Sprite>(EntPath);
            potion = Resources.Load<Sprite>(PotionPath);
            maraTear = Resources.Load<Sprite>(MaraTearPath);

            if (common == null)
            {
                common = CreateSpriteFromTexture(CommonPath);
            }

            if (ent == null)
            {
                ent = CreateSpriteFromTexture(EntPath);
            }

            if (potion == null)
            {
                potion = CreateSpriteFromTexture(PotionPath);
            }

            if (maraTear == null)
            {
                maraTear = CreateSpriteFromTexture(MaraTearPath);
            }
        }

        private static Sprite CreateSpriteFromTexture(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
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
