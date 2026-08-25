using System;
using System.Collections.Generic;
using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Stores collected mob drops on the player. Slots fill in pickup order (left to right).
    /// Healing potions go to PlayerQuickAccessInventory, not here.
    /// </summary>
    public sealed class PlayerLootInventory : MonoBehaviour
    {
        [Serializable]
        private struct InventoryStack
        {
            public LootItemId itemId;
            public int count;
        }

        [SerializeField] private List<InventoryStack> stacks = new List<InventoryStack>();

        public event Action Changed;

        public IReadOnlyList<LootItemId> StackOrder
        {
            get
            {
                LootItemId[] order = new LootItemId[stacks.Count];
                for (int i = 0; i < stacks.Count; i++)
                {
                    order[i] = stacks[i].itemId;
                }

                return order;
            }
        }

        public int GetCount(LootItemId id)
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].itemId == id)
                {
                    return stacks[i].count;
                }
            }

            return 0;
        }

        public int GetCountAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= stacks.Count)
            {
                return 0;
            }

            return stacks[slotIndex].count;
        }

        public LootItemId GetItemIdAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= stacks.Count)
            {
                return LootItemId.Common;
            }

            return stacks[slotIndex].itemId;
        }

        public int StackCount => stacks.Count;

        public int CommonCount => GetCount(LootItemId.Common);
        public int EntCount => GetCount(LootItemId.Ent);
        public int MaraTearCount => GetCount(LootItemId.MaraTear);
        public int SporeBagCount => GetCount(LootItemId.SporeBag);

        public void Add(LootItemId id, int amount = 1)
        {
            if (amount <= 0 || id == LootItemId.Potion)
            {
                return;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].itemId != id)
                {
                    continue;
                }

                InventoryStack stack = stacks[i];
                stack.count += amount;
                stacks[i] = stack;
                Changed?.Invoke();
                return;
            }

            stacks.Add(new InventoryStack
            {
                itemId = id,
                count = amount,
            });
            Changed?.Invoke();
        }

        public void EnsureMinimum(LootItemId id, int minimumAmount)
        {
            int amountToAdd = minimumAmount - GetCount(id);
            if (amountToAdd > 0)
            {
                Add(id, amountToAdd);
            }
        }

        public bool TryRemove(LootItemId id, int amount = 1)
        {
            if (amount <= 0)
            {
                return false;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].itemId != id || stacks[i].count < amount)
                {
                    continue;
                }

                InventoryStack stack = stacks[i];
                stack.count -= amount;
                if (stack.count == 0)
                {
                    stacks.RemoveAt(i);
                }
                else
                {
                    stacks[i] = stack;
                }

                Changed?.Invoke();
                return true;
            }

            return false;
        }

        public void Clear()
        {
            if (stacks.Count == 0)
            {
                return;
            }

            stacks.Clear();
            Changed?.Invoke();
        }
    }
}
