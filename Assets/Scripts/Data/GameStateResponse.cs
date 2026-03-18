using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class GameStateResponse
    {
        public EconomyState economy;
        public List<ServerPlot> plots = new();
        public List<ServerVase> vases = new();
        public List<ServerGarden> gardens = new();
        public List<ServerMallum> mallums = new();
        public List<ServerMallumHouse> mallumHouses = new();
        public List<ServerBird> birds = new();
        public ServerApotheke apotheke;
        public ServerWeather weather;
        public int tutorialStep;
    }

    [Serializable]
    public class ServerPlot
    {
        public int id;
        public string seedItemKey;
        public string state;
        public string plantTimeUtc;
        public int waterCount;
        public string lastWateredUtc;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins = new();
        public bool fertilized;
        public List<string> potionItemKeys = new();
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
        public List<string> unlockedSkins = new();
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
        public bool fertilized;
    }

    [Serializable]
    public class ServerMallum
    {
        public int id;
        public string state;
        public string assignedQuestName;
        public string startTimeUtc;
        public int assignedVaseId;
        public List<ServerReward> pendingRewards = new();
    }

    [Serializable]
    public class ServerReward
    {
        public string item_key;
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
        public float sunrise_hour;
        public float sunset_hour;
    }

    [Serializable]
    public class ServerWeatherResponse
    {
        public ServerWeather weather;
    }

    [Serializable]
    public class ServerForecastDay
    {
        public string dayLabel;
        public float tempHigh;
        public float tempLow;
        public string condition;
        public int moonPhase;
        public float humidity;
        public float windSpeed;
        public float cloudCover;
    }

    [Serializable]
    public class ServerForecastResponse
    {
        public List<ServerForecastDay> forecast = new();
    }

    // Request DTOs
    [Serializable] public class CraftRequest { public int gridX; public int gridY; public bool freeMode; }
    [Serializable] public class PlantRequest { public int plotId; public string seedItemKey; public bool freeMode; }
    [Serializable] public class WaterRequest { public int plotId; public int vaseId; }
    [Serializable] public class HarvestRequest { public int plotId; }
    [Serializable] public class FillVaseRequest { public int vaseId; }
    [Serializable] public class CheckVaseRequest { public int vaseId; }
    [Serializable] public class InstantFinishPlotRequest { public int plotId; }
    [Serializable] public class InstantFinishVaseRequest { public int vaseId; }
    [Serializable] public class MoveBuildingRequest { public string type; public int id; public int gridX; public int gridY; }
    [Serializable] public class PlantGardenRequest { public string plantName; public int gridX; public int gridY; public bool freeMode; }
    [Serializable] public class CollectGardenRequest { public int gardenId; }
    [Serializable] public class QuestRequest { public string questName; public bool freeMode; }
    [Serializable] public class SetSkinRequest { public string skinName; }
    [Serializable] public class PlotSkinRequest { public int plotId; public string skinName; }
    [Serializable] public class VaseSkinRequest { public int vaseId; public string skinName; }
    [Serializable]
    public class ServerMallumHouse
    {
        public int id;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins = new();
    }

    [Serializable]
    public class ServerBird
    {
        public int id;
        public int gridX;
        public int gridY;
        public string itemKey;
        public int itemCount;
    }

    [Serializable]
    public class ServerApotheke
    {
        public int id;
        public int gridX;
        public int gridY;
    }

    [Serializable] public class BirdCollectRequest { public int birdId; }
    [Serializable] public class ApothekeCraftRequest { public string recipeName; public bool freeMode; }
    [Serializable] public class ApothekeCraftResponse { public string resultItem; public int resultQuantity; }
    [Serializable] public class BirdCheckResponse { public List<ServerBird> newBirds = new(); }
    [Serializable] public class BirdCollectResponse { public string itemKey; public int itemCount; }
    [Serializable] public class HouseSkinRequest { public int houseId; public string skinName; }
    [Serializable] public class LocationRequest { public float lat; public float lon; }
    [Serializable] public class FertilizePlotRequest { public int plotId; }
    [Serializable] public class ApplyPotionRequest { public int plotId; public string potionItemKey; }
    [Serializable] public class FertilizeGardenRequest { public int gardenId; }
    [Serializable] public class HarvestResponse { public float score; public int drops; public int bonusDrops; public string itemKey; }
    [Serializable] public class UpgradeFlameResponse { public int flameLevel; }
    [Serializable] public class CollectGardenResponse { public ServerGarden garden; public string yieldItem; public int yieldAmount; }
    [Serializable] public class CollectQuestResponse { public ServerMallum mallum; public List<ServerReward> rewards = new(); }
}
