using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int version = 2;
        public float mana;
        public int gems;
        public int flameLevel = 1;
        public List<VaseSave> vases = new();
        public List<PlotSave> plots = new();
        public List<GardenSave> gardens = new();
        public List<InventoryItem> inventory = new();
        public VisitorSave currentVisitor;
        public List<ActiveVisitorQuest> activeQuests = new();
        public string lastVisitorFetchDateUtc;
        public List<MallumSave> mallums = new();
        public List<MallumHouseSave> mallumHouses = new();
        public string rainStartTimeUtc;
        public string lastRainEffectTimeUtc;
        public int apothekeServerId;
        public int apothekeGridX = 1;
        public int apothekeGridY = 0;
        public List<BirdSave> birds = new();
        public string lastBirdCheckHourUtc;
        public List<string> discoveredSeeds = new();
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public int tutorialStep;
    }

    [Serializable]
    public class VaseSave
    {
        public int serverId;
        public int capacity = 5;
        public int currentWater;
        public string fillStartTimeUtc;
        public VaseState state = VaseState.Empty;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins = new();
    }

    [Serializable]
    public class PlotSave
    {
        public int serverId;
        public string seedItemKey;
        public string plantTimeUtc;
        public int waterCount;
        public PlotState state = PlotState.Empty;
        public int gridX;
        public int gridY;
        public GrowthSnapshots snapshots = new();
        public string lastWateredUtc;
        public bool subscribeWater;
        public string skinName;
        public List<string> unlockedSkins = new();

        // Transient — pre-fetched from server, not saved to disk
        [NonSerialized] public HarvestResponse cachedHarvestPreview;
    }

    [Serializable]
    public class GardenSave
    {
        public int serverId;
        public string plantName;
        public string plantTimeUtc;
        public string lastYieldTimeUtc;
        public bool mature;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class InventoryItem
    {
        public string itemKey;
        public int count;
    }
}
