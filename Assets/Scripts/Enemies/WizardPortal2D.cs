using System.Collections.Generic;
using Castlevania2D.Loot;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Portal visual: play frames 1→N once (appear), then loop 1→N forward forever.
    /// On each spawn pulse, spawns Ent and Mushomor (each if under its own max alive).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WizardPortal2D : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField] private float frameRate = 12f;

        [Header("Spawn")]
        [SerializeField] private GameObject entPrefab;
        [SerializeField] private GameObject mushomorPrefab;
        [SerializeField] private float spawnInterval = 10f;
        [SerializeField] private int maxEnts = 1;
        [SerializeField] private int maxMushomors = 1;
        [SerializeField] private bool spawnImmediately = true;
        [SerializeField] private Vector2 spawnOffset = Vector2.zero;
        [SerializeField] private Transform spawnParent;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float frameTimer;
        private bool appearComplete;
        private float spawnTimer;
        private bool spawningEnabled = true;
        private readonly List<GameObject> liveEnts = new List<GameObject>(4);
        private readonly List<GameObject> liveMushomors = new List<GameObject>(4);

        public void Configure(
            Sprite[] portalFrames,
            GameObject ent,
            GameObject mushomor,
            float playbackFrameRate,
            float interval,
            int maxAliveEnts,
            int maxAliveMushomors,
            bool immediate,
            Vector2 offset,
            Transform parent)
        {
            frames = portalFrames ?? System.Array.Empty<Sprite>();
            entPrefab = ent;
            mushomorPrefab = mushomor;
            frameRate = Mathf.Max(1f, playbackFrameRate);
            spawnInterval = Mathf.Max(0.1f, interval);
            maxEnts = Mathf.Max(0, maxAliveEnts);
            maxMushomors = Mathf.Max(0, maxAliveMushomors);
            spawnImmediately = immediate;
            spawnOffset = offset;
            spawnParent = parent;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            frameIndex = 0;
            frameTimer = 0f;
            appearComplete = false;
            ApplyFrame();

            spawnTimer = spawnImmediately ? 0f : spawnInterval;
        }

        public void SetSpawningEnabled(bool enabled)
        {
            spawningEnabled = enabled;
        }

        /// <summary>Stops spawning, stops animation, and hides this portal.</summary>
        public void SealAndHide()
        {
            spawningEnabled = false;
            enabled = false;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyFrame();
        }

        private void Update()
        {
            Animate();
            TickSpawn();
        }

        private void Animate()
        {
            if (frames == null || frames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            if (frames.Length == 1)
            {
                spriteRenderer.sprite = frames[0];
                appearComplete = true;
                return;
            }

            float frameDuration = 1f / Mathf.Max(1f, frameRate);
            frameTimer += Time.deltaTime;
            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                AdvanceFrame();
            }

            ApplyFrame();
        }

        private void AdvanceFrame()
        {
            if (!appearComplete)
            {
                // Appear: play 0 → last once.
                if (frameIndex >= frames.Length - 1)
                {
                    appearComplete = true;
                    frameIndex = 0;
                    return;
                }

                frameIndex++;
                return;
            }

            // Loop forward forever: 0 → last → 0 …
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        private void ApplyFrame()
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            if (frames[index] != null)
            {
                spriteRenderer.sprite = frames[index];
            }
        }

        private void TickSpawn()
        {
            if (!spawningEnabled)
            {
                return;
            }

            if (entPrefab == null && mushomorPrefab == null)
            {
                return;
            }

            PruneDead(liveEnts);
            PruneDead(liveMushomors);

            bool canSpawnEnt = entPrefab != null && liveEnts.Count < maxEnts;
            bool canSpawnMushomor = mushomorPrefab != null && liveMushomors.Count < maxMushomors;
            if (!canSpawnEnt && !canSpawnMushomor)
            {
                spawnTimer = spawnInterval;
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
            {
                return;
            }

            if (canSpawnEnt)
            {
                SpawnUnit(entPrefab, "Enemy_Ent_FromPortal", liveEnts);
            }

            if (canSpawnMushomor)
            {
                SpawnUnit(mushomorPrefab, "Enemy_Mushomor_FromPortal", liveMushomors);
            }

            spawnTimer = spawnInterval;
        }

        private void SpawnUnit(GameObject prefab, string instanceName, List<GameObject> liveList)
        {
            // Spawn at portal world position, then plant each grounded summon on the floor below.
            Vector3 position = transform.position + (Vector3)spawnOffset;
            // Keep summons unparented so they survive portal/wizard teardown.
            GameObject unit = Instantiate(prefab, position, Quaternion.identity);
            unit.name = instanceName;
            if (!unit.activeSelf)
            {
                unit.SetActive(true);
            }

            var entWalker = unit.GetComponent<EntWalkerEnemy2D>();
            if (entWalker != null)
            {
                entWalker.PlaceOnGroundFromSpawn();
                entWalker.MarkAsPortalSummon();
            }

            var mushomorMovement = unit.GetComponent<MushomorMovement2D>();
            if (mushomorMovement != null)
            {
                mushomorMovement.PlaceOnGroundFromSpawn();
                mushomorMovement.MarkAsPortalSummon();
            }

            // Prefabs carry LootDropOnDeath; bootstrap covers summons that lack it.
            LootRuntimeBootstrap.EnsureLootDrop(unit);
            EnemyCollisionPassThrough2D.EnsureOn(unit);
            liveList.Add(unit);
        }

        private static void PruneDead(List<GameObject> liveList)
        {
            for (int i = liveList.Count - 1; i >= 0; i--)
            {
                if (liveList[i] == null)
                {
                    liveList.RemoveAt(i);
                }
            }
        }
    }
}
