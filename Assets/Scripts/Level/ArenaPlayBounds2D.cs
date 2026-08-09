using UnityEngine;
using UnityEngine.Tilemaps;

namespace Castlevania2D.Level
{
    public static class ArenaPlayBounds2D
    {
        public readonly struct HorizontalBounds
        {
            public HorizontalBounds(float minCenterX, float maxCenterX, bool isValid)
            {
                MinCenterX = minCenterX;
                MaxCenterX = maxCenterX;
                IsValid = isValid;
            }

            public float MinCenterX { get; }
            public float MaxCenterX { get; }
            public bool IsValid { get; }
        }

        public static HorizontalBounds GetActorHorizontalBounds(
            Collider2D actorCollider,
            string leftWallObjectName,
            string rightWallObjectName,
            float fallbackMinX,
            float fallbackMaxX)
        {
            if (actorCollider == null)
            {
                return new HorizontalBounds(fallbackMinX, fallbackMaxX, fallbackMaxX > fallbackMinX);
            }

            if (!TryGetWallColumnInnerEdges(leftWallObjectName, rightWallObjectName, out float innerMinX, out float innerMaxX))
            {
                innerMinX = fallbackMinX;
                innerMaxX = fallbackMaxX;
            }

            float halfWidth = actorCollider.bounds.extents.x;
            float minCenterX = innerMinX + halfWidth;
            float maxCenterX = innerMaxX - halfWidth;

            if (maxCenterX < minCenterX)
            {
                float centerX = (innerMinX + innerMaxX) * 0.5f;
                minCenterX = centerX;
                maxCenterX = centerX;
            }

            return new HorizontalBounds(minCenterX, maxCenterX, innerMaxX > innerMinX);
        }

        public static bool TryGetWallColumnInnerEdges(
            string leftWallObjectName,
            string rightWallObjectName,
            out float innerMinX,
            out float innerMaxX)
        {
            innerMinX = 0f;
            innerMaxX = 0f;

            if (!TryResolvePhysicalWallSides(
                    leftWallObjectName,
                    rightWallObjectName,
                    out string physicalLeftName,
                    out string physicalRightName))
            {
                return false;
            }

            bool hasLeft = TryGetWallColumnInnerEdge(physicalLeftName, isLeftWall: true, out innerMinX);
            bool hasRight = TryGetWallColumnInnerEdge(physicalRightName, isLeftWall: false, out innerMaxX);

            return hasLeft && hasRight && innerMaxX > innerMinX;
        }

        public static bool TryGetWallColumnOuterEdges(
            string leftWallObjectName,
            string rightWallObjectName,
            out float outerMinX,
            out float outerMaxX)
        {
            outerMinX = 0f;
            outerMaxX = 0f;

            if (!TryResolvePhysicalWallSides(
                    leftWallObjectName,
                    rightWallObjectName,
                    out string physicalLeftName,
                    out string physicalRightName))
            {
                return false;
            }

            bool hasLeft = TryGetWallColumnOuterEdge(physicalLeftName, isLeftWall: true, out outerMinX);
            bool hasRight = TryGetWallColumnOuterEdge(physicalRightName, isLeftWall: false, out outerMaxX);

            return hasLeft && hasRight && outerMaxX > outerMinX;
        }

        /// <summary>
        /// After LeftWall↔RightWall position swap, object names may not match arena side.
        /// Pick the physical left/right walls by world X.
        /// </summary>
        private static bool TryResolvePhysicalWallSides(
            string wallNameA,
            string wallNameB,
            out string physicalLeftName,
            out string physicalRightName)
        {
            physicalLeftName = null;
            physicalRightName = null;

            bool hasA = TryGetWallWorldCenterX(wallNameA, out float centerA);
            bool hasB = TryGetWallWorldCenterX(wallNameB, out float centerB);
            if (!hasA || !hasB)
            {
                return false;
            }

            if (centerA <= centerB)
            {
                physicalLeftName = wallNameA;
                physicalRightName = wallNameB;
            }
            else
            {
                physicalLeftName = wallNameB;
                physicalRightName = wallNameA;
            }

            return true;
        }

        private static bool TryGetWallWorldCenterX(string wallObjectName, out float centerX)
        {
            centerX = 0f;
            if (!TryGetWallTilemap(wallObjectName, out Tilemap tilemap))
            {
                Collider2D collider = FindWallCollider(wallObjectName);
                if (collider == null)
                {
                    return false;
                }

                Physics2D.SyncTransforms();
                centerX = collider.bounds.center.x;
                return true;
            }

            tilemap.CompressBounds();
            BoundsInt cellBounds = tilemap.cellBounds;
            if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            {
                return false;
            }

            Vector3Int sampleCell = new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0);
            centerX = tilemap.GetCellCenterWorld(sampleCell).x;
            return true;
        }

        private static bool TryGetWallColumnInnerEdge(string wallObjectName, bool isLeftWall, out float innerEdgeX)
        {
            // Prefer physics collider so WallColumnGridCollider2D inwardInset is respected.
            if (TryGetWallColliderInnerEdge(wallObjectName, isLeftWall, out innerEdgeX))
            {
                return true;
            }

            innerEdgeX = 0f;
            if (!TryGetWallTilemap(wallObjectName, out Tilemap tilemap))
            {
                return false;
            }

            tilemap.CompressBounds();
            BoundsInt cellBounds = tilemap.cellBounds;
            if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            {
                return false;
            }

            int sampleY = cellBounds.yMin;
            if (isLeftWall)
            {
                innerEdgeX = tilemap.CellToWorld(new Vector3Int(cellBounds.xMax, sampleY, 0)).x;
            }
            else
            {
                innerEdgeX = tilemap.CellToWorld(new Vector3Int(cellBounds.xMin, sampleY, 0)).x;
            }

            return true;
        }

        private static bool TryGetWallColumnOuterEdge(string wallObjectName, bool isLeftWall, out float outerEdgeX)
        {
            outerEdgeX = 0f;

            if (!TryGetWallTilemap(wallObjectName, out Tilemap tilemap))
            {
                return TryGetWallColliderOuterEdge(wallObjectName, isLeftWall, out outerEdgeX);
            }

            tilemap.CompressBounds();
            BoundsInt cellBounds = tilemap.cellBounds;
            if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            {
                return false;
            }

            int sampleY = cellBounds.yMin;
            if (isLeftWall)
            {
                outerEdgeX = tilemap.CellToWorld(new Vector3Int(cellBounds.xMin, sampleY, 0)).x;
            }
            else
            {
                outerEdgeX = tilemap.CellToWorld(new Vector3Int(cellBounds.xMax, sampleY, 0)).x;
            }

            return true;
        }

        private static bool TryGetWallTilemap(string wallObjectName, out Tilemap tilemap)
        {
            tilemap = null;
            if (string.IsNullOrWhiteSpace(wallObjectName))
            {
                return false;
            }

            GameObject wallObject = GameObject.Find(wallObjectName);
            if (wallObject == null)
            {
                return false;
            }

            tilemap = wallObject.GetComponent<Tilemap>();
            return tilemap != null;
        }

        private static bool TryGetWallColliderInnerEdge(string wallObjectName, bool isLeftWall, out float innerEdgeX)
        {
            innerEdgeX = 0f;
            Collider2D collider = FindWallCollider(wallObjectName);
            if (collider == null)
            {
                return false;
            }

            Physics2D.SyncTransforms();
            innerEdgeX = isLeftWall ? collider.bounds.max.x : collider.bounds.min.x;
            return true;
        }

        private static bool TryGetWallColliderOuterEdge(string wallObjectName, bool isLeftWall, out float outerEdgeX)
        {
            outerEdgeX = 0f;
            Collider2D collider = FindWallCollider(wallObjectName);
            if (collider == null)
            {
                return false;
            }

            Physics2D.SyncTransforms();
            outerEdgeX = isLeftWall ? collider.bounds.min.x : collider.bounds.max.x;
            return true;
        }

        private static Collider2D FindWallCollider(string objectName)
        {
            GameObject wallObject = GameObject.Find(objectName);
            if (wallObject == null)
            {
                return null;
            }

            BoxCollider2D boxCollider = wallObject.GetComponent<BoxCollider2D>();
            if (boxCollider != null && boxCollider.enabled)
            {
                return boxCollider;
            }

            Collider2D[] colliders = wallObject.GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return collider;
                }
            }

            return null;
        }
    }
}
