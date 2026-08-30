using System;
using Castlevania2D.Save;
using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Carries loot and potions across gameplay scene loads (Prototype, Hub, Tafl).
    /// </summary>
    public static class PlayerInventorySession
    {
        public const string PlayerObjectName = "Player_HeroKnight";

        private static InventoryStackSaveData[] stacks = Array.Empty<InventoryStackSaveData>();
        private static int healingPotionCount;

        public static bool HasSnapshot { get; private set; }

        public static bool SuppressSceneBootstrap { get; set; }

        public static int CommonCount
        {
            get
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    InventoryStackSaveData stack = stacks[i];
                    if (stack != null && stack.itemId == (int)LootItemId.Common)
                    {
                        return stack.count;
                    }
                }

                return 0;
            }
        }

        public static void Clear()
        {
            stacks = Array.Empty<InventoryStackSaveData>();
            healingPotionCount = 0;
            HasSnapshot = false;
            SuppressSceneBootstrap = false;
        }

        public static void ReplaceFromPlayerSave(PlayerSaveData source)
        {
            if (source == null)
            {
                return;
            }

            healingPotionCount = source.healingPotionCount;
            if (source.inventory == null || source.inventory.Length == 0)
            {
                stacks = Array.Empty<InventoryStackSaveData>();
                HasSnapshot = true;
                return;
            }

            stacks = new InventoryStackSaveData[source.inventory.Length];
            for (int i = 0; i < source.inventory.Length; i++)
            {
                InventoryStackSaveData stack = source.inventory[i];
                stacks[i] = stack == null
                    ? null
                    : new InventoryStackSaveData
                    {
                        itemId = stack.itemId,
                        count = stack.count,
                    };
            }

            HasSnapshot = true;
        }

        public static void CaptureFromScene()
        {
            GameObject player = FindPlayer();
            if (player == null)
            {
                return;
            }

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                stacks = Array.Empty<InventoryStackSaveData>();
            }
            else
            {
                stacks = new InventoryStackSaveData[inventory.StackCount];
                for (int i = 0; i < inventory.StackCount; i++)
                {
                    stacks[i] = new InventoryStackSaveData
                    {
                        itemId = (int)inventory.GetItemIdAtSlot(i),
                        count = inventory.GetCountAtSlot(i),
                    };
                }
            }

            PlayerQuickAccessInventory quickAccess = player.GetComponent<PlayerQuickAccessInventory>();
            healingPotionCount = quickAccess != null ? quickAccess.HealingPotionCount : 0;
            HasSnapshot = true;
        }

        public static void ApplyToScene()
        {
            if (!HasSnapshot)
            {
                return;
            }

            GameObject player = FindPlayer();
            if (player == null)
            {
                return;
            }

            PlayerLootInventory inventory = player.GetComponent<PlayerLootInventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<PlayerLootInventory>();
            }

            inventory.Clear();
            for (int i = 0; i < stacks.Length; i++)
            {
                InventoryStackSaveData stack = stacks[i];
                if (stack == null
                    || !Enum.IsDefined(typeof(LootItemId), stack.itemId)
                    || stack.count <= 0)
                {
                    continue;
                }

                inventory.Add((LootItemId)stack.itemId, stack.count);
            }

            PlayerQuickAccessInventory quickAccess = player.GetComponent<PlayerQuickAccessInventory>();
            if (quickAccess == null)
            {
                quickAccess = player.AddComponent<PlayerQuickAccessInventory>();
            }

            quickAccess.SetHealingPotionCount(healingPotionCount);
        }

        public static bool TrySpendCommon(int amount)
        {
            if (amount < 0 || CommonCount < amount)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            SetCommonCount(CommonCount - amount);
            return true;
        }

        public static void AddCommon(int amount)
        {
            if (amount > 0)
            {
                SetCommonCount(CommonCount + amount);
            }
        }

        private static void SetCommonCount(int count)
        {
            count = Mathf.Max(0, count);
            for (int i = 0; i < stacks.Length; i++)
            {
                InventoryStackSaveData stack = stacks[i];
                if (stack == null || stack.itemId != (int)LootItemId.Common)
                {
                    continue;
                }

                if (count == 0)
                {
                    RemoveStackAt(i);
                }
                else
                {
                    stack.count = count;
                    stacks[i] = stack;
                }

                HasSnapshot = true;
                return;
            }

            if (count <= 0)
            {
                return;
            }

            var next = new InventoryStackSaveData[stacks.Length + 1];
            for (int i = 0; i < stacks.Length; i++)
            {
                next[i] = stacks[i];
            }

            next[stacks.Length] = new InventoryStackSaveData
            {
                itemId = (int)LootItemId.Common,
                count = count,
            };
            stacks = next;
            HasSnapshot = true;
        }

        private static GameObject FindPlayer()
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null
                    && transform.name == PlayerObjectName
                    && transform.gameObject.scene.IsValid())
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static void RemoveStackAt(int index)
        {
            if (stacks.Length <= 1)
            {
                stacks = Array.Empty<InventoryStackSaveData>();
                return;
            }

            var next = new InventoryStackSaveData[stacks.Length - 1];
            int write = 0;
            for (int i = 0; i < stacks.Length; i++)
            {
                if (i == index)
                {
                    continue;
                }

                next[write] = stacks[i];
                write++;
            }

            stacks = next;
        }
    }
}
