using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WoodenElevatorCabin2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer cabinRenderer;
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private int cabinSortingOrder = 1;
        [SerializeField] private int beamSortingOrder = 6;
        [SerializeField] private int playerInsideSortingOrder = 3;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private int playerOverlapCount;
        private SpriteRenderer playerRenderer;
        private int playerSortingOrder;

        private void Awake()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.isTrigger = true;
            }

            ApplyPropSorting();
        }

        private void OnDisable()
        {
            RestorePlayerSorting();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!TryTrackPlayer(other, 1))
            {
                return;
            }

            ApplyPlayerInsideSorting();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!TryTrackPlayer(other, -1))
            {
                return;
            }

            if (playerOverlapCount <= 0)
            {
                RestorePlayerSorting();
            }
        }

        private bool TryTrackPlayer(Collider2D other, int delta)
        {
            if (!IsPlayerCollider(other))
            {
                return false;
            }

            CachePlayerRenderer(other);
            playerOverlapCount = Mathf.Max(0, playerOverlapCount + delta);
            return true;
        }

        private void CachePlayerRenderer(Collider2D other)
        {
            if (playerRenderer != null)
            {
                return;
            }

            Transform root = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;
            playerRenderer = root.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
            {
                playerRenderer = root.GetComponentInChildren<SpriteRenderer>();
            }

            if (playerRenderer != null)
            {
                playerSortingOrder = playerRenderer.sortingOrder;
            }
        }

        private void ApplyPropSorting()
        {
            if (cabinRenderer != null)
            {
                cabinRenderer.sortingOrder = cabinSortingOrder;
            }

            if (beamRenderer != null)
            {
                beamRenderer.sortingOrder = beamSortingOrder;
            }
        }

        private void ApplyPlayerInsideSorting()
        {
            if (playerRenderer == null)
            {
                return;
            }

            playerRenderer.sortingOrder = playerInsideSortingOrder;
        }

        private void RestorePlayerSorting()
        {
            playerOverlapCount = 0;
            if (playerRenderer == null)
            {
                return;
            }

            playerRenderer.sortingOrder = playerSortingOrder;
        }

        private bool IsPlayerCollider(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            Transform root = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;

            if (root.CompareTag("Player"))
            {
                return true;
            }

            return !string.IsNullOrEmpty(playerObjectName)
                   && root.name.Equals(playerObjectName, System.StringComparison.Ordinal);
        }
    }
}
