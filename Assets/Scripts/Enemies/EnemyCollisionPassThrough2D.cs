using System;
using System.Collections.Generic;
using UnityEngine;
using EnemyHealth = Castlevania2D.Health.Health;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Lets enemy bodies overlap instead of pushing or blocking each other.
    /// Attached at play start to every non-player <see cref="EnemyHealth"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class EnemyCollisionPassThrough2D : MonoBehaviour
    {
        private static readonly List<Collider2D> Bodies = new List<Collider2D>(32);

        private Collider2D[] ownedBodies;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Bodies.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToSceneEnemies()
        {
            EnemyHealth[] all = UnityEngine.Object.FindObjectsByType<EnemyHealth>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                EnemyHealth health = all[i];
                if (health == null || IsPlayerRoot(health.transform.root))
                {
                    continue;
                }

                if (health.GetComponent<EnemyCollisionPassThrough2D>() == null)
                {
                    health.gameObject.AddComponent<EnemyCollisionPassThrough2D>();
                }
            }
        }

        public static bool IsEnemyBody(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform root = collider.attachedRigidbody != null
                ? collider.attachedRigidbody.transform
                : collider.transform.root;
            if (root == null || IsPlayerRoot(root))
            {
                return false;
            }

            return root.GetComponent<EnemyHealth>() != null;
        }

        private void OnEnable()
        {
            ownedBodies = CollectBodyColliders();
            for (int i = 0; i < ownedBodies.Length; i++)
            {
                Register(ownedBodies[i]);
            }
        }

        private void OnDisable()
        {
            if (ownedBodies == null)
            {
                return;
            }

            for (int i = 0; i < ownedBodies.Length; i++)
            {
                Unregister(ownedBodies[i]);
            }

            ownedBodies = null;
        }

        private Collider2D[] CollectBodyColliders()
        {
            Collider2D[] all = GetComponentsInChildren<Collider2D>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsBodyCollider(all[i]))
                {
                    count++;
                }
            }

            var bodies = new Collider2D[count];
            int write = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsBodyCollider(all[i]))
                {
                    bodies[write++] = all[i];
                }
            }

            return bodies;
        }

        private static bool IsBodyCollider(Collider2D collider)
        {
            return collider != null && collider.enabled && !collider.isTrigger;
        }

        private static void Register(Collider2D body)
        {
            if (body == null)
            {
                return;
            }

            for (int i = Bodies.Count - 1; i >= 0; i--)
            {
                Collider2D other = Bodies[i];
                if (other == null)
                {
                    Bodies.RemoveAt(i);
                    continue;
                }

                if (other == body)
                {
                    return;
                }

                Physics2D.IgnoreCollision(body, other, true);
            }

            Bodies.Add(body);
        }

        private static void Unregister(Collider2D body)
        {
            Bodies.Remove(body);
        }

        private static bool IsPlayerRoot(Transform root)
        {
            return root.CompareTag("Player") ||
                   root.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   root.name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
