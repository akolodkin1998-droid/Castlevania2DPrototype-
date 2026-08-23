using System;
using System.Collections.Generic;
using Castlevania2D.Enemies;
using Castlevania2D.Environment;
using Castlevania2D.Level;
using Castlevania2D.Loot;
using UnityEngine;
using HealthComponent = Castlevania2D.Health.Health;

namespace Castlevania2D.Save
{
    public static class PrototypeSaveStateService
    {
        private const string PrototypeSceneName = "Prototype";
        private const string PlayerName = "Player_HeroKnight";

        public static bool TryCapture(out GameSaveData data)
        {
            data = null;
            GameObject player = FindSceneObject(PlayerName);
            if (player == null)
            {
                Debug.LogError("[PrototypeSaveStateService] Player was not found.");
                return false;
            }

            data = new GameSaveData();
            CapturePlayer(data.player, player);
            CaptureBoss(data.bossFlower);
            CaptureBasket(data.basket01, FindSceneComponent<StoneBasketHitStages2D>("StoneBasket_01"));
            CaptureBasket(data.basket02, FindSceneComponent<StoneBasketHitStages2D>("StoneBasket_02"));
            CaptureLever(data.lever);
            CaptureElevator(data.elevator);
            CaptureWizard(data.wizard);
            data.pickups = CapturePickups();
            return true;
        }

        public static bool Apply(GameSaveData data)
        {
            if (data == null || data.player == null)
            {
                return false;
            }

            GameObject player = FindSceneObject(PlayerName);
            if (player == null)
            {
                Debug.LogError("[PrototypeSaveStateService] Cannot apply save: player was not found.");
                return false;
            }

            ApplyPlayer(data.player, player);
            ApplyBoss(data.bossFlower);
            ApplyBasket(data.basket01, FindSceneComponent<StoneBasketHitStages2D>("StoneBasket_01"));
            ApplyBasket(data.basket02, FindSceneComponent<StoneBasketHitStages2D>("StoneBasket_02"));
            ApplyLever(data.lever);
            ApplyElevator(data.elevator);
            ApplyWizard(data.wizard);
            ApplyPickups(data.pickups);
            return true;
        }

        private static void CapturePlayer(PlayerSaveData target, GameObject player)
        {
            Vector3 position = player.transform.position;
            target.positionX = position.x;
            target.positionY = position.y;
            target.positionZ = position.z;

            HealthComponent health = player.GetComponent<HealthComponent>();
            target.currentHealth = health != null ? health.CurrentHealth : 1;

            PlayerQuickAccessInventory quickAccess = player.GetComponent<PlayerQuickAccessInventory>();
            target.healingPotionCount = quickAccess != null ? quickAccess.HealingPotionCount : 0;

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                target.inventory = Array.Empty<InventoryStackSaveData>();
                return;
            }

            target.inventory = new InventoryStackSaveData[inventory.StackCount];
            for (int i = 0; i < inventory.StackCount; i++)
            {
                target.inventory[i] = new InventoryStackSaveData
                {
                    itemId = (int)inventory.GetItemIdAtSlot(i),
                    count = inventory.GetCountAtSlot(i),
                };
            }
        }

        private static void ApplyPlayer(PlayerSaveData source, GameObject player)
        {
            player.transform.position =
                new Vector3(source.positionX, source.positionY, source.positionZ);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            HealthComponent health = player.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.SetCurrentHealthForLoad(source.currentHealth);
            }

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<PlayerLootInventory>();
            }

            inventory.Clear();
            if (source.inventory != null)
            {
                for (int i = 0; i < source.inventory.Length; i++)
                {
                    InventoryStackSaveData stack = source.inventory[i];
                    if (stack != null
                        && Enum.IsDefined(typeof(LootItemId), stack.itemId)
                        && stack.count > 0)
                    {
                        inventory.Add((LootItemId)stack.itemId, stack.count);
                    }
                }
            }

            PlayerQuickAccessInventory quickAccess = player.GetComponent<PlayerQuickAccessInventory>();
            if (quickAccess == null)
            {
                quickAccess = player.AddComponent<PlayerQuickAccessInventory>();
            }

            quickAccess.SetHealingPotionCount(source.healingPotionCount);
        }

        private static void CaptureBoss(BossSaveData target)
        {
            GameObject boss = FindSceneObject("Boss_Flower");
            if (boss == null)
            {
                target.defeated = true;
                return;
            }

            HealthComponent health = boss.GetComponent<HealthComponent>();
            target.currentHealth = health != null ? health.CurrentHealth : 0;
            target.defeated = !boss.activeSelf || target.currentHealth <= 0;

            IBossSaveState saveState = FindBossSaveState(boss);
            if (saveState != null)
            {
                target.attackStarted = saveState.HasStartedAttackSequence;
                target.defeated |= saveState.HasDisappeared;
            }
        }

        private static void ApplyBoss(BossSaveData source)
        {
            if (source == null)
            {
                return;
            }

            GameObject boss = FindSceneObject("Boss_Flower");
            if (boss == null)
            {
                return;
            }

            if (!boss.activeSelf)
            {
                boss.SetActive(true);
            }

            HealthComponent health = boss.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.SetCurrentHealthForLoad(source.defeated ? 0 : source.currentHealth);
            }

            FindBossSaveState(boss)?.ApplySavedState(source.attackStarted, source.defeated);
            if (source.defeated && boss.activeSelf)
            {
                boss.SetActive(false);
            }
        }

        private static IBossSaveState FindBossSaveState(GameObject boss)
        {
            MonoBehaviour[] behaviours = boss.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IBossSaveState saveState)
                {
                    return saveState;
                }
            }

            return null;
        }

        private static void CaptureBasket(BasketSaveData target, StoneBasketHitStages2D basket)
        {
            if (basket == null)
            {
                return;
            }

            target.hitCount = basket.HitCount;
            target.hasFallen = basket.HasFallen;
            target.positionX = basket.transform.position.x;
            target.positionY = basket.transform.position.y;
        }

        private static void ApplyBasket(BasketSaveData source, StoneBasketHitStages2D basket)
        {
            if (source != null && basket != null)
            {
                basket.ApplySavedState(
                    source.hitCount,
                    source.hasFallen,
                    new Vector2(source.positionX, source.positionY));
            }
        }

        private static void CaptureLever(LeverSaveData target)
        {
            GameObject leverObject = FindSceneObject("Lever");
            if (leverObject == null)
            {
                target.active = false;
                return;
            }

            target.active = leverObject.activeSelf;
            LeverWhenBasketsFull2D lever = leverObject.GetComponent<LeverWhenBasketsFull2D>();
            target.phase = lever != null ? lever.SavePhase : 0;
        }

        private static void ApplyLever(LeverSaveData source)
        {
            if (source == null)
            {
                return;
            }

            GameObject leverObject = FindSceneObject("Lever");
            if (leverObject == null)
            {
                return;
            }

            if (!leverObject.activeSelf)
            {
                leverObject.SetActive(true);
            }

            leverObject.GetComponent<LeverWhenBasketsFull2D>()?.ApplySavedPhase(source.phase);
            leverObject.SetActive(source.active);
        }

        private static void CaptureElevator(ElevatorSaveData target)
        {
            ElevatorMechanism2D elevator = FindSceneComponent<ElevatorMechanism2D>("MechanismFrame");
            if (elevator == null)
            {
                return;
            }

            target.landingCount = elevator.LandingCount;
            target.assemblyWorldY = elevator.AssemblyWorldY;
            target.ropeLocalScaleY = elevator.RopeLocalScaleY;
            target.ropeLocalPositionY = elevator.RopeLocalPositionY;
        }

        private static void ApplyElevator(ElevatorSaveData source)
        {
            ElevatorMechanism2D elevator = FindSceneComponent<ElevatorMechanism2D>("MechanismFrame");
            if (source != null && elevator != null)
            {
                elevator.ApplySavedState(
                    source.landingCount,
                    source.assemblyWorldY,
                    source.ropeLocalScaleY,
                    source.ropeLocalPositionY);
            }
        }

        private static void CaptureWizard(WizardSaveData target)
        {
            GameObject wizard = FindSceneObject("Enemy_Wizard");
            if (wizard != null)
            {
                HealthComponent health = wizard.GetComponent<HealthComponent>();
                WizardPlayerSight2D sight = wizard.GetComponent<WizardPlayerSight2D>();
                WizardAttackAnimator2D attack = wizard.GetComponent<WizardAttackAnimator2D>();
                WizardPortalSpawner2D portals = wizard.GetComponent<WizardPortalSpawner2D>();
                WizardRestCollapse2D collapse = wizard.GetComponent<WizardRestCollapse2D>();

                target.currentHealth = health != null ? health.CurrentHealth : 0;
                target.defeated = !wizard.activeSelf || target.currentHealth <= 0;
                target.hasSighted = sight != null && sight.HasSighted;
                target.attackSequenceStarted = attack != null && attack.IsSequenceStarted;
                target.restForced = attack != null && attack.IsRestForced;
                target.portalsPlaced = portals != null && portals.PortalsPlaced;
                target.portalsSealed = portals != null && portals.IsPermanentlySealed;
                target.collapseArmed = collapse != null && collapse.IsArmed;
                target.basketsFallen = collapse != null && collapse.AreBasketsFallen;
                target.firstRestCycleCompleted =
                    collapse != null && collapse.IsFirstRestIdleCycleCompleted;
                target.collapsed = collapse != null && collapse.IsCollapsed;
            }
            else
            {
                target.defeated = true;
            }

            GameObject ground3 = FindSceneObject("Ground 3");
            target.ground3Active = ground3 != null && ground3.activeSelf;
        }

        private static void ApplyWizard(WizardSaveData source)
        {
            if (source == null)
            {
                return;
            }

            GameObject wizard = FindSceneObject("Enemy_Wizard");
            if (wizard != null)
            {
                if (!wizard.activeSelf)
                {
                    wizard.SetActive(true);
                }

                HealthComponent health = wizard.GetComponent<HealthComponent>();
                if (health != null)
                {
                    health.SetCurrentHealthForLoad(
                        source.defeated ? 0 : source.currentHealth);
                }

                wizard.GetComponent<WizardPlayerSight2D>()?.ApplySavedSight(source.hasSighted);
                wizard.GetComponent<WizardAttackAnimator2D>()?.ApplySavedState(
                    source.attackSequenceStarted,
                    source.restForced);
                wizard.GetComponent<WizardPortalSpawner2D>()?.ApplySavedState(
                    source.portalsPlaced,
                    source.portalsSealed);
                wizard.GetComponent<WizardRestCollapse2D>()?.ApplySavedState(
                    source.collapseArmed,
                    source.basketsFallen,
                    source.portalsSealed,
                    source.firstRestCycleCompleted,
                    source.collapsed);

                if (source.defeated)
                {
                    wizard.SetActive(false);
                }
            }

            GameObject ground3 = FindSceneObject("Ground 3");
            if (ground3 != null)
            {
                ground3.SetActive(source.ground3Active);
            }
        }

        private static WorldPickupSaveData[] CapturePickups()
        {
            LootPickup2D[] pickups = UnityEngine.Object.FindObjectsByType<LootPickup2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var result = new List<WorldPickupSaveData>(pickups.Length);

            for (int i = 0; i < pickups.Length; i++)
            {
                LootPickup2D pickup = pickups[i];
                if (pickup == null || pickup.gameObject.scene.name != PrototypeSceneName)
                {
                    continue;
                }

                Vector3 position = pickup.transform.position;
                result.Add(new WorldPickupSaveData
                {
                    itemId = (int)pickup.ItemId,
                    positionX = position.x,
                    positionY = position.y,
                    positionZ = position.z,
                });
            }

            return result.ToArray();
        }

        private static void ApplyPickups(WorldPickupSaveData[] savedPickups)
        {
            LootPickup2D[] existing = UnityEngine.Object.FindObjectsByType<LootPickup2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].gameObject.scene.name == PrototypeSceneName)
                {
                    UnityEngine.Object.Destroy(existing[i].gameObject);
                }
            }

            if (savedPickups == null)
            {
                return;
            }

            for (int i = 0; i < savedPickups.Length; i++)
            {
                WorldPickupSaveData pickup = savedPickups[i];
                if (pickup == null || !Enum.IsDefined(typeof(LootItemId), pickup.itemId))
                {
                    continue;
                }

                LootPickupSpawner.SpawnRestored(
                    (LootItemId)pickup.itemId,
                    new Vector3(pickup.positionX, pickup.positionY, pickup.positionZ));
            }
        }

        private static T FindSceneComponent<T>(string objectName) where T : Component
        {
            return FindSceneObject(objectName)?.GetComponent<T>();
        }

        private static GameObject FindSceneObject(string objectName)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null
                    && transform.name == objectName
                    && transform.gameObject.scene.IsValid()
                    && transform.gameObject.scene.name == PrototypeSceneName)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }
    }
}
