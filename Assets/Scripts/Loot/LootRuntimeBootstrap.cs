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
        private const int StartingMaraTearCount = 10;
        private const int BossMaraTearDropCount = 5;

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

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<PlayerLootInventory>();
            }

            inventory.EnsureMinimum(LootItemId.MaraTear, StartingMaraTearCount);

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

                if (health.gameObject.name == BossObjectName)
                {
                    EnsureBossMaraTearDrop(health.gameObject);
                    continue;
                }

                EnsureLootDrop(health.gameObject);
            }
        }

        private static void EnsureBossMaraTearDrop(GameObject boss)
        {
            LootDropOnDeath drop = boss.GetComponent<LootDropOnDeath>();
            if (drop == null)
            {
                drop = boss.AddComponent<LootDropOnDeath>();
            }

            drop.ConfigureBossMaraTearDrop(BossMaraTearDropCount);
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
