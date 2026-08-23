using System;
using UnityEngine;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Plays the save feedback animation. Persistence logic can call PlaySaveAnimation
    /// after a successful save without depending on animation details.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SaveIdolAnimator2D : MonoBehaviour
    {
        private const string ResourceFolder = "Environment/SaveIdol/";
        private const int SaveFrameCount = 9;

        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite[] saveFrames;
        [SerializeField] [Min(1f)] private float frameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;

        public event Action SaveAnimationCompleted;

        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureFramesLoaded();
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
            if (!IsAnimating)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.unscaledDeltaTime;

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;

                if (frameIndex >= saveFrames.Length)
                {
                    CompleteAnimation();
                    return;
                }

                spriteRenderer.sprite = saveFrames[frameIndex];
            }
        }

        public bool PlaySaveAnimation()
        {
            if (IsAnimating)
            {
                return false;
            }

            EnsureFramesLoaded();
            if (saveFrames == null || saveFrames.Length == 0 || saveFrames[0] == null)
            {
                Debug.LogWarning("[SaveIdolAnimator2D] Save animation frames are not assigned.", this);
                return false;
            }

            frameIndex = 0;
            frameTimer = 0f;
            IsAnimating = true;
            spriteRenderer.sprite = saveFrames[0];
            return true;
        }

        private void CompleteAnimation()
        {
            IsAnimating = false;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyIdleSprite();
            SaveAnimationCompleted?.Invoke();
        }

        private void ApplyIdleSprite()
        {
            if (spriteRenderer != null && idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        private void EnsureFramesLoaded()
        {
            if (idleSprite == null)
            {
                idleSprite = Resources.Load<Sprite>(ResourceFolder + "Idol_01");
            }

            bool allSaveFramesAssigned = saveFrames != null && saveFrames.Length == SaveFrameCount;
            if (allSaveFramesAssigned)
            {
                for (int i = 0; i < saveFrames.Length; i++)
                {
                    if (saveFrames[i] == null)
                    {
                        allSaveFramesAssigned = false;
                        break;
                    }
                }
            }

            if (allSaveFramesAssigned)
            {
                return;
            }

            saveFrames = new Sprite[SaveFrameCount];
            for (int i = 0; i < SaveFrameCount; i++)
            {
                saveFrames[i] = Resources.Load<Sprite>(ResourceFolder + $"Idol_{i + 2:D2}");
            }
        }

#if UNITY_EDITOR
        public void EditorAssignFrames(Sprite newIdleSprite, Sprite[] newSaveFrames)
        {
            idleSprite = newIdleSprite;
            saveFrames = newSaveFrames;
        }
#endif
    }
}
