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
        private SpriteRenderer cabinRenderer;
        private SpriteRenderer playerRenderer;
        private int playerSortingOrder;
        private Transform passengerRoot;
        private Collider2D passengerBody;
        private WoodenElevator2D elevator;
        private bool passengerInside;
        private Transform playerRoot;
        private float nextPlayerSearchTime;

        public void RefreshPassengerNow()
        {
            CachePlayer();
            SetInside(IsPlayerInCabin());
        }

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
                Transform cabin = root.Find("Cabin");
                ceilingCollider = ceiling != null ? ceiling.GetComponent<BoxCollider2D>() : null;
                floorCollider = floor != null ? floor.GetComponent<BoxCollider2D>() : null;
                cabinRenderer = cabin != null ? cabin.GetComponent<SpriteRenderer>() : null;
            }
        }

        private void OnDisable()
        {
            SetInside(false);
            RestorePlayerSorting();
        }

        private void FixedUpdate()
        {
            CachePlayer();
            SetInside(IsPlayerInCabin());
        }

        private void CachePlayer()
        {
            if (playerRoot != null || Time.unscaledTime < nextPlayerSearchTime)
            {
                return;
            }

            nextPlayerSearchTime = Time.unscaledTime + 0.5f;
            GameObject playerObject = GameObject.Find(playerObjectName);
            if (playerObject == null)
            {
                return;
            }

            playerRoot = playerObject.transform;
            passengerRoot = playerRoot;
            passengerBody = FindBodyCollider(playerObject);
            CachePlayerRenderer(passengerBody);
        }

        private static Collider2D FindBodyCollider(GameObject playerObject)
        {
            Collider2D[] colliders = playerObject.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return collider;
                }
            }

            return null;
        }

        private bool IsPlayerInCabin()
        {
            if (playerRoot == null)
            {
                return false;
            }

            Bounds body = passengerBody != null
                ? passengerBody.bounds
                : new Bounds(playerRoot.position, new Vector3(0.8f, 1.8f, 1f));

            if (cabinRenderer != null)
            {
                Bounds cabin = cabinRenderer.bounds;
                cabin.Expand(new Vector3(0.4f, 0.6f, 0f));
                if (cabin.Intersects(body))
                {
                    return true;
                }
            }

            if (floorCollider == null)
            {
                return false;
            }

            Bounds floor = floorCollider.bounds;
            float floorTop = floor.max.y;
            float feet = body.min.y;
            bool overlapsFloorX = body.max.x > floor.min.x && body.min.x < floor.max.x;
            bool standingOnFloor = feet >= floorTop - 0.45f && feet <= floorTop + 0.7f;
            if (!overlapsFloorX || !standingOnFloor)
            {
                return false;
            }

            if (ceilingCollider != null && feet >= ceilingCollider.bounds.min.y)
            {
                return false;
            }

            return true;
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
            if (playerRenderer != null || playerRoot == null)
            {
                return;
            }

            playerRenderer = playerRoot.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
            {
                playerRenderer = playerRoot.GetComponentInChildren<SpriteRenderer>();
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
    }
}
