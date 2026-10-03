using UnityEngine;

namespace Castlevania2D.Intro
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class IntroSeatedNpc2D : MonoBehaviour
    {
        private const int HoldFrameIndex = 3;
        private const int SpeakFirstIndex = 2;
        private const int SpeakLastIndex = 9;

        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] lookUpFrames;
        [SerializeField] private Sprite[] talkFrames;
        [SerializeField] [Min(1f)] private float speakFrameRate = 8f;

        private SpriteRenderer spriteRenderer;
        private bool speaking;
        private int frameIndex = HoldFrameIndex;
        private float frameTimer;

        private void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFramesIfNeeded();
            PlayIdle();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFramesIfNeeded();
            PlayIdle();
        }

        private void Update()
        {
            if (!Application.isPlaying || !speaking)
            {
                return;
            }

            if (spriteRenderer == null || idleFrames == null || idleFrames.Length <= SpeakLastIndex)
            {
                return;
            }

            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(0.1f, speakFrameRate / 3f);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                frameIndex++;
                if (frameIndex > SpeakLastIndex)
                {
                    frameIndex = SpeakFirstIndex;
                }
            }

            ApplyIndex(frameIndex);
        }

        public void PlayIdle()
        {
            speaking = false;
            frameTimer = 0f;
            frameIndex = HoldFrameIndex;
            ApplyIndex(HoldFrameIndex);
        }

        public void PlaySpeak()
        {
            if (idleFrames == null || idleFrames.Length <= SpeakLastIndex)
            {
                LoadFramesIfNeeded();
            }

            speaking = true;
            frameTimer = 0f;
            frameIndex = SpeakFirstIndex;
            ApplyIndex(SpeakFirstIndex);
        }

        public void PlayLookUpThenTalk()
        {
            PlaySpeak();
        }

        public void PlayTalk()
        {
            PlaySpeak();
        }

        private void ApplyIndex(int index)
        {
            if (spriteRenderer == null || idleFrames == null || idleFrames.Length == 0)
            {
                return;
            }

            Sprite frame = idleFrames[Mathf.Clamp(index, 0, idleFrames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void LoadFramesIfNeeded()
        {
            if (HasIdleSet())
            {
                return;
            }

            idleFrames = LoadSorted("Npcs/IntroHost/Idle");
        }

        private bool HasIdleSet()
        {
            if (idleFrames == null || idleFrames.Length <= SpeakLastIndex)
            {
                return false;
            }

            for (int i = SpeakFirstIndex; i <= SpeakLastIndex; i++)
            {
                if (idleFrames[i] == null)
                {
                    return false;
                }
            }

            return idleFrames[HoldFrameIndex] != null;
        }

        private static Sprite[] LoadSorted(string resourceFolder)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(resourceFolder);
            if (loaded == null || loaded.Length == 0)
            {
                return loaded;
            }

            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            return loaded;
        }

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] idle, Sprite[] lookUp, Sprite[] talk)
        {
            idleFrames = idle;
            lookUpFrames = lookUp;
            talkFrames = talk;
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyIndex(HoldFrameIndex);
        }
#endif
    }
}
