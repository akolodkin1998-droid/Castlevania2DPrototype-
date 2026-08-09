using Castlevania2D.Combat;
using Castlevania2D.Input;
using UnityEngine;

namespace Castlevania2D.Movement
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class CharacterMotor2D : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float jumpVelocity = 12f;

        [Header("Ground Check")]
        [Tooltip("Place this at the character's feet.")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.12f;
        [SerializeField] private LayerMask groundMask;

        private Rigidbody2D body;
        private ICharacterInput input;
        private CombatKnockbackReceiver2D knockbackReceiver;
        private float horizontalInput;
        private bool jumpQueued;

        public bool IsGrounded { get; private set; }
        public int FacingDirection { get; private set; } = 1;
        public Vector2 Velocity => body.linearVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<ICharacterInput>();
            knockbackReceiver = GetComponent<CombatKnockbackReceiver2D>();
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();
            UpdateFacingDirection();
            ApplyMovement();

            jumpQueued = false;
        }

        private void ReadInput()
        {
            if (input == null)
            {
                horizontalInput = 0f;
                return;
            }

            horizontalInput = Mathf.Clamp(input.Move.x, -1f, 1f);

            if (input.JumpPressed)
            {
                jumpQueued = true;
            }
        }

        private void UpdateGroundedState()
        {
            IsGrounded = groundCheck != null &&
                         Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask) != null;
        }

        private void UpdateFacingDirection()
        {
            if (Mathf.Abs(horizontalInput) <= 0.01f)
            {
                return;
            }

            FacingDirection = horizontalInput > 0f ? 1 : -1;
        }

        private void ApplyMovement()
        {
            if (knockbackReceiver != null && knockbackReceiver.TryGetKnockbackVelocity(out Vector2 knockbackVelocity))
            {
                Vector2 launched = body.linearVelocity;
                launched.x = knockbackVelocity.x;
                if (Mathf.Abs(knockbackVelocity.y) > 0.01f)
                {
                    launched.y = knockbackVelocity.y;
                }

                body.linearVelocity = launched;
                return;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.x = horizontalInput * moveSpeed;

            if (jumpQueued && IsGrounded)
            {
                velocity.y = jumpVelocity;
            }

            body.linearVelocity = velocity;
        }
    }
}
