using UnityEngine;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Plays staged basket fill/damage anims when hit by an eagle stone.
    /// Hit 1: 0002–0006, hit 2: 0007, hit 3: 0008, hit 4: 0009–0011, hit 5: 0012–0015.
    /// On lever second strike: fallFrames play once while the basket drops onto a portal.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StoneBasketHitStages2D : MonoBehaviour
    {
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] hit1Frames;
        [SerializeField] private Sprite[] hit2Frames;
        [SerializeField] private Sprite[] hit3Frames;
        [SerializeField] private Sprite[] hit4Frames;
        [SerializeField] private Sprite[] hit5Frames;
        [SerializeField] private Sprite[] fallFrames;
        [SerializeField] private float frameRate = 12f;
        [Tooltip("Basket is full after this many stone hits (default 5).")]
        [SerializeField] private int maxHits = 5;

        private SpriteRenderer spriteRenderer;
        private int hitCount;
        private bool animating;
        private bool fallLoopWhileCollapsing;
        private Sprite[] activeFrames;
        private int frameIndex;
        private float frameTimer;

        public int HitCount => hitCount;
        public int MaxHits => maxHits;
        /// <summary>True after exactly <see cref="maxHits"/> successful stone hits (default 5).</summary>
        public bool IsFull => hitCount >= maxHits;
        public bool HasFallen => hasFallen;

        public event System.Action Filled;

        private bool collapsing;
        private bool hasFallen;
        private Vector2 collapseTarget;
        private Rigidbody2D body;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            if (idleSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        /// <summary>Drop the basket onto a world point (portal) with gravity.</summary>
        public void BeginFallOnto(Vector2 worldTarget)
        {
            if (collapsing)
            {
                return;
            }

            collapsing = true;
            hasFallen = true;
            collapseTarget = worldTarget;
            BeginFallAnimation();

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 2.4f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void BeginFallAnimation()
        {
            Sprite[] frames = CompactFrames(fallFrames);
            if (frames.Length == 0)
            {
                Debug.LogWarning(
                    "[StoneBasketHitStages2D] Fall frames missing on " + name +
                    ". Re-run Tools → Castlevania 2D → Setup Stone Baskets.",
                    this);
                fallLoopWhileCollapsing = false;
                return;
            }

            // 9 frames @ 12 FPS ≈ 0.75s covers the short portal drop; if still airborne
            // after the last frame, hold it. If the clip is shorter than expected fall,
            // loop until land (only when fewer than ~half a second of frames).
            float animSeconds = frames.Length / Mathf.Max(1f, frameRate);
            fallLoopWhileCollapsing = animSeconds < 0.45f;

            activeFrames = frames;
            frameIndex = 0;
            frameTimer = 0f;
            animating = true;
            ApplyFrame(0);
        }

        private void FixedUpdate()
        {
            if (!collapsing || body == null)
            {
                return;
            }

            Vector2 pos = body.position;
            float dx = collapseTarget.x - pos.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                float vx = Mathf.Sign(dx) * Mathf.Min(6f, Mathf.Abs(dx) * 4f);
                body.linearVelocity = new Vector2(vx, body.linearVelocity.y);
            }

            // Settle near portal height — freeze once landed close enough.
            if (pos.y <= collapseTarget.y + 0.35f && Mathf.Abs(body.linearVelocity.y) < 0.4f && Mathf.Abs(dx) < 0.6f)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.position = new Vector2(collapseTarget.x, collapseTarget.y + 0.15f);
                collapsing = false;
                hasFallen = true;
                fallLoopWhileCollapsing = false;
                if (animating && activeFrames != null && activeFrames.Length > 0)
                {
                    ApplyFrame(activeFrames.Length - 1);
                    animating = false;
                }
            }
        }

        /// <summary>Called when an eagle stone reaches this basket.</summary>
        public void RegisterHit()
        {
            if (hitCount >= maxHits)
            {
                return;
            }

            hitCount++;
            Sprite[] stage = CompactFrames(GetStageFrames(hitCount));
            if (stage.Length == 0)
            {
                // Keep current visual if stage sprites failed to import.
                Debug.LogWarning(
                    "[StoneBasketHitStages2D] Hit stage " + hitCount +
                    " has no valid sprites on " + name + ". Re-run Tools → Setup Stone Baskets.",
                    this);
            }
            else
            {
                fallLoopWhileCollapsing = false;
                activeFrames = stage;
                frameIndex = 0;
                frameTimer = 0f;
                animating = true;
                ApplyFrame(0);
            }

            if (hitCount >= maxHits)
            {
                Filled?.Invoke();
            }
        }

        private void Update()
        {
            if (!animating || activeFrames == null || activeFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            if (activeFrames.Length == 1)
            {
                ApplyFrame(0);
                animating = false;
                return;
            }

            float duration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                if (frameIndex >= activeFrames.Length - 1)
                {
                    if (fallLoopWhileCollapsing && collapsing)
                    {
                        frameIndex = 0;
                        ApplyFrame(0);
                        continue;
                    }

                    ApplyFrame(activeFrames.Length - 1);
                    animating = false;
                    fallLoopWhileCollapsing = false;
                    return;
                }

                frameIndex++;
                ApplyFrame(frameIndex);
            }
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null || activeFrames == null)
            {
                return;
            }

            if (index < 0 || index >= activeFrames.Length || activeFrames[index] == null)
            {
                return;
            }

            spriteRenderer.sprite = activeFrames[index];
        }

        private static Sprite[] CompactFrames(Sprite[] source)
        {
            if (source == null || source.Length == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            if (count == source.Length)
            {
                return source;
            }

            var compact = new Sprite[count];
            int write = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    compact[write++] = source[i];
                }
            }

            return compact;
        }

        private Sprite[] GetStageFrames(int hit)
        {
            switch (hit)
            {
                case 1: return hit1Frames;
                case 2: return hit2Frames;
                case 3: return hit3Frames;
                case 4: return hit4Frames;
                case 5: return hit5Frames;
                default: return null;
            }
        }

        public void ApplySavedState(int savedHitCount, bool savedHasFallen, Vector2 savedPosition)
        {
            hitCount = Mathf.Clamp(savedHitCount, 0, maxHits);
            hasFallen = savedHasFallen;
            collapsing = false;
            animating = false;
            fallLoopWhileCollapsing = false;
            frameIndex = 0;
            frameTimer = 0f;
            transform.position = new Vector3(savedPosition.x, savedPosition.y, transform.position.z);

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.bodyType = savedHasFallen ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
            }

            Sprite[] frames = savedHasFallen
                ? CompactFrames(fallFrames)
                : CompactFrames(GetStageFrames(hitCount));

            if (hitCount == 0 && !savedHasFallen)
            {
                activeFrames = null;
                if (spriteRenderer != null && idleSprite != null)
                {
                    spriteRenderer.sprite = idleSprite;
                }

                return;
            }

            activeFrames = frames;
            if (frames.Length > 0)
            {
                ApplyFrame(frames.Length - 1);
            }
        }

#if UNITY_EDITOR
        public void EditorAssignStages(
            Sprite idle,
            Sprite[] stage1,
            Sprite[] stage2,
            Sprite[] stage3,
            Sprite[] stage4,
            Sprite[] stage5,
            Sprite[] fall = null)
        {
            idleSprite = idle;
            hit1Frames = stage1 ?? System.Array.Empty<Sprite>();
            hit2Frames = stage2 ?? System.Array.Empty<Sprite>();
            hit3Frames = stage3 ?? System.Array.Empty<Sprite>();
            hit4Frames = stage4 ?? System.Array.Empty<Sprite>();
            hit5Frames = stage5 ?? System.Array.Empty<Sprite>();
            if (fall != null)
            {
                fallFrames = fall;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (idleSprite != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
#endif
    }
}
