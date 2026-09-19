using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WoodenElevatorCabin2D : MonoBehaviour
    {
        [SerializeField] private int playerInsideSortingOrder = 3;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        private BoxCollider2D interiorCollider;
        private BoxCollider2D ceilingCollider;
        private BoxCollider2D floorCollider;
        private SpriteRenderer playerRenderer;
        private int playerSortingOrder;
        private Transform passengerRoot;
        private Collider2D passengerBody;
        private WoodenElevator2D elevator;
        private bool passengerInside;

        private void Awake()
        {
            elevator = GetComponentInParent<WoodenElevator2D>();
            interiorCollider = GetComponent<BoxCollider2D>();
            if (interiorCollider != null)
            {
                interiorCollider.isTrigger = true;
            }

            Transform root = transform.parent;
            if (root != null)
            {
                Transform ceiling = root.Find("Ceiling");
                Transform floor = root.Find("Floor");
                ceilingCollider = ceiling != null ? ceiling.GetComponent<BoxCollider2D>() : null;
                floorCollider = floor != null ? floor.GetComponent<BoxCollider2D>() : null;
            }
        }

        private void OnDisable()
        {
            SetInside(false);
            RestorePlayerSorting();
        }

        private void FixedUpdate()
        {
            bool inside = passengerBody != null && IsStandingInsideCabin(passengerBody);
            SetInside(inside);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCapturePassenger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryCapturePassenger(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsPlayerBodyCollider(other))
            {
                return;
            }

            if (passengerBody != null && other != passengerBody)
            {
                return;
            }

            SetInside(false);
        }

        private void TryCapturePassenger(Collider2D other)
        {
            if (!IsPlayerBodyCollider(other))
            {
                return;
            }

            CachePlayerRenderer(other);
            passengerRoot = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform;
            passengerBody = other;
            SetInside(IsStandingInsideCabin(other));
        }

        private bool IsStandingInsideCabin(Collider2D body)
        {
            if (body == null)
            {
                return false;
            }

            Bounds bodyBounds = body.bounds;
            if (ceilingCollider != null && bodyBounds.min.y >= ceilingCollider.bounds.min.y)
            {
                return false;
            }

            if (floorCollider != null && bodyBounds.min.y < floorCollider.bounds.min.y - 0.25f)
            {
                return false;
            }

            return interiorCollider == null || interiorCollider.bounds.Intersects(bodyBounds);
        }

        private void SetInside(bool inside)
        {
            if (inside != passengerInside)
            {
                passengerInside = inside;
                if (inside)
                {
                    ApplyPlayerInsideSorting();
                }
                else
                {
                    RestorePlayerSorting();
                }
            }

            NotifyElevatorPassenger(inside);
        }

        private void NotifyElevatorPassenger(bool inside)
        {
            if (elevator == null)
            {
                elevator = GetComponentInParent<WoodenElevator2D>();
            }

            if (elevator == null)
            {
                return;
            }

            elevator.SetPassenger(passengerRoot, inside);
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
            if (playerRenderer == null)
            {
                return;
            }

            playerRenderer.sortingOrder = playerSortingOrder;
        }

        private bool IsPlayerBodyCollider(Collider2D other)
        {
            if (other == null || other.isTrigger)
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
