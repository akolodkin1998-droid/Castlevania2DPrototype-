using System.Collections.Generic;
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
            "GearBox",
        };

        [SerializeField] private WoodenElevatorWinch2D winch;
        [SerializeField] [Min(0.1f)] private float descentSpeed = 2f;
        [SerializeField] [Min(0.001f)] private float groundSkin = 0.03f;

        private static readonly RaycastHit2D[] GroundHits = new RaycastHit2D[16];
        private static readonly Collider2D[] OverlapHits = new Collider2D[32];

        private Transform[] carParts;
        private BoxCollider2D floorCollider;
        private ContactFilter2D groundFilter;
        private readonly HashSet<Collider2D> ignoredLandings = new HashSet<Collider2D>();
        private bool armed;
        private bool descending;
        private bool finished;
        private bool passengerInside;
        private Transform passenger;
        private Rigidbody2D passengerBody;
        private WoodenElevatorCabin2D cabin;
        private float traveledDown;

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
            if (cabin == null)
            {
                cabin = GetComponentInChildren<WoodenElevatorCabin2D>(true);
            }

            cabin?.RefreshPassengerNow();
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
            cabin = GetComponentInChildren<WoodenElevatorCabin2D>(true);
        }

        private void FixedUpdate()
        {
            if (armed && !descending && !finished)
            {
                if (cabin == null)
                {
                    cabin = GetComponentInChildren<WoodenElevatorCabin2D>(true);
                }

                cabin?.RefreshPassengerNow();
            }

            if (!descending)
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
                if (traveledDown >= 0.75f)
                {
                    FinishDescent();
                }

                return;
            }

            MoveCar(-allowed);
            traveledDown += allowed;
            if (allowed < requested - 0.0001f && traveledDown >= 0.75f)
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
            CaptureStartingSupports();
            traveledDown = 0f;
            descending = true;
            SetMoving(true);
        }

        private void FinishDescent()
        {
            descending = false;
            finished = true;
            armed = false;
            ignoredLandings.Clear();
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

            var found = new List<Transform>(CarChildNames.Length);
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

        private void CaptureStartingSupports()
        {
            ignoredLandings.Clear();
            if (floorCollider == null)
            {
                return;
            }

            int overlapCount = floorCollider.Overlap(groundFilter, OverlapHits);
            for (int i = 0; i < overlapCount; i++)
            {
                if (OverlapHits[i] != null)
                {
                    ignoredLandings.Add(OverlapHits[i]);
                }
            }

            int hitCount = floorCollider.Cast(Vector2.down, groundFilter, GroundHits, 0.45f);
            for (int i = 0; i < hitCount; i++)
            {
                if (GroundHits[i].collider != null)
                {
                    ignoredLandings.Add(GroundHits[i].collider);
                }
            }
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

                float untilContact = hit.distance - groundSkin;
                if (untilContact <= 0f)
                {
                    continue;
                }

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
            if (hit == floorCollider || ignoredLandings.Contains(hit))
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

            Transform root = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.transform
                : hitTransform.root;
            if (root != null
                && (root.CompareTag("Player") || root.name == "Player_HeroKnight"))
            {
                return true;
            }

            return false;
        }
    }
}
