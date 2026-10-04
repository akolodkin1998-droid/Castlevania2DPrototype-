using Castlevania2D.Input;
using Castlevania2D.Loot;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Combat
{
    /// <summary>
    /// Key 1 potion: two sheathe frames, then the heal clip, then reverse sheathe.
    /// </summary>
    public sealed class HeroPotionDrink2D : MonoBehaviour
    {
        private const string SheatheFolder = "Player/Ult/Sheathe";
        private const string HealFolder = "Player/Heal";
        private const int SheatheFrameCount = 2;

        [SerializeField] [Min(1f)] private float characterFrameRate = 14f;
        [SerializeField] [Min(0.1f)] private float poseScale = 0.7f;
        [SerializeField] private float feetSink = 0.02f;

        private IHeroSwordUltHost host;
        private PlayerQuickAccessInventory inventory;
        private Sprite[] sheatheFrames;
        private Sprite[] healFrames;
        private SpriteRenderer body;
        private SpriteRenderer pose;
        private bool bodyWasEnabled;
        private Animator animator;
        private bool animatorWasEnabled;
        private bool drinking;
        private bool healApplied;
        private int phase;
        private int frameIndex;
        private float frameTimer;
        private int facing;

        public bool IsDrinking => drinking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject player = GameObject.Find("Player_HeroKnight");
            if (player == null || player.GetComponent<HeroPotionDrink2D>() != null)
            {
                return;
            }

            player.AddComponent<HeroPotionDrink2D>();
        }

        private void Awake()
        {
            host = GetComponent<IHeroSwordUltHost>();
            inventory = GetComponent<PlayerQuickAccessInventory>();
            body = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            poseScale = 0.7f;
            feetSink = 0.02f;
        }

        public bool TryBegin()
        {
            if (drinking || host == null || !host.CanBeginPotion())
            {
                return false;
            }

            if (GameplayInputLock.IsLocked || DialogueBoxUI.IsOpen)
            {
                return false;
            }

            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            if (session != null && !session.CanInteract)
            {
                return false;
            }

            if (inventory == null)
            {
                inventory = GetComponent<PlayerQuickAccessInventory>();
            }

            if (inventory == null || !inventory.CanUseHealingPotion())
            {
                return false;
            }

            EnsureFrames();
            if (sheatheFrames.Length < SheatheFrameCount || healFrames.Length == 0)
            {
                return false;
            }

            if (!inventory.TryConsumeHealingPotion())
            {
                return false;
            }

            facing = host.FacingDirection >= 0 ? 1 : -1;
            body = host.BodyRenderer != null ? host.BodyRenderer : body;
            animator = host.BodyAnimator != null ? host.BodyAnimator : animator;
            if (animator != null)
            {
                animatorWasEnabled = animator.enabled;
                animator.enabled = false;
            }

            ShowPose();
            phase = 0;
            frameIndex = 0;
            frameTimer = 0f;
            healApplied = false;
            drinking = true;
            host.NotifyPotionStarted();
            ApplyCharacterSprite(sheatheFrames[0]);
            return true;
        }

        private void Update()
        {
            if (!drinking)
            {
                return;
            }

            TickDrink();
        }

        private void TickDrink()
        {
            if (host != null && host.BodyRenderer != null)
            {
                body = host.BodyRenderer;
            }

            Rigidbody2D body2d = GetComponent<Rigidbody2D>();
            if (body2d != null)
            {
                body2d.linearVelocity = Vector2.zero;
            }

            AlignPose();

            frameTimer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, characterFrameRate);
            if (frameTimer < step)
            {
                return;
            }

            frameTimer -= step;

            if (phase == 0)
            {
                frameIndex++;
                if (frameIndex >= SheatheFrameCount)
                {
                    phase = 1;
                    frameIndex = 0;
                    ApplyHeal();
                    ApplyCharacterSprite(healFrames[0]);
                    return;
                }

                ApplyCharacterSprite(sheatheFrames[frameIndex]);
                return;
            }

            if (phase == 1)
            {
                frameIndex++;
                if (frameIndex >= healFrames.Length)
                {
                    phase = 2;
                    frameIndex = SheatheFrameCount - 1;
                    ApplyCharacterSprite(sheatheFrames[frameIndex]);
                    return;
                }

                ApplyCharacterSprite(healFrames[frameIndex]);
                return;
            }

            frameIndex--;
            if (frameIndex < 0)
            {
                Finish();
                return;
            }

            ApplyCharacterSprite(sheatheFrames[frameIndex]);
        }

        private void ApplyHeal()
        {
            if (healApplied)
            {
                return;
            }

            healApplied = true;
            if (inventory == null)
            {
                inventory = GetComponent<PlayerQuickAccessInventory>();
            }

            inventory?.ApplyHealingPotionRestore();
        }

        private void Finish()
        {
            drinking = false;
            HidePose();
            if (animator != null)
            {
                animator.enabled = animatorWasEnabled;
            }

            if (!healApplied)
            {
                ApplyHeal();
            }

            host?.NotifyPotionFinished();
        }

        private void ApplyCharacterSprite(Sprite sprite)
        {
            SpriteRenderer target = pose != null ? pose : body;
            if (target != null && sprite != null)
            {
                target.sprite = sprite;
                target.flipX = facing < 0;
            }

            AlignPose();
        }

        private void ShowPose()
        {
            if (pose == null)
            {
                var poseObject = new GameObject("PotionPose");
                poseObject.transform.SetParent(transform, false);
                pose = poseObject.AddComponent<SpriteRenderer>();
            }

            float scale = poseScale > 0.1f ? poseScale : 1f;
            pose.transform.localScale = new Vector3(scale, scale, 1f);
            if (body != null)
            {
                bodyWasEnabled = body.enabled;
                body.enabled = false;
                pose.sortingLayerID = body.sortingLayerID;
                pose.sortingOrder = body.sortingOrder;
            }

            pose.enabled = true;
            AlignPose();
        }

        private void AlignPose()
        {
            if (pose == null)
            {
                return;
            }

            float scale = poseScale > 0.1f ? poseScale : 1f;
            pose.flipX = facing < 0;
            pose.transform.localScale = new Vector3(scale, scale, 1f);
            pose.transform.localPosition = new Vector3(0f, -feetSink, 0f);
        }

        private void HidePose()
        {
            if (pose != null)
            {
                pose.enabled = false;
                pose.sprite = null;
            }

            if (body != null)
            {
                body.enabled = bodyWasEnabled;
            }
        }

        private void EnsureFrames()
        {
            if (sheatheFrames != null && sheatheFrames.Length >= SheatheFrameCount
                && healFrames != null && healFrames.Length > 0)
            {
                return;
            }

            sheatheFrames = LoadSorted(SheatheFolder);
            healFrames = LoadSorted(HealFolder);
        }

        private static Sprite[] LoadSorted(string folder)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(folder);
            if (sprites == null || sprites.Length == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
    }
}
