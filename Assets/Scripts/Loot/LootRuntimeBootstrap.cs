using EnemyHealth = Castlevania2D.Health.Health;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Wires loot dropping at runtime without relying on editor setup tools.
    /// Also used for portal-summoned mobs instantiated after scene load.
    /// </summary>
    public static class LootRuntimeBootstrap
    {
        private const string PlayerObjectName = "Player_HeroKnight";
        private const string BossObjectName = "Boss_Flower";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.name != "Prototype")
            {
                return;
            }

            if (!LootDropSprites.AreReady)
            {
                Debug.LogWarning("LootRuntimeBootstrap: drop textures not found in Resources/Items.");
                return;
            }

            EnsurePlayerComponents();
            WireSceneEnemies();
        }

        /// <summary>
        /// Ensures a mob (including portal summons) has loot configured for Health.Died.
        /// </summary>
        public static void EnsureLootDrop(GameObject owner)
        {
            if (!ShouldReceiveLoot(owner))
            {
                return;
            }

            if (!LootDropSprites.AreReady)
            {
                return;
            }

            bool isEnt = owner.name.IndexOf("Ent", System.StringComparison.OrdinalIgnoreCase) >= 0;
            LootDropOnDeath drop = owner.GetComponent<LootDropOnDeath>();
            if (drop == null)
            {
                drop = owner.AddComponent<LootDropOnDeath>();
            }

            drop.Configure(
                LootDropSprites.Common,
                LootDropSprites.Potion,
                isEnt ? LootDropSprites.Ent : null,
                isEnt);
        }

        private static void EnsurePlayerComponents()
        {
            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                return;
            }

            if (player.GetComponent<PlayerLootInventory>() == null)
            {
                player.AddComponent<PlayerLootInventory>();
            }

            if (player.GetComponent<PlayerQuickAccessInventory>() == null)
            {
                player.AddComponent<PlayerQuickAccessInventory>();
            }
        }

        private static void WireSceneEnemies()
        {
            EnemyHealth[] allHealth = Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            for (int i = 0; i < allHealth.Length; i++)
            {
                EnemyHealth health = allHealth[i];
                if (health == null)
                {
                    continue;
                }

                EnsureLootDrop(health.gameObject);
            }
        }

        private static bool ShouldReceiveLoot(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (gameObject.name == PlayerObjectName || gameObject.name == BossObjectName)
            {
                return false;
            }

            // Portal mage (Enemy_Wizard*) — not EvilWizard.
            if (gameObject.name.StartsWith("Enemy_Wizard", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (gameObject.GetComponent<LootPickup2D>() != null)
            {
                return false;
            }

            string name = gameObject.name;
            return name.StartsWith("Enemy_", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
