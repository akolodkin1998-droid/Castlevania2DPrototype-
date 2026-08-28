using Castlevania2D.Loot;
using Castlevania2D.Player;
using Castlevania2D.Vfx;
using UnityEngine;

namespace Castlevania2D.Combat
{
    /// <summary>
    /// Consumes a SporeBag from inventory, plays foot dust, and force-jumps.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SporeBagActivator2D : MonoBehaviour
    {
        private const string DustResourceFolder = "Vfx/SporeDust/SporeDust_";
        private const int DustFrameCount = 10;

        [SerializeField] [Min(1f)] private float dustFrameRate = 16f;
        [SerializeField] private Vector3 dustLocalOffset = new Vector3(0f, -0.05f, 0f);
        [SerializeField] private Vector3 dustScale = new Vector3(1.71875f, 1.71875f, 1f);
        [SerializeField] private int dustSortingOrder = 8;
        [SerializeField] [Min(0f)] private float jumpForceMultiplier = 1.5f;
        [SerializeField] [Min(0f)] private float useCooldown = 2f;

        private PlayerLootInventory inventory;
        private IForcedJump forcedJump;
        private Sprite[] dustFrames;
        private bool dustLoadAttempted;
        private float nextUseTime;

        private void Awake()
        {
            inventory = GetComponent<PlayerLootInventory>();
            forcedJump = GetComponent<IForcedJump>();
        }

        public bool TryActivate()
        {
            if (Time.time < nextUseTime)
            {
                return false;
            }

            if (inventory == null)
            {
                inventory = GetComponent<PlayerLootInventory>();
            }

            if (inventory == null || !inventory.TryRemove(LootItemId.SporeBag))
            {
                return false;
            }

            nextUseTime = Time.time + useCooldown;
            SpawnDust();
            if (forcedJump == null)
            {
                forcedJump = GetComponent<IForcedJump>();
            }

            forcedJump?.ForceJump(jumpForceMultiplier);
            return true;
        }

        private void SpawnDust()
        {
            EnsureDustFrames();
            if (dustFrames == null || dustFrames.Length == 0)
            {
                return;
            }

            var dustObject = new GameObject("SporeDust");
            Transform feetAnchor = ResolveFeetAnchor();
            dustObject.transform.SetParent(feetAnchor, false);
            dustObject.transform.localPosition = dustLocalOffset;
            dustObject.transform.localRotation = Quaternion.identity;
            dustObject.transform.localScale = dustScale;

            SpriteRenderer renderer = dustObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = dustSortingOrder;

            OneShotSpriteAnimation2D animation = dustObject.AddComponent<OneShotSpriteAnimation2D>();
            animation.Play(dustFrames, dustFrameRate);
        }

        private Transform ResolveFeetAnchor()
        {
            Transform groundSensor = transform.Find("GroundSensor");
            return groundSensor != null ? groundSensor : transform;
        }

        private void EnsureDustFrames()
        {
            if (dustLoadAttempted)
            {
                return;
            }

            dustLoadAttempted = true;
            var loaded = new Sprite[DustFrameCount];
            int count = 0;
            for (int i = 0; i < DustFrameCount; i++)
            {
                string path = DustResourceFolder + $"{i + 1:D2}";
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(path);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 1f),
                            100f);
                    }
                }

                if (sprite != null)
                {
                    loaded[count++] = sprite;
                }
            }

            if (count == 0)
            {
                return;
            }

            if (count == DustFrameCount)
            {
                dustFrames = loaded;
                return;
            }

            dustFrames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                dustFrames[i] = loaded[i];
            }
        }
    }
}
