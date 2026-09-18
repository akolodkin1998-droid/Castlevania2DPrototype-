using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WoodenElevatorWinch2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] spinFrames;
        [SerializeField] [Min(1f)] private float spinFrameRate = 10f;

        private SpriteRenderer spriteRenderer;
        private bool spinning;
        private int frameIndex;
        private float frameTimer;

        public bool IsSpinning => spinning;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyHoldFrame();
        }

        private void Update()
        {
            if (!spinning || spinFrames == null || spinFrames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, spinFrameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= spinFrames.Length)
                {
                    frameIndex = 0;
                }

                ApplyFrame(frameIndex);
            }
        }

        public void SetSpinning(bool shouldSpin)
        {
            if (spinning == shouldSpin)
            {
                return;
            }

            spinning = shouldSpin;
            frameTimer = 0f;
            if (!spinning)
            {
                frameIndex = 0;
                ApplyHoldFrame();
            }
            else if (spinFrames != null && spinFrames.Length > 0)
            {
                ApplyFrame(frameIndex);
            }
        }

        public void EditorAssignFrames(Sprite[] frames, float frameRate)
        {
            spinFrames = frames ?? System.Array.Empty<Sprite>();
            spinFrameRate = Mathf.Max(1f, frameRate);
            frameIndex = 0;
            ApplyHoldFrame();
        }

        private void ApplyHoldFrame()
        {
            ApplyFrame(0);
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null || spinFrames == null || spinFrames.Length == 0)
            {
                return;
            }

            Sprite frame = spinFrames[Mathf.Clamp(index, 0, spinFrames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }
    }
}
