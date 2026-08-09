using Castlevania2D.Combat;
using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Lift basket progression driven by shield-ricocheted stones landing while the player is in the basket zone.
    /// Frame sequence: initial → landing 1 → landing 2 → landing 3.
    /// Basket zone = BoxCollider2D Offset/Size on this object — edit them freely in the Inspector / Scene gizmo.
    /// Optional per-landing anim frames play once on that hit, then hold frameSprites[landingCount].
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ElevatorMechanism2D : MonoBehaviour
    {
        [Header("Frame Sprites (0 = initial, then landings 1-3 hold)")]
        [SerializeField] private Sprite[] frameSprites = new Sprite[4];

        [Header("Landing Animations (play once per hit, then hold)")]
        [SerializeField] private Sprite[] landing1AnimFrames;
        [SerializeField] private Sprite[] landing2AnimFrames;
        [SerializeField] private Sprite[] landing3AnimFrames;
        [SerializeField] private float landingAnimFrameRate = 12f;

        [Header("Detection")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";
        [SerializeField] private float playerPresenceGraceSeconds = 0.35f;

        [Header("Descent (siblings under LiftMechanism)")]
        [SerializeField] private Transform ropeTransform;
        [SerializeField] private Transform assemblyReferenceTransform;
        [SerializeField] private float targetAssemblyWorldY = -38.5f;
        [SerializeField] private float descentStepWorldY = 2f;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer ropeSpriteRenderer;
        private BoxCollider2D basketTrigger;
        private Transform playerRoot;
        private int landingCount;
        private int playerOverlapCount;
        private float playerLastInsideTime = float.NegativeInfinity;
        private bool landingAnimPlaying;
        private int landingAnimIndex;
        private float landingAnimElapsed;
        private Sprite[] activeLandingAnimFrames;

        public int LandingCount => landingCount;
        public int MaxLandings => frameSprites != null ? frameSprites.Length - 1 : 0;
        public float DescentStepWorldY => descentStepWorldY;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            basketTrigger = GetComponent<BoxCollider2D>();
            EnsureBasketTriggerIsTriggerOnly();
            CachePlayerRoot();
            CacheDescentTransforms();
            ApplyFrameSprite();
        }

        private void Update()
        {
            TickLandingAnimation();
        }

        /// <summary>
        /// Keeps collider as trigger, but never overwrites Offset/Size — those are authored in the Inspector.
        /// </summary>
        private void EnsureBasketTriggerIsTriggerOnly()
        {
            if (basketTrigger == null)
            {
                return;
            }

            basketTrigger.isTrigger = true;
        }

        private void CachePlayerRoot()
        {
            if (playerRoot != null)
            {
                return;
            }

            GameObject playerObject = GameObject.Find(playerObjectName);
            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
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
            if (!IsPlayerPresentInBasket())
            {
                return;
            }

            EnemyProjectile2D projectile = other.GetComponent<EnemyProjectile2D>()
                ?? other.GetComponentInParent<EnemyProjectile2D>();
            if (projectile == null || !projectile.IsPlayerReflected)
            {
                return;
            }

            CachePlayerRoot();
            if (playerRoot != null && !projectile.WasReflectedBy(playerRoot.gameObject))
            {
                return;
            }

            if (landingCount >= MaxLandings)
            {
                return;
            }

            landingCount++;
            ApplyFrameSprite();
            ApplyDescentStep();
            Destroy(projectile.gameObject);
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
            switch (landing)
            {
                case 1:
                    return landing1AnimFrames;
                case 2:
                    return landing2AnimFrames;
                case 3:
                    return landing3AnimFrames;
                default:
                    return null;
            }
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
                    StopLandingAnimation();
                    ApplyHoldSprite();
                    return;
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

            int spriteIndex = Mathf.Clamp(landingCount, 0, frameSprites.Length - 1);
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
            StopLandingAnimation();
            ApplyFrameSprite();
        }
    }
}
