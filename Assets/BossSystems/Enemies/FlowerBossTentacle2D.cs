using Castlevania2D.Combat;
using Castlevania2D.Input;
using Castlevania2D.Movement;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    public enum FlowerBossTentacleSurface
    {
        Ground,
        LeftWall,
        RightWall
    }

    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class FlowerBossTentacle2D : MonoBehaviour, IDamageable
    {
        private const int MaxHitPoints = 1;

        [SerializeField] private float frameRate = 12f;
        [SerializeField] private float deathAnimationSpeedMultiplier = 2f;
        [SerializeField] private int damage = 5;
        [SerializeField] private LayerMask targetMask;

        private float baseFrameRate = 12f;

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D damageCollider;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private FlowerBossTentacleSurface surface;
        private int frameIndex;
        private int direction = 1;
        private float frameTimer;
        private bool isPlaying;
        private bool canDealDamage;
        private bool hasDealtDamage;
        private bool isDying;
        private int currentHealth = MaxHitPoints;

        public bool CanReceiveDamage => !isDying && currentHealth > 0 && isPlaying;

        public void Initialize(
            Sprite[] animationFrames,
            FlowerBossTentacleSurface spawnSurface,
            float animationFrameRate,
            int contactDamageAmount,
            LayerMask damageTargetMask)
        {
            CacheComponents();

            frames = animationFrames ?? System.Array.Empty<Sprite>();
            surface = spawnSurface;
            baseFrameRate = Mathf.Max(1f, animationFrameRate);
            frameRate = baseFrameRate;
            damage = Mathf.Max(1, contactDamageAmount);
            targetMask = damageTargetMask;

            if (frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            ApplySurfaceOrientation();
            BeginAnimation();
        }

        private void Awake()
        {
            CacheComponents();
            damageCollider.isTrigger = true;
        }

        private void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (damageCollider == null)
            {
                damageCollider = GetComponent<BoxCollider2D>();
            }
        }

        private void Update()
        {
            if (!isPlaying || frames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / frameRate;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (!AdvanceFrame())
                {
                    return;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // First contact only (e.g. spawn already overlapping the player).
            TryDamage(other);
        }

        private void ApplySurfaceOrientation()
        {
            // Left arena wall grows right (+X); right arena wall grows left (-X).
            transform.localRotation = surface switch
            {
                FlowerBossTentacleSurface.LeftWall => Quaternion.Euler(0f, 0f, -90f),
                FlowerBossTentacleSurface.RightWall => Quaternion.Euler(0f, 0f, 90f),
                _ => Quaternion.identity
            };
        }

        public DamageResult ReceiveDamage(DamageInfo damageInfo)
        {
            if (!CanReceiveDamage || damageInfo.Amount <= 0)
            {
                return DamageResult.Ignored;
            }

            // Any melee (or other) hit is lethal: tentacles are 1 HP.
            currentHealth = 0;
            BeginDying();
            return DamageResult.Applied;
        }

        private void BeginDying()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;
            canDealDamage = false;
            if (damageCollider != null)
            {
                damageCollider.enabled = false;
            }

            float speedMultiplier = Mathf.Max(1f, deathAnimationSpeedMultiplier);
            frameRate = baseFrameRate * speedMultiplier;

            // Keep isPlaying true so Update finishes the remaining ping-pong frames.
        }

        private void BeginAnimation()
        {
            frameIndex = 0;
            direction = 1;
            frameTimer = 0f;
            isPlaying = true;
            isDying = false;
            currentHealth = MaxHitPoints;
            canDealDamage = true;
            hasDealtDamage = false;
            ApplyFrame(0);
            damageCollider.enabled = true;
        }

        private bool AdvanceFrame()
        {
            if (frames.Length == 1)
            {
                Finish();
                return false;
            }

            int nextIndex = frameIndex + direction;
            if (nextIndex >= frames.Length)
            {
                direction = -1;
                nextIndex = frames.Length - 2;
            }
            else if (nextIndex < 0)
            {
                Finish();
                return false;
            }

            frameIndex = nextIndex;
            ApplyFrame(frameIndex);
            return true;
        }

        private void ApplyFrame(int index)
        {
            spriteRenderer.sprite = frames[index];
            if (!isDying)
            {
                SyncDamageColliderToSprite();
            }
        }

        private void SyncDamageColliderToSprite()
        {
            if (damageCollider == null || spriteRenderer.sprite == null)
            {
                return;
            }

            // Local sprite bounds with bottom-center pivot match the visible tentacle.
            Bounds bounds = spriteRenderer.sprite.bounds;
            damageCollider.size = bounds.size;
            damageCollider.offset = bounds.center;
            damageCollider.isTrigger = true;
        }

        private void TryDamage(Collider2D other)
        {
            // One full ping-pong = one attack: at most one hit of damage for this tentacle instance.
            if (isDying || !canDealDamage || hasDealtDamage || !TryGetPlayerDamageable(other, out IDamageable damageable, out GameObject playerRoot))
            {
                return;
            }

            if (targetMask.value != 0 && (targetMask.value & (1 << playerRoot.layer)) == 0)
            {
                return;
            }

            if (!damageable.CanReceiveDamage)
            {
                return;
            }

            hasDealtDamage = true;
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitDirection = ((Vector2)playerRoot.transform.position - (Vector2)transform.position).normalized;
            damageable.ReceiveDamage(new DamageInfo(damage, gameObject, hitPoint, hitDirection));
        }

        private static bool TryGetPlayerDamageable(Collider2D other, out IDamageable damageable, out GameObject playerRoot)
        {
            damageable = null;
            playerRoot = null;

            // Prototype player is HeroKnight (no PlayerInputReader / CharacterMotor2D on the demo hero).
            HeroKnight hero = other.GetComponentInParent<HeroKnight>();
            if (hero != null)
            {
                playerRoot = hero.gameObject;
            }
            else
            {
                PlayerInputReader inputReader = other.GetComponentInParent<PlayerInputReader>();
                if (inputReader != null)
                {
                    playerRoot = inputReader.gameObject;
                }
                else
                {
                    CharacterMotor2D motor = other.GetComponentInParent<CharacterMotor2D>();
                    if (motor != null)
                    {
                        playerRoot = motor.gameObject;
                    }
                }
            }

            if (playerRoot == null)
            {
                return false;
            }

            damageable = playerRoot.GetComponentInParent<IDamageable>();
            return damageable != null;
        }

        private void Finish()
        {
            isPlaying = false;
            canDealDamage = false;
            isDying = true;
            if (damageCollider != null)
            {
                damageCollider.enabled = false;
            }

            Destroy(gameObject);
        }

        public static FlowerBossTentacle2D Spawn(
            Transform parent,
            Vector3 worldPosition,
            Sprite[] animationFrames,
            FlowerBossTentacleSurface spawnSurface,
            float animationFrameRate,
            int contactDamageAmount,
            LayerMask damageTargetMask,
            float visualScale = 0.28f)
        {
            GameObject tentacleObject = new GameObject("FlowerBoss_Tentacle");

            SpriteRenderer renderer = tentacleObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = spawnSurface == FlowerBossTentacleSurface.Ground ? 2 : 1;

            Rigidbody2D body = tentacleObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = tentacleObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            FlowerBossTentacle2D tentacle = tentacleObject.AddComponent<FlowerBossTentacle2D>();
            tentacle.transform.SetParent(parent, worldPositionStays: true);
            tentacle.transform.position = worldPosition;
            tentacle.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
            tentacle.Initialize(animationFrames, spawnSurface, animationFrameRate, contactDamageAmount, damageTargetMask);
            return tentacle;
        }
    }
}
