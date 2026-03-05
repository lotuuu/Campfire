using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class GameStateResponse
    {
        public EconomyState economy;
        public List<ServerPlot> plots;
        public List<ServerVase> vases;
        public List<ServerGarden> gardens;
        public List<ServerMallum> mallums;
        public ServerWeather weather;
    }

    [Serializable]
    public class ServerPlot
    {
        public int id;
        public string seedName;
        public string state;
        public string plantTimeUtc;
        public int waterCount;
        public string lastWateredUtc;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins;
    }

    [Serializable]
    public class ServerVase
    {
        public int id;
        public int capacity;
        public int currentWater;
        public string state;
        public string fillStartTimeUtc;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins;
    }

    [Serializable]
    public class ServerGarden
    {
        public int id;
        public string plantName;
        public string plantTimeUtc;
        public string lastYieldTimeUtc;
        public bool mature;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class ServerMallum
    {
        public int id;
        public string state;
        public string assignedQuestName;
        public string startTimeUtc;
        public int assignedVaseId;
        public List<ServerReward> pendingRewards;
    }

    [Serializable]
    public class ServerReward
    {
        public string seed_name;
        public int count;
    }

    [Serializable]
    public class ServerWeather
    {
        public float temperature;
        public float humidity;
        public float wind_speed;
        public float cloud_cover;
        public string condition;
        public bool is_raining;
        public int moon_phase;
    }

    // Request DTOs
    [Serializable] public class CraftRequest { public int gridX; public int gridY; }
    [Serializable] public class PlantRequest { public int plotId; public string seedName; }
    [Serializable] public class WaterRequest { public int plotId; public int vaseId; }
    [Serializable] public class HarvestRequest { public int plotId; }
    [Serializable] public class FillVaseRequest { public int vaseId; }
    [Serializable] public class CheckVaseRequest { public int vaseId; }
    [Serializable] public class PlantGardenRequest { public string plantName; public int gridX; public int gridY; }
    [Serializable] public class CollectGardenRequest { public int gardenId; }
    [Serializable] public class QuestRequest { public string questName; }
    [Serializable] public class MallumIdRequest { public int mallumId; }
    [Serializable] public class SetSkinRequest { public string skinName; }
    [Serializable] public class PlotSkinRequest { public int plotId; public string skinName; }
    [Serializable] public class VaseSkinRequest { public int vaseId; public string skinName; }
    [Serializable] public class LocationRequest { public float lat; public float lon; }
    [Serializable] public class HarvestResponse { public float score; public int drops; public string itemName; }
    [Serializable] public class CollectGardenResponse { public ServerGarden garden; public string yieldItem; public int yieldAmount; }
    [Serializable] public class CollectQuestResponse { public ServerMallum mallum; public List<ServerReward> rewards; }
}
