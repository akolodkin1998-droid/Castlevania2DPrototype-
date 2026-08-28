using UnityEngine;

namespace Castlevania2D.Hub
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HubIdleNpc2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] [Min(1f)] private float frameRate = 8f;
        [SerializeField] private bool pingPong = true;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            StripPhysics();
            ApplyFrame();
            if (GetComponent<HubMerchantTalk2D>() == null)
            {
                gameObject.AddComponent<HubMerchantTalk2D>();
            }
        }

        private void StripPhysics()
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
                Destroy(colliders[i]);
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                Destroy(body);
            }
        }

        private void Update()
        {
            if (idleFrames == null || idleFrames.Length <= 1)
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

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] frames, float rate)
        {
            idleFrames = frames;
            frameRate = rate;
        }
#endif
    }
}
