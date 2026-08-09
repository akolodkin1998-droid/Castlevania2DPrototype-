using EnemyHealth = Castlevania2D.Health.Health;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>Plays a stationary mouse idle animation until another mouse state owns the renderer.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MouseIdleEnemy2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool pingPong = true;

        private SpriteRenderer spriteRenderer;
        private EnemyHealth health;
        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private bool isDead;
        private bool isAnimationPlaying = true;

        public void SetAnimationPlaying(bool value)
        {
            isAnimationPlaying = value;
            if (value)
            {
                frameTimer = 0f;
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<EnemyHealth>();
            ApplyFrame();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (isDead || !isAnimationPlaying || idleFrames == null || idleFrames.Length <= 1)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                AdvanceFrame();
            }

            ApplyFrame();
        }

        private void OnDied()
        {
            isDead = true;
        }

        private void AdvanceFrame()
        {
            if (!pingPong)
            {
                frameIndex = (frameIndex + 1) % idleFrames.Length;
                return;
            }

            frameIndex += frameDirection;
            if (frameIndex >= idleFrames.Length - 1)
            {
                frameIndex = idleFrames.Length - 1;
                frameDirection = -1;
            }
            else if (frameIndex <= 0)
            {
                frameIndex = 0;
                frameDirection = 1;
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || idleFrames == null || idleFrames.Length == 0)
            {
                return;
            }

            Sprite frame = idleFrames[Mathf.Clamp(frameIndex, 0, idleFrames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }
    }
}
