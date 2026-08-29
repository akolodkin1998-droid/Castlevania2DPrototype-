using UnityEngine;

namespace Castlevania2D.Input
{
    public sealed class PlayerInputReader : MonoBehaviour, ICharacterInput
    {
        [Header("Legacy Input Axes")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string jumpButton = "Jump";
        [SerializeField] private string attackButton = "Fire1";

        public Vector2 Move { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool AttackPressed { get; private set; }

        private void Update()
        {
            if (GameplayInputLock.IsLocked)
            {
                Move = Vector2.zero;
                JumpPressed = false;
                AttackPressed = false;
                return;
            }

            Move = new Vector2(UnityEngine.Input.GetAxisRaw(horizontalAxis), 0f);
            JumpPressed = UnityEngine.Input.GetButtonDown(jumpButton);
            AttackPressed = UnityEngine.Input.GetButtonDown(attackButton);
        }
    }
}
