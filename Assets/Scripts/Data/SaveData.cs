using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public float mana;
        public int gems;
        public int flameLevel = 1;
        public List<VaseSave> vases = new();
        public List<PlotSave> plots = new();
        public List<GardenSave> gardens = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public List<InventoryItem> items = new();
        public float lastManaCollectTime;
        public string lastVisitorDateUtc;
        public List<MallumSave> mallums = new();
        public List<MallumHouseSave> mallumHouses = new();
        public string rainStartTimeUtc;
        public string lastRainEffectTimeUtc;
        public int apothekeGridX = 1;
        public int apothekeGridY = 0;
    }

    [Serializable]
    public class VaseSave
    {
        public int capacity = 5;
        public int currentWater;
        public string fillStartTimeUtc;
        public VaseState state = VaseState.Empty;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class PlotSave
    {
        public string seedName;
        public string plantTimeUtc;
        public int waterCount;
        public PlotState state = PlotState.Empty;
        public int gridX;
        public int gridY;
        public GrowthSnapshots snapshots = new();
        public string lastWateredUtc;
    }

    [Serializable]
    public class GardenSave
    {
        public string plantName;
        public string plantTimeUtc;
        public string lastYieldTimeUtc;
        public bool mature;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }

    [Serializable]
    public class InventoryItem
    {
        public string itemName;
        public int count;
    }
}
