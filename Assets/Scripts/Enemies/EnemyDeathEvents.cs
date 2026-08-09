using System;

namespace Castlevania2D.Enemies
{
    public enum EnemyDeathKind
    {
        Ent = 0,
        Mushomor = 1,
    }

    /// <summary>
    /// Global death signals for Ent / Mushomor (including portal spawns).
    /// </summary>
    public static class EnemyDeathEvents
    {
        public static event Action<EnemyDeathKind> Died;

        public static void Raise(EnemyDeathKind kind)
        {
            Died?.Invoke(kind);
        }
    }
}
