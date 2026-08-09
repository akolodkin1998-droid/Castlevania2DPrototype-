using UnityEngine;
using UnityEngine.Tilemaps;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Builds a BoxCollider2D over the painted wall column, or leaves Offset/Size to you when manual.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap))]
    [DefaultExecutionOrder(-50)]
    public sealed class WallColumnGridCollider2D : MonoBehaviour
    {
        [Tooltip(
            "If enabled, BoxCollider2D Offset/Size stay as you set them in the Inspector / Scene gizmo. " +
            "Disable (or use Tools → Rebuild Right Wall) to sync collider from painted tiles again.")]
        [SerializeField]
        private bool manualCollider = true;

        [Tooltip(
            "World units to carve from the arena-facing side of the wall collider. " +
            "Only used when Manual Collider is off / Rebuild From Tiles. " +
            "1 ≈ one tile of extra walkable space.")]
        [SerializeField]
        private float inwardInset = 0f;

        [Tooltip("Minimum collider width after inward inset (keeps a thin outer strip when inset ≈ column width).")]
        [SerializeField]
        private float minThickness = 0.05f;

        private BoxCollider2D boxCollider;
        private TilemapCollider2D tilemapCollider;
        private CompositeCollider2D compositeCollider;
        private Rigidbody2D body;

        private void Awake()
        {
            Rebuild(forceFromTiles: false);
        }

        private void OnEnable()
        {
            if (boxCollider == null)
            {
                Rebuild(forceFromTiles: false);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Rebuild(forceFromTiles: false);
                return;
            }

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Rebuild(forceFromTiles: false);
                }
            };
        }
#endif

        /// <summary>Runtime / validate path — respects Manual Collider.</summary>
        public void Rebuild()
        {
            Rebuild(forceFromTiles: false);
        }

        /// <summary>Editor rebuild menus — always syncs Offset/Size from painted tiles.</summary>
        public void RebuildFromTiles()
        {
            Rebuild(forceFromTiles: true);
        }

        public void Rebuild(bool forceFromTiles)
        {
            Tilemap tilemap = GetComponent<Tilemap>();
            if (tilemap == null)
            {
                return;
            }

            DisableSpriteColliders();
            EnsureBoxCollider();

            boxCollider.isTrigger = false;
            boxCollider.enabled = true;

            if (manualCollider && !forceFromTiles)
            {
                // Keep authored Offset/Size — do not overwrite from tiles.
                return;
            }

            tilemap.CompressBounds();
            BoundsInt cellBounds = tilemap.cellBounds;
            if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            {
                return;
            }

            Vector3 localMin = tilemap.CellToLocal(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
            Vector3 localMax = tilemap.CellToLocal(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));

            ApplyInwardInset(tilemap, cellBounds, ref localMin, ref localMax);

            Vector3 localCenter = (localMin + localMax) * 0.5f;
            Vector3 localSize = localMax - localMin;

            boxCollider.offset = localCenter;
            boxCollider.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));
        }

        private void EnsureBoxCollider()
        {
            boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }
        }

        private void ApplyInwardInset(
            Tilemap tilemap,
            BoundsInt cellBounds,
            ref Vector3 localMin,
            ref Vector3 localMax)
        {
            float inset = Mathf.Max(0f, inwardInset);
            if (inset <= 0f)
            {
                return;
            }

            float width = Mathf.Abs(localMax.x - localMin.x);
            float thickness = Mathf.Max(minThickness, width - inset);
            bool isPhysicalRightWall = IsPhysicalRightWall(tilemap, cellBounds);

            if (isPhysicalRightWall)
            {
                localMin.x = localMax.x - thickness;
            }
            else
            {
                localMax.x = localMin.x + thickness;
            }
        }

        private static bool IsPhysicalRightWall(Tilemap tilemap, BoundsInt cellBounds)
        {
            Vector3Int sampleCell = new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0);
            return tilemap.GetCellCenterWorld(sampleCell).x >= 0f;
        }

        private void DisableSpriteColliders()
        {
            tilemapCollider = tilemapCollider != null ? tilemapCollider : GetComponent<TilemapCollider2D>();
            if (tilemapCollider != null)
            {
                tilemapCollider.enabled = false;
            }

            compositeCollider = compositeCollider != null ? compositeCollider : GetComponent<CompositeCollider2D>();
            if (compositeCollider != null)
            {
                compositeCollider.enabled = false;
            }

            body = body != null ? body : GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;
        }
    }
}
