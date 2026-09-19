using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ClimbableRope2D : MonoBehaviour
    {
        [SerializeField] private WoodenElevatorRope2D rope;
        [SerializeField] private float grabWidth = 0.35f;
        [SerializeField] private float topPadding = 0.35f;
        [SerializeField] private float bottomPadding = 0.15f;

        private BoxCollider2D grabCollider;

        public float GrabX
        {
            get
            {
                Transform top = ResolveTop();
                return top != null ? top.position.x : transform.position.x;
            }
        }

        private void Awake()
        {
            if (rope == null)
            {
                rope = GetComponent<WoodenElevatorRope2D>();
            }

            grabCollider = GetComponent<BoxCollider2D>();
            grabCollider.isTrigger = true;
            SyncCollider();
        }

        private void LateUpdate()
        {
            SyncCollider();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            NotifyTouch(other, true);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            NotifyTouch(other, true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            NotifyTouch(other, false);
        }

        public void GetClimbRange(out float bottomY, out float topY)
        {
            Transform top = ResolveTop();
            Transform bottom = ResolveBottom();
            float rawBottom = bottom != null ? bottom.position.y : transform.position.y;
            float rawTop = top != null ? top.position.y : rawBottom + 1f;
            if (rawTop < rawBottom)
            {
                float swap = rawTop;
                rawTop = rawBottom;
                rawBottom = swap;
            }

            bottomY = rawBottom + bottomPadding;
            topY = rawTop - topPadding;
            if (topY < bottomY)
            {
                topY = rawTop;
                bottomY = rawBottom;
            }
        }

        private void NotifyTouch(Collider2D other, bool touching)
        {
            if (other == null || other.isTrigger)
            {
                return;
            }

            Component host = other.attachedRigidbody != null
                ? other.attachedRigidbody
                : other.transform;
            IRopeClimber climber = host.GetComponentInParent<IRopeClimber>();
            if (climber == null)
            {
                return;
            }

            if (touching)
            {
                climber.NotifyRopeTouch(this);
            }
            else
            {
                climber.NotifyRopeLeave(this);
            }
        }

        private Transform ResolveTop()
        {
            return rope != null ? rope.TopAnchor : null;
        }

        private Transform ResolveBottom()
        {
            return rope != null ? rope.BottomAnchor : null;
        }

        private void SyncCollider()
        {
            if (grabCollider == null)
            {
                grabCollider = GetComponent<BoxCollider2D>();
            }

            if (grabCollider == null)
            {
                return;
            }

            Transform top = ResolveTop();
            Transform bottom = ResolveBottom();
            if (top == null || bottom == null)
            {
                return;
            }

            float height = Mathf.Abs(top.position.y - bottom.position.y);
            float worldScaleY = Mathf.Abs(transform.lossyScale.y);
            float localHeight = height / Mathf.Max(0.0001f, worldScaleY);
            grabCollider.isTrigger = true;
            grabCollider.size = new Vector2(grabWidth, Mathf.Max(0.2f, localHeight));
            grabCollider.offset = new Vector2(0f, grabCollider.size.y * 0.5f);
        }
    }
}
