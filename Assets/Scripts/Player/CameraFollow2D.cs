using Castlevania2D.Level;
using UnityEngine;

namespace Castlevania2D.Player
{
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

        private Camera cameraComponent;
        private Vector3 velocity;
        private bool hasWallBounds;
        private float sceneMinX;
        private float sceneMaxX;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
        }

        private void Start()
        {
            CacheWallBounds();
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            if (cameraComponent == null)
            {
                cameraComponent = GetComponent<Camera>();
            }

            Vector3 snapped = BuildTargetPosition();
            velocity = Vector3.zero;
            transform.position = snapped;
        }

        private void LateUpdate()
        {
            if (target == null || cameraComponent == null)
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

            return targetPosition;
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
    }
}
