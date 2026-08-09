using System.Collections.Generic;
using Castlevania2D.Combat;
using Castlevania2D.Movement;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using BossHealthComponent = global::Castlevania2D.Health.Health;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Lives outside Castlevania2D.asmdef (Assembly-CSharp) so it can read HeroKnight GroundSensor.
    /// </summary>
    public sealed class FlowerBossTentacleAttack2D : MonoBehaviour
    {
        private const string TentacleSpritesFolder = "Assets/Art/Sprites/Characters/Enemies/Flower Boss/Tentacles";
        private const string TentacleSpritePrefix = "FlowerBoss_Tentacle_";
        private const string TentacleRootName = "FlowerBoss_Tentacles";

        [Header("--- SPAWN PLACEMENT (регулировка тут) ---")]
        [Tooltip("Низ щупальца на ЛЕВОЙ стороне арены (внутренняя грань). Меньше X = глубже в стену / левее; больше X = в арену / правее.")]
        [SerializeField] private float wallSpawnX = -11.4f;
        [Tooltip("Низ щупальца на ПРАВОЙ стороне арены (внутренняя грань). Больше X = глубже в стену / правее; меньше X = в арену / левее (к персонажу).")]
        [SerializeField] private float rightWallSpawnX = 13f;
        [Tooltip("Низ щупальца на полу и под героем. Больше = выше над травой.")]
        [SerializeField] private float groundSpawnY = 0.5f;
        [Tooltip("Высота на блоке стены: 0 = низ блока, 1 = верх блока.")]
        [Range(0f, 1f)]
        [SerializeField] private float wallSpawnHeightNormalized = 0.5f;

        [Header("Tilemaps / Player")]
        [SerializeField] private Tilemap leftWallTilemap;
        [FormerlySerializedAs("leftWallSecondaryTilemap")]
        [SerializeField] private Tilemap rightWallTilemap;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Transform tentacleRoot;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private CharacterMotor2D playerMotor;
        [SerializeField] private Sensor_HeroKnight playerGroundSensor;
        [SerializeField] private Sprite[] tentacleFrames = System.Array.Empty<Sprite>();

        [Header("Boss Hit Wave")]
        [SerializeField] private int leftWallSpawnCount = 4;
        [SerializeField] private int groundSpawnCount = 3;
        [SerializeField] private int tentacleDamage = 5;
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private float tentacleScale = 0.14f;
        [SerializeField] private float bossHitSpawnCooldown = 3f;

        [Header("Stand Trap Under Player")]
        [SerializeField] private float standTrapDelay = 1.5f;
        [SerializeField] private float standMoveResetDistance = 0.75f;

        [Header("Layers / Names")]
        [SerializeField] private LayerMask playerLayerMask;
        [SerializeField] private string leftWallObjectName = "LeftWall";
        [FormerlySerializedAs("leftWallSecondaryObjectName")]
        [SerializeField] private string rightWallObjectName = "RightWall";
        [SerializeField] private string groundObjectName = "Ground";
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private BossHealthComponent bossHealth;
        private bool isSubscribed;
        private float nextBossHitSpawnTime;
        private float groundedStandTimer;
        private float standAnchorX;
        private bool hasStandAnchor;

        private void Awake()
        {
            bossHealth = GetComponent<BossHealthComponent>();
            ResolveReferences();
        }

        private void Start()
        {
            EnsureTentacleFramesLoaded();
            ResolveReferences();
            SubscribeToBossDamage();
        }

        private void Update()
        {
            TickStandTrapUnderPlayer();
        }

        private void OnDisable()
        {
            UnsubscribeFromBossDamage();
        }

        private void OnDestroy()
        {
            UnsubscribeFromBossDamage();
        }

        public void Configure(Sprite[] frames, Tilemap leftWall, Tilemap ground, Transform root)
        {
            tentacleFrames = frames ?? System.Array.Empty<Sprite>();
            leftWallTilemap = leftWall;
            groundTilemap = ground;
            tentacleRoot = root;
            ResolveReferences();
        }

        private void SubscribeToBossDamage()
        {
            if (isSubscribed || bossHealth == null)
            {
                return;
            }

            bossHealth.Damaged += OnBossDamaged;
            isSubscribed = true;
        }

        private void UnsubscribeFromBossDamage()
        {
            if (!isSubscribed || bossHealth == null)
            {
                return;
            }

            bossHealth.Damaged -= OnBossDamaged;
            isSubscribed = false;
        }

        private void OnBossDamaged(DamageInfo damageInfo)
        {
            if (Time.time < nextBossHitSpawnTime)
            {
                return;
            }

            EnsureTentacleFramesLoaded();
            if (tentacleFrames == null || tentacleFrames.Length == 0)
            {
                Debug.LogWarning("FlowerBossTentacleAttack2D: no tentacle sprites assigned.");
                return;
            }

            ResolveReferences();
            Transform root = ResolveTentacleRoot();
            SpawnWallTentacles(leftWallSpawnCount, root);
            SpawnTentaclesOnSurface(groundTilemap, groundSpawnCount, FlowerBossTentacleSurface.Ground, root);
            nextBossHitSpawnTime = Time.time + bossHitSpawnCooldown;
        }

        private void SpawnWallTentacles(int spawnCount, Transform root)
        {
            if (spawnCount <= 0)
            {
                return;
            }

            List<(Tilemap tilemap, Vector3Int cell)> wallSites = new List<(Tilemap, Vector3Int)>();
            AppendOccupiedCells(leftWallTilemap, wallSites);
            AppendOccupiedCells(rightWallTilemap, wallSites);
            if (wallSites.Count == 0)
            {
                return;
            }

            int actualSpawnCount = Mathf.Min(spawnCount, wallSites.Count);
            Shuffle(wallSites);

            for (int i = 0; i < actualSpawnCount; i++)
            {
                (Tilemap tilemap, Vector3Int cell) = wallSites[i];
                FlowerBossTentacleSurface surface = ResolveWallSurface(tilemap);
                SpawnTentacleAtCell(tilemap, cell, surface, root);
            }
        }

        private static void AppendOccupiedCells(Tilemap tilemap, List<(Tilemap tilemap, Vector3Int cell)> sites)
        {
            if (tilemap == null)
            {
                return;
            }

            List<Vector3Int> cells = CollectOccupiedCells(tilemap);
            for (int i = 0; i < cells.Count; i++)
            {
                sites.Add((tilemap, cells[i]));
            }
        }

        private void TickStandTrapUnderPlayer()
        {
            ResolveReferences();
            EnsureTentacleFramesLoaded();

            if (playerTransform == null || tentacleFrames == null || tentacleFrames.Length == 0)
            {
                ResetStandTimer();
                return;
            }

            if (!IsPlayerGrounded())
            {
                ResetStandTimer();
                return;
            }

            float playerX = playerTransform.position.x;
            if (!hasStandAnchor || Mathf.Abs(playerX - standAnchorX) > standMoveResetDistance)
            {
                standAnchorX = playerX;
                hasStandAnchor = true;
                groundedStandTimer = 0f;
            }

            groundedStandTimer += Time.deltaTime;
            if (groundedStandTimer < standTrapDelay)
            {
                return;
            }

            // Independent of boss-hit damage / bossHitSpawnCooldown.
            Vector3 underPlayer = new Vector3(playerX, groundSpawnY, 0f);
            FlowerBossTentacle2D.Spawn(
                ResolveTentacleRoot(),
                underPlayer,
                tentacleFrames,
                FlowerBossTentacleSurface.Ground,
                frameRate,
                tentacleDamage,
                playerLayerMask,
                tentacleScale);

            ResetStandTimer();
        }

        private bool IsPlayerGrounded()
        {
            if (playerGroundSensor != null)
            {
                return playerGroundSensor.State();
            }

            HeroKnight heroKnight = playerTransform != null
                ? playerTransform.GetComponent<HeroKnight>()
                : null;
            if (heroKnight != null)
            {
                return heroKnight.IsGrounded;
            }

            if (playerMotor != null)
            {
                return playerMotor.IsGrounded;
            }

            return false;
        }

        private void ResetStandTimer()
        {
            groundedStandTimer = 0f;
            hasStandAnchor = false;
        }

        private Transform ResolveTentacleRoot()
        {
            if (tentacleRoot != null)
            {
                return tentacleRoot;
            }

            GameObject existingRoot = GameObject.Find(TentacleRootName);
            if (existingRoot == null)
            {
                existingRoot = new GameObject(TentacleRootName);
            }

            tentacleRoot = existingRoot.transform;
            return tentacleRoot;
        }

        private void SpawnTentaclesOnSurface(
            Tilemap tilemap,
            int spawnCount,
            FlowerBossTentacleSurface surface,
            Transform root)
        {
            if (tilemap == null || spawnCount <= 0)
            {
                return;
            }

            List<Vector3Int> spawnCells = surface == FlowerBossTentacleSurface.Ground
                ? CollectTopFaceCells(tilemap)
                : CollectOccupiedCells(tilemap);
            if (spawnCells.Count == 0)
            {
                return;
            }

            int actualSpawnCount = Mathf.Min(spawnCount, spawnCells.Count);
            Shuffle(spawnCells);

            for (int i = 0; i < actualSpawnCount; i++)
            {
                SpawnTentacleAtCell(tilemap, spawnCells[i], surface, root);
            }
        }

        private void SpawnTentacleAtCell(
            Tilemap tilemap,
            Vector3Int cell,
            FlowerBossTentacleSurface surface,
            Transform root = null)
        {
            if (tilemap == null || tentacleFrames == null || tentacleFrames.Length == 0)
            {
                return;
            }

            Transform spawnRoot = root != null ? root : ResolveTentacleRoot();
            Vector3 worldPosition = GetInwardFaceWorldPosition(tilemap, cell, surface);
            FlowerBossTentacle2D.Spawn(
                spawnRoot,
                worldPosition,
                tentacleFrames,
                surface,
                frameRate,
                tentacleDamage,
                playerLayerMask,
                tentacleScale);
        }

        private Vector3 GetInwardFaceWorldPosition(Tilemap tilemap, Vector3Int cell, FlowerBossTentacleSurface surface)
        {
            Vector3 cellSize = tilemap.layoutGrid != null
                ? tilemap.layoutGrid.cellSize
                : Vector3.one;
            Vector3 cellMin = tilemap.CellToWorld(cell);

            if (surface == FlowerBossTentacleSurface.Ground)
            {
                return new Vector3(cellMin.x + cellSize.x * 0.5f, groundSpawnY, 0f);
            }

            float wallY = cellMin.y + cellSize.y * Mathf.Clamp01(wallSpawnHeightNormalized);

            // Side is determined by world placement (names may be swapped), not object name.
            if (surface == FlowerBossTentacleSurface.LeftWall)
            {
                // Grow rightward into the arena from the left wall's inner (right) face.
                return new Vector3(wallSpawnX, wallY, 0f);
            }

            // Right-side arena wall: grow leftward into the arena from the fixed inner X.
            return new Vector3(rightWallSpawnX, wallY, 0f);
        }

        /// <summary>
        /// Chooses grow direction from the wall's world X (into the arena), not from the GameObject name.
        /// After LeftWall↔RightWall position swap, names and sides can diverge.
        /// </summary>
        private static FlowerBossTentacleSurface ResolveWallSurface(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return FlowerBossTentacleSurface.LeftWall;
            }

            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
            {
                return FlowerBossTentacleSurface.LeftWall;
            }

            Vector3Int sampleCell = new Vector3Int(bounds.xMin, bounds.yMin, 0);
            Vector3 cellCenter = tilemap.GetCellCenterWorld(sampleCell);
            return cellCenter.x < 0f
                ? FlowerBossTentacleSurface.LeftWall
                : FlowerBossTentacleSurface.RightWall;
        }

        private static List<Vector3Int> CollectOccupiedCells(Tilemap tilemap)
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            BoundsInt bounds = tilemap.cellBounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (tilemap.HasTile(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private static List<Vector3Int> CollectTopFaceCells(Tilemap tilemap)
        {
            List<Vector3Int> cells = new List<Vector3Int>();
            BoundsInt bounds = tilemap.cellBounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    if (!tilemap.HasTile(cell))
                    {
                        continue;
                    }

                    if (tilemap.HasTile(cell + Vector3Int.up))
                    {
                        continue;
                    }

                    cells.Add(cell);
                }
            }

            return cells;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }

        private void ResolveReferences()
        {
            if (leftWallTilemap == null)
            {
                leftWallTilemap = FindTilemapByName(leftWallObjectName);
            }

            if (rightWallTilemap == null)
            {
                rightWallTilemap = FindTilemapByName(rightWallObjectName);
            }

            if (groundTilemap == null)
            {
                groundTilemap = FindTilemapByName(groundObjectName);
            }

            if (playerTransform == null)
            {
                GameObject playerObject = GameObject.Find(playerObjectName);
                if (playerObject == null)
                {
                    HeroKnight hero = Object.FindAnyObjectByType<HeroKnight>();
                    if (hero != null)
                    {
                        playerObject = hero.gameObject;
                    }
                }

                if (playerObject != null)
                {
                    playerTransform = playerObject.transform;
                }
            }

            if (playerTransform == null)
            {
                return;
            }

            if (playerMotor == null)
            {
                playerMotor = playerTransform.GetComponent<CharacterMotor2D>();
            }

            if (playerGroundSensor == null)
            {
                Transform sensorTransform = playerTransform.Find("GroundSensor");
                if (sensorTransform != null)
                {
                    playerGroundSensor = sensorTransform.GetComponent<Sensor_HeroKnight>();
                }
            }

            // 0 = "Nothing" in the Inspector; treat as unset and wire the player root layer.
            if (playerLayerMask.value == 0)
            {
                playerLayerMask = 1 << playerTransform.gameObject.layer;
            }
        }

        private static Tilemap FindTilemapByName(string objectName)
        {
            GameObject tilemapObject = GameObject.Find(objectName);
            return tilemapObject != null ? tilemapObject.GetComponent<Tilemap>() : null;
        }

        private void EnsureTentacleFramesLoaded()
        {
            if (tentacleFrames != null && tentacleFrames.Length > 0)
            {
                return;
            }

#if UNITY_EDITOR
            tentacleFrames = LoadTentacleSpritesFromProject();
#endif
        }

#if UNITY_EDITOR
        private static Sprite[] LoadTentacleSpritesFromProject()
        {
            if (!AssetDatabase.IsValidFolder(TentacleSpritesFolder))
            {
                return System.Array.Empty<Sprite>();
            }

            string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TentacleSpritesFolder });
            List<(int index, Sprite sprite)> sprites = new List<(int, Sprite)>();

            for (int i = 0; i < spriteGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!fileName.StartsWith(TentacleSpritePrefix, System.StringComparison.OrdinalIgnoreCase))
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
