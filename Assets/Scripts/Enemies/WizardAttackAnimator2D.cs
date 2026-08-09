using System;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// On first player sight: Idle → Attack1 once → Attack2 once → loop Attack2 frames 17–19.
    /// Wizard never deals damage. Facing matches idle (flipX stays true).
    /// Fires <see cref="PortalsRequested"/> when Attack1 finishes / Attack2 begins.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WizardAttackAnimator2D : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Attack1,
            Attack2,
            Attack2Loop,
        }

        [Header("Frames")]
        [SerializeField] private Sprite[] attack1Frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attack2Frames = Array.Empty<Sprite>();

        [Header("Playback")]
        [SerializeField] private float frameRate = 12f;
        [Tooltip("Keep true so Attack1/Attack2/loop match idle facing.")]
        [SerializeField] private bool flipX = true;
        [Tooltip("1-based source frame number for Attack2 loop start (download_0017).")]
        [SerializeField] private int loopStartFrameNumber = 17;
        [Tooltip("1-based source frame number for Attack2 loop end (download_0019).")]
        [SerializeField] private int loopEndFrameNumber = 19;

        [Header("Refs")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private WizardIdleEnemy2D idleEnemy;
        [SerializeField] private WizardPlayerSight2D playerSight;
        [SerializeField] private WizardPortalSpawner2D portalSpawner;

        /// <summary>Raised once when Attack1 completes and Attack2 begins (place portals).</summary>
        public event Action PortalsRequested;

        private EnemyHealth health;
        private Phase phase = Phase.Idle;
        private Sprite[] activeFrames = Array.Empty<Sprite>();
        private int frameIndex;
        private float frameTimer;
        private int loopStartIndex;
        private int loopEndIndex;
        private int loopDirection = 1;
        private bool portalsRequested;
        private bool dead;
        private bool sequenceStarted;
        private bool restForced;

        public void EditorAssignFrames(Sprite[] attack1, Sprite[] attack2)
        {
            attack1Frames = attack1 ?? Array.Empty<Sprite>();
            attack2Frames = attack2 ?? Array.Empty<Sprite>();
            ResolveLoopIndices();
        }

        /// <summary>
        /// Stops Attack1/Attack2/loop, resumes idle ping-pong, and permanently
        /// blocks the attack sequence from restarting (portals sealed / rest).
        /// Preserves flipX facing.
        /// </summary>
        public void ReturnToIdle()
        {
            EnterRestState();
        }

        /// <summary>Alias for <see cref="ReturnToIdle"/>.</summary>
        public void EnterRestState()
        {
            if (dead)
            {
                return;
            }

            restForced = true;
            sequenceStarted = true;
            phase = Phase.Idle;
            activeFrames = Array.Empty<Sprite>();
            frameTimer = 0f;

            if (playerSight != null)
            {
                playerSight.PlayerSighted -= OnPlayerSighted;
            }

            if (idleEnemy != null)
            {
                idleEnemy.SetPlaybackEnabled(true);
                // Arm first rest-idle cycle watch (collapse listens for IdleCycleCompleted).
                idleEnemy.BeginRestCycleWatch();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flipX;
            }
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (idleEnemy == null)
            {
                idleEnemy = GetComponent<WizardIdleEnemy2D>();
            }

            if (playerSight == null)
            {
                playerSight = GetComponent<WizardPlayerSight2D>();
            }

            if (portalSpawner == null)
            {
                portalSpawner = GetComponent<WizardPortalSpawner2D>();
            }

            health = GetComponent<EnemyHealth>();
            ResolveLoopIndices();
        }

        private void OnEnable()
        {
            if (playerSight != null)
            {
                playerSight.PlayerSighted += OnPlayerSighted;
            }

            if (health != null)
            {
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (playerSight != null)
            {
                playerSight.PlayerSighted -= OnPlayerSighted;
            }

            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (dead || phase == Phase.Idle || activeFrames == null || activeFrames.Length == 0)
            {
                return;
            }

            // Keep facing inverted like idle for every attack frame, including loop 17–19.
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = flipX;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (!AdvanceFrame())
                {
                    return;
                }
            }
        }

        private void OnPlayerSighted()
        {
            if (restForced || sequenceStarted || dead)
            {
                return;
            }

            BeginAttackSequence();
        }

        private void OnDied()
        {
            dead = true;
            phase = Phase.Idle;
            activeFrames = Array.Empty<Sprite>();
        }

        private void BeginAttackSequence()
        {
            if (restForced || dead)
            {
                return;
            }

            sequenceStarted = true;
            if (idleEnemy != null)
            {
                idleEnemy.SetPlaybackEnabled(false);
            }

            // Fallback: force portals as soon as attack starts (in case Attack1 never finishes).
            RequestPortals();

            if (attack1Frames == null || attack1Frames.Length == 0)
            {
                Debug.LogWarning("WizardAttackAnimator2D: no Attack1 frames.", this);
                TransitionToAttack2AndRequestPortals();
                return;
            }

            BeginPhase(Phase.Attack1, attack1Frames, startIndex: 0);
        }

        private void BeginPhase(Phase next, Sprite[] frames, int startIndex)
        {
            phase = next;
            activeFrames = frames ?? Array.Empty<Sprite>();
            frameIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, activeFrames.Length - 1));
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        private bool AdvanceFrame()
        {
            switch (phase)
            {
                case Phase.Attack1:
                    if (frameIndex >= activeFrames.Length - 1)
                    {
                        TransitionToAttack2AndRequestPortals();
                        return phase != Phase.Idle;
                    }

                    frameIndex++;
                    ApplyCurrentFrame();
                    return true;

                case Phase.Attack2:
                    if (frameIndex >= activeFrames.Length - 1)
                    {
                        BeginAttack2Loop();
                        return phase == Phase.Attack2Loop;
                    }

                    frameIndex++;
                    ApplyCurrentFrame();
                    return true;

                case Phase.Attack2Loop:
                    AdvanceLoopFrame();
                    ApplyCurrentFrame();
                    return true;

                default:
                    return false;
            }
        }

        private void TransitionToAttack2AndRequestPortals()
        {
            RequestPortals();

            if (attack2Frames == null || attack2Frames.Length == 0)
            {
                Debug.LogWarning("WizardAttackAnimator2D: no Attack2 frames.", this);
                phase = Phase.Idle;
                activeFrames = Array.Empty<Sprite>();
                return;
            }

            sequenceStarted = true;
            if (idleEnemy != null)
            {
                idleEnemy.SetPlaybackEnabled(false);
            }

            BeginPhase(Phase.Attack2, attack2Frames, startIndex: 0);
        }

        private void RequestPortals()
        {
            if (portalsRequested)
            {
                return;
            }

            portalsRequested = true;

            if (portalSpawner == null)
            {
                portalSpawner = GetComponent<WizardPortalSpawner2D>();
            }

            // Direct call is more reliable than event-only wiring.
            if (portalSpawner != null)
            {
                portalSpawner.SpawnPortals();
            }

            PortalsRequested?.Invoke();
        }

        private void BeginAttack2Loop()
        {
            ResolveLoopIndices();
            if (attack2Frames == null || attack2Frames.Length == 0)
            {
                phase = Phase.Idle;
                return;
            }

            phase = Phase.Attack2Loop;
            activeFrames = attack2Frames;
            frameIndex = loopStartIndex;
            loopDirection = 1;
            frameTimer = 0f;
            ApplyCurrentFrame();
        }

        private void AdvanceLoopFrame()
        {
            int next = frameIndex + loopDirection;
            if (next > loopEndIndex || next < loopStartIndex)
            {
                loopDirection = -loopDirection;
                next = frameIndex + loopDirection;
            }

            frameIndex = Mathf.Clamp(next, loopStartIndex, loopEndIndex);
        }

        private void ApplyCurrentFrame()
        {
            if (spriteRenderer == null || activeFrames == null || activeFrames.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(frameIndex, 0, activeFrames.Length - 1);
            Sprite sprite = activeFrames[index];
            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }

            spriteRenderer.flipX = flipX;
        }

        private void ResolveLoopIndices()
        {
            if (attack2Frames == null || attack2Frames.Length == 0)
            {
                loopStartIndex = 0;
                loopEndIndex = 0;
                return;
            }

            int startNumber = Mathf.Min(loopStartFrameNumber, loopEndFrameNumber);
            int endNumber = Mathf.Max(loopStartFrameNumber, loopEndFrameNumber);

            loopStartIndex = FindFrameIndexBySourceNumber(attack2Frames, startNumber);
            loopEndIndex = FindFrameIndexBySourceNumber(attack2Frames, endNumber);

            if (loopStartIndex < 0)
            {
                loopStartIndex = Mathf.Clamp(startNumber - 1, 0, attack2Frames.Length - 1);
            }

            if (loopEndIndex < 0)
            {
                loopEndIndex = Mathf.Clamp(endNumber - 1, 0, attack2Frames.Length - 1);
            }

            if (loopEndIndex < loopStartIndex)
            {
                int swap = loopStartIndex;
                loopStartIndex = loopEndIndex;
                loopEndIndex = swap;
            }
        }

        private static int FindFrameIndexBySourceNumber(Sprite[] frames, int sourceNumber)
        {
            string needle = sourceNumber.ToString("D3");
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null)
                {
                    continue;
                }

                string name = frames[i].name;
                if (name.EndsWith("_" + needle, StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Prefer exact _NNN suffix match if possible.
                    if (name.EndsWith("_" + needle, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(needle, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            // Fallback: 1-based index into ordered array.
            int fallback = sourceNumber - 1;
            if (fallback >= 0 && fallback < frames.Length)
            {
                return fallback;
            }

            return -1;
        }
    }
}
