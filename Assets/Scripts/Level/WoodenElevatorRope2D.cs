using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WoodenElevatorRope2D : MonoBehaviour
    {
        [SerializeField] private Transform topAnchor;
        [SerializeField] private Transform bottomAnchor;
        [SerializeField] [Min(0.02f)] private float width = 0.06f;

        private SpriteRenderer spriteRenderer;
        private float authoredLocalHeight;
        private float authoredGap;
        private bool captured;

        public Transform TopAnchor => topAnchor;
        public Transform BottomAnchor => bottomAnchor;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (GetComponent<ClimbableRope2D>() == null)
            {
                gameObject.AddComponent<ClimbableRope2D>();
            }

            CaptureAuthoredLength();
        }

        private void LateUpdate()
        {
            ApplyAuthoredStretch();
        }

        private void CaptureAuthoredLength()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            authoredLocalHeight = spriteRenderer != null ? spriteRenderer.size.y : 1f;
            authoredGap = MeasureGap();
            captured = true;
        }

        private void ApplyAuthoredStretch()
        {
            if (!captured)
            {
                CaptureAuthoredLength();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            float extraLocal = 0f;
            if (topAnchor != null && bottomAnchor != null)
            {
                float worldScaleY = Mathf.Abs(transform.lossyScale.y);
                extraLocal = (MeasureGap() - authoredGap) / Mathf.Max(0.0001f, worldScaleY);
            }

            float height = Mathf.Max(0.02f, authoredLocalHeight + extraLocal);
            spriteRenderer.enabled = true;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = new Vector2(Mathf.Max(0.02f, width), height);
        }

        public void UpdateLength()
        {
            if (!Application.isPlaying)
            {
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponent<SpriteRenderer>();
                }

                if (spriteRenderer == null || spriteRenderer.sprite == null || topAnchor == null || bottomAnchor == null)
                {
                    return;
                }

                float distance = MeasureGap();
                if (distance < 0.02f)
                {
                    return;
                }

                float worldScaleY = Mathf.Abs(transform.lossyScale.y);
                float localHeight = distance / Mathf.Max(0.0001f, worldScaleY);
                spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                spriteRenderer.size = new Vector2(Mathf.Max(0.02f, width), localHeight);
                return;
            }

            ApplyAuthoredStretch();
        }

        private float MeasureGap()
        {
            if (topAnchor == null || bottomAnchor == null)
            {
                return 0f;
            }

            return topAnchor.position.y - bottomAnchor.position.y;
        }
    }
}
