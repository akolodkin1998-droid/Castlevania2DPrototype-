using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DecorOneWayPlatform2D : MonoBehaviour
    {
        [SerializeField] private float surfaceThickness = 0.08f;
        [SerializeField] private float surfaceArc = 160f;

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Apply();
            }
        }
#endif

        public void Apply()
        {
            RemoveCollider<EdgeCollider2D>();
            RemoveCollider<PolygonCollider2D>();

            PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
            if (effector == null)
            {
                effector = gameObject.AddComponent<PlatformEffector2D>();
            }

            effector.useOneWay = true;
            effector.useOneWayGrouping = false;
            effector.surfaceArc = surfaceArc;

            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider2D>();
            }

            Bounds localBounds = spriteRenderer.sprite.bounds;
            float scaledThickness = surfaceThickness / Mathf.Max(Mathf.Abs(transform.lossyScale.y), 0.0001f);
            box.size = new Vector2(localBounds.size.x, scaledThickness);
            box.offset = new Vector2(localBounds.center.x, localBounds.max.y - scaledThickness * 0.5f);
            box.usedByEffector = true;
        }

        private void RemoveCollider<TCollider>() where TCollider : Collider2D
        {
            TCollider collider = GetComponent<TCollider>();
            if (collider == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(collider);
                return;
            }
#endif
            Destroy(collider);
        }
    }
}
