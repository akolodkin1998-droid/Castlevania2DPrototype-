using UnityEngine;

namespace Castlevania2D.Intro
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class IntroWomanNpc2D : MonoBehaviour
    {
        private const int IdleFrameA = 1;
        private const int IdleFrameB = 2;
        private const int CoughFrameCount = 12;

        [SerializeField] private Sprite[] coughFrames;
        [SerializeField] [Min(1f)] private float idleFrameRate = 3f;
        [SerializeField] [Min(1f)] private float coughFrameRate = 10f;

        private SpriteRenderer spriteRenderer;
        private bool coughing;
        private int frameIndex = IdleFrameA;
        private int idleDirection = 1;
        private float frameTimer;

        public bool IsCoughing => coughing;

        private void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFramesIfNeeded();
            if (!Application.isPlaying)
            {
                ApplyIndex(IdleFrameA);
                return;
            }

            if (!coughing)
            {
                PlayIdle();
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFramesIfNeeded();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            LoadFramesIfNeeded();
            if (spriteRenderer == null || coughFrames == null || coughFrames.Length == 0)
            {
                return;
            }

            float rate = coughing ? coughFrameRate : idleFrameRate;
            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(1f, rate);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                Advance();
            }

            ApplyIndex(frameIndex);
        }

        public void PlayIdle()
        {
            coughing = false;
            frameIndex = IdleFrameA;
            idleDirection = 1;
            frameTimer = 0f;
            ApplyIndex(frameIndex);
        }

        public void PlayCough()
        {
            LoadFramesIfNeeded();
            if (coughFrames == null || coughFrames.Length == 0)
            {
                Debug.LogError("[IntroWomanNpc2D] Cough frames missing.");
                return;
            }

            coughing = true;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyIndex(0);
        }

        private void Advance()
        {
            if (coughing)
            {
                if (frameIndex >= coughFrames.Length - 1)
                {
                    PlayIdle();
                    return;
                }

                frameIndex++;
                return;
            }

            if (coughFrames.Length <= IdleFrameB)
            {
                frameIndex = Mathf.Clamp(IdleFrameA, 0, coughFrames.Length - 1);
                return;
            }

            frameIndex += idleDirection;
            if (frameIndex >= IdleFrameB)
            {
                frameIndex = IdleFrameB;
                idleDirection = -1;
            }
            else if (frameIndex <= IdleFrameA)
            {
                frameIndex = IdleFrameA;
                idleDirection = 1;
            }
        }

        private void ApplyIndex(int index)
        {
            if (spriteRenderer == null || coughFrames == null || coughFrames.Length == 0)
            {
                return;
            }

            Sprite frame = coughFrames[Mathf.Clamp(index, 0, coughFrames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void LoadFramesIfNeeded()
        {
            if (HasCoughSet())
            {
                return;
            }

            Sprite[] loaded = Resources.LoadAll<Sprite>("Npcs/IntroWoman");
            if (loaded == null || loaded.Length == 0)
            {
                return;
            }

            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            coughFrames = loaded;
        }

        private bool HasCoughSet()
        {
            if (coughFrames == null || coughFrames.Length < CoughFrameCount)
            {
                return false;
            }

            for (int i = 0; i < CoughFrameCount; i++)
            {
                if (coughFrames[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] frames)
        {
            coughFrames = frames;
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyIndex(IdleFrameA);
        }
#endif
    }
}
