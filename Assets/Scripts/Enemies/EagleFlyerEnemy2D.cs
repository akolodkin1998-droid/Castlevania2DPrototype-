using Castlevania2D.Combat;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Eagle attack loop:
    /// fly at fixed world Y across the screen on flight anim → near player play attack windup →
    /// fire projectile on attack frame 9 → exit off-screen on frames 16..26 →
    /// re-enter from the opposite side and repeat.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EagleFlyerEnemy2D : MonoBehaviour
    {
        private enum Phase
        {
            Cruise,
            Attack,
            Exit,
        }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Flight")]
        [SerializeField] private Sprite[] flightFrames;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool pingPongFlight = true;
        [SerializeField] private float cruiseSpeed = 4.5f;
        [SerializeField] private float exitSpeed = 7.5f;
        [Tooltip("Fixed world Y for cruise/exit flight. Not tied to player or camera height.")]
        [SerializeField] private float cruiseWorldY = -32f;
        [SerializeField] private float offscreenPadding = 2.5f;
        [SerializeField] private float attackTriggerDistanceX = 2.8f;

        [Header("Attack")]
        [SerializeField] private Sprite[] attackApproachFrames;
        [SerializeField] private Sprite[] attackExitFrames;
        [Tooltip("Index in attackApproachFrames that fires the projectile (frame 9).")]
        [SerializeField] private int projectileFireFrameIndex = -1;
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private float projectileSpeed = 7f;
        [SerializeField] private float projectileScale = 0.12f;
        [SerializeField] private float projectileColliderRadius = 0.12f;
        [SerializeField] private int projectileSortingOrder = 5;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.2f, -0.15f);

        [Header("Combat")]
        [SerializeField] private int contactDamage = 10;

        [Header("Basket Assault")]
        [Tooltip("When true and player X >= basketModeMinPlayerX, eagle does not attack the player.")]
        [SerializeField] private bool basketModeEnabled = true;
        [SerializeField] private float basketModeMinPlayerX = 54f;
        [Tooltip("How close (world X) the eagle must be to the basket to start a kill-reaction throw.")]
        [SerializeField] private float basketAttackTriggerDistanceX = 11f;

        [Header("Player Attack Gate")]
        [Tooltip("Eagle does not throw stones at the player while player world Y is below this value.")]
        [SerializeField] private float minPlayerAttackWorldY = -60.5f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Collider2D bodyCollider;
        private EnemyHealth health;
        private ContactDamage2D contact;
        private Camera viewCamera;

        private Phase phase = Phase.Cruise;
        private int passDirection = 1;
        private int frameIndex;
        private int frameDirection = 1;
        private float frameTimer;
        private bool dead;
        private bool firedProjectile;
        private bool started;
        private Transform throwAimTarget;
        private Transform pendingBasket;
        private bool pendingBasketThrow;

        public bool IsBasketModeActive
        {
            get
            {
                if (!basketModeEnabled)
                {
                    return false;
                }

                ResolveTargetIfNeeded();
                return target != null && target.position.x >= basketModeMinPlayerX;
            }
        }

        private bool CanThrowAtPlayer()
        {
            ResolveTargetIfNeeded();
            return target != null && target.position.y >= minPlayerAttackWorldY;
        }

        public void ConfigureBasketMode(float minPlayerX, bool enabled)
        {
            basketModeMinPlayerX = minPlayerX;
            basketModeEnabled = enabled;
        }

        public void QueueBasketThrow(Transform basket)
        {
            if (basket == null || dead)
            {
                return;
            }

            pendingBasket = basket;
            pendingBasketThrow = true;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void EditorAssignSprites(
            Sprite[] flight,
            Sprite[] attackApproach,
            Sprite[] attackExit,
            Sprite projectile,
            int fireFrameIndex)
        {
            flightFrames = flight;
            attackApproachFrames = attackApproach;
            attackExitFrames = attackExit;
            projectileSprite = projectile;
            projectileFireFrameIndex = fireFrameIndex;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            bodyCollider = GetComponent<Collider2D>();
            health = GetComponent<EnemyHealth>();
            contact = GetComponent<ContactDamage2D>();
            viewCamera = Camera.main;

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (flightFrames != null && flightFrames.Length > 0 && spriteRenderer != null)
            {
                spriteRenderer.sprite = flightFrames[0];
                SyncColliderToSprite();
            }
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

        private void Start()
        {
            ResolveTargetIfNeeded();
            BeginPassFromNearestSide();
            started = true;
        }

        private void Update()
        {
            if (dead || !started)
            {
                return;
            }

            AdvanceAnimation(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (dead || !started)
            {
                return;
            }

            ResolveTargetIfNeeded();
            MovePass(Time.fixedDeltaTime);
        }

        private void BeginPassFromNearestSide()
        {
            GetCameraBounds(out float minX, out float maxX, out float cruiseY);

            float playerX = target != null ? target.position.x : (minX + maxX) * 0.5f;
            float distLeft = Mathf.Abs(playerX - (minX - offscreenPadding));
            float distRight = Mathf.Abs((maxX + offscreenPadding) - playerX);
            // Start from the farther side so the first pass crosses over the player.
            passDirection = distLeft >= distRight ? 1 : -1;

            float startX = passDirection > 0 ? minX - offscreenPadding : maxX + offscreenPadding;
            body.position = new Vector2(startX, cruiseY);
            transform.position = new Vector3(startX, cruiseY, transform.position.z);
            ApplyFacing();
            EnterCruise();
        }

        private void BeginPassFromOppositeSide()
        {
            passDirection *= -1;
            GetCameraBounds(out float minX, out float maxX, out float cruiseY);
            float startX = passDirection > 0 ? minX - offscreenPadding : maxX + offscreenPadding;
            body.position = new Vector2(startX, cruiseY);
            transform.position = new Vector3(startX, cruiseY, transform.position.z);
            ApplyFacing();
            EnterCruise();
        }

        private void EnterCruise()
        {
            phase = Phase.Cruise;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            firedProjectile = false;
            if (flightFrames != null && flightFrames.Length > 0)
            {
                spriteRenderer.sprite = flightFrames[0];
                SyncColliderToSprite();
            }
        }

        private void EnterAttack()
        {
            phase = Phase.Attack;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            firedProjectile = false;
            if (attackApproachFrames != null && attackApproachFrames.Length > 0)
            {
                spriteRenderer.sprite = attackApproachFrames[0];
                SyncColliderToSprite();
                MaybeFireProjectile();
            }
            else
            {
                EnterExit();
            }
        }

        private void EnterExit()
        {
            phase = Phase.Exit;
            frameIndex = 0;
            frameDirection = 1;
            frameTimer = 0f;
            if (attackExitFrames != null && attackExitFrames.Length > 0)
            {
                spriteRenderer.sprite = attackExitFrames[0];
                SyncColliderToSprite();
            }
        }

        private void AdvanceAnimation(float deltaTime)
        {
            Sprite[] frames = GetActiveFrames();
            if (frames == null || frames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;

                if (phase == Phase.Cruise)
                {
                    AdvanceCruiseFrame(frames);
                }
                else if (phase == Phase.Attack)
                {
                    AdvanceAttackFrame(frames);
                }
                else
                {
                    AdvanceExitFrame(frames);
                }
            }
        }

        private Sprite[] GetActiveFrames()
        {
            switch (phase)
            {
                case Phase.Attack:
                    return attackApproachFrames;
                case Phase.Exit:
                    return attackExitFrames;
                default:
                    return flightFrames;
            }
        }

        private void AdvanceCruiseFrame(Sprite[] frames)
        {
            if (frames.Length == 1)
            {
                spriteRenderer.sprite = frames[0];
                return;
            }

            if (pingPongFlight)
            {
                frameIndex += frameDirection;
                if (frameIndex >= frames.Length - 1)
                {
                    frameIndex = frames.Length - 1;
                    frameDirection = -1;
                }
                else if (frameIndex <= 0)
                {
                    frameIndex = 0;
                    frameDirection = 1;
                }
            }
            else
            {
                frameIndex = (frameIndex + 1) % frames.Length;
            }

            spriteRenderer.sprite = frames[frameIndex];
            SyncColliderToSprite();
        }

        private void AdvanceAttackFrame(Sprite[] frames)
        {
            if (frameIndex >= frames.Length - 1)
            {
                EnterExit();
                return;
            }

            frameIndex++;
            spriteRenderer.sprite = frames[frameIndex];
            SyncColliderToSprite();
            MaybeFireProjectile();

            if (frameIndex >= frames.Length - 1)
            {
                // Hold last approach frame until next tick swaps to exit.
            }
        }

        private void AdvanceExitFrame(Sprite[] frames)
        {
            if (frames.Length == 0)
            {
                return;
            }

            if (frameIndex < frames.Length - 1)
            {
                frameIndex++;
                spriteRenderer.sprite = frames[frameIndex];
                SyncColliderToSprite();
            }
        }

        private void MaybeFireProjectile()
        {
            if (firedProjectile || attackApproachFrames == null)
            {
                return;
            }

            int fireIndex = projectileFireFrameIndex;
            if (fireIndex < 0 || fireIndex >= attackApproachFrames.Length)
            {
                return;
            }

            if (frameIndex != fireIndex)
            {
                return;
            }

            firedProjectile = true;
            SpawnProjectile();
        }

        private void SpawnProjectile()
        {
            if (projectileSprite == null)
            {
                return;
            }

            Transform aim = throwAimTarget != null ? throwAimTarget : target;
            Vector2 toAim = aim != null
                ? ((Vector2)aim.position - (Vector2)transform.position)
                : new Vector2(passDirection, -1f);
            if (toAim.sqrMagnitude < 0.0001f)
            {
                toAim = new Vector2(passDirection, -1f);
            }

            float facingSign = passDirection >= 0 ? 1f : -1f;
            Vector3 spawnPosition = transform.position + new Vector3(
                projectileSpawnOffset.x * facingSign,
                projectileSpawnOffset.y,
                0f);

            GameObject projectileObject = new GameObject("EagleProjectile");
            projectileObject.transform.position = spawnPosition;
            projectileObject.transform.localScale = new Vector3(projectileScale, projectileScale, 1f);

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            projectileRenderer.sprite = projectileSprite;
            projectileRenderer.sortingOrder = projectileSortingOrder;
            projectileRenderer.flipX = toAim.x > 0f;

            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = projectileColliderRadius;

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            projectileBody.freezeRotation = true;

            EnemyProjectile2D projectile = projectileObject.AddComponent<EnemyProjectile2D>();
            projectile.Launch(gameObject, toAim.normalized, projectileDamage, projectileSpeed);

            if (throwAimTarget != null)
            {
                pendingBasketThrow = false;
                pendingBasket = null;
                throwAimTarget = null;
            }
        }

        private void MovePass(float deltaTime)
        {
            GetCameraBounds(out float minX, out float maxX, out float cruiseY);
            float speed = phase == Phase.Exit ? exitSpeed : cruiseSpeed;
            Vector2 current = body.position;
            Vector2 next = new Vector2(current.x + passDirection * speed * deltaTime, cruiseY);
            body.MovePosition(next);
            ApplyFacing();

            if (phase == Phase.Cruise)
            {
                if (IsBasketModeActive)
                {
                    TryEnterBasketAttack(next.x);
                }
                else if (CanThrowAtPlayer())
                {
                    float distanceX = Mathf.Abs(target.position.x - next.x);
                    bool approaching =
                        (passDirection > 0 && next.x <= target.position.x) ||
                        (passDirection < 0 && next.x >= target.position.x);

                    if (approaching && distanceX <= attackTriggerDistanceX)
                    {
                        throwAimTarget = null;
                        EnterAttack();
                    }
                }
            }

            bool offscreen = passDirection > 0
                ? next.x > maxX + offscreenPadding
                : next.x < minX - offscreenPadding;

            if (offscreen && (phase == Phase.Exit || phase == Phase.Cruise))
            {
                // If cruise somehow never attacked, still recycle pass.
                BeginPassFromOppositeSide();
            }
        }

        private void TryEnterBasketAttack(float eagleX)
        {
            if (!pendingBasketThrow || pendingBasket == null)
            {
                return;
            }

            float distanceX = Mathf.Abs(pendingBasket.position.x - eagleX);
            bool approaching =
                (passDirection > 0 && eagleX <= pendingBasket.position.x) ||
                (passDirection < 0 && eagleX >= pendingBasket.position.x);

            // Also allow a throw if we are already near the basket (pass may start mid-screen).
            bool nearEnough = distanceX <= basketAttackTriggerDistanceX;
            if ((approaching && nearEnough) || distanceX <= basketAttackTriggerDistanceX * 0.5f)
            {
                throwAimTarget = pendingBasket;
                EnterAttack();
            }
        }

        private void GetCameraBounds(out float minX, out float maxX, out float cruiseY)
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            cruiseY = cruiseWorldY;

            if (viewCamera == null || !viewCamera.orthographic)
            {
                Vector3 fallbackCenter = target != null ? target.position : transform.position;
                minX = fallbackCenter.x - 10f;
                maxX = fallbackCenter.x + 10f;
                return;
            }

            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            Vector3 cam = viewCamera.transform.position;
            minX = cam.x - halfWidth;
            maxX = cam.x + halfWidth;
        }

        private void ApplyFacing()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            // Source art faces left; flip when flying right.
            spriteRenderer.flipX = passDirection > 0;
        }

        private void ResolveTargetIfNeeded()
        {
            if (target != null)
            {
                return;
            }

            GameObject player = GameObject.Find(playerObjectName);
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void OnDamaged(DamageInfo damage)
        {
        }

        private void OnDied()
        {
            dead = true;
            if (body != null)
            {
                body.simulated = false;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (contact != null)
            {
                contact.enabled = false;
            }
        }

        private void SyncColliderToSprite()
        {
            if (bodyCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            if (bodyCollider is BoxCollider2D box)
            {
                Bounds bounds = spriteRenderer.sprite.bounds;
                box.size = bounds.size * 0.7f;
                box.offset = bounds.center;
            }
        }
    }
}
