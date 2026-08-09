using UnityEngine;

namespace Castlevania2D.Combat
{
    /// <summary>
    /// Provides a shield/block face normal for ricocheting projectiles after a successful block.
    /// </summary>
    public interface IProjectileReflectSurface
    {
        Vector2 GetReflectNormal(Vector2 hitPoint, Vector2 incomingDirection);
    }
}
