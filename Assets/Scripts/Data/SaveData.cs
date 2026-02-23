using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int dewdrops;
        public int sunShards;
        public int auraDust;

        public ActivePlantSave activePlant;
        public List<GreenhousePlantSave> greenhousePlants = new();
        public List<string> discoveredVariants = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public int greenhouseSlots = 6;
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
    public class GreenhousePlantSave
    {
        public string seedName;
        public string variantName;
        public string harvestTimeUtc;
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }
}