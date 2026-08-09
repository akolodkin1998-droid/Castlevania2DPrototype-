using UnityEngine;

namespace Castlevania2D.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PatrolEnemy2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private Transform wallCheck;
        [SerializeField] private Transform ledgeCheck;
        [SerializeField] private float checkRadius = 0.12f;
        [SerializeField] private LayerMask groundMask;
        [Tooltip("When false, enemies keep walking off platform edges and fall with gravity.")]
        [SerializeField] private bool reverseAtLedge;

        private Rigidbody2D body;
        private int direction = 1;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);

            bool touchesWall = wallCheck != null &&
                               Physics2D.OverlapCircle(wallCheck.position, checkRadius, groundMask) != null;
            bool hasGroundAhead = ledgeCheck != null &&
                                  Physics2D.OverlapCircle(ledgeCheck.position, checkRadius, groundMask) != null;

            if (touchesWall || (reverseAtLedge && !hasGroundAhead))
            {
                Flip();
            }
        }

        private void Flip()
        {
            direction *= -1;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            transform.localScale = scale;
        }
    }
}
