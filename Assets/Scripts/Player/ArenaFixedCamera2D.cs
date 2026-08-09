using UnityEngine;

namespace Castlevania2D.Player
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaFixedCamera2D : MonoBehaviour
    {
        [SerializeField] private string redSkyObjectName = "Red Sky";
        [SerializeField] private float centerZ = -10f;
        [SerializeField] private Vector2 fallbackCenter = new Vector2(0.48f, 4.37f);
        [SerializeField] private Vector2 fallbackHalfExtents = new Vector2(10.232748f, 4.725f);
        [SerializeField] private Color backgroundColor = new Color(0.14f, 0.03f, 0.05f, 1f);

        private Camera cameraComponent;
        private Rect lastViewport = new Rect(-1f, -1f, -1f, -1f);
        private float lastWindowAspect = -1f;

        private void OnEnable()
        {
            cameraComponent = GetComponent<Camera>();
            ApplyFixedFraming();
        }

        private void LateUpdate()
        {
            if (cameraComponent == null)
            {
                return;
            }

            float windowAspect = GetWindowAspect();
            if (windowAspect <= 0f)
            {
                return;
            }

            if (!Mathf.Approximately(windowAspect, lastWindowAspect) || cameraComponent.rect != lastViewport)
            {
                ApplyFixedFraming();
            }
        }

        public void ApplyFixedFraming()
        {
            Camera camera = GetComponent<Camera>();
            if (camera == null)
            {
                return;
            }

            cameraComponent = camera;

            CameraFollow2D follow = GetComponent<CameraFollow2D>();
            if (follow != null)
            {
                follow.enabled = false;
            }

            Bounds frameBounds = ResolveRedSkyBounds();

            float targetAspect = frameBounds.size.x / frameBounds.size.y;
            if (targetAspect <= 0f)
            {
                return;
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographicSize = frameBounds.extents.y;

            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(frameBounds.center.x, frameBounds.center.y, centerZ);

            float windowAspect = GetWindowAspect();
            ApplyViewportLetterbox(camera, windowAspect, targetAspect);
            lastViewport = camera.rect;
            lastWindowAspect = windowAspect;
        }

        private Bounds ResolveRedSkyBounds()
        {
            GameObject redSkyObject = GameObject.Find(redSkyObjectName);
            if (redSkyObject != null
                && redSkyObject.TryGetComponent(out SpriteRenderer spriteRenderer)
                && spriteRenderer.sprite != null
                && HasValidBounds(spriteRenderer.bounds))
            {
                return spriteRenderer.bounds;
            }

            return new Bounds(
                new Vector3(fallbackCenter.x, fallbackCenter.y, 0f),
                new Vector3(fallbackHalfExtents.x * 2f, fallbackHalfExtents.y * 2f, 0f));
        }

        private static float GetWindowAspect()
        {
            if (Screen.height <= 0)
            {
                return 0f;
            }

            return (float)Screen.width / Screen.height;
        }

        private static void ApplyViewportLetterbox(Camera camera, float windowAspect, float targetAspect)
        {
            if (windowAspect <= 0f || targetAspect <= 0f)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            Rect viewport;
            if (windowAspect > targetAspect)
            {
                float width = targetAspect / windowAspect;
                viewport = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                float height = windowAspect / targetAspect;
                viewport = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            camera.rect = viewport;
        }

        private static bool HasValidBounds(Bounds bounds)
        {
            return bounds.size.sqrMagnitude > 0.001f;
        }
    }
}
