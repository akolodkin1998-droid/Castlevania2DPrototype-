using Castlevania2D.Combat;
using Castlevania2D.Movement;
using UnityEngine;

namespace Castlevania2D.Player
{
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        [SerializeField] private string moveSpeedParameter = "MoveSpeed";
        [SerializeField] private string verticalSpeedParameter = "VerticalSpeed";
        [SerializeField] private string groundedParameter = "IsGrounded";
        [SerializeField] private string attackingParameter = "IsAttacking";
        [SerializeField] private string deadParameter = "IsDead";

        private Animator animator;
        private CharacterMotor2D motor;
        private MeleeAttack meleeAttack;
        private Castlevania2D.Health.Health health;

        private int moveSpeedHash;
        private int verticalSpeedHash;
        private int groundedHash;
        private int attackingHash;
        private int deadHash;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            motor = GetComponent<CharacterMotor2D>();
            meleeAttack = GetComponent<MeleeAttack>();
            health = GetComponent<Castlevania2D.Health.Health>();

            moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
            verticalSpeedHash = Animator.StringToHash(verticalSpeedParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
            attackingHash = Animator.StringToHash(attackingParameter);
            deadHash = Animator.StringToHash(deadParameter);
        }

        private void Update()
        {
            if (motor != null)
            {
                animator.SetFloat(moveSpeedHash, Mathf.Abs(motor.Velocity.x));
                animator.SetFloat(verticalSpeedHash, motor.Velocity.y);
                animator.SetBool(groundedHash, motor.IsGrounded);
            }

            if (meleeAttack != null)
            {
                animator.SetBool(attackingHash, meleeAttack.IsSwinging);
            }

            if (health != null)
            {
                animator.SetBool(deadHash, !health.IsAlive);
            }
        }
    }
}
