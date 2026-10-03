using UnityEngine;

namespace Castlevania2D.Terem
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TeremKnyazNpc2D : MonoBehaviour
    {
        private const int HoldFrameIndex = 0;
        private const int ListenFirstIndex = 0;
        private const int ListenLastIndex = 3;
        private const int SpeakFirstIndex = 4;
        private const int SpeakLastIndex = 8;
        private const int RequiredFrameCount = 9;
        private const string ResourceFolder = "Npcs/TeremKnyaz";

        [SerializeField] private Sprite[] frames;
        [SerializeField] [Min(1f)] private float speakFrameRate = 4f;
        [SerializeField] [Min(1f)] private float listenFrameRate = 3f;
        [SerializeField] [Min(0.5f)] private float listenIntervalMin = 3.5f;
        [SerializeField] [Min(0.5f)] private float listenIntervalMax = 7f;

        private SpriteRenderer spriteRenderer;
        private Mode mode = Mode.Idle;
        private bool listenReacting;
        private int frameIndex = HoldFrameIndex;
        private int direction = 1;
        private float frameTimer;
        private float listenWait;

        private enum Mode
        {
            Idle,
            Speak,
            Listen
        }

        private void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFramesIfNeeded();
            if (!Application.isPlaying)
            {
                ApplyIndex(HoldFrameIndex);
                return;
            }

            PlayIdle();
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
            if (spriteRenderer == null || frames == null || frames.Length < RequiredFrameCount)
            {
                return;
            }

            if (mode == Mode.Idle)
            {
                return;
            }

            if (mode == Mode.Listen && !listenReacting)
            {
                listenWait -= Time.unscaledDeltaTime;
                if (listenWait <= 0f)
                {
                    StartListenCycle();
                }

                return;
            }

            float rate = mode == Mode.Speak ? speakFrameRate : listenFrameRate;
            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(0.1f, rate);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                if (!AdvancePingPong())
                {
                    break;
                }
            }

            ApplyIndex(frameIndex);
        }

        public void PlayIdle()
        {
            mode = Mode.Idle;
            listenReacting = false;
            frameTimer = 0f;
            direction = 1;
            frameIndex = HoldFrameIndex;
            ApplyIndex(HoldFrameIndex);
        }

        public void PlaySpeak()
        {
            LoadFramesIfNeeded();
            mode = Mode.Speak;
            listenReacting = false;
            frameTimer = 0f;
            direction = 1;
            frameIndex = SpeakFirstIndex;
            ApplyIndex(SpeakFirstIndex);
        }

        public void PlayListen()
        {
            LoadFramesIfNeeded();
            mode = Mode.Listen;
            listenReacting = false;
            frameTimer = 0f;
            direction = 1;
            frameIndex = HoldFrameIndex;
            listenWait = NextListenWait();
            ApplyIndex(HoldFrameIndex);
        }

        private void StartListenCycle()
        {
            listenReacting = true;
            frameTimer = 0f;
            direction = 1;
            frameIndex = ListenFirstIndex;
            ApplyIndex(ListenFirstIndex);
        }

        private bool AdvancePingPong()
        {
            int first = mode == Mode.Speak ? SpeakFirstIndex : ListenFirstIndex;
            int last = mode == Mode.Speak ? SpeakLastIndex : ListenLastIndex;
            frameIndex += direction;
            if (frameIndex >= last)
            {
                frameIndex = last;
                direction = -1;
            }
            else if (frameIndex <= first)
            {
                frameIndex = first;
                direction = 1;
                if (mode == Mode.Listen)
                {
                    listenReacting = false;
                    listenWait = NextListenWait();
                    ApplyIndex(HoldFrameIndex);
                    return false;
                }
            }

            return true;
        }

        private float NextListenWait()
        {
            float min = Mathf.Min(listenIntervalMin, listenIntervalMax);
            float max = Mathf.Max(listenIntervalMin, listenIntervalMax);
            return Random.Range(min, max);
        }

        private void ApplyIndex(int index)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void LoadFramesIfNeeded()
        {
            if (HasFrameSet())
            {
                return;
            }

            Sprite[] loaded = Resources.LoadAll<Sprite>(ResourceFolder);
            if (loaded == null || loaded.Length == 0)
            {
                return;
            }

            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            frames = loaded;
        }

        private bool HasFrameSet()
        {
            if (frames == null || frames.Length < RequiredFrameCount)
            {
                return false;
            }

            for (int i = 0; i < RequiredFrameCount; i++)
            {
                if (frames[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] assigned)
        {
            frames = assigned;
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyIndex(HoldFrameIndex);
        }
#endif
    }
}
