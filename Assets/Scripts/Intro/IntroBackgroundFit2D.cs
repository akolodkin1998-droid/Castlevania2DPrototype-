using UnityEngine;

namespace Castlevania2D.Intro
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class IntroBackgroundFit2D : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            Fit();
        }

        private void LateUpdate()
        {
            Fit();
        }

        public void Fit()
        {
            if (targetCamera == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            float worldHeight = targetCamera.orthographicSize * 2f;
            float worldWidth = worldHeight * targetCamera.aspect;
            Vector2 size = spriteRenderer.sprite.bounds.size;
            if (size.x <= 0.0001f || size.y <= 0.0001f)
            {
                return;
            }

            transform.position = new Vector3(
                targetCamera.transform.position.x,
                targetCamera.transform.position.y,
                0f);
            transform.localScale = new Vector3(worldWidth / size.x, worldHeight / size.y, 1f);
        }
    }
}
