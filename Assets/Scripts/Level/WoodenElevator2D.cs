using UnityEngine;

namespace Castlevania2D.Level
{
    [DisallowMultipleComponent]
    public sealed class WoodenElevator2D : MonoBehaviour
    {
        private static readonly string[] CarChildNames =
        {
            "Cabin",
            "Floor",
            "LeftWall",
            "Ceiling",
            "Interior",
            "FrontBeam",
            "HangingRope",
        };

        [SerializeField] private WoodenElevatorWinch2D winch;
        [SerializeField] [Min(0.1f)] private float descentSpeed = 2f;
        [SerializeField] [Min(0.001f)] private float groundSkin = 0.03f;

        private static readonly RaycastHit2D[] GroundHits = new RaycastHit2D[16];

        private Transform[] carParts;
        private BoxCollider2D floorCollider;
        private ContactFilter2D groundFilter;
        private bool armed;
        private bool descending;
        private bool finished;
        private bool passengerInside;
        private Transform passenger;
        private Rigidbody2D passengerBody;

        public bool IsMoving { get; private set; }
        public bool IsArmed => armed;
        public bool HasFinished => finished;

        public void ArmFromLever()
        {
            if (finished || descending)
            {
                return;
            }

            armed = true;
            if (passengerInside)
            {
                BeginDescent();
            }
        }

        public void SetPassenger(Transform passengerRoot, bool inside)
        {
            passengerInside = inside;
            if (inside)
            {
                passenger = passengerRoot;
                passengerBody = passengerRoot != null
                    ? passengerRoot.GetComponent<Rigidbody2D>()
                    : null;
                if (armed && !descending && !finished)
                {
                    BeginDescent();
                }
            }
            else if (!descending)
            {
                passenger = null;
                passengerBody = null;
            }
        }

        public void SetMoving(bool moving)
        {
            IsMoving = moving;
            if (winch != null)
            {
                winch.SetSpinning(moving);
            }
        }

        private void Awake()
        {
            groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false,
                useDepth = false
            };
            CacheCarParts();
        }

        private void FixedUpdate()
        {
            if (!descending || !passengerInside)
            {
                return;
            }

            float requested = descentSpeed * Time.fixedDeltaTime;
            float allowed = requested;
            if (TryGetGroundDistance(requested, out float groundDistance))
            {
                allowed = Mathf.Min(requested, groundDistance);
            }

            if (allowed <= 0.0001f)
            {
                FinishDescent();
                return;
            }

            MoveCar(-allowed);
            if (allowed < requested - 0.0001f)
            {
                FinishDescent();
            }
        }

        private void BeginDescent()
        {
            if (descending || finished)
            {
                return;
            }

            CacheCarParts();
            descending = true;
            SetMoving(true);
        }

        private void FinishDescent()
        {
            descending = false;
            finished = true;
            armed = false;
            SetMoving(false);
        }

        private void MoveCar(float deltaY)
        {
            Vector3 offset = new Vector3(0f, deltaY, 0f);
            if (carParts != null)
            {
                for (int i = 0; i < carParts.Length; i++)
                {
                    if (carParts[i] != null)
                    {
                        carParts[i].position += offset;
                    }
                }
            }

            if (!passengerInside || passenger == null)
            {
                return;
            }

            if (passengerBody != null)
            {
                passengerBody.position += new Vector2(0f, deltaY);
            }
            else
            {
                passenger.position += offset;
            }
        }

        private void CacheCarParts()
        {
            if (carParts != null)
            {
                return;
            }

            var found = new System.Collections.Generic.List<Transform>(CarChildNames.Length);
            for (int i = 0; i < CarChildNames.Length; i++)
            {
                Transform child = transform.Find(CarChildNames[i]);
                if (child != null)
                {
                    found.Add(child);
                }
            }

            carParts = found.ToArray();
            Transform floor = transform.Find("Floor");
            floorCollider = floor != null ? floor.GetComponent<BoxCollider2D>() : null;
        }

        private bool TryGetGroundDistance(float maxDistance, out float distance)
        {
            distance = maxDistance;
            if (floorCollider == null)
            {
                return false;
            }

            float castDistance = maxDistance + groundSkin;
            int hitCount = floorCollider.Cast(Vector2.down, groundFilter, GroundHits, castDistance);
            bool hitGround = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = GroundHits[i];
                if (hit.collider == null || ShouldIgnoreGroundHit(hit.collider))
                {
                    continue;
                }

                float untilContact = Mathf.Max(0f, hit.distance - groundSkin);
                if (!hitGround || untilContact < distance)
                {
                    distance = untilContact;
                    hitGround = true;
                }
            }

            return hitGround;
        }

        private bool ShouldIgnoreGroundHit(Collider2D hit)
        {
            if (hit == floorCollider)
            {
                return true;
            }

            Transform hitTransform = hit.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform == transform)
            {
                return true;
            }

            if (passenger != null
                && (hitTransform == passenger || hitTransform.IsChildOf(passenger)))
            {
                return true;
            }

            return false;
        }
    }
}
