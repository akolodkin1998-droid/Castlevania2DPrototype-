using Castlevania2D.Combat;
using EnemyHealth = Castlevania2D.Health.Health;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>Temporarily owns the mouse renderer while its hurt animation plays.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MouseHurtAnimator2D : MonoBehaviour
    {
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private float frameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private EnemyHealth health;
        private MouseIdleEnemy2D idleAnimation;
        private MouseDiveAttack2D diveAttack;
        private bool isPlayingHurt;
        private bool isDead;
        private int frameIndex;
        private float frameTimer;

        public bool IsPlayingHurt => isPlayingHurt;

        /// <summary>Stops the hurt clip permanently while the death animation owns the renderer.</summary>
        public void SetDead()
        {
            isDead = true;
            isPlayingHurt = false;
        }

#if UNITY_EDITOR
        public void EditorAssignHurtFrames(Sprite[] frames)
        {
            hurtFrames = frames ?? System.Array.Empty<Sprite>();
        }
#endif

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<EnemyHealth>();
            idleAnimation = GetComponent<MouseIdleEnemy2D>();
            diveAttack = GetComponent<MouseDiveAttack2D>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (!isPlayingHurt)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= hurtFrames.Length)
                {
                    EndHurt();
                    return;
                }

                ApplyFrame();
            }
        }

        private void OnDamaged(DamageInfo _)
        {
            if (isDead || hurtFrames == null || hurtFrames.Length == 0)
            {
                return;
            }

            idleAnimation?.SetAnimationPlaying(false);
            diveAttack?.PauseForHurt();
            isPlayingHurt = true;
            frameIndex = 0;
            frameTimer = 0f;
            ApplyFrame();
        }

        private void OnDied()
        {
            SetDead();
        }

        private void EndHurt()
        {
            isPlayingHurt = false;
            frameIndex = 0;
            frameTimer = 0f;

            if (!isDead)
            {
                diveAttack?.ResumeAfterHurt();
            }
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || hurtFrames[frameIndex] == null)
            {
                return;
            }

            spriteRenderer.sprite = hurtFrames[frameIndex];
        }
    }
}
