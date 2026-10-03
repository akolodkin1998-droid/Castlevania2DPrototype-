using System;
using UnityEngine;

namespace Castlevania2D.Environment
{
    /// <summary>
    /// Idle loops frames 1-3. Interaction plays the remaining Mara frames once.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SaveIdolAnimator2D : MonoBehaviour
    {
        private const string ResourceFolder = "Environment/SaveIdol/";
        private const int IdleFrameCount = 3;
        private const int SaveFrameCount = 7;

        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] saveFrames;
        [SerializeField] [Min(1f)] private float idleFrameRate = 6f;
        [SerializeField] [Min(1f)] private float frameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private int idleIndex;
        private int idleDirection = 1;
        private float idleTimer;
        private int saveIndex;
        private float saveTimer;

        public event Action SaveAnimationCompleted;

        public bool IsAnimating { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureFramesLoaded();
            ApplyIdleFrame();
        }

        private void OnDisable()
        {
            IsAnimating = false;
            saveIndex = 0;
            saveTimer = 0f;
            idleIndex = 0;
            idleDirection = 1;
            idleTimer = 0f;
            ApplyIdleFrame();
        }

        private void Update()
        {
            if (IsAnimating)
            {
                TickSaveAnimation();
                return;
            }

            TickIdleAnimation();
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

            saveIndex = 0;
            saveTimer = 0f;
            IsAnimating = true;
            spriteRenderer.sprite = saveFrames[0];
            return true;
        }

        private void TickIdleAnimation()
        {
            if (idleFrames == null || idleFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            if (idleFrames.Length == 1)
            {
                ApplyIdleFrame();
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, idleFrameRate);
            idleTimer += Time.deltaTime;
            while (idleTimer >= frameDuration)
            {
                idleTimer -= frameDuration;
                AdvanceIdleFrame();
                ApplyIdleFrame();
            }
        }

        private void AdvanceIdleFrame()
        {
            idleIndex += idleDirection;
            if (idleIndex >= idleFrames.Length - 1)
            {
                idleIndex = idleFrames.Length - 1;
                idleDirection = -1;
            }
            else if (idleIndex <= 0)
            {
                idleIndex = 0;
                idleDirection = 1;
            }
        }

        private void TickSaveAnimation()
        {
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            saveTimer += Time.unscaledDeltaTime;

            while (saveTimer >= frameDuration)
            {
                saveTimer -= frameDuration;
                saveIndex++;

                if (saveIndex >= saveFrames.Length)
                {
                    CompleteAnimation();
                    return;
                }

                spriteRenderer.sprite = saveFrames[saveIndex];
            }
        }

        private void CompleteAnimation()
        {
            IsAnimating = false;
            saveIndex = 0;
            saveTimer = 0f;
            idleIndex = 0;
            idleDirection = 1;
            idleTimer = 0f;
            ApplyIdleFrame();
            SaveAnimationCompleted?.Invoke();
        }

        private void ApplyIdleFrame()
        {
            if (spriteRenderer == null || idleFrames == null || idleFrames.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(idleIndex, 0, idleFrames.Length - 1);
            if (idleFrames[index] != null)
            {
                spriteRenderer.sprite = idleFrames[index];
            }
        }

        private void EnsureFramesLoaded()
        {
            if (!HasAllFrames(idleFrames, IdleFrameCount))
            {
                idleFrames = new Sprite[IdleFrameCount];
                for (int i = 0; i < IdleFrameCount; i++)
                {
                    idleFrames[i] = Resources.Load<Sprite>(ResourceFolder + $"Idol_{i + 1:D2}");
                }
            }

            if (HasAllFrames(saveFrames, SaveFrameCount))
            {
                return;
            }

            saveFrames = new Sprite[SaveFrameCount];
            for (int i = 0; i < SaveFrameCount; i++)
            {
                saveFrames[i] = Resources.Load<Sprite>(ResourceFolder + $"Idol_{i + IdleFrameCount + 1:D2}");
            }
        }

        private static bool HasAllFrames(Sprite[] frames, int expectedCount)
        {
            if (frames == null || frames.Length != expectedCount)
            {
                return false;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        public void EditorAssignFrames(Sprite[] newIdleFrames, Sprite[] newSaveFrames)
        {
            idleFrames = newIdleFrames;
            saveFrames = newSaveFrames;
        }
#endif
    }
}
