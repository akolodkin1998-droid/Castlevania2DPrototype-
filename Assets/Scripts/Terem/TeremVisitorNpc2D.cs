using UnityEngine;

namespace Castlevania2D.Terem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TeremVisitorNpc2D : MonoBehaviour
    {
        private const int IdleFrameIndex = 0;
        private const int WalkFirstIndex = 3;
        private const int WalkCount = 15;
        private const int KneelCount = 10;
        private const int SpeakCount = 19;
        private const int SpeakFirstIndex = 4;
        private const int SpeakLastIndex = 18;
        private const string WalkFolder = "Npcs/TeremVisitor";
        private const string KneelFolder = "Npcs/TeremVisitorKneel";
        private const string SpeakFolder = "Npcs/TeremVisitorSpeak";

        [SerializeField] private Sprite[] frames;
        [SerializeField] private Sprite[] kneelFrames;
        [SerializeField] private Sprite[] speakFrames;
        [SerializeField] private TeremKnyazNpc2D knyaz;
        [SerializeField] private Vector3 stopOffset = new Vector3(-2.2f, 0f, 0f);
        [SerializeField] [Min(0.1f)] private float walkSpeed = 1.8f;
        [SerializeField] [Min(1f)] private float walkFrameRate = 8f;
        [SerializeField] [Min(1f)] private float kneelFrameRate = 8f;
        [SerializeField] [Min(1f)] private float speakFrameRate = 6f;
        [SerializeField] [Min(0f)] private float startDelay = 0.8f;

        private SpriteRenderer spriteRenderer;
        private Phase phase;
        private int frameIndex = IdleFrameIndex;
        private int kneelIndex;
        private int speakIndex = SpeakFirstIndex;
        private int speakDirection = 1;
        private float frameTimer;
        private float delayTimer;
        private bool speakRequested;

        private enum Phase
        {
            Delay,
            Walk,
            KneelDown,
            HoldKneel,
            StandUp,
            Standing,
            Speak
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ReloadFrames();
            if (knyaz == null)
            {
                knyaz = FindFirstObjectByType<TeremKnyazNpc2D>();
            }
        }

        private void Start()
        {
            ReloadFrames();
            delayTimer = startDelay;
            phase = Phase.Delay;
            ApplyWalk(IdleFrameIndex);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ReloadFrames();
            switch (phase)
            {
                case Phase.Delay:
                    TickDelay();
                    break;
                case Phase.Walk:
                    TickWalk();
                    break;
                case Phase.KneelDown:
                    TickKneel(1);
                    break;
                case Phase.HoldKneel:
                    ApplyKneel(LastKneelIndex());
                    if (speakRequested)
                    {
                        BeginStand();
                    }

                    break;
                case Phase.StandUp:
                    TickKneel(-1);
                    break;
                case Phase.Standing:
                    if (speakRequested)
                    {
                        BeginSpeak();
                        break;
                    }

                    ApplyWalk(IdleFrameIndex);
                    break;
                case Phase.Speak:
                    TickSpeak();
                    break;
            }
        }

        public bool IsReadyForDialogue =>
            phase == Phase.HoldKneel
            || phase == Phase.StandUp
            || phase == Phase.Standing
            || phase == Phase.Speak;

        public void PlaySpeak()
        {
            speakRequested = true;
            if (phase == Phase.HoldKneel)
            {
                BeginStand();
                return;
            }

            if (phase == Phase.Standing || phase == Phase.Speak)
            {
                BeginSpeak();
            }
        }

        public void PlayIdle()
        {
            speakRequested = false;
            if (phase == Phase.Speak || phase == Phase.Standing)
            {
                phase = Phase.Standing;
                ApplyWalk(IdleFrameIndex);
            }
        }

        private void TickDelay()
        {
            delayTimer -= Time.unscaledDeltaTime;
            if (delayTimer > 0f)
            {
                ApplyWalk(IdleFrameIndex);
                return;
            }

            if (HasWalkSet() && !HasArrived())
            {
                phase = Phase.Walk;
                frameIndex = WalkFirstIndex;
                frameTimer = 0f;
                ApplyWalk(frameIndex);
                return;
            }

            BeginKneel();
        }

        private void TickWalk()
        {
            if (!HasWalkSet())
            {
                BeginKneel();
                return;
            }

            Vector3 target = TargetPosition();
            Vector3 current = transform.position;
            Vector3 delta = target - current;
            delta.z = 0f;
            float step = walkSpeed * Time.unscaledDeltaTime;
            if (delta.sqrMagnitude <= step * step)
            {
                transform.position = new Vector3(target.x, current.y, current.z);
                BeginKneel();
                return;
            }

            Face(delta.x);
            transform.position = current + delta.normalized * step;

            int first = Mathf.Min(WalkFirstIndex, frames.Length - 1);
            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(0.1f, walkFrameRate);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                frameIndex++;
                if (frameIndex >= frames.Length)
                {
                    frameIndex = first;
                }
            }

            ApplyWalk(frameIndex);
        }

        private void TickKneel(int direction)
        {
            if (!HasKneelSet())
            {
                phase = direction > 0 ? Phase.HoldKneel : Phase.Standing;
                return;
            }

            int last = LastKneelIndex();
            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(0.1f, kneelFrameRate);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                kneelIndex += direction;
                if (direction > 0 && kneelIndex >= last)
                {
                    kneelIndex = last;
                    phase = Phase.HoldKneel;
                    ApplyKneel(kneelIndex);
                    return;
                }

                if (direction < 0 && kneelIndex <= 0)
                {
                    kneelIndex = 0;
                    phase = speakRequested ? Phase.Speak : Phase.Standing;
                    if (phase == Phase.Speak)
                    {
                        BeginSpeak();
                    }
                    else
                    {
                        ApplyWalk(IdleFrameIndex);
                    }

                    return;
                }
            }

            ApplyKneel(kneelIndex);
        }

        private void BeginSpeak()
        {
            if (!HasSpeakSet())
            {
                phase = Phase.Standing;
                ApplyWalk(IdleFrameIndex);
                return;
            }

            phase = Phase.Speak;
            speakIndex = SpeakFirstIndex;
            speakDirection = 1;
            frameTimer = 0f;
            ApplySpeak(speakIndex);
        }

        private void TickSpeak()
        {
            if (!HasSpeakSet())
            {
                ApplyWalk(IdleFrameIndex);
                return;
            }

            int first = Mathf.Min(SpeakFirstIndex, speakFrames.Length - 1);
            int last = Mathf.Min(SpeakLastIndex, speakFrames.Length - 1);
            if (last < first)
            {
                last = first;
            }

            frameTimer += Time.unscaledDeltaTime;
            float duration = 1f / Mathf.Max(0.1f, speakFrameRate);
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                speakIndex += speakDirection;
                if (speakIndex >= last)
                {
                    speakIndex = last;
                    speakDirection = -1;
                }
                else if (speakIndex <= first)
                {
                    speakIndex = first;
                    speakDirection = 1;
                }
            }

            ApplySpeak(speakIndex);
        }

        private void BeginKneel()
        {
            if (speakRequested)
            {
                BeginSpeak();
                return;
            }

            phase = Phase.KneelDown;
            kneelIndex = 0;
            frameTimer = 0f;
            if (HasKneelSet())
            {
                ApplyKneel(0);
            }
        }

        private void BeginStand()
        {
            phase = Phase.StandUp;
            kneelIndex = LastKneelIndex();
            frameTimer = 0f;
            if (HasKneelSet())
            {
                ApplyKneel(kneelIndex);
            }
        }

        private bool HasArrived()
        {
            Vector3 delta = TargetPosition() - transform.position;
            delta.z = 0f;
            return delta.sqrMagnitude <= 0.01f;
        }

        private Vector3 TargetPosition()
        {
            Vector3 origin = knyaz != null ? knyaz.transform.position : transform.position;
            return new Vector3(origin.x + stopOffset.x, transform.position.y, transform.position.z);
        }

        private void Face(float directionX)
        {
            if (spriteRenderer == null || Mathf.Abs(directionX) < 0.001f)
            {
                return;
            }

            spriteRenderer.flipX = directionX < 0f;
        }

        private int LastKneelIndex()
        {
            return kneelFrames == null || kneelFrames.Length == 0 ? 0 : kneelFrames.Length - 1;
        }

        private bool HasWalkSet()
        {
            return CountValid(frames) > WalkFirstIndex;
        }

        private bool HasKneelSet()
        {
            return CountValid(kneelFrames) >= 2;
        }

        private bool HasSpeakSet()
        {
            return CountValid(speakFrames) > SpeakFirstIndex;
        }

        private void ApplyWalk(int index)
        {
            ApplySprite(frames, index);
        }

        private void ApplyKneel(int index)
        {
            ApplySprite(kneelFrames, index);
        }

        private void ApplySpeak(int index)
        {
            ApplySprite(speakFrames, index);
        }

        private void ApplySprite(Sprite[] set, int index)
        {
            if (spriteRenderer == null || set == null || set.Length == 0)
            {
                return;
            }

            Sprite frame = set[Mathf.Clamp(index, 0, set.Length - 1)];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void ReloadFrames()
        {
            Sprite[] walk = LoadNumbered(WalkFolder, "Visitor_", WalkCount);
            if (CountValid(walk) > CountValid(frames))
            {
                frames = walk;
            }

            Sprite[] kneel = LoadNumbered(KneelFolder, "Kneel_", KneelCount);
            if (CountValid(kneel) > CountValid(kneelFrames))
            {
                kneelFrames = kneel;
            }

            Sprite[] speak = LoadNumbered(SpeakFolder, "Speak_", SpeakCount);
            if (CountValid(speak) > CountValid(speakFrames))
            {
                speakFrames = speak;
            }
        }

        private static int CountValid(Sprite[] set)
        {
            if (set == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static Sprite[] LoadNumbered(string folder, string prefix, int count)
        {
            var loaded = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                loaded[i] = Resources.Load<Sprite>(folder + "/" + prefix + (i + 1).ToString("00"));
            }

            return loaded;
        }

#if UNITY_EDITOR
        public void EditorAssign(Sprite[] assigned, TeremKnyazNpc2D target)
        {
            frames = assigned;
            knyaz = target;
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyWalk(IdleFrameIndex);
        }
#endif
    }
}
