using System;

namespace Castlevania2D.Save
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 1;
        public string sceneName = "Prototype";
        public string timestamp = string.Empty;
        public PlayerSaveData player = new PlayerSaveData();
        public BossSaveData bossFlower = new BossSaveData();
        public BasketSaveData basket01 = new BasketSaveData();
        public BasketSaveData basket02 = new BasketSaveData();
        public LeverSaveData lever = new LeverSaveData();
        public ElevatorSaveData elevator = new ElevatorSaveData();
        public WizardSaveData wizard = new WizardSaveData();
        public WorldPickupSaveData[] pickups = Array.Empty<WorldPickupSaveData>();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public float positionX;
        public float positionY;
        public float positionZ;
        public int currentHealth;
        public int healingPotionCount;
        public InventoryStackSaveData[] inventory = Array.Empty<InventoryStackSaveData>();
    }

    [Serializable]
    public sealed class InventoryStackSaveData
    {
        public int itemId;
        public int count;
    }

    [Serializable]
    public sealed class BossSaveData
    {
        public int currentHealth;
        public bool defeated;
        public bool attackStarted;
    }

    [Serializable]
    public sealed class BasketSaveData
    {
        public int hitCount;
        public bool hasFallen;
        public float positionX;
        public float positionY;
    }

    [Serializable]
    public sealed class LeverSaveData
    {
        public int phase;
        public bool active = true;
    }

    [Serializable]
    public sealed class ElevatorSaveData
    {
        public int landingCount;
        public float assemblyWorldY;
        public float ropeLocalScaleY;
        public float ropeLocalPositionY;
    }

    [Serializable]
    public sealed class WizardSaveData
    {
        public int currentHealth;
        public bool defeated;
        public bool hasSighted;
        public bool attackSequenceStarted;
        public bool restForced;
        public bool portalsPlaced;
        public bool portalsSealed;
        public bool collapseArmed;
        public bool basketsFallen;
        public bool firstRestCycleCompleted;
        public bool collapsed;
        public bool ground3Active = true;
    }

    [Serializable]
    public sealed class WorldPickupSaveData
    {
        public int itemId;
        public float positionX;
        public float positionY;
        public float positionZ;
    }
}
