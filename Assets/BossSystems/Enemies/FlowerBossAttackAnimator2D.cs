using System;
using System.Collections.Generic;
using Castlevania2D.Combat;
using Castlevania2D.Save;
using UnityEngine;
using BossHealthComponent = global::Castlevania2D.Health.Health;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// First boss hit: play Attack1 once, then Attack2 ping-pong until death.
    /// On Health.Died: stop Attack2/projectiles, play Death once (scaled to Idle visual
    /// height so larger reincarnation canvases do not blow up), then disable Boss_Flower
    /// (GrassCover goes with it; destroyOnDeath stays false so we control disappearance).
    /// Lives in Assembly-CSharp (BossSystems) like tentacle attack — outside Castlevania2D.asmdef.
    /// Takes over SpriteRenderer from Idle Animator after the first hit.
    /// Compensates Idle vs Attack pivot/canvas mismatch so the stem stays where Idle stood
    /// without shifting the etalon hitbox or GrassCover in world space.
    /// Attack2 frame FlowerBoss_Attack2_050 launches yellow Evil Wizard projectiles
    /// in random directions; count scales with HP lost (base + 1 per N HP).
    /// </summary>
    public sealed class FlowerBossAttackAnimator2D : MonoBehaviour, IBossSaveState
    {
        private const string Attack1SpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Attack1";
        private const string Attack2SpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Attack2";
        private const string DeathSpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Death";
        private const string Attack1Prefix = "FlowerBoss_Attack1_";
        private const string Attack2Prefix = "FlowerBoss_Attack2_";
        private const string DeathPrefix = "FlowerBoss_Death_";
        private const string Attack2ProjectileFrameSuffix = "_050";
        private const string GrassCoverObjectName = "GrassCover";
        private const int DefaultAttack2ProjectileFrameNumber = 50;

        private enum AttackPhase
        {
            Idle,
            Attack1,
            Attack2PingPong,
            Death
        }

        [Header("Frames")]
        [SerializeField] private Sprite[] attack1Frames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attack2Frames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] deathFrames = System.Array.Empty<Sprite>();

        [Header("Playback")]
        [SerializeField] private float frameRate = 12f;

        [Header("Attack2 Projectiles")]
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private Color projectileColor = new Color(1f, 0.92f, 0.2f, 1f);
        [SerializeField] private int projectileDamage = 10;
        [SerializeField] private float projectileSpeed = 6f;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0f, 1.2f);
        [SerializeField] private int projectileCountBase = 15;
        [SerializeField] private int hpLostPerExtraProjectile = 25;
        [SerializeField] private float projectileScale = 1.4f;
        [SerializeField] private int projectileSortingOrder = 4;
        [SerializeField] private float projectileColliderRadius = 0.18f;
        [Tooltip("1-based Attack2 frame number (FlowerBoss_Attack2_050 → 50). Used when sprite name has no _050.")]
        [SerializeField] private int attack2ProjectileFrameNumber = DefaultAttack2ProjectileFrameNumber;

        [Header("Optional")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator idleAnimator;

        private BossHealthComponent bossHealth;
        private BoxCollider2D hitbox;
        private Transform grassCover;
        private bool isSubscribed;
        private bool hasStartedAttackSequence;
        private bool isAlive = true;
        private AttackPhase phase = AttackPhase.Idle;
        private Sprite[] activeFrames = System.Array.Empty<Sprite>();
        private int frameIndex;
        private int direction = 1;
        private float frameTimer;
        private bool isOnProjectileFrame;
        private int resolvedProjectileFrameIndex = -1;

        private bool hasRootLock;
        private Vector3 rootedWorldPosition;
        private Vector2 rootedVisualRootLocal;
        private float rootedWorldStemY;
        private float rootedVisualWorldHeight;
        private Vector3 homeLocalScale = Vector3.one;
        private Vector3 homeGrassLocalScale = Vector3.one;
        private Vector2 homeHitboxOffset;
        private Vector3 homeGrassLocalPosition;
        private bool hasDisappeared;

        public bool HasStartedAttackSequence => hasStartedAttackSequence;
        public bool HasDisappeared => hasDisappeared;
        private FlowerBossTentacleAttack2D tentacleAttack;

        private void Awake()
        {
            bossHealth = GetComponent<BossHealthComponent>();
            hitbox = GetComponent<BoxCollider2D>();
            tentacleAttack = GetComponent<FlowerBossTentacleAttack2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (idleAnimator == null)
            {
                idleAnimator = GetComponent<Animator>();
            }

            Transform grass = transform.Find(GrassCoverObjectName);
            if (grass != null)
            {
                grassCover = grass;
            }

            // Subscribe in Awake so Health.Died is wired before any early damage.
            Subscribe();
        }

        private void Start()
        {
            EnsureFramesLoaded();
            ResolveProjectileFrameIndex();
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (hasDisappeared || phase == AttackPhase.Idle || activeFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            if (!isAlive && phase != AttackPhase.Death)
            {
                return;
            }

            float safeRate = Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            float frameDuration = 1f / safeRate;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                if (!AdvanceFrame())
                {
                    return;
                }
            }
        }

        public void Configure(Sprite[] attack1, Sprite[] attack2, float playbackFrameRate = 12f)
        {
            Configure(attack1, attack2, deathFrames, playbackFrameRate);
        }

        public void Configure(Sprite[] attack1, Sprite[] attack2, Sprite[] death, float playbackFrameRate = 12f)
        {
            attack1Frames = attack1 ?? System.Array.Empty<Sprite>();
            attack2Frames = attack2 ?? System.Array.Empty<Sprite>();
            deathFrames = death ?? System.Array.Empty<Sprite>();
            frameRate = Mathf.Max(1f, playbackFrameRate);
            ResolveProjectileFrameIndex();
        }

        public void ConfigureProjectile(Sprite sprite, int damage = 10, float speed = 6f)
        {
            projectileSprite = sprite;
            projectileDamage = Mathf.Max(0, damage);
            projectileSpeed = Mathf.Max(0f, speed);
        }

        private void Subscribe()
        {
            if (isSubscribed || bossHealth == null)
            {
                return;
            }

            bossHealth.Damaged += OnBossDamaged;
            bossHealth.Died += OnBossDied;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || bossHealth == null)
            {
                return;
            }

            bossHealth.Damaged -= OnBossDamaged;
            bossHealth.Died -= OnBossDied;
            isSubscribed = false;
        }

        private void OnBossDamaged(DamageInfo _)
        {
            if (hasStartedAttackSequence || !isAlive)
            {
                return;
            }

            StartAttackSequence();
        }

        public void ApplySavedState(bool savedAttackStarted, bool savedDefeated)
        {
            if (savedDefeated)
            {
                isAlive = false;
                hasDisappeared = true;
                StopCombatSystemsForDeath();
                gameObject.SetActive(false);
                return;
            }

            isAlive = true;
            hasDisappeared = false;
            if (savedAttackStarted)
            {
                StartAttackSequence();
            }
        }

        private void OnBossDied()
        {
            if (hasDisappeared || phase == AttackPhase.Death)
            {
                return;
            }

            isAlive = false;
            isOnProjectileFrame = false;
            StartDeathSequence();
        }

        private void StartDeathSequence()
        {
            // Keep this component + Health enabled so Update can finish Death frames.
            // Only combat hitboxes / tentacle spawns are disabled below.
            enabled = true;
            EnsureFramesLoaded();
            deathFrames = CompactSprites(deathFrames);
            StopCombatSystemsForDeath();

            if (idleAnimator != null)
            {
                idleAnimator.enabled = false;
            }

            if (!hasRootLock)
            {
                CaptureIdleRootLock();
            }

            if (!HasValidSprites(deathFrames))
            {
                Debug.LogWarning("FlowerBossAttackAnimator2D: no death sprites assigned; disappearing immediately.");
                DisappearBoss();
                return;
            }

            BeginPhase(AttackPhase.Death, deathFrames);
        }

        private void StopCombatSystemsForDeath()
        {
            // Leave already-spawned tentacles in the world; only stop new waves / stand traps.
            // Do not disable Health or this animator — Died already fired; we need Update for Death.
            if (tentacleAttack != null)
            {
                tentacleAttack.enabled = false;
            }

            if (hitbox != null)
            {
                hitbox.enabled = false;
            }

            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private void DisappearBoss()
        {
            if (hasDisappeared)
            {
                return;
            }

            hasDisappeared = true;
            phase = AttackPhase.Idle;
            activeFrames = System.Array.Empty<Sprite>();
            isOnProjectileFrame = false;
            // Health.destroyOnDeath stays false; we hide the whole boss (GrassCover included).
            gameObject.SetActive(false);
        }

        private void StartAttackSequence()
        {
            if (!isAlive || phase == AttackPhase.Death || hasDisappeared)
            {
                return;
            }

            EnsureFramesLoaded();
            if (attack1Frames.Length == 0 && attack2Frames.Length == 0)
            {
                Debug.LogWarning("FlowerBossAttackAnimator2D: no attack sprites assigned.");
                return;
            }

            hasStartedAttackSequence = true;
            CaptureIdleRootLock();

            if (idleAnimator != null)
            {
                idleAnimator.enabled = false;
            }

            if (attack1Frames.Length > 0)
            {
                BeginPhase(AttackPhase.Attack1, attack1Frames);
            }
            else
            {
                BeginPhase(AttackPhase.Attack2PingPong, attack2Frames);
            }
        }

        private void CaptureIdleRootLock()
        {
            hasRootLock = false;
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            rootedWorldPosition = transform.position;
            rootedVisualRootLocal = GetVisualRootLocal(spriteRenderer);
            rootedWorldStemY = spriteRenderer.bounds.min.y;
            rootedVisualWorldHeight = spriteRenderer.bounds.size.y;
            homeLocalScale = transform.localScale;
            if (hitbox != null)
            {
                homeHitboxOffset = hitbox.offset;
            }

            if (grassCover != null)
            {
                homeGrassLocalPosition = grassCover.localPosition;
                homeGrassLocalScale = grassCover.localScale;
            }

            hasRootLock = true;
        }

        private void BeginPhase(AttackPhase nextPhase, Sprite[] frames)
        {
            phase = nextPhase;
            activeFrames = frames;
            frameIndex = 0;
            direction = 1;
            frameTimer = 0f;
            isOnProjectileFrame = false;
            if (nextPhase == AttackPhase.Attack2PingPong)
            {
                ResolveProjectileFrameIndex();
            }

            ApplyFrame(0);
        }

        private bool AdvanceFrame()
        {
            if (activeFrames.Length == 0)
            {
                return false;
            }

            if (phase == AttackPhase.Death)
            {
                return AdvanceDeathFrame();
            }

            if (activeFrames.Length == 1)
            {
                if (phase == AttackPhase.Attack1)
                {
                    TransitionToAttack2();
                    return phase == AttackPhase.Attack2PingPong;
                }

                ApplyFrame(frameIndex);
                return true;
            }

            if (phase == AttackPhase.Attack1)
            {
                int nextIndex = frameIndex + 1;
                if (nextIndex >= activeFrames.Length)
                {
                    TransitionToAttack2();
                    return phase == AttackPhase.Attack2PingPong;
                }

                frameIndex = nextIndex;
                ApplyFrame(frameIndex);
                return true;
            }

            // Attack2 ping-pong forever until death.
            int pingPongNext = frameIndex + direction;
            if (pingPongNext >= activeFrames.Length)
            {
                direction = -1;
                pingPongNext = activeFrames.Length - 2;
            }
            else if (pingPongNext < 0)
            {
                direction = 1;
                pingPongNext = 1;
            }

            if (pingPongNext < 0)
            {
                pingPongNext = 0;
            }

            frameIndex = pingPongNext;
            ApplyFrame(frameIndex);
            return true;
        }

        private bool AdvanceDeathFrame()
        {
            if (activeFrames.Length <= 1)
            {
                ApplyFrame(0);
                DisappearBoss();
                return false;
            }

            int nextIndex = frameIndex + 1;
            if (nextIndex >= activeFrames.Length)
            {
                DisappearBoss();
                return false;
            }

            frameIndex = nextIndex;
            ApplyFrame(frameIndex);
            return true;
        }

        private void TransitionToAttack2()
        {
            if (attack2Frames.Length == 0)
            {
                phase = AttackPhase.Idle;
                activeFrames = System.Array.Empty<Sprite>();
                return;
            }

            BeginPhase(AttackPhase.Attack2PingPong, attack2Frames);
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null || activeFrames == null ||
                index < 0 || index >= activeFrames.Length || activeFrames[index] == null)
            {
                return;
            }

            ResetRootFollowers();
            RestoreHomeVisualScale();
            if (hasRootLock)
            {
                transform.position = rootedWorldPosition;
            }

            spriteRenderer.sprite = activeFrames[index];

            if (hasRootLock && phase == AttackPhase.Death)
            {
                // Death/reincarnation canvases are often larger than Idle/Attack at the same PPU.
                // Scale visual only (etalon root scale stays 0.375) so Death matches Idle height.
                MatchDeathVisualSizeToReference();
                LockStemToRootedWorldY();
            }
            else if (hasRootLock)
            {
                // Attack sprites were imported bottom-center while Idle uses Center (and canvases differ).
                // Nudge the boss transform so this frame's visual stem/base matches Idle, then counter-shift
                // hitbox + GrassCover so their world pose stays on the Idle etalon.
                Vector2 currentRootLocal = GetVisualRootLocal(spriteRenderer);
                Vector2 localDelta = rootedVisualRootLocal - currentRootLocal;
                if (localDelta.sqrMagnitude >= 0.0000001f)
                {
                    transform.position = rootedWorldPosition + transform.TransformVector(localDelta);

                    if (hitbox != null)
                    {
                        hitbox.offset = homeHitboxOffset - localDelta;
                    }

                    if (grassCover != null)
                    {
                        grassCover.localPosition = homeGrassLocalPosition - (Vector3)localDelta;
                    }
                }
            }

            TryFireProjectilesOnFrameEnter(index);
        }

        private void RestoreHomeVisualScale()
        {
            if (!hasRootLock)
            {
                return;
            }

            transform.localScale = homeLocalScale;
            if (grassCover != null)
            {
                grassCover.localScale = homeGrassLocalScale;
            }
        }

        private void MatchDeathVisualSizeToReference()
        {
            if (rootedVisualWorldHeight <= 0.0001f || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            float currentHeight = spriteRenderer.bounds.size.y;
            if (currentHeight <= 0.0001f)
            {
                return;
            }

            float mul = rootedVisualWorldHeight / currentHeight;
            transform.localScale = new Vector3(
                homeLocalScale.x * mul,
                homeLocalScale.y * mul,
                homeLocalScale.z);

            // Keep GrassCover at etalon world size while the death sprite is scaled.
            if (grassCover != null)
            {
                float inv = 1f / mul;
                grassCover.localScale = new Vector3(
                    homeGrassLocalScale.x * inv,
                    homeGrassLocalScale.y * inv,
                    homeGrassLocalScale.z);
            }

            transform.position = rootedWorldPosition;
        }

        private void LockStemToRootedWorldY()
        {
            float dy = rootedWorldStemY - spriteRenderer.bounds.min.y;
            if (Mathf.Abs(dy) < 0.0000001f)
            {
                return;
            }

            transform.position += new Vector3(0f, dy, 0f);
            Vector2 localDelta = transform.InverseTransformVector(new Vector3(0f, dy, 0f));

            if (hitbox != null)
            {
                hitbox.offset = homeHitboxOffset - localDelta;
            }

            if (grassCover != null)
            {
                grassCover.localPosition = homeGrassLocalPosition - (Vector3)localDelta;
            }
        }

        private void TryFireProjectilesOnFrameEnter(int index)
        {
            if (phase != AttackPhase.Attack2PingPong || !isAlive)
            {
                isOnProjectileFrame = false;
                return;
            }

            bool onProjectileFrame = IsAttack2ProjectileFrame(index);
            if (onProjectileFrame && !isOnProjectileFrame)
            {
                LaunchRadialProjectiles();
            }

            isOnProjectileFrame = onProjectileFrame;
        }

        private bool IsAttack2ProjectileFrame(int index)
        {
            if (attack2Frames == null || index < 0 || index >= attack2Frames.Length)
            {
                return false;
            }

            Sprite frame = attack2Frames[index];
            if (frame != null &&
                frame.name.IndexOf(Attack2ProjectileFrameSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (resolvedProjectileFrameIndex >= 0)
            {
                return index == resolvedProjectileFrameIndex;
            }

            return index + 1 == attack2ProjectileFrameNumber;
        }

        private void ResolveProjectileFrameIndex()
        {
            resolvedProjectileFrameIndex = -1;
            if (attack2Frames == null || attack2Frames.Length == 0)
            {
                return;
            }

            for (int i = 0; i < attack2Frames.Length; i++)
            {
                Sprite frame = attack2Frames[i];
                if (frame != null &&
                    frame.name.IndexOf(Attack2ProjectileFrameSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    resolvedProjectileFrameIndex = i;
                    return;
                }
            }

            int fallbackIndex = attack2ProjectileFrameNumber - 1;
            if (fallbackIndex >= 0 && fallbackIndex < attack2Frames.Length)
            {
                resolvedProjectileFrameIndex = fallbackIndex;
            }
        }

        private void LaunchRadialProjectiles()
        {
            if (projectileSprite == null || projectileCountBase <= 0 || projectileSpeed <= 0f)
            {
                return;
            }

            int count = GetProjectileCountForCurrentHealth();
            Vector3 spawnPosition = transform.position + (Vector3)projectileSpawnOffset;

            for (int i = 0; i < count; i++)
            {
                float angleDegrees = UnityEngine.Random.Range(0f, 360f);
                float angleRadians = angleDegrees * Mathf.Deg2Rad;
                Vector2 launchDirection = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
                SpawnProjectile(spawnPosition, launchDirection);
            }
        }

        /// <summary>
        /// count = base + floor((maxHealth - currentHealth) / hpLostPerExtra).
        /// Recomputed on every Attack2_050 fire so the burst grows as the boss loses HP.
        /// </summary>
        private int GetProjectileCountForCurrentHealth()
        {
            int baseCount = Mathf.Max(1, projectileCountBase);
            int step = Mathf.Max(1, hpLostPerExtraProjectile);
            if (bossHealth == null)
            {
                return baseCount;
            }

            int healthLost = Mathf.Max(0, bossHealth.MaxHealth - bossHealth.CurrentHealth);
            return baseCount + (healthLost / step);
        }

        private void SpawnProjectile(Vector3 spawnPosition, Vector2 launchDirection)
        {
            GameObject projectileObject = new GameObject("FlowerBossProjectile");
            projectileObject.transform.position = spawnPosition;
            float scale = Mathf.Max(0.01f, projectileScale);
            projectileObject.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
            projectileRenderer.sprite = projectileSprite;
            projectileRenderer.color = projectileColor;
            projectileRenderer.sortingOrder = projectileSortingOrder;
            projectileRenderer.flipX = launchDirection.x < 0f;

            CircleCollider2D projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.radius = Mathf.Max(0.05f, projectileColliderRadius);

            Rigidbody2D projectileBody = projectileObject.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            projectileBody.freezeRotation = true;

            EnemyProjectile2D projectile = projectileObject.AddComponent<EnemyProjectile2D>();
            projectile.Launch(gameObject, launchDirection, projectileDamage, projectileSpeed);
        }

        private void ResetRootFollowers()
        {
            if (!hasRootLock)
            {
                return;
            }

            if (hitbox != null)
            {
                hitbox.offset = homeHitboxOffset;
            }

            if (grassCover != null)
            {
                grassCover.localPosition = homeGrassLocalPosition;
            }
        }

        /// <summary>
        /// Visual stem/base in local space: pivot vertical axis (x=0) + bottom of mesh bounds.
        /// Using bounds.center.x would drift horizontally when attack vines change canvas width.
        /// </summary>
        private static Vector2 GetVisualRootLocal(SpriteRenderer renderer)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 worldRoot = new Vector3(
                renderer.transform.position.x,
                worldBounds.min.y,
                renderer.transform.position.z);
            Vector3 localRoot = renderer.transform.InverseTransformPoint(worldRoot);
            return new Vector2(localRoot.x, localRoot.y);
        }

        private void EnsureFramesLoaded()
        {
            attack1Frames = CompactSprites(attack1Frames);
            attack2Frames = CompactSprites(attack2Frames);
            deathFrames = CompactSprites(deathFrames);

            if (!HasValidSprites(attack1Frames) ||
                !HasValidSprites(attack2Frames) ||
                !HasValidSprites(deathFrames))
            {
#if UNITY_EDITOR
                if (!HasValidSprites(attack1Frames))
                {
                    attack1Frames = LoadSpritesFromProject(Attack1SpritesFolder, Attack1Prefix);
                }

                if (!HasValidSprites(attack2Frames))
                {
                    attack2Frames = LoadSpritesFromProject(Attack2SpritesFolder, Attack2Prefix);
                }

                if (!HasValidSprites(deathFrames))
                {
                    deathFrames = LoadSpritesFromProject(DeathSpritesFolder, DeathPrefix);
                }
#endif
            }

            ResolveProjectileFrameIndex();
        }

        private static bool HasValidSprites(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite[] CompactSprites(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            int validCount = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == frames.Length)
            {
                return frames;
            }

            if (validCount == 0)
            {
                return System.Array.Empty<Sprite>();
            }

            Sprite[] compacted = new Sprite[validCount];
            int writeIndex = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    compacted[writeIndex++] = frames[i];
                }
            }

            return compacted;
        }

#if UNITY_EDITOR
        private static Sprite[] LoadSpritesFromProject(string folder, string prefix)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return System.Array.Empty<Sprite>();
            }

            string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            List<(int index, Sprite sprite)> sprites = new List<(int, Sprite)>();

            for (int i = 0; i < spriteGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!fileName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    continue;
                }

                sprites.Add((ExtractFrameNumber(fileName), sprite));
            }

            sprites.Sort((left, right) => left.index.CompareTo(right.index));
            Sprite[] result = new Sprite[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                result[i] = sprites[i].sprite;
            }

            return result;
        }

        private static int ExtractFrameNumber(string fileName)
        {
            int separatorIndex = fileName.LastIndexOf('_');
            if (separatorIndex < 0 || separatorIndex == fileName.Length - 1)
            {
                return 0;
            }

            string indexText = fileName.Substring(separatorIndex + 1);
            return int.TryParse(indexText, out int index) ? index : 0;
        }
#endif
    }
}
