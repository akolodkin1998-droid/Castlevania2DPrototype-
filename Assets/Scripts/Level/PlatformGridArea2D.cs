using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Authoring frame on Platform_Grid parent. No physics — Offset/Size are visual-only.
    /// Edit them in the Inspector; the cyan gizmo shows the area in Scene view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlatformGridArea2D : MonoBehaviour
    {
        [SerializeField] private Vector2 offset = new Vector2(0.5f, -18f);
        [SerializeField] private Vector2 size = new Vector2(28f, 50f);
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.25f);
        [SerializeField] private Color gizmoWireColor = new Color(0.15f, 0.7f, 1f, 0.95f);

        public Vector2 Offset => offset;
        public Vector2 Size => size;

        private void Awake()
        {
            StripAnyColliders();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            StripAnyColliders();
        }

        private void OnDrawGizmos()
        {
            DrawAreaGizmo(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawAreaGizmo(selected: true);
        }

        private void DrawAreaGizmo(bool selected)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 center = offset;
            Vector3 boxSize = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.05f);

            Color fill = gizmoColor;
            Color wire = gizmoWireColor;
            if (selected)
            {
                fill.a = Mathf.Min(1f, fill.a + 0.2f);
                wire.a = 1f;
            }

            Gizmos.color = fill;
            Gizmos.DrawCube(center, boxSize);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(center, boxSize);

            Gizmos.matrix = previous;
        }
#endif

        private void StripAnyColliders()
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    DestroyCollider(colliders[i]);
                }
            }
        }

        private static void DestroyCollider(Collider2D collider)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
