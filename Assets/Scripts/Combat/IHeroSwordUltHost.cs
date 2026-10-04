using UnityEngine;

namespace Castlevania2D.Combat
{
    public interface IHeroSwordUltHost
    {
        bool CanBeginUlt();
        void NotifyUltStarted();
        void NotifyUltFinished();
        bool CanBeginPotion();
        void NotifyPotionStarted();
        void NotifyPotionFinished();
        int FacingDirection { get; }
        int AttackDamage { get; }
        bool IsGrounded { get; }
        Vector2 FeetPosition { get; }
        SpriteRenderer BodyRenderer { get; }
        Animator BodyAnimator { get; }
    }
}
