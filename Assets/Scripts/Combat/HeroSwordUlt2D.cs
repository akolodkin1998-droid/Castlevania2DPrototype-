using Castlevania2D.Enemies;
using Castlevania2D.Input;
using Castlevania2D.Save;
using Castlevania2D.UI;
using Castlevania2D.Vfx;
using UnityEngine;

namespace Castlevania2D.Combat
{
    /// <summary>
    /// Q ult: sheathe shield, plant the sword, then reverse both clips.
    /// Up to five fire pillars spawn on ground in front; walls and pits cut the line.
    /// </summary>
    public sealed class HeroSwordUlt2D : MonoBehaviour
    {
        private const string SheatheStripPath = "Player/Ult/HeroKnight_sheathe";
        private const string UltStripPath = "Player/Ult/HeroKnight_ult";
        private const string PillarStripPath = "Player/Ult/HeroKnight_pillar";
        private const string SheatheFolder = "Player/Ult/Sheathe";
        private const string UltFolder = "Player/Ult/Cast";
        private const string PillarFolder = "Player/Ult/Pillar";
        private const int MaxPillars = 5;
        private const int UltReverseStartFrame = 10;

        [SerializeField] [Min(1f)] private float characterFrameRate = 12f;
        [SerializeField] [Min(0.1f)] private float ultPoseScale = 0.7f;
        [SerializeField] [Min(0.4f)] private float pillarSpacing = 1.7f;
        [SerializeField] [Min(0.2f)] private float wallPad = 0.28f;
        [SerializeField] [Min(0.5f)] private float groundProbeHeight = 1.15f;
        [SerializeField] [Min(0.5f)] private float groundRay = 2.4f;
        [SerializeField] [Min(0.1f)] private float maxGroundDrop = 0.7f;
        [SerializeField] private float pillarSink = 0.38f;
        [SerializeField] private float ultFeetSink = 0.02f;

        private IHeroSwordUltHost host;
        private Sprite[] sheatheFrames;
        private Sprite[] ultFrames;
        private Sprite[] pillarFrames;
        private SpriteRenderer body;
        private SpriteRenderer ultPose;
        private bool bodyWasEnabled;
        private Animator animator;
        private bool animatorWasEnabled;
        private bool casting;
        private int phase;
        private int frameIndex;
        private float frameTimer;
        private int facing;
        private int plannedCount;
        private readonly Vector2[] pillarSlots = new Vector2[MaxPillars];
        private readonly bool[] pillarSpawned = new bool[MaxPillars];
        private HeroUltPillar2D firstPillar;
        private bool fifthFromReverse;

        public bool IsCasting => casting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject player = GameObject.Find("Player_HeroKnight");
            if (player == null || player.GetComponent<HeroSwordUlt2D>() != null)
            {
                return;
            }

            player.AddComponent<HeroSwordUlt2D>();
        }

        private void Awake()
        {
            host = GetComponent<IHeroSwordUltHost>();
            body = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            ultPoseScale = 0.7f;
            pillarSink = 0.38f;
            ultFeetSink = 0.02f;
        }

        private void Update()
        {
            if (casting)
            {
                TickCast();
                return;
            }

            if (GameplayInputLock.IsLocked || DialogueBoxUI.IsOpen)
            {
                return;
            }

            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            if (session != null && !session.CanInteract)
            {
                return;
            }

            if (!UnityEngine.Input.GetKeyDown(KeyCode.Q))
            {
                return;
            }

            TryBegin();
        }

        private void TryBegin()
        {
            if (host == null || !host.CanBeginUlt())
            {
                return;
            }

            EnsureFrames();
            if (sheatheFrames.Length == 0 || ultFrames.Length == 0 || pillarFrames.Length == 0)
            {
                return;
            }

            facing = host.FacingDirection >= 0 ? 1 : -1;
            plannedCount = PlanPillars(host.FeetPosition, facing);
            if (plannedCount <= 0)
            {
                return;
            }

            body = host.BodyRenderer != null ? host.BodyRenderer : body;
            animator = host.BodyAnimator != null ? host.BodyAnimator : animator;
            if (animator != null)
            {
                animatorWasEnabled = animator.enabled;
                animator.enabled = false;
            }

            ShowUltPose();

            for (int i = 0; i < MaxPillars; i++)
            {
                pillarSpawned[i] = false;
            }

            firstPillar = null;
            fifthFromReverse = false;
            phase = 0;
            frameIndex = 0;
            frameTimer = 0f;
            casting = true;
            host.NotifyUltStarted();
            ApplyCharacterSprite(sheatheFrames[0]);
        }

        private void TickCast()
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

            AlignUltPose();

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
                if (frameIndex >= sheatheFrames.Length)
                {
                    phase = 1;
                    frameIndex = 0;
                    ApplyCharacterSprite(ultFrames[0]);
                    return;
                }

                ApplyCharacterSprite(sheatheFrames[frameIndex]);
                return;
            }

            if (phase == 1)
            {
                frameIndex++;
                if (frameIndex >= ultFrames.Length)
                {
                    SpawnPillar(0);
                    StartUltReverseFromFrame10();
                    return;
                }

                ApplyCharacterSprite(ultFrames[frameIndex]);
                return;
            }

            if (phase == 2)
            {
                frameIndex--;
                if (frameIndex < 0)
                {
                    StartSheatheReverse();
                    return;
                }

                ApplyCharacterSprite(ultFrames[frameIndex]);
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

        private void StartUltReverseFromFrame10()
        {
            phase = 2;
            int startIndex = Mathf.Clamp(UltReverseStartFrame - 1, 0, ultFrames.Length - 1);
            frameIndex = startIndex;
            ApplyCharacterSprite(ultFrames[frameIndex]);
        }

        private void StartSheatheReverse()
        {
            phase = 3;
            frameIndex = Mathf.Max(-1, sheatheFrames.Length - 2);
            if (frameIndex < 0)
            {
                Finish();
                return;
            }

            ApplyCharacterSprite(sheatheFrames[frameIndex]);
        }

        private void Finish()
        {
            casting = false;
            HideUltPose();
            if (animator != null)
            {
                animator.enabled = animatorWasEnabled;
            }

            host?.NotifyUltFinished();
        }

        private void ApplyCharacterSprite(Sprite sprite)
        {
            SpriteRenderer target = ultPose != null ? ultPose : body;
            if (target != null && sprite != null)
            {
                target.sprite = sprite;
                target.flipX = facing < 0;
            }

            AlignUltPose();
        }

        private void ShowUltPose()
        {
            if (ultPose == null)
            {
                var poseObject = new GameObject("UltPose");
                poseObject.transform.SetParent(transform, false);
                ultPose = poseObject.AddComponent<SpriteRenderer>();
            }

            float scale = ultPoseScale > 0.1f ? ultPoseScale : 1f;
            ultPose.transform.localScale = new Vector3(scale, scale, 1f);
            if (body != null)
            {
                bodyWasEnabled = body.enabled;
                body.enabled = false;
                ultPose.sortingLayerID = body.sortingLayerID;
                ultPose.sortingOrder = body.sortingOrder;
            }

            ultPose.enabled = true;
            AlignUltPose();
        }

        private void AlignUltPose()
        {
            if (ultPose == null)
            {
                return;
            }

            float scale = ultPoseScale > 0.1f ? ultPoseScale : 1f;
            ultPose.flipX = facing < 0;
            ultPose.transform.localScale = new Vector3(scale, scale, 1f);
            ultPose.transform.localPosition = new Vector3(0f, -ultFeetSink, 0f);
        }

        private void HideUltPose()
        {
            if (ultPose != null)
            {
                ultPose.enabled = false;
                ultPose.sprite = null;
            }

            if (body != null)
            {
                body.enabled = bodyWasEnabled;
            }
        }

        private void SpawnPillar(int slot)
        {
            if (slot < 0 || slot >= plannedCount || pillarSpawned[slot] || pillarFrames.Length == 0)
            {
                return;
            }

            pillarSpawned[slot] = true;
            int order = 50;
            HeroUltPillar2D spawned = HeroUltPillar2D.Spawn(
                pillarSlots[slot],
                pillarFrames,
                gameObject,
                host != null ? Mathf.Max(1, host.AttackDamage * 5) : 125,
                facing,
                order,
                OnFirstPillarFrame);
            if (slot == 0)
            {
                firstPillar = spawned;
            }
        }

        private void OnFirstPillarFrame(HeroUltPillar2D pillar, int displayFrame, bool reversing)
        {
            if (pillar != firstPillar)
            {
                return;
            }

            if (!reversing)
            {
                if (displayFrame >= 3)
                {
                    SpawnPillar(1);
                }

                if (displayFrame >= 6)
                {
                    SpawnPillar(2);
                }

                if (displayFrame >= 8)
                {
                    SpawnPillar(3);
                }

                return;
            }

            if (displayFrame <= 6 && !fifthFromReverse)
            {
                fifthFromReverse = true;
                SpawnPillar(4);
            }
        }

        private int PlanPillars(Vector2 feet, int dir)
        {
            if (HasWall(feet, dir, wallPad))
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < MaxPillars; i++)
            {
                float distance = pillarSpacing * (i + 1);
                if (HasWall(feet, dir, distance - 0.12f))
                {
                    break;
                }

                Vector2 probe = new Vector2(feet.x + dir * distance, feet.y + groundProbeHeight);
                if (!TryFindGround(probe, out float groundY))
                {
                    break;
                }

                if (feet.y - groundY > maxGroundDrop)
                {
                    break;
                }

                pillarSlots[count] = new Vector2(probe.x, groundY - pillarSink);
                count++;
            }

            return count;
        }

        private static bool HasWall(Vector2 feet, int dir, float distance)
        {
            if (distance <= 0.01f)
            {
                return false;
            }

            Vector2 origin = feet + new Vector2(0f, 0.7f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(dir, 0f), distance);
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsBlockingWorld(hits[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindGround(Vector2 from, out float groundY)
        {
            groundY = from.y;
            RaycastHit2D[] hits = Physics2D.RaycastAll(from, Vector2.down, 2.4f);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (!IsBlockingWorld(hit))
                {
                    continue;
                }

                if (hit.distance < best)
                {
                    best = hit.distance;
                    groundY = hit.point.y;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsBlockingWorld(RaycastHit2D hit)
        {
            Collider2D collider = hit.collider;
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                return false;
            }

            if (collider.GetComponentInParent<HeroUltPillar2D>() != null)
            {
                return false;
            }

            string rootName = collider.transform.root.name;
            if (rootName.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0
                || rootName.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (EnemyCollisionPassThrough2D.IsEnemyBody(collider))
            {
                return false;
            }

            return hit.point != Vector2.zero || hit.distance >= 0f;
        }

        private void EnsureFrames()
        {
            if (sheatheFrames != null && sheatheFrames.Length > 0
                && ultFrames != null && ultFrames.Length > 0
                && pillarFrames != null && pillarFrames.Length > 0)
            {
                return;
            }

            sheatheFrames = LoadFolderThenStrip(SheatheFolder, SheatheStripPath);
            ultFrames = LoadFolderThenStrip(UltFolder, UltStripPath);
            pillarFrames = LoadFolderThenStrip(PillarFolder, PillarStripPath);
        }

        private static Sprite[] LoadFolderThenStrip(string folder, string stripPath)
        {
            Sprite[] folderSprites = Resources.LoadAll<Sprite>(folder);
            if (folderSprites != null && folderSprites.Length > 1)
            {
                System.Array.Sort(folderSprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                return folderSprites;
            }

            Sprite[] strip = Resources.LoadAll<Sprite>(stripPath);
            if (strip == null)
            {
                return System.Array.Empty<Sprite>();
            }

            System.Array.Sort(strip, (a, b) => string.CompareOrdinal(a.name, b.name));
            return strip;
        }
    }
}
