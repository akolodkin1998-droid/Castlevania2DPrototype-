namespace Castlevania2D.Combat
{
    public interface IDamageable
    {
        bool CanReceiveDamage { get; }
        DamageResult ReceiveDamage(DamageInfo damage);
    }
}

