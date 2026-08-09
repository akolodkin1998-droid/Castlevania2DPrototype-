using Castlevania2D.Combat;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Plays Mushomor hurt frames once on Health.Damaged, then yields the SpriteRenderer.
    /// On Died: disable contact/colliders, play death frames once @ deathFrameRate, then Destroy
    /// (destroyOnDeath stays false — we destroy from here after the death clip).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MushomorHurtAnimator2D : MonoBehaviour
    {
        [Header("Hurt Animation")]
        [SerializeField] private Sprite[] hurtFrames;
        [SerializeField] private float frameRate = 10f;

        [Header("Death Animation")]
        [SerializeField] private Sprite[] deathFrames;
        [SerializeField] private float deathFrameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private EnemyHealth health;
        private ContactDamage2D contactDamage;
        private Collider2D bodyCollider;
        private Rigidbody2D body;
        private MushomorMovement2D movement;
        private MushomorMeleeAttack2D attack;

        private bool playingHurt;
        private bool playingDeath;
        private bool dead;
        private int frameIndex;
        private float frameTimer;

        public bool IsPlayingHurt => playingHurt;
        public bool IsPlayingDeath => playingDeath;

#if UNITY_EDITOR
        public void EditorAssignHurtFrames(Sprite[] frames)
        {
            hurtFrames = frames ?? System.Array.Empty<Sprite>();
        }

        public void EditorAssignDeathFrames(Sprite[] frames)
        {
            deathFrames = frames ?? System.Array.Empty<Sprite>();
        }
#endif

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            health = GetComponent<EnemyHealth>();
            contactDamage = GetComponent<ContactDamage2D>();
            bodyCollider = GetComponent<Collider2D>();
            body = GetComponent<Rigidbody2D>();
            movement = GetComponent<MushomorMovement2D>();
            attack = GetComponent<MushomorMeleeAttack2D>();
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
            if (playingDeath)
            {
                AdvanceDeath();
                return;
            }

            if (!playingHurt || hurtFrames == null || hurtFrames.Length == 0)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= hurtFrames.Length)
                {
                    EndHurt();
                    return;
                }

                if (spriteRenderer != null && hurtFrames[frameIndex] != null)
                {
                    spriteRenderer.sprite = hurtFrames[frameIndex];
                }
            }
        }

        private void OnDamaged(DamageInfo _)
        {
            // Health reduces CurrentHealth before Damaged; do not gate on IsAlive or killing blows skip hurt.
            if (dead || playingDeath || hurtFrames == null || hurtFrames.Length == 0)
            {
                return;
            }

            if (attack != null)
            {
                attack.CancelAttack();
            }

            playingHurt = true;
            frameIndex = 0;
            frameTimer = 0f;
            if (spriteRenderer != null && hurtFrames[0] != null)
            {
                spriteRenderer.sprite = hurtFrames[0];
                spriteRenderer.color = Color.white;
            }
        }

        private void OnDied()
        {
            if (dead)
            {
                return;
            }

            dead = true;
            EnemyDeathEvents.Raise(EnemyDeathKind.Mushomor);

            if (movement != null)
            {
                movement.SetDead(true);
            }

            if (attack != null)
            {
                attack.SetDead(true);
                attack.CancelAttack();
            }

            if (contactDamage != null)
            {
                contactDamage.enabled = false;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                body.simulated = false;
            }

            BeginDeath();
        }

        private void BeginDeath()
        {
            playingHurt = false;

            if (deathFrames == null || deathFrames.Length == 0)
            {
                FinishDeath();
                return;
            }

            playingDeath = true;
            frameIndex = 0;
            frameTimer = 0f;
            if (spriteRenderer != null && deathFrames[0] != null)
            {
                spriteRenderer.sprite = deathFrames[0];
                spriteRenderer.color = Color.white;
            }
        }

        private void AdvanceDeath()
        {
            if (deathFrames == null || deathFrames.Length == 0)
            {
                FinishDeath();
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, deathFrameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;
                if (frameIndex >= deathFrames.Length)
                {
                    FinishDeath();
                    return;
                }

                if (spriteRenderer != null && deathFrames[frameIndex] != null)
                {
                    spriteRenderer.sprite = deathFrames[frameIndex];
                }
            }
        }

        private void FinishDeath()
        {
            playingDeath = false;
            // destroyOnDeath is false on the prefab; we own cleanup after the death clip.
            Destroy(gameObject);
        }

        private void EndHurt()
        {
            playingHurt = false;
            frameIndex = 0;
            frameTimer = 0f;

            if (dead)
            {
                BeginDeath();
                return;
            }

            if (movement != null)
            {
                movement.RestoreIdleSprite();
            }
        }
    }
}
