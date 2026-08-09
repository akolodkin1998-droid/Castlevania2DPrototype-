using Castlevania2D.Environment;
using UnityEngine;

namespace Castlevania2D.Enemies
{
    /// <summary>
    /// When the player is past <see cref="basketModeMinPlayerX"/>, listens for Ent/Mushomor deaths
    /// and queues eagle stone throws at a non-full basket.
    /// Preferred mapping is Ent→StoneBasket_01, Mushomor→StoneBasket_02; if that basket is
    /// already full, the throw redirects to the other basket when it still has room.
    /// If both baskets are full, no throw is queued.
    /// </summary>
    [RequireComponent(typeof(EagleFlyerEnemy2D))]
    public sealed class EagleBasketAssault2D : MonoBehaviour
    {
        [Header("Zone")]
        [Tooltip("Player world X at/above this enables basket mode (no player attacks).")]
        [SerializeField] private float basketModeMinPlayerX = 54f;
        [SerializeField] private string playerObjectName = "Player_HeroKnight";

        [Header("Baskets")]
        [SerializeField] private Transform stoneBasket01;
        [SerializeField] private Transform stoneBasket02;
        [SerializeField] private string basket01Name = "StoneBasket_01";
        [SerializeField] private string basket02Name = "StoneBasket_02";

        private EagleFlyerEnemy2D eagle;
        private Transform player;
        private StoneBasketHitStages2D basket01Stages;
        private StoneBasketHitStages2D basket02Stages;

        private void Awake()
        {
            eagle = GetComponent<EagleFlyerEnemy2D>();
            ResolveRefs();
        }

        private void OnEnable()
        {
            EnemyDeathEvents.Died += OnEnemyDied;
        }

        private void OnDisable()
        {
            EnemyDeathEvents.Died -= OnEnemyDied;
        }

        private void Start()
        {
            ResolveRefs();
            if (eagle != null)
            {
                eagle.ConfigureBasketMode(basketModeMinPlayerX, enabled: true);
            }
        }

        private void OnEnemyDied(EnemyDeathKind kind)
        {
            ResolveRefs();
            if (eagle == null || !eagle.IsBasketModeActive)
            {
                return;
            }

            if (kind != EnemyDeathKind.Ent && kind != EnemyDeathKind.Mushomor)
            {
                return;
            }

            Transform target = GetPreferredBasket(kind);
            if (target != null)
            {
                eagle.QueueBasketThrow(target);
            }
        }

        /// <summary>
        /// Prefers the kill-type basket (Ent→01, Mushomor→02) when it has fewer than
        /// 5 hits (<see cref="StoneBasketHitStages2D.IsFull"/>); otherwise falls back to
        /// the other non-full basket. Once one basket is full, all throws go to the other
        /// until it is also full. Returns null if both are full.
        /// </summary>
        private Transform GetPreferredBasket(EnemyDeathKind kind)
        {
            Transform preferred = kind == EnemyDeathKind.Ent ? stoneBasket01 : stoneBasket02;
            Transform other = kind == EnemyDeathKind.Ent ? stoneBasket02 : stoneBasket01;
            StoneBasketHitStages2D preferredStages = kind == EnemyDeathKind.Ent ? basket01Stages : basket02Stages;
            StoneBasketHitStages2D otherStages = kind == EnemyDeathKind.Ent ? basket02Stages : basket01Stages;

            if (CanReceiveThrow(preferred, preferredStages))
            {
                return preferred;
            }

            if (CanReceiveThrow(other, otherStages))
            {
                return other;
            }

            return null;
        }

        private static bool CanReceiveThrow(Transform basket, StoneBasketHitStages2D stages)
        {
            if (basket == null)
            {
                return false;
            }

            // Missing stages component: allow throw (visual fill tracking unavailable).
            return stages == null || !stages.IsFull;
        }

        private void ResolveRefs()
        {
            if (player == null)
            {
                GameObject go = GameObject.Find(playerObjectName);
                if (go != null)
                {
                    player = go.transform;
                }
            }

            if (stoneBasket01 == null)
            {
                GameObject b1 = GameObject.Find(basket01Name);
                if (b1 != null)
                {
                    stoneBasket01 = b1.transform;
                }
            }

            if (stoneBasket02 == null)
            {
                GameObject b2 = GameObject.Find(basket02Name);
                if (b2 != null)
                {
                    stoneBasket02 = b2.transform;
                }
            }

            if (basket01Stages == null && stoneBasket01 != null)
            {
                basket01Stages = stoneBasket01.GetComponent<StoneBasketHitStages2D>();
            }

            if (basket02Stages == null && stoneBasket02 != null)
            {
                basket02Stages = stoneBasket02.GetComponent<StoneBasketHitStages2D>();
            }
        }
    }
}
