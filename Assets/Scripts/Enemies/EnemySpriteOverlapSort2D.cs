using System.Collections.Generic;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// When two enemy sprites overlap, the crossing (moving) one draws in front
    /// so the sprites do not z-fight / double.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class EnemySpriteOverlapSort2D : MonoBehaviour
    {
        private const int UniqueOrderSpan = 8;
        private const int OverlapFrontBoost = 8;
        private const float MovingSpeedSqr = 0.0025f;

        private static readonly List<EnemySpriteOverlapSort2D> Active =
            new List<EnemySpriteOverlapSort2D>(32);

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private int baseSortingOrder;
        private int uniqueOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            if (spriteRenderer != null)
            {
                baseSortingOrder = spriteRenderer.sortingOrder;
            }

            uniqueOffset = Mathf.Abs(GetInstanceID()) % UniqueOrderSpan;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            ApplyOrder(inFront: false);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = baseSortingOrder;
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null || !spriteRenderer.enabled)
            {
                return;
            }

            bool inFront = false;
            Bounds bounds = spriteRenderer.bounds;
            for (int i = 0; i < Active.Count; i++)
            {
                EnemySpriteOverlapSort2D other = Active[i];
                if (other == null || other == this || other.spriteRenderer == null)
                {
                    continue;
                }

                if (!bounds.Intersects(other.spriteRenderer.bounds))
                {
                    continue;
                }

                if (IsCrossingInFrontOf(other))
                {
                    inFront = true;
                    break;
                }
            }

            ApplyOrder(inFront);
        }

        private bool IsCrossingInFrontOf(EnemySpriteOverlapSort2D other)
        {
            float thisSpeed = GetSpeedSqr();
            float otherSpeed = other.GetSpeedSqr();
            bool thisMoving = thisSpeed >= MovingSpeedSqr;
            bool otherMoving = otherSpeed >= MovingSpeedSqr;

            if (thisMoving && !otherMoving)
            {
                return true;
            }

            if (!thisMoving)
            {
                return false;
            }

            if (thisSpeed > otherSpeed + 0.0001f)
            {
                return true;
            }

            if (otherSpeed > thisSpeed + 0.0001f)
            {
                return false;
            }

            return uniqueOffset >= other.uniqueOffset;
        }

        private float GetSpeedSqr()
        {
            if (body == null)
            {
                return 0f;
            }

            return body.linearVelocity.sqrMagnitude;
        }

        private void ApplyOrder(bool inFront)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            int order = baseSortingOrder + uniqueOffset;
            if (inFront)
            {
                order += OverlapFrontBoost;
            }

            if (spriteRenderer.sortingOrder != order)
            {
                spriteRenderer.sortingOrder = order;
            }
        }
    }
}
