using UnityEngine;

namespace Castlevania2D.Level
{
    /// <summary>
    /// Shows a world-space prompt sprite above a skeleton when the player stands on a ground line.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkeletonPromptPopup2D : MonoBehaviour
    {
        [Header("Ground Line")]
        [SerializeField] private Vector2 lineCenter = new Vector2(19.7f, -37.2f);
        [SerializeField] private float lineHalfWidth = 1.4f;
        [SerializeField] private float lineHalfHeight = 0.6f;

        [Header("Popup")]
        [SerializeField] private Sprite promptSprite;
        [SerializeField] private SpriteRenderer popupRenderer;
        [SerializeField] private Vector3 popupLocalPosition = new Vector3(0f, 2.35f, 0f);
        [SerializeField] private Vector3 popupScale = new Vector3(0.15f, 0.15f, 1f);
        [SerializeField] private float popDuration = 0.18f;

        [Header("Detection")]
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private Transform playerRoot;
        private bool visible;
        private float popProgress;

        private void Awake()
        {
            EnsurePopup();
            HideImmediate();
        }

        private void Start()
        {
            CachePlayerRoot();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsurePopup();
        }
#endif

        private void Update()
        {
            if (playerRoot == null)
            {
                CachePlayerRoot();
            }

            if (playerRoot != null)
            {
                Vector2 playerPosition = playerRoot.position;
                visible = Mathf.Abs(playerPosition.x - lineCenter.x) <= lineHalfWidth
                          && Mathf.Abs(playerPosition.y - lineCenter.y) <= lineHalfHeight;
            }

            UpdatePopupVisual();
        }

        private void UpdatePopupVisual()
        {
            if (popupRenderer == null)
            {
                return;
            }

            float target = visible ? 1f : 0f;
            float speed = popDuration > 0.001f ? (1f / popDuration) : 1000f;
            popProgress = Mathf.MoveTowards(popProgress, target, speed * Time.deltaTime);

            float eased = popProgress * popProgress * (3f - 2f * popProgress);
            popupRenderer.transform.localScale = popupScale * eased;

            Color color = Color.white;
            color.a = eased;
            popupRenderer.color = color;

            bool shouldEnable = popProgress > 0.001f && popupRenderer.sprite != null;
            if (popupRenderer.enabled != shouldEnable)
            {
                popupRenderer.enabled = shouldEnable;
            }
        }

        private void EnsurePopup()
        {
            if (popupRenderer == null)
            {
                Transform existing = transform.Find("PromptPopup");
                if (existing != null)
                {
                    popupRenderer = existing.GetComponent<SpriteRenderer>();
                }
            }

            if (popupRenderer == null)
            {
                GameObject popupObject = new GameObject("PromptPopup");
                popupObject.transform.SetParent(transform, false);
                popupRenderer = popupObject.AddComponent<SpriteRenderer>();
            }

            popupRenderer.transform.localPosition = popupLocalPosition;
            popupRenderer.sortingOrder = Mathf.Max(popupRenderer.sortingOrder, 5);

            if (promptSprite != null)
            {
                popupRenderer.sprite = promptSprite;
            }
        }

        private void HideImmediate()
        {
            visible = false;
            popProgress = 0f;
            if (popupRenderer == null)
            {
                return;
            }

            popupRenderer.enabled = false;
            popupRenderer.transform.localScale = Vector3.zero;
            Color color = Color.white;
            color.a = 0f;
            popupRenderer.color = color;
        }

        private void CachePlayerRoot()
        {
            if (playerRoot != null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(playerObjectName))
            {
                GameObject playerObject = GameObject.Find(playerObjectName);
                if (playerObject != null)
                {
                    playerRoot = playerObject.transform;
                    return;
                }
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerRoot = taggedPlayer.transform.root;
            }
        }
    }
}
