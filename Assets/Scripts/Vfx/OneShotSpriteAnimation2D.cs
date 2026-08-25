using UnityEngine;

namespace Castlevania2D.Vfx
{
    /// <summary>
    /// One-shot world sprite animation that destroys itself when finished.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class OneShotSpriteAnimation2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] [Min(1f)] private float frameRate = 14f;
        [SerializeField] private bool destroyOnComplete = true;

        private SpriteRenderer spriteRenderer;
        private float frameTimer;
        private int frameIndex;
        private bool finished;

        public void Play(Sprite[] animationFrames, float rate)
        {
            frames = animationFrames;
            frameRate = Mathf.Max(1f, rate);
            frameIndex = 0;
            frameTimer = 0f;
            finished = false;
            EnsureRenderer();
            ApplyFrame();
        }

        private void Awake()
        {
            EnsureRenderer();
        }

        private void Update()
        {
            if (finished || frames == null || frames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float duration = 1f / frameRate;
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                if (frameIndex >= frames.Length - 1)
                {
                    finished = true;
                    if (destroyOnComplete)
                    {
                        Destroy(gameObject);
                    }

                    return;
                }

                frameIndex++;
                ApplyFrame();
            }
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }
    }
}
