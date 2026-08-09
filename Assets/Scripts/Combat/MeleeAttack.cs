using Castlevania2D.Input;
using Castlevania2D.Movement;
using UnityEngine;

namespace Castlevania2D.Combat
{
    public sealed class MeleeAttack : MonoBehaviour
    {
        [SerializeField] private Hitbox2D hitbox;
        [SerializeField] private float activeTime = 0.12f;
        [SerializeField] private float cooldownTime = 0.35f;

        private ICharacterInput input;
        private CharacterMotor2D motor;
        private float activeTimer;
        private float cooldownTimer;
        private bool isSwinging;

        public bool IsSwinging => isSwinging;

        private void Awake()
        {
            input = GetComponent<ICharacterInput>();
            motor = GetComponent<CharacterMotor2D>();

            if (hitbox != null)
            {
                hitbox.Configure(gameObject, 1);
                hitbox.EndSwing();
            }
        }

        private void Update()
        {
            TickTimers();

            if (input != null && input.AttackPressed && cooldownTimer <= 0f)
            {
                BeginAttack();
            }
        }

        private void BeginAttack()
        {
            isSwinging = true;
            activeTimer = activeTime;
            cooldownTimer = cooldownTime;

            if (hitbox != null)
            {
                int facingDirection = motor != null ? motor.FacingDirection : 1;
                hitbox.Configure(gameObject, facingDirection);
                hitbox.BeginSwing();
            }
        }

        private void TickTimers()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (!isSwinging)
            {
                return;
            }

            activeTimer -= Time.deltaTime;
            if (activeTimer > 0f)
            {
                return;
            }

            isSwinging = false;
            if (hitbox != null)
            {
                hitbox.EndSwing();
            }
        }
    }
}
