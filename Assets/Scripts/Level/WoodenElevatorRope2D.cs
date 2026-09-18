using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WoodenElevatorRope2D : MonoBehaviour
    {
        [SerializeField] private Transform topAnchor;
        [SerializeField] private Transform bottomAnchor;

        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Stretch();
        }

        private void LateUpdate()
        {
            Stretch();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Stretch();
            }
        }
#endif

        public void Stretch()
        {
            if (topAnchor == null || bottomAnchor == null)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Vector3 top = topAnchor.position;
            Vector3 bottom = bottomAnchor.position;
            float distance = top.y - bottom.y;
            if (distance < 0.02f)
            {
                spriteRenderer.enabled = false;
                return;
            }

            spriteRenderer.enabled = true;
            transform.position = new Vector3(bottom.x, bottom.y, bottom.z);
            transform.rotation = Quaternion.identity;

            float parentScaleY = transform.parent != null
                ? Mathf.Abs(transform.parent.lossyScale.y)
                : 1f;
            float localHeight = distance / Mathf.Max(0.0001f, parentScaleY);
            float width = spriteRenderer.sprite.bounds.size.x;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = new Vector2(Mathf.Max(0.02f, width), localHeight);
            transform.localScale = Vector3.one;
        }
    }
}
