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
        public int greenhouseSlots = 6;

        // v2: terrarium environments
        public List<string> unlockedEnvironments = new();

        // v3: per-environment unlocked slot counts
        public List<EnvironmentSlotsSave> environmentSlots = new();
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
}
