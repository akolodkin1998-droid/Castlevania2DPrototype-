using System;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// Fires once when the target crosses the configured activation condition.
    /// </summary>
    public sealed class WizardPlayerSight2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("World X Activation")]
        [SerializeField] private bool useWorldXActivationGate = true;
        [SerializeField] private float activationMinTargetWorldX = 64f;

        [Header("Legacy Sight")]
        [SerializeField] private float sightRange = 18f;
        [SerializeField] private bool useHorizontalDistanceOnly = true;

        public event Action PlayerSighted;

        private bool hasSighted;

        public bool HasSighted => hasSighted;

        public void EditorAssignTarget(Transform player)
        {
            target = player;
        }

        public void ResetSight()
        {
            hasSighted = false;
            CacheTargetIfNeeded();
            enabled = true;
        }

        public void ApplySavedSight(bool savedHasSighted)
        {
            hasSighted = savedHasSighted;
            CacheTargetIfNeeded();
            enabled = !savedHasSighted;
        }

        private void Awake()
        {
            CacheTargetIfNeeded();
        }

        private void Update()
        {
            if (hasSighted)
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            if (useWorldXActivationGate)
            {
                if (target.position.x >= activationMinTargetWorldX &&
                    EnemyAggroLimits.IsWithinVerticalRange(transform, target))
                {
                    SightTarget();
                }

                return;
            }

            if (!EnemyAggroLimits.IsWithinVerticalRange(transform, target))
            {
                return;
            }

            float distance = useHorizontalDistanceOnly
                ? Mathf.Abs(target.position.x - transform.position.x)
                : Vector2.Distance(transform.position, target.position);

            if (distance <= Mathf.Max(0.01f, sightRange))
            {
                SightTarget();
            }
        }

        private void CacheTargetIfNeeded()
        {
            if (target != null || string.IsNullOrEmpty(playerObjectName))
            {
                return;
            }

            GameObject player = GameObject.Find(playerObjectName);
            if (player != null)
            {
                target = player.transform;
            }
        }

        private void SightTarget()
        {
            hasSighted = true;
            PlayerSighted?.Invoke();
            enabled = false;
        }
    }
}
