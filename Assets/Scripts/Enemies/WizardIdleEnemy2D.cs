using System;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Stationary wizard: ping-pong idle sprite animation. Optional Flip X for facing.
    /// After <see cref="BeginRestCycleWatch"/>, raises <see cref="IdleCycleCompleted"/>
    /// once when the first full idle cycle finishes (forward+reverse for ping-pong).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WizardIdleEnemy2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool flipX = true;
        [SerializeField] private bool pingPong = true;

        private SpriteRenderer spriteRenderer;
        private EnemyHealth health;
        private int frameIndex;
        private int direction = 1;
        private float frameTimer;
        private bool dead;
        private bool playbackEnabled = true;

        private bool restCycleWatchActive;
        private bool restCycleCompleted;
        private bool reachedEndOfForward;

        /// <summary>
        /// Fired once after <see cref="BeginRestCycleWatch"/> when the first full
        /// idle cycle completes (all frames forward then reverse back to start, or
        /// one loop through all frames when ping-pong is off).
        /// </summary>
        public event Action IdleCycleCompleted;

        /// <summary>
        /// Approximate wall-clock duration of one full idle cycle (ping-pong round-trip
        /// or one forward loop). Used as collapse fallback if the cycle event is missed.
        /// </summary>
        public float RestCycleDurationSeconds
        {
            get
            {
                if (idleFrames == null || idleFrames.Length == 0)
                {
                    return 0.5f;
                }

                float fps = Mathf.Max(1f, frameRate);
                if (idleFrames.Length == 1)
                {
                    return 1f / fps;
                }

                // Ping-pong 0→last→0 needs 2*(N-1) advances; loop needs N advances.
                int advances = pingPong
                    ? (idleFrames.Length - 1) * 2
                    : idleFrames.Length;
                return advances / fps;
            }
        }

        public void EditorAssignFrames(Sprite[] frames)
        {
            idleFrames = frames;
        }

        /// <summary>When false, idle stops writing the SpriteRenderer (attack animator owns it).</summary>
        public void SetPlaybackEnabled(bool enabled)
        {
            playbackEnabled = enabled;
            if (enabled && !dead)
            {
                // Snap to idle immediately (same frame as lever seal / rest).
                ApplyFacingAndFrame();
            }
        }

        /// <summary>
        /// Start watching for the first complete idle cycle after ReturnToIdle / rest.
        /// Resets playback to frame 0 so the cycle is measured cleanly.
        /// Idempotent until a cycle completes; subsequent calls after completion are no-ops.
        /// </summary>
        public void BeginRestCycleWatch()
        {
            if (restCycleCompleted)
            {
                return;
            }

            restCycleWatchActive = true;
            reachedEndOfForward = false;
            frameIndex = 0;
            direction = 1;
            frameTimer = 0f;
            ApplyFacingAndFrame();
            Debug.Log(
                $"[WizardIdleEnemy2D] BeginRestCycleWatch (frames={idleFrames?.Length ?? 0}, " +
                $"pingPong={pingPong}, duration≈{RestCycleDurationSeconds:0.###}s)",
                this);
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<EnemyHealth>();
            ApplyFacingAndFrame();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (health != null)
            {
                health.Died += OnDied;
            }

            dead = false;
            frameIndex = 0;
            direction = 1;
            frameTimer = 0f;
            ApplyFacingAndFrame();
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void OnDied()
        {
            dead = true;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (dead || !playbackEnabled || idleFrames == null || idleFrames.Length == 0)
            {
                return;
            }

            if (idleFrames.Length == 1)
            {
                spriteRenderer.sprite = idleFrames[0];
                // Single-frame "idle": treat one displayed frame after watch as one cycle.
                if (restCycleWatchActive && !restCycleCompleted)
                {
                    NotifyRestCycleCompleted();
                }

                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                AdvanceFrame();
            }

            ApplyFacingAndFrame();
        }

        private void AdvanceFrame()
        {
            if (pingPong)
            {
                int next = frameIndex + direction;
                if (next >= idleFrames.Length || next < 0)
                {
                    // Complete when reverse bounces past start AFTER forward already hit the end.
                    // (Old check used direction<0 && next<=0 at the *forward* bounce — next is
                    // Length-2 there, so IdleCycleCompleted never fired for N>2.)
                    bool hitForwardEnd = next >= idleFrames.Length;
                    bool hitReverseStart = next < 0;

                    if (hitForwardEnd)
                    {
                        reachedEndOfForward = true;
                    }

                    direction = -direction;
                    next = frameIndex + direction;

                    if (restCycleWatchActive &&
                        !restCycleCompleted &&
                        reachedEndOfForward &&
                        hitReverseStart)
                    {
                        frameIndex = 0;
                        direction = 1;
                        NotifyRestCycleCompleted();
                        return;
                    }
                }

                frameIndex = Mathf.Clamp(next, 0, idleFrames.Length - 1);
                return;
            }

            frameIndex = (frameIndex + 1) % idleFrames.Length;
            if (restCycleWatchActive && !restCycleCompleted && frameIndex == 0)
            {
                NotifyRestCycleCompleted();
            }
        }

        private void NotifyRestCycleCompleted()
        {
            if (restCycleCompleted)
            {
                return;
            }

            restCycleCompleted = true;
            restCycleWatchActive = false;
            Debug.Log("[WizardIdleEnemy2D] IdleCycleCompleted (first rest cycle)", this);
            IdleCycleCompleted?.Invoke();
        }

        private void ApplyFacingAndFrame()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.flipX = flipX;
            if (idleFrames != null && idleFrames.Length > 0)
            {
                int index = Mathf.Clamp(frameIndex, 0, idleFrames.Length - 1);
                if (idleFrames[index] != null)
                {
                    spriteRenderer.sprite = idleFrames[index];
                }
            }
        }
    }
}
