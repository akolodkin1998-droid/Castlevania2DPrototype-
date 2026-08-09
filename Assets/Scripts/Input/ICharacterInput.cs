using UnityEngine;

namespace Castlevania2D.Input
{
    public interface ICharacterInput
    {
        Vector2 Move { get; }
        bool JumpPressed { get; }
        bool AttackPressed { get; }
    }
}
