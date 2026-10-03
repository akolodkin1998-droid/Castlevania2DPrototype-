using Castlevania2D.Environment;
using Castlevania2D.Loot;
using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Crane hoist: idle frame 1. Frames 2-9 play after the crate lever is pulled.
    /// Key sits on the right roller and falls when hit by a stone.
    /// </summary>
        [DisallowMultipleComponent]
        [RequireComponent(typeof(SpriteRenderer))]
        public sealed class ElevatorMechanism2D : MonoBehaviour
        {
            private const string HoistResourcePrefix = "Environment/Crane/Hoist_";
            private const string KeyResourcePath = "Items/Drop_LikhoKey";
            private const string CraneKeyObjectName = "CraneKey";
            private const float CrateSpriteUvX = 95f / 256f;
            private const float CrateSpriteUvY = 1f - 168f / 256f;

        [Header("Frame Sprites (0 = idle, then hoist 2-9)")]
        [SerializeField] private Sprite[] frameSprites = new Sprite[9];

        [Header("Landing Animations (play once per hit, then hold)")]
        [SerializeField] private Sprite[] landing1AnimFrames;
        [SerializeField] private Sprite[] landing2AnimFrames;
        [SerializeField] private Sprite[] landing3AnimFrames;
        [SerializeField] private float landingAnimFrameRate = 12f;

        [Header("Detection")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";
        [SerializeField] private float playerPresenceGraceSeconds = 0.35f;

        [Header("Hoist (siblings under LiftMechanism)")]
        [SerializeField] private Transform ropeTransform;
        [SerializeField] private Transform assemblyReferenceTransform;
        [SerializeField] private Transform craneKeyTransform;
        [SerializeField] private float targetAssemblyWorldY = -37.2f;
        [SerializeField] private float descentStepWorldY = 2f;
        [SerializeField] private float hoistLiftDurationSeconds = 5f;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer ropeSpriteRenderer;
        private BoxCollider2D basketTrigger;
        private Transform playerRoot;
        private Rigidbody2D playerBody;
        private Collider2D playerCollider;
        private Collider2D assemblyCollider;
        private bool hoistCollisionIgnored;
        private int landingCount;
        private int playerOverlapCount;
        private float playerLastInsideTime = float.NegativeInfinity;
        private bool landingAnimPlaying;
        private int landingAnimIndex;
        private float landingAnimElapsed;
        private Sprite[] activeLandingAnimFrames;
        private bool hoistArmed;
        private bool hoistLifting;
        private float hoistLiftElapsed;
        private float hoistLiftDuration;
        private Vector3 hoistAssemblyStart;
        private Vector3 hoistAssemblyEnd;
        private Vector3 hoistRopeScaleStart;
        private Vector3 hoistRopeScaleEnd;
        private Vector3 hoistRopePosStart;
        private Vector3 hoistRopePosEnd;
        private float assemblyRestWorldX;

        public int LandingCount => landingCount;
        public int MaxLandings => 1;
        public float DescentStepWorldY => descentStepWorldY;
        public float AssemblyWorldY =>
            assemblyReferenceTransform != null ? assemblyReferenceTransform.position.y : 0f;
        public float RopeLocalScaleY => ropeTransform != null ? ropeTransform.localScale.y : 0f;
        public float RopeLocalPositionY => ropeTransform != null ? ropeTransform.localPosition.y : 0f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            targetAssemblyWorldY = -37.2f;
            RemoveBasketCollider();
            CachePlayerRoot();
            CacheDescentTransforms();
            ShowHoistParts();
            PinAssemblyToAuthoredHorizontal();
            LoadHoistSpritesIfNeeded();
            EnsureCraneGadgets();
            ApplyFrameSprite();
        }

        public void StartHoistFromLever()
        {
            if (hoistArmed || landingCount > 0)
            {
                return;
            }

            hoistArmed = true;
            landingCount = 1;
            BeginHoistLift();
            ApplyFrameSprite();
        }

        private void Update()
        {
            TickLandingAnimation();
        }

        private void FixedUpdate()
        {
            TickHoistLift();
        }

        private void RemoveBasketCollider()
        {
            BoxCollider2D[] boxes = GetComponents<BoxCollider2D>();
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].enabled = false;
            }

            basketTrigger = null;
        }

        private void CachePlayerRoot()
        {
            if (playerRoot != null)
            {
                CachePlayerPhysics();
                return;
            }

            GameObject playerObject = GameObject.Find(playerObjectName);
            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
                CachePlayerPhysics();
            }
        }

        private void CachePlayerPhysics()
        {
            if (playerRoot == null)
            {
                return;
            }

            if (playerBody == null)
            {
                playerBody = playerRoot.GetComponent<Rigidbody2D>();
            }

            if (playerCollider == null)
            {
                playerCollider = playerRoot.GetComponent<Collider2D>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (TryTrackPlayerEnter(other))
            {
                return;
            }

            TryRegisterStoneLanding(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsPlayerCollider(other))
            {
                playerLastInsideTime = Time.time;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
            if (playerOverlapCount == 0)
            {
                playerLastInsideTime = Time.time;
            }
        }

        private bool TryTrackPlayerEnter(Collider2D other)
        {
            if (!IsPlayerCollider(other))
            {
                return false;
            }

            playerOverlapCount++;
            playerLastInsideTime = Time.time;
            return true;
        }

        private void TryRegisterStoneLanding(Collider2D other)
        {
        }

        private bool IsPlayerPresentInBasket()
        {
            if (playerOverlapCount > 0)
            {
                return true;
            }

            return Time.time - playerLastInsideTime <= playerPresenceGraceSeconds;
        }

        private bool IsPlayerCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            Transform root = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;

            if (root.CompareTag("Player"))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(playerObjectName) &&
                root.name.Equals(playerObjectName, System.StringComparison.Ordinal))
            {
                return true;
            }

            return root.name.IndexOf("Hero", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || root.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CacheDescentTransforms()
        {
            Transform liftRoot = transform.parent;
            if (ropeTransform == null && liftRoot != null)
            {
                ropeTransform = liftRoot.Find("Rope");
            }

            if (assemblyReferenceTransform == null && liftRoot != null)
            {
                assemblyReferenceTransform = liftRoot.Find("AssemblyReference");
            }

            if (craneKeyTransform == null)
            {
                if (liftRoot != null)
                {
                    craneKeyTransform = liftRoot.Find(CraneKeyObjectName);
                }

                if (craneKeyTransform == null)
                {
                    craneKeyTransform = transform.Find(CraneKeyObjectName);
                }
            }

            if (ropeTransform != null)
            {
                ropeSpriteRenderer = ropeTransform.GetComponent<SpriteRenderer>();
            }
        }

        private void ApplyDescentStep()
        {
            if (assemblyReferenceTransform == null || descentStepWorldY <= 0f)
            {
                return;
            }

            float currentWorldY = assemblyReferenceTransform.position.y;
            float remainingWorldY = targetAssemblyWorldY - currentWorldY;
            if (Mathf.Approximately(remainingWorldY, 0f))
            {
                return;
            }

            float stepMagnitude = Mathf.Min(descentStepWorldY, Mathf.Abs(remainingWorldY));
            float stepDeltaY = Mathf.Sign(remainingWorldY) * stepMagnitude;

            Vector3 assemblyWorldPosition = assemblyReferenceTransform.position;
            assemblyWorldPosition.y += stepDeltaY;
            assemblyReferenceTransform.position = assemblyWorldPosition;

            ShortenRopeFromBottom(stepMagnitude);
        }

        /// <summary>
        /// Bottom-center pivot: reduce visible length from the bottom while the top attachment stays fixed.
        /// </summary>
        private void ShortenRopeFromBottom(float worldLengthReduction)
        {
            if (ropeTransform == null || ropeSpriteRenderer == null || ropeSpriteRenderer.sprite == null)
            {
                return;
            }

            if (worldLengthReduction <= 0f)
            {
                return;
            }

            float parentScaleY = ropeTransform.parent != null ? ropeTransform.parent.lossyScale.y : 1f;
            float spriteHeight = ropeSpriteRenderer.sprite.bounds.size.y;
            if (spriteHeight <= Mathf.Epsilon || parentScaleY <= Mathf.Epsilon)
            {
                return;
            }

            float deltaScaleY = worldLengthReduction / (spriteHeight * parentScaleY);
            Vector3 ropeScale = ropeTransform.localScale;
            float newScaleY = Mathf.Max(0.01f, ropeScale.y - deltaScaleY);
            float appliedDeltaScaleY = ropeScale.y - newScaleY;

            ropeTransform.localScale = new Vector3(ropeScale.x, newScaleY, ropeScale.z);

            Vector3 ropeLocalPosition = ropeTransform.localPosition;
            ropeLocalPosition.y += spriteHeight * appliedDeltaScaleY;
            ropeTransform.localPosition = ropeLocalPosition;
        }

        private void ApplyFrameSprite()
        {
            if (spriteRenderer == null || frameSprites == null || frameSprites.Length == 0)
            {
                return;
            }

            if (landingCount > 0 && TryStartLandingAnimation(landingCount))
            {
                return;
            }

            StopLandingAnimation();
            ApplyHoldSprite();
        }

        private Sprite[] GetLandingAnimFrames(int landing)
        {
            if (landing != 1 || frameSprites == null || frameSprites.Length < 2)
            {
                return null;
            }

            int count = frameSprites.Length - 1;
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = frameSprites[i + 1];
            }

            return frames;
        }

        private void LoadHoistSpritesIfNeeded()
        {
            var loaded = new Sprite[9];
            bool any = false;
            for (int i = 0; i < loaded.Length; i++)
            {
                loaded[i] = Resources.Load<Sprite>(HoistResourcePrefix + (i + 1).ToString("00"));
                if (loaded[i] != null)
                {
                    any = true;
                }
            }

            if (!any)
            {
                return;
            }

            frameSprites = loaded;
            if (spriteRenderer != null && spriteRenderer.sprite == null && loaded[0] != null)
            {
                spriteRenderer.sprite = loaded[0];
            }
            else if (spriteRenderer != null && loaded[0] != null && landingCount <= 0)
            {
                spriteRenderer.sprite = loaded[0];
            }
        }

        private void ShowHoistParts()
        {
            CacheDescentTransforms();
            if (ropeTransform != null)
            {
                ropeTransform.gameObject.SetActive(true);
            }

            if (assemblyReferenceTransform != null)
            {
                assemblyReferenceTransform.gameObject.SetActive(true);
            }
        }

        private void BeginHoistLift()
        {
            CacheDescentTransforms();
            ShowHoistParts();
            hoistLiftElapsed = 0f;
            hoistLiftDuration = hoistLiftDurationSeconds > 0.1f ? hoistLiftDurationSeconds : 5f;
            hoistLifting = true;
            SetHoistPassengerCollision(true);

            float liftDistance = 0f;
            if (assemblyReferenceTransform != null)
            {
                Vector3 start = assemblyReferenceTransform.position;
                start.x = assemblyRestWorldX != 0f ? assemblyRestWorldX : start.x;
                hoistAssemblyStart = start;
                hoistAssemblyEnd = new Vector3(start.x, targetAssemblyWorldY, start.z);
                assemblyReferenceTransform.position = start;
                liftDistance = hoistAssemblyEnd.y - hoistAssemblyStart.y;
            }

            if (ropeTransform == null || ropeSpriteRenderer == null || ropeSpriteRenderer.sprite == null)
            {
                return;
            }

            hoistRopeScaleStart = ropeTransform.localScale;
            hoistRopePosStart = ropeTransform.localPosition;
            hoistRopeScaleEnd = hoistRopeScaleStart;
            hoistRopePosEnd = hoistRopePosStart;

            float parentScaleY = ropeTransform.parent != null ? ropeTransform.parent.lossyScale.y : 1f;
            float spriteHeight = ropeSpriteRenderer.sprite.bounds.size.y;
            if (spriteHeight <= Mathf.Epsilon || parentScaleY <= Mathf.Epsilon)
            {
                return;
            }

            float deltaScaleY = liftDistance / (spriteHeight * parentScaleY);
            float newScaleY = Mathf.Max(0.01f, hoistRopeScaleStart.y - deltaScaleY);
            float appliedDeltaScaleY = hoistRopeScaleStart.y - newScaleY;
            hoistRopeScaleEnd = new Vector3(hoistRopeScaleStart.x, newScaleY, hoistRopeScaleStart.z);
            hoistRopePosEnd = hoistRopePosStart + new Vector3(0f, spriteHeight * appliedDeltaScaleY, 0f);
        }

        private void TickHoistLift()
        {
            if (!hoistLifting)
            {
                return;
            }

            hoistLiftElapsed += Time.deltaTime;
            float t = hoistLiftDuration > 0.001f ? hoistLiftElapsed / hoistLiftDuration : 1f;
            bool finished = t >= 1f;
            if (finished)
            {
                t = 1f;
                hoistLifting = false;
            }

            t = t * t * (3f - 2f * t);
            if (assemblyReferenceTransform != null)
            {
                Vector3 previous = assemblyReferenceTransform.position;
                Vector3 next = Vector3.Lerp(hoistAssemblyStart, hoistAssemblyEnd, t);
                next.x = hoistAssemblyStart.x;
                assemblyReferenceTransform.position = next;
                CarryPassengerWithLog(next.y - previous.y);
            }

            if (ropeTransform != null)
            {
                ropeTransform.localScale = Vector3.Lerp(hoistRopeScaleStart, hoistRopeScaleEnd, t);
                ropeTransform.localPosition = Vector3.Lerp(hoistRopePosStart, hoistRopePosEnd, t);
            }

            if (finished)
            {
                SetHoistPassengerCollision(false);
                SnapPassengerOntoLog();
                StopLandingAnimation();
                ApplyHoldSprite();
            }
        }

        private bool IsPassengerOnLog()
        {
            CachePlayerRoot();
            if (playerRoot == null || assemblyReferenceTransform == null)
            {
                return false;
            }

            if (assemblyCollider == null)
            {
                assemblyCollider = assemblyReferenceTransform.GetComponent<Collider2D>();
            }

            Bounds bounds = assemblyCollider != null
                ? assemblyCollider.bounds
                : new Bounds(assemblyReferenceTransform.position, new Vector3(4f, 1f, 1f));

            Vector3 playerPosition = playerRoot.position;
            const float padX = 0.75f;
            bool overLog = playerPosition.x >= bounds.min.x - padX && playerPosition.x <= bounds.max.x + padX;
            return overLog && playerPosition.y >= bounds.min.y - 1.4f && playerPosition.y <= bounds.max.y + 3.5f;
        }

        private void CarryPassengerWithLog(float deltaY)
        {
            if (Mathf.Abs(deltaY) <= 0.00001f || !IsPassengerOnLog())
            {
                return;
            }

            if (playerBody != null)
            {
                playerBody.position += new Vector2(0f, deltaY);
                Vector2 velocity = playerBody.linearVelocity;
                if (velocity.y < 0f)
                {
                    velocity.y = 0f;
                    playerBody.linearVelocity = velocity;
                }
            }
            else
            {
                playerRoot.position += new Vector3(0f, deltaY, 0f);
            }
        }

        private void SetHoistPassengerCollision(bool ignore)
        {
            CachePlayerRoot();
            if (assemblyReferenceTransform != null && assemblyCollider == null)
            {
                assemblyCollider = assemblyReferenceTransform.GetComponent<Collider2D>();
            }

            if (playerCollider == null || assemblyCollider == null || hoistCollisionIgnored == ignore)
            {
                return;
            }

            Physics2D.IgnoreCollision(playerCollider, assemblyCollider, ignore);
            hoistCollisionIgnored = ignore;
        }

        private void SnapPassengerOntoLog()
        {
            if (!IsPassengerOnLog() || assemblyCollider == null)
            {
                return;
            }

            float surfaceY = assemblyCollider.bounds.max.y;
            float feetY = playerCollider != null ? playerCollider.bounds.min.y : playerRoot.position.y;
            float lift = surfaceY - feetY + 0.02f;
            if (lift <= 0f)
            {
                return;
            }

            if (playerBody != null)
            {
                playerBody.position += new Vector2(0f, lift);
            }
            else
            {
                playerRoot.position += new Vector3(0f, lift, 0f);
            }
        }

        private void PinAssemblyToAuthoredHorizontal()
        {
            if (assemblyReferenceTransform == null)
            {
                return;
            }

            assemblyRestWorldX = assemblyReferenceTransform.position.x;
        }

        private void EnsureCraneGadgets()
        {
            CacheDescentTransforms();
            if (craneKeyTransform == null)
            {
                CreateAuthoredKeyPart();
            }
            else
            {
                WireKeyPerch(craneKeyTransform.gameObject);
            }

            Transform crate = transform.Find("CraneCrate");
            if (crate == null)
            {
                CreateCrateInspect();
                crate = transform.Find("CraneCrate");
            }

            if (crate != null)
            {
                GearBoxInspect2D strayLift = crate.GetComponent<GearBoxInspect2D>();
                if (strayLift != null)
                {
                    Destroy(strayLift);
                }
            }
        }

        private void CreateAuthoredKeyPart()
        {
            Transform liftRoot = transform.parent != null ? transform.parent : transform;
            var keyObject = new GameObject(CraneKeyObjectName);
            keyObject.transform.SetParent(liftRoot, false);
            keyObject.transform.localPosition = transform.localPosition + new Vector3(-3.2f, 4f, 0f);
            keyObject.transform.localRotation = Quaternion.identity;
            keyObject.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            craneKeyTransform = keyObject.transform;
            WireKeyPerch(keyObject);
        }

        private void WireKeyPerch(GameObject keyObject)
        {
            Sprite keySprite = LootDropSprites.LikhoKey;
            if (keySprite == null)
            {
                keySprite = Resources.Load<Sprite>(KeyResourcePath);
            }

            SpriteRenderer keyRenderer = keyObject.GetComponent<SpriteRenderer>();
            if (keyRenderer == null)
            {
                keyRenderer = keyObject.AddComponent<SpriteRenderer>();
            }

            if (keyRenderer.sprite == null)
            {
                keyRenderer.sprite = keySprite;
            }

            keyRenderer.color = Color.white;
            keyRenderer.sortingOrder = 30;
            if (spriteRenderer != null)
            {
                keyRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            }

            BoxCollider2D keyBox = keyObject.GetComponent<BoxCollider2D>();
            if (keyBox == null)
            {
                keyBox = keyObject.AddComponent<BoxCollider2D>();
                keyBox.size = new Vector2(1.2f, 1.2f);
            }

            keyBox.isTrigger = true;
            if (keyObject.GetComponent<CraneKeyPerch2D>() == null)
            {
                keyObject.AddComponent<CraneKeyPerch2D>();
            }
        }

        private void CreateCrateInspect()
        {
            var crateObject = new GameObject("CraneCrate", typeof(CraneCrateInspect2D));
            crateObject.transform.SetParent(transform, false);
            crateObject.transform.localPosition = SpriteUvToLocal(CrateSpriteUvX, CrateSpriteUvY);
            CraneCrateInspect2D inspect = crateObject.GetComponent<CraneCrateInspect2D>();
            inspect.BindHoist(this);
        }

        private Vector3 SpriteUvToLocal(float u, float v)
        {
            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            if (sprite == null)
            {
                return Vector3.zero;
            }

            Bounds bounds = sprite.bounds;
            return new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                Mathf.Lerp(bounds.min.y, bounds.max.y, v),
                0f);
        }

        private bool TryStartLandingAnimation(int landing)
        {
            Sprite[] frames = GetLandingAnimFrames(landing);
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            // Always restart so each hit plays even if the first frame matches the current hold.
            activeLandingAnimFrames = frames;
            landingAnimPlaying = true;
            landingAnimIndex = 0;
            landingAnimElapsed = 0f;
            Sprite first = frames[0];
            if (first != null)
            {
                spriteRenderer.sprite = first;
            }

            return true;
        }

        private void TickLandingAnimation()
        {
            if (!landingAnimPlaying || spriteRenderer == null)
            {
                return;
            }

            if (activeLandingAnimFrames == null || activeLandingAnimFrames.Length == 0)
            {
                StopLandingAnimation();
                ApplyHoldSprite();
                return;
            }

            float frameDuration = landingAnimFrameRate > 0f ? 1f / landingAnimFrameRate : 1f / 12f;
            landingAnimElapsed += Time.deltaTime;

            while (landingAnimElapsed >= frameDuration)
            {
                landingAnimElapsed -= frameDuration;
                landingAnimIndex++;

                if (landingAnimIndex >= activeLandingAnimFrames.Length)
                {
                    if (hoistLifting)
                    {
                        landingAnimIndex = 0;
                    }
                    else
                    {
                        StopLandingAnimation();
                        ApplyHoldSprite();
                        return;
                    }
                }

                Sprite frame = activeLandingAnimFrames[landingAnimIndex];
                if (frame != null)
                {
                    spriteRenderer.sprite = frame;
                }
            }
        }

        private void ApplyHoldSprite()
        {
            if (spriteRenderer == null || frameSprites == null || frameSprites.Length == 0)
            {
                return;
            }

            int spriteIndex = landingCount <= 0 ? 0 : frameSprites.Length - 1;
            Sprite hold = frameSprites[spriteIndex];
            if (hold == null && activeLandingAnimFrames != null && activeLandingAnimFrames.Length > 0)
            {
                hold = activeLandingAnimFrames[activeLandingAnimFrames.Length - 1];
            }

            if (hold != null)
            {
                spriteRenderer.sprite = hold;
            }
        }

        private void StopLandingAnimation()
        {
            landingAnimPlaying = false;
            landingAnimIndex = 0;
            landingAnimElapsed = 0f;
            activeLandingAnimFrames = null;
        }

        public void ConfigureFrames(Sprite initial, Sprite landing1, Sprite landing2, Sprite landing3)
        {
            frameSprites = new[] { initial, landing1, landing2, landing3 };
            landingCount = 0;
            StopLandingAnimation();
            ApplyFrameSprite();
        }

        public void ConfigureLandingAnimations(
            Sprite[] landing1Frames,
            Sprite[] landing2Frames,
            Sprite[] landing3Frames,
            float frameRate = 12f)
        {
            landing1AnimFrames = landing1Frames;
            landing2AnimFrames = landing2Frames;
            landing3AnimFrames = landing3Frames;
            landingAnimFrameRate = frameRate > 0f ? frameRate : 12f;
        }

        public void ResetLandings()
        {
            landingCount = 0;
            hoistArmed = false;
            StopLandingAnimation();
            ApplyFrameSprite();
        }

        public void ApplySavedState(
            int savedLandingCount,
            float savedAssemblyWorldY,
            float savedRopeLocalScaleY,
            float savedRopeLocalPositionY)
        {
            CacheDescentTransforms();
            landingCount = Mathf.Clamp(savedLandingCount, 0, MaxLandings);
            hoistArmed = landingCount > 0;
            StopLandingAnimation();

            if (assemblyReferenceTransform != null)
            {
                Vector3 position = assemblyReferenceTransform.position;
                position.y = savedAssemblyWorldY;
                assemblyReferenceTransform.position = position;
            }

            if (ropeTransform != null)
            {
                Vector3 scale = ropeTransform.localScale;
                scale.y = savedRopeLocalScaleY;
                ropeTransform.localScale = scale;

                Vector3 position = ropeTransform.localPosition;
                position.y = savedRopeLocalPositionY;
                ropeTransform.localPosition = position;
            }

            ApplyHoldSprite();
        }
    }
}
