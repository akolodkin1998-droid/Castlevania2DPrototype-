using Castlevania2D.Input;
using Castlevania2D.Save;
using Castlevania2D.UI;
using UnityEngine;

namespace Castlevania2D.Loot
{
    /// <summary>
    /// Player can take grounded loot with F. No prompt is shown.
    /// </summary>
    public sealed class PlayerManualLootPickup2D : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float reach = 1.8f;

        private void Update()
        {
            if (GameplayInputLock.IsLocked || DialogueBoxUI.IsOpen)
            {
                return;
            }

            SaveLoadSessionController session = SaveLoadSessionController.Instance;
            if (session != null && !session.CanInteract)
            {
                return;
            }

            if (!UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return;
            }

            LootPickup2D loot = LootPickup2D.FindNearest(transform.position, reach, true);
            if (loot != null)
            {
                loot.TryCollectToPlayer();
            }
        }
    }
}
