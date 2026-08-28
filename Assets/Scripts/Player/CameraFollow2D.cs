using Castlevania2D.Level;
using UnityEngine;

namespace Castlevania2D.Player
{
    [DefaultExecutionOrder(150)]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        // Positive Y places the player lower on screen.
        // Bottom edge locked: offsetY = orthoSize - (playerY - bottomWorldY).
        // At orthoSize 6 with bottom ≈ -38.197: offsetY 4.815.
        [SerializeField] private Vector2 offset = new Vector2(0f, 4.815f);
        [SerializeField] private float smoothTime = 0.15f;
        [SerializeField] private bool lockWorldY = true;
        [SerializeField] private float lockedWorldY = 4.99f;
        [SerializeField] private bool clampToSceneWalls = true;
        [SerializeField] private string leftWallObjectName = "LeftWall";
        [SerializeField] private string rightWallObjectName = "RightWall";
        [SerializeField] private float fallbackSceneMinX = -12f;
        [SerializeField] private float fallbackSceneMaxX = 15f;
        [SerializeField] private Collider2D[] cameraStopWalls;

        private Camera cameraComponent;
        private Vector3 velocity;
        private bool hasWallBounds;
        private float sceneMinX;
        private float sceneMaxX;
        private float savedOrthographicSize;
        private bool hasOrthoSnapshot;
        private bool viewLocked;
        private Vector3 lockedWorldCenter;
        private Vector2 lockedHalfExtents;
        private bool frameToSprite;
        private float lastFramedAspect = -1f;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent != null)
            {
                savedOrthographicSize = cameraComponent.orthographicSize;
                hasOrthoSnapshot = savedOrthographicSize > 0.5f;
            }
        }

        private void Start()
        {
            CacheTargetIfNeeded();
            if (!viewLocked)
            {
                RestoreDefaultViewport();
                RestoreDefaultZoom();
            }

            CacheWallBounds();
            SnapToTarget();
        }

        public void SnapNow()
        {
            SnapToTarget();
        }

        public void EnterLockedView(Vector3 worldCenter, float orthographicSize)
        {
            viewLocked = true;
            frameToSprite = false;
            lockedWorldCenter = worldCenter;
            RestoreDefaultViewport();
            ApplyLockedZoom(orthographicSize);
            velocity = Vector3.zero;
            SnapToTarget();
        }

        public void EnterFramedSpriteView(Bounds spriteBounds)
        {
            if (spriteBounds.size.sqrMagnitude < 0.001f)
            {
                EnterLockedView(spriteBounds.center, 6f);
                return;
            }

            viewLocked = true;
            frameToSprite = true;
            lockedWorldCenter = spriteBounds.center;
            lockedHalfExtents = spriteBounds.extents;
            lastFramedAspect = -1f;
            velocity = Vector3.zero;
            ApplySpriteFrame();
            SnapToTarget();
        }

        public void ExitLockedView()
        {
            viewLocked = false;
            frameToSprite = false;
            RestoreDefaultViewport();
            RestoreDefaultZoom();
            velocity = Vector3.zero;
        }

        private void CacheTargetIfNeeded()
        {
            if (target != null)
            {
                return;
            }

            GameObject playerObject = GameObject.Find("Player_HeroKnight");
            if (playerObject != null)
            {
                target = playerObject.transform;
            }
        }

        private void RestoreDefaultZoom()
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent == null)
            {
                return;
            }

            float size = hasOrthoSnapshot ? savedOrthographicSize : 6f;
            if (size < 0.5f || size > 20f)
            {
                size = 6f;
            }

            cameraComponent.orthographicSize = size;
        }

        private void RestoreDefaultViewport()
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent == null)
            {
                return;
            }

            cameraComponent.rect = new Rect(0f, 0f, 1f, 1f);
        }

        private void ApplySpriteFrame()
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent == null || lockedHalfExtents.y <= 0.001f)
            {
                return;
            }

            float windowAspect = Screen.height > 0
                ? (float)Screen.width / Screen.height
                : 0f;
            if (windowAspect <= 0f)
            {
                return;
            }

            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = lockedHalfExtents.y;
            float spriteAspect = lockedHalfExtents.x / lockedHalfExtents.y;
            ApplyLetterbox(cameraComponent, windowAspect, spriteAspect);
            lastFramedAspect = windowAspect;
        }

        private static void ApplyLetterbox(Camera camera, float windowAspect, float targetAspect)
        {
            if (windowAspect <= 0f || targetAspect <= 0f)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            if (windowAspect > targetAspect)
            {
                float width = targetAspect / windowAspect;
                camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
                return;
            }

            float height = windowAspect / targetAspect;
            camera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        private void ApplyLockedZoom(float orthographicSize)
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (cameraComponent == null)
            {
                return;
            }

            float size = orthographicSize;
            if (size < 0.5f || size > 12f)
            {
                size = hasOrthoSnapshot ? savedOrthographicSize : 6f;
            }

            if (size < 0.5f || size > 12f)
            {
                size = 6f;
            }

            cameraComponent.orthographicSize = size;
        }

        private void SnapToTarget()
        {
            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            if (viewLocked)
            {
                velocity = Vector3.zero;
                transform.position = new Vector3(
                    lockedWorldCenter.x,
                    lockedWorldCenter.y,
                    transform.position.z);
                return;
            }

            if (target == null)
            {
                return;
            }

            Vector3 snapped = BuildTargetPosition();
            velocity = Vector3.zero;
            transform.position = snapped;
        }

        private void LateUpdate()
        {
            if (cameraComponent == null)
            {
                return;
            }

            if (viewLocked)
            {
                if (frameToSprite)
                {
                    float windowAspect = Screen.height > 0
                        ? (float)Screen.width / Screen.height
                        : 0f;
                    if (!Mathf.Approximately(windowAspect, lastFramedAspect))
                    {
                        ApplySpriteFrame();
                    }
                }

                transform.position = new Vector3(
                    lockedWorldCenter.x,
                    lockedWorldCenter.y,
                    transform.position.z);
                return;
            }

            CacheTargetIfNeeded();
            if (target == null)
            {
                return;
            }

            if (clampToSceneWalls && !hasWallBounds)
            {
                CacheWallBounds();
            }

            Vector3 targetPosition = BuildTargetPosition();

            Vector3 nextPosition;
            if (smoothTime <= 0.0001f)
            {
                nextPosition = targetPosition;
                velocity = Vector3.zero;
            }
            else
            {
                nextPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
            }

            if (hasWallBounds)
            {
                nextPosition.x = ClampCameraX(nextPosition.x);
            }

            StopCameraAgainstStopWalls(ref nextPosition);
            transform.position = nextPosition;
        }

        private Vector3 BuildTargetPosition()
        {
            float targetY = lockWorldY ? lockedWorldY : target.position.y + offset.y;
            Vector3 targetPosition = new Vector3(
                target.position.x + offset.x,
                targetY,
                transform.position.z);

            if (hasWallBounds)
            {
                targetPosition.x = ClampCameraX(targetPosition.x);
            }

            StopCameraAgainstStopWalls(ref targetPosition);
            return targetPosition;
        }

        private void StopCameraAgainstStopWalls(ref Vector3 cameraPosition)
        {
            if (cameraStopWalls == null || cameraComponent == null)
            {
                return;
            }

            float halfWidth = GetCameraHalfWidth();
            float halfHeight = GetCameraHalfHeight();
            if (halfWidth <= 0f || halfHeight <= 0f)
            {
                return;
            }

            float viewBottom = cameraPosition.y - halfHeight;
            float viewTop = cameraPosition.y + halfHeight;

            for (int i = 0; i < cameraStopWalls.Length; i++)
            {
                Collider2D wallCollider = cameraStopWalls[i];
                if (wallCollider == null || !wallCollider.enabled)
                {
                    continue;
                }

                Bounds wall = wallCollider.bounds;
                bool cameraHitsWallVertically = viewTop > wall.min.y && viewBottom < wall.max.y;
                if (!cameraHitsWallVertically)
                {
                    continue;
                }

                float viewLeft = cameraPosition.x - halfWidth;
                float viewRight = cameraPosition.x + halfWidth;

                if (cameraPosition.x >= wall.center.x && viewLeft < wall.max.x)
                {
                    cameraPosition.x = wall.max.x + halfWidth;
                    velocity.x = 0f;
                }
                else if (cameraPosition.x < wall.center.x && viewRight > wall.min.x)
                {
                    cameraPosition.x = wall.min.x - halfWidth;
                    velocity.x = 0f;
                }
            }
        }

        private void CacheWallBounds()
        {
            hasWallBounds = false;

            if (!clampToSceneWalls)
            {
                return;
            }

            if (ArenaPlayBounds2D.TryGetWallColumnOuterEdges(
                    leftWallObjectName,
                    rightWallObjectName,
                    out float outerMinX,
                    out float outerMaxX))
            {
                sceneMinX = outerMinX;
                sceneMaxX = outerMaxX;
            }
            else
            {
                sceneMinX = fallbackSceneMinX;
                sceneMaxX = fallbackSceneMaxX;
            }

            hasWallBounds = sceneMaxX > sceneMinX;
        }

        private float ClampCameraX(float cameraX)
        {
            float halfWidth = GetCameraHalfWidth();
            if (halfWidth <= 0f)
            {
                return cameraX;
            }

            float minCenterX = sceneMinX + halfWidth;
            float maxCenterX = sceneMaxX - halfWidth;

            float clampedX;
            if (minCenterX <= maxCenterX)
            {
                clampedX = Mathf.Clamp(cameraX, minCenterX, maxCenterX);
            }
            else
            {
                float sceneCenterX = (sceneMinX + sceneMaxX) * 0.5f;
                clampedX = cameraX < sceneCenterX ? minCenterX : maxCenterX;
            }

            if (Mathf.Approximately(clampedX, minCenterX) || Mathf.Approximately(clampedX, maxCenterX))
            {
                velocity.x = 0f;
            }

            return clampedX;
        }

        private float GetCameraHalfWidth()
        {
            if (cameraComponent == null || !cameraComponent.orthographic)
            {
                return 0f;
            }

            float depth = Mathf.Abs(transform.position.z);
            Vector3 leftEdge = cameraComponent.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth));
            Vector3 rightEdge = cameraComponent.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth));
            return Mathf.Abs(rightEdge.x - leftEdge.x) * 0.5f;
        }

        private float GetCameraHalfHeight()
        {
            if (cameraComponent == null || !cameraComponent.orthographic)
            {
                return 0f;
            }

            float depth = Mathf.Abs(transform.position.z);
            Vector3 bottom = cameraComponent.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth));
            Vector3 top = cameraComponent.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
            return Mathf.Abs(top.y - bottom.y) * 0.5f;
        }
    }
}
