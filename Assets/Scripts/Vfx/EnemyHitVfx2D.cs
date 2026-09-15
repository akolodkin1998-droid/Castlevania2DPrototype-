using System;
using Castlevania2D.Combat;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Vfx
{
    /// <summary>
    /// Plays the shared hit-blood burst on any non-player <see cref="Castlevania2D.Health.Health"/>.
    /// Attaches automatically to current and future enemies; scale follows body size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHitVfx2D : MonoBehaviour
    {
        private const string ResourcePrefix = "Vfx/HitBlood/HitBlood_";
        private const int FrameCount = 5;
        private const float NativeSpriteWorldSize = 64f / 100f;
        private const float BodyCoverage = 0.6f;
        private const float MinScale = 0.45f;
        private const float MaxScale = 3.75f;
        private const float HitToCenterBias = 0.6f;

        [SerializeField] [Min(1f)] private float frameRate = 12f;
        [SerializeField] private int sortingOrderBoost = 5;

        private static Sprite[] frames;
        private static bool framesLoadAttempted;

        private EnemyHealth health;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Bind()
        {
            frames = null;
            framesLoadAttempted = false;
            EnemyHealth.Enabled -= OnHealthEnabled;
            EnemyHealth.Enabled += OnHealthEnabled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachExisting()
        {
            EnemyHealth[] all = UnityEngine.Object.FindObjectsByType<EnemyHealth>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                EnsureOn(all[i]);
            }
        }

        private static void OnHealthEnabled(EnemyHealth spawned)
        {
            EnsureOn(spawned);
        }

        public static void EnsureOn(GameObject enemyRoot)
        {
            if (enemyRoot == null)
            {
                return;
            }

            EnemyHealth health = enemyRoot.GetComponent<EnemyHealth>();
            if (health == null)
            {
                health = enemyRoot.GetComponentInChildren<EnemyHealth>(true);
            }

            EnsureOn(health);
        }

        public static void EnsureOn(EnemyHealth hostHealth)
        {
            if (hostHealth == null || IsPlayerRoot(hostHealth.transform.root))
            {
                return;
            }

            if (hostHealth.GetComponent<EnemyHitVfx2D>() == null)
            {
                hostHealth.gameObject.AddComponent<EnemyHitVfx2D>();
            }
        }

        public static void PlayOn(Component host, DamageInfo damage)
        {
            PlayOn(host, damage, 12f, 5);
        }

        private static void PlayOn(Component host, DamageInfo damage, float playFrameRate, int orderBoost)
        {
            if (host == null)
            {
                return;
            }

            Sprite[] animationFrames = LoadFrames();
            if (animationFrames == null || animationFrames.Length == 0)
            {
                return;
            }

            SpriteRenderer renderer = host.GetComponentInChildren<SpriteRenderer>(true);
            Vector3 position = ResolvePosition(host, renderer, damage);
            float scale = ResolveScale(host, renderer);
            if (damage.Direction.x < 0f)
            {
                scale = -scale;
            }

            var vfxObject = new GameObject("EnemyHitBlood");
            vfxObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            vfxObject.transform.localScale = new Vector3(scale, Mathf.Abs(scale), 1f);

            SpriteRenderer vfxRenderer = vfxObject.AddComponent<SpriteRenderer>();
            if (renderer != null)
            {
                vfxRenderer.sortingLayerID = renderer.sortingLayerID;
                vfxRenderer.sortingOrder = renderer.sortingOrder + Mathf.Max(1, orderBoost);
            }
            else
            {
                vfxRenderer.sortingOrder = 12;
            }

            OneShotSpriteAnimation2D animation = vfxObject.AddComponent<OneShotSpriteAnimation2D>();
            animation.Play(animationFrames, Mathf.Max(1f, playFrameRate));
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        private void OnDamaged(DamageInfo damage)
        {
            PlayOn(this, damage, frameRate, sortingOrderBoost);
        }

        private static Vector3 ResolvePosition(Component host, SpriteRenderer renderer, DamageInfo damage)
        {
            Vector3 center = renderer != null ? renderer.bounds.center : host.transform.position;
            Vector3 hitPoint = new Vector3(damage.Point.x, damage.Point.y, center.z);
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                hitPoint.x = Mathf.Clamp(hitPoint.x, bounds.min.x, bounds.max.x);
                hitPoint.y = Mathf.Clamp(hitPoint.y, bounds.min.y, bounds.max.y);
            }

            return Vector3.Lerp(hitPoint, center, HitToCenterBias);
        }

        private static float ResolveScale(Component host, SpriteRenderer renderer)
        {
            float bodySize = 0f;
            Collider2D[] colliders = host.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                bodySize = Mathf.Max(bodySize, collider.bounds.size.x, collider.bounds.size.y);
            }

            if (bodySize < 0.08f && renderer != null)
            {
                bodySize = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y);
            }

            if (bodySize < 0.08f)
            {
                bodySize = 1f;
            }

            float scale = (bodySize * BodyCoverage) / NativeSpriteWorldSize;
            return Mathf.Clamp(scale, MinScale, MaxScale);
        }

        private static Sprite[] LoadFrames()
        {
            if (framesLoadAttempted)
            {
                return frames;
            }

            framesLoadAttempted = true;
            var loaded = new Sprite[FrameCount];
            int count = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                string path = ResourcePrefix + $"{i + 1:D2}";
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>(path);
                    if (texture != null)
                    {
                        sprite = Sprite.Create(
                            texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f),
                            100f);
                    }
                }

                if (sprite != null)
                {
                    loaded[count++] = sprite;
                }
            }

            if (count == 0)
            {
                return null;
            }

            if (count == FrameCount)
            {
                frames = loaded;
                return frames;
            }

            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = loaded[i];
            }

            return frames;
        }

        private static bool IsPlayerRoot(Transform root)
        {
            return root != null &&
                   (root.CompareTag("Player") ||
                    root.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
