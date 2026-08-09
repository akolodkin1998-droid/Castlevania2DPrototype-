namespace Castlevania2D.Combat
{
    public interface IDamageBlocker
    {
        bool IsBlockingDamage(DamageInfo damage);

        /// <summary>
        /// True while the player is actively holding block and can ricochet reflected projectiles.
        /// </summary>
        bool IsProjectileReflectActive { get; }
    }
}
