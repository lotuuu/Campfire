using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int gold;
        public int sunShards;
        public int auraDust;

        // v2: multi-slot replaces single activePlant
        public ActivePlantSave activePlant;
        public List<PlantSlotSave> activeSlots = new();
        public List<GreenhousePlantSave> greenhousePlants = new();
        public List<string> discoveredVariants = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public List<ConsumableInventoryEntry> consumableInventory = new();
        public List<EnvironmentConsumableSave> environmentConsumables = new();
        public int greenhouseSlots = 3;

        // v2: terrarium environments
        public List<string> unlockedEnvironments = new();

        // v3: per-environment unlocked slot counts
        public List<EnvironmentSlotsSave> environmentSlots = new();

        // v4: active environment shown in terrarium
        public int activeEnvironmentIndex;
    }

    [Serializable]
    public class EnvironmentSlotsSave
    {
        public string environmentName;
        public int unlockedSlots;
    }

    [Serializable]
    public class ActivePlantSave
    {
        public string seedName;
        public string variantName;
        public string plantTimeUtc;
        public float growthSpeedMultiplier = 1f;
        public bool isActive;
    }

    [Serializable]
    public class PlantSlotSave
    {
        public int environmentIndex;
        public int slotIndex;
        public string seedName;
        public string variantName;
        public string plantTimeUtc;
        public float growthSpeedMultiplier = 1f;
        public List<string> appliedConsumables = new(); // ConsumableType.ToString() — slot-scoped only (Fertilizer, QualityDirt)
    }

    [Serializable]
    public class GreenhousePlantSave
    {
        public string seedName;
        public string variantName;
        public string harvestTimeUtc;
        public QualityTier qualityTier;
        public string tierStartTimeUtc;  // when current tier began decaying
        public bool isWithered;          // true once plant has passed below D
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }

    [Serializable]
    public class ConsumableInventoryEntry
    {
        public string consumableType; // ConsumableType.ToString()
        public int count;
    }

    [Serializable]
    public class EnvironmentConsumableSave
    {
        public int envIndex;
        public string consumableType; // ConsumableType.ToString()
    }
}
