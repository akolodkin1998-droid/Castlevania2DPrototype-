using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    public sealed class WoodenElevator2D : MonoBehaviour
    {
        [SerializeField] private WoodenElevatorWinch2D winch;

        public bool IsMoving { get; private set; }

        public void SetMoving(bool moving)
        {
            IsMoving = moving;
            if (winch != null)
            {
                winch.SetSpinning(moving);
            }
        }
    }
}
