using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Shared aggro limits. Prototype grid cells are 1 world unit.
    /// </summary>
    public static class EnemyAggroLimits
    {
        public const float VerticalTiles = 3f;

        public static bool IsWithinVerticalRange(Vector3 from, Vector3 to)
        {
            return Mathf.Abs(to.y - from.y) <= VerticalTiles;
        }

        public static bool IsWithinVerticalRange(Transform from, Transform to)
        {
            return from != null &&
                   to != null &&
                   IsWithinVerticalRange(from.position, to.position);
        }

        public static float CapVerticalRange(float range)
        {
            return Mathf.Min(Mathf.Max(0f, range), VerticalTiles);
        }
    }
}
