using UnityEngine;

namespace Castlevania2D.Npcs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PairLoopIdleSprite2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] [Min(0.25f)] private float frameRate = 2f;
        [SerializeField] [Min(1)] private int pairSize = 2;
        [SerializeField] [Min(1)] private int pairPlayCount = 2;

        private SpriteRenderer spriteRenderer;
        private int step;
        private float timer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyFrame();
        }

        private void Update()
        {
            if (frames == null || frames.Length < pairSize)
            {
                return;
            }

            timer += Time.deltaTime;
            float duration = 1f / Mathf.Max(1f, frameRate);
            while (timer >= duration)
            {
                timer -= duration;
                step++;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            int safePair = Mathf.Max(1, pairSize);
            int safePlay = Mathf.Max(1, pairPlayCount);
            int pairCount = Mathf.Max(1, frames.Length / safePair);
            int stepsPerPair = safePair * safePlay;
            int inCycle = step % (pairCount * stepsPerPair);
            int pair = inCycle / stepsPerPair;
            int within = inCycle % stepsPerPair;
            int index = (pair * safePair) + (within % safePair);
            if (index >= frames.Length)
            {
                index = frames.Length - 1;
            }

            Sprite frame = frames[index];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        public void SetFrameRate(float rate)
        {
            frameRate = Mathf.Max(0.25f, rate);
        }

        public void AssignFrames(Sprite[] idleFrames, float rate)
        {
            frames = idleFrames;
            SetFrameRate(rate);
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            ApplyFrame();
        }
    }
}
