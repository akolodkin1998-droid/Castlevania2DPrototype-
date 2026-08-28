using System;
using UnityEngine;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SceneWarpTotemAnimator2D : MonoBehaviour
    {
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] warpFrames;
        [SerializeField] [Min(1f)] private float frameRate = 10f;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;

        public event Action WarpAnimationCompleted;

        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyIdleSprite();
        }

        private void OnDisable()
        {
            IsAnimating = false;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyIdleSprite();
        }

        private void Update()
        {
            if (!IsAnimating || warpFrames == null || warpFrames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.unscaledDeltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= warpFrames.Length)
                {
                    CompleteAnimation();
                    return;
                }

                if (warpFrames[frameIndex] != null)
                {
                    spriteRenderer.sprite = warpFrames[frameIndex];
                }
            }
        }

        public bool PlayWarpAnimation()
        {
            if (IsAnimating || warpFrames == null || warpFrames.Length == 0)
            {
                return false;
            }

            frameIndex = 0;
            frameTimer = 0f;
            IsAnimating = true;
            if (warpFrames[0] != null)
            {
                spriteRenderer.sprite = warpFrames[0];
            }

            return true;
        }

        private void CompleteAnimation()
        {
            IsAnimating = false;
            WarpAnimationCompleted?.Invoke();
        }

        private void ApplyIdleSprite()
        {
            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

#if UNITY_EDITOR
        public void EditorAssignFrames(Sprite newIdleSprite, Sprite[] newWarpFrames)
        {
            idleSprite = newIdleSprite;
            warpFrames = newWarpFrames;
        }
#endif
    }
}
