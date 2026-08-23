using UnityEngine;

namespace Castlevania2D.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BurningFloorLampAnimator2D : MonoBehaviour
    {
        private const string ResourceFolder = "Environment/BurningFloorLamp/";
        private const int ExpectedFrameCount = 10;

        [SerializeField] private Sprite[] frames;
        [SerializeField] [Min(1f)] private float frameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureFramesLoaded();
            ApplyCurrentFrame();
        }

        private void OnEnable()
        {
            frameIndex = 0;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;
                ApplyCurrentFrame();
            }
        }

        private void ApplyCurrentFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void EnsureFramesLoaded()
        {
            bool allFramesAssigned = frames != null && frames.Length == ExpectedFrameCount;
            if (allFramesAssigned)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] == null)
                    {
                        allFramesAssigned = false;
                        break;
                    }
                }
            }

            if (allFramesAssigned)
            {
                return;
            }

            frames = new Sprite[ExpectedFrameCount];
            for (int i = 0; i < ExpectedFrameCount; i++)
            {
                frames[i] = Resources.Load<Sprite>(ResourceFolder + $"FloorLamp_{i + 1:D2}");
            }
        }

#if UNITY_EDITOR
        public void EditorAssignFrames(Sprite[] newFrames)
        {
            frames = newFrames;
        }
#endif
    }
}
