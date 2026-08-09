using UnityEngine;

namespace Castlevania2D.Combat
{
    /// <summary>Temporary velocity override used by strong enemy hits (e.g. Likho kick).</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class CombatKnockbackReceiver2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private float remainingTime;
        private Vector2 velocity;

        public bool IsActive => remainingTime > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (remainingTime <= 0f)
            {
                return;
            }

            remainingTime -= Time.fixedDeltaTime;
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                Vector2 current = body.linearVelocity;
                current.x = 0f;
                body.linearVelocity = current;
            }
        }

        /// <summary>Overrides horizontal (and optional vertical) motion for <paramref name="duration"/> seconds.</summary>
        public void ApplyKnockback(Vector2 impulseVelocity, float duration)
        {
            velocity = impulseVelocity;
            remainingTime = Mathf.Max(0.01f, duration);
            body.linearVelocity = new Vector2(impulseVelocity.x, impulseVelocity.y);
        }

        public bool TryGetKnockbackVelocity(out Vector2 knockbackVelocity)
        {
            if (remainingTime <= 0f)
            {
                knockbackVelocity = default;
                return false;
            }

            knockbackVelocity = velocity;
            return true;
        }
    }
}
