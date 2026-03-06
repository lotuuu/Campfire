using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    // ── Server response DTOs (JsonUtility-compatible) ──

    [Serializable]
    public class ServerSeedConfig
    {
        public string seedName;
        public float growthDurationHours;
        public int minDrops;
        public int maxDrops;
        public float manaCost;
        public int tier;
        // recipe is deserialized manually (nested map)
    }

    [Serializable]
    public class ServerQuestConfig
    {
        public string questName;
        public int durationMinutes;
        public int requiredFlameLevel;
        public int rewardRolls;
        // rewardPool deserialized manually
    }

    [Serializable]
    public class ServerGardenConfig
    {
        public string plantName;
        public float growthDurationHours;
        public string yieldItem;
        public int yieldAmount;
        public float yieldIntervalHours;
        public int waterRequired;
        public float manaCost;
    }

    [Serializable]
    public class ServerFlameConfig
    {
        public float base_mana_per_second;
        public float mana_per_level;
        public int max_flame_level;
        public List<int> entity_caps;
        public List<int> grid_sizes;
        // upgrade_recipes deserialized manually
    }

    [Serializable]
    public class ServerVaseConfig
    {
        public float craft_cost_mana;
        public int default_capacity;
        public float fill_duration_minutes;
        public List<int> capacity_tiers;
        public List<float> upgrade_costs;
    }

    [Serializable]
    public class ServerMallumHouseConfig
    {
        public int mallums_per_house;
        // house_costs deserialized manually
    }

    public class ConfigService : MonoBehaviour
    {
        public static ConfigService Instance { get; private set; }
        public bool IsLoaded { get; private set; }

        private Dictionary<string, ServerSeedConfig> _seedConfigs = new();
        private Dictionary<string, Dictionary<string, object>> _seedRecipes = new();
        private Dictionary<string, ServerQuestConfig> _questConfigs = new();
        private Dictionary<string, List<Dictionary<string, object>>> _questRewardPools = new();
        private Dictionary<string, ServerGardenConfig> _gardenConfigs = new();
        private ServerFlameConfig _flameConfig;
        private List<List<Dictionary<string, object>>> _flameUpgradeRecipes;
        private ServerVaseConfig _vaseConfig;
        private ServerMallumHouseConfig _mallumHouseConfig;
        private List<Dictionary<string, object>> _houseCosts;
        private Dictionary<string, object> _buildingCostConfig;

        private static string ServerBaseUrl =>
#if UNITY_EDITOR
            "http://localhost:4000";
#else
            DevServerConfig.BaseUrl;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public async Task<bool> FetchConfigs()
        {
            try
            {
                var url = ServerBaseUrl + "/game/configs";
                using var req = UnityWebRequest.Get(url);
                req.downloadHandler = new DownloadHandlerBuffer();
                var token = SocialSaveManager.Instance?.Data?.authToken;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var tcs = new TaskCompletionSource<bool>();
                var op = req.SendWebRequest();
                op.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                if (req.responseCode != 200)
                {
                    Debug.LogWarning($"ConfigService: fetch failed (HTTP {req.responseCode})");
                    return false;
                }

                ParseResponse(req.downloadHandler.text);
                IsLoaded = true;
                Debug.Log($"ConfigService: loaded {_seedConfigs.Count} seeds, {_questConfigs.Count} quests, {_gardenConfigs.Count} gardens");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ConfigService: fetch failed ({e.Message})");
                return false;
            }
        }

        // ── Typed Accessors ──

        public ServerSeedConfig GetSeed(string name) =>
            _seedConfigs.TryGetValue(name, out var s) ? s : null;

        public Dictionary<string, object> GetSeedRecipe(string name) =>
            _seedRecipes.TryGetValue(name, out var r) ? r : null;

        public ServerQuestConfig GetQuest(string name) =>
            _questConfigs.TryGetValue(name, out var q) ? q : null;

        public List<Dictionary<string, object>> GetQuestRewardPool(string name) =>
            _questRewardPools.TryGetValue(name, out var r) ? r : null;

        public ServerGardenConfig GetGarden(string name) =>
            _gardenConfigs.TryGetValue(name, out var g) ? g : null;

        public ServerFlameConfig FlameConfig => _flameConfig;
        public List<List<Dictionary<string, object>>> FlameUpgradeRecipes => _flameUpgradeRecipes;
        public ServerVaseConfig VaseConfig => _vaseConfig;
        public ServerMallumHouseConfig MallumHouseConfig => _mallumHouseConfig;
        public List<Dictionary<string, object>> HouseCosts => _houseCosts;
        public Dictionary<string, object> BuildingCostConfig => _buildingCostConfig;

        // ── Recipe Conversion ──

        public static GrowthRecipe ConvertRecipe(Dictionary<string, object> recipeMap)
        {
            var recipe = new GrowthRecipe();
            if (recipeMap == null) return recipe;

            if (recipeMap.TryGetValue("heat", out var heatObj) && heatObj is Dictionary<string, object> heat)
            {
                recipe.useHeat = true;
                recipe.idealTempMin = GetFloat(heat, "ideal_min");
                recipe.idealTempMax = GetFloat(heat, "ideal_max");
                recipe.heatTolerance = GetFloat(heat, "tolerance");
                recipe.heatWeight = GetFloat(heat, "weight", 1f);
            }
            if (recipeMap.TryGetValue("wind", out var windObj) && windObj is Dictionary<string, object> wind)
            {
                recipe.useWind = true;
                recipe.idealWindMin = GetFloat(wind, "ideal_min");
                recipe.idealWindMax = GetFloat(wind, "ideal_max");
                recipe.windTolerance = GetFloat(wind, "tolerance");
                recipe.windWeight = GetFloat(wind, "weight", 1f);
            }
            if (recipeMap.TryGetValue("humidity", out var humObj) && humObj is Dictionary<string, object> hum)
            {
                recipe.useHumidity = true;
                recipe.idealHumidityMin = GetFloat(hum, "ideal_min");
                recipe.idealHumidityMax = GetFloat(hum, "ideal_max");
                recipe.humidityTolerance = GetFloat(hum, "tolerance");
                recipe.humidityWeight = GetFloat(hum, "weight", 1f);
            }
            if (recipeMap.TryGetValue("sunlight", out var sunObj) && sunObj is Dictionary<string, object> sun)
            {
                recipe.useSunlight = true;
                recipe.idealSunlightMin = GetFloat(sun, "ideal_min");
                recipe.idealSunlightMax = GetFloat(sun, "ideal_max");
                recipe.sunlightTolerance = GetFloat(sun, "tolerance");
                recipe.sunlightWeight = GetFloat(sun, "weight", 1f);
            }
            if (recipeMap.TryGetValue("rain", out var rainObj) && rainObj is Dictionary<string, object> rain)
            {
                recipe.useRain = true;
                recipe.idealRainMin = GetFloat(rain, "ideal_min");
                recipe.idealRainMax = GetFloat(rain, "ideal_max");
                recipe.rainTolerance = GetFloat(rain, "tolerance");
                recipe.rainWeight = GetFloat(rain, "weight", 1f);
            }
            if (recipeMap.TryGetValue("moon", out var moonObj) && moonObj is Dictionary<string, object> moon)
            {
                recipe.useMoon = true;
                recipe.requiredMoonPhase = (MoonPhase)(int)GetFloat(moon, "ideal_min");
                recipe.moonWeight = GetFloat(moon, "weight", 1f);
            }
            if (recipeMap.TryGetValue("waterings", out var waterObj) && waterObj is Dictionary<string, object> water)
            {
                recipe.useWaterings = true;
                recipe.idealWateringsMin = (int)GetFloat(water, "ideal_min");
                recipe.idealWateringsMax = (int)GetFloat(water, "ideal_max");
                recipe.wateringsTolerance = GetFloat(water, "tolerance");
                recipe.wateringsWeight = GetFloat(water, "weight", 1f);
            }

            return recipe;
        }

        private static float GetFloat(Dictionary<string, object> dict, string key, float defaultValue = 0f)
        {
            if (!dict.TryGetValue(key, out var val)) return defaultValue;
            if (val is double d) return (float)d;
            if (val is long l) return l;
            if (val is float f) return f;
            if (val is int i) return i;
            return defaultValue;
        }

        // ── JSON Parsing (manual for nested maps since JsonUtility can't handle Dictionary) ──

        private void ParseResponse(string json)
        {
            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null) return;

            // Seeds
            if (root.TryGetValue("seeds", out var seedsObj) && seedsObj is Dictionary<string, object> seeds)
            {
                foreach (var kv in seeds)
                {
                    if (kv.Value is Dictionary<string, object> seedMap)
                    {
                        var config = new ServerSeedConfig
                        {
                            seedName = GetString(seedMap, "seedName"),
                            growthDurationHours = GetFloat(seedMap, "growthDurationHours"),
                            minDrops = (int)GetFloat(seedMap, "minDrops"),
                            maxDrops = (int)GetFloat(seedMap, "maxDrops"),
                            manaCost = GetFloat(seedMap, "manaCost"),
                            tier = (int)GetFloat(seedMap, "tier")
                        };
                        _seedConfigs[kv.Key] = config;

                        if (seedMap.TryGetValue("recipe", out var recipeObj) && recipeObj is Dictionary<string, object> recipeMap)
                            _seedRecipes[kv.Key] = recipeMap;
                    }
                }
            }

            // Quests
            if (root.TryGetValue("quests", out var questsObj) && questsObj is Dictionary<string, object> quests)
            {
                foreach (var kv in quests)
                {
                    if (kv.Value is Dictionary<string, object> questMap)
                    {
                        var config = new ServerQuestConfig
                        {
                            questName = GetString(questMap, "questName"),
                            durationMinutes = (int)GetFloat(questMap, "durationMinutes"),
                            requiredFlameLevel = (int)GetFloat(questMap, "requiredFlameLevel"),
                            rewardRolls = (int)GetFloat(questMap, "rewardRolls")
                        };
                        _questConfigs[kv.Key] = config;

                        if (questMap.TryGetValue("rewardPool", out var poolObj) && poolObj is List<object> pool)
                        {
                            var rewards = new List<Dictionary<string, object>>();
                            foreach (var item in pool)
                                if (item is Dictionary<string, object> r) rewards.Add(r);
                            _questRewardPools[kv.Key] = rewards;
                        }
                    }
                }
            }

            // Gardens
            if (root.TryGetValue("gardens", out var gardensObj) && gardensObj is Dictionary<string, object> gardens)
            {
                foreach (var kv in gardens)
                {
                    if (kv.Value is Dictionary<string, object> gardenMap)
                    {
                        _gardenConfigs[kv.Key] = new ServerGardenConfig
                        {
                            plantName = GetString(gardenMap, "plantName"),
                            growthDurationHours = GetFloat(gardenMap, "growthDurationHours"),
                            yieldItem = GetString(gardenMap, "yieldItem"),
                            yieldAmount = (int)GetFloat(gardenMap, "yieldAmount"),
                            yieldIntervalHours = GetFloat(gardenMap, "yieldIntervalHours"),
                            waterRequired = (int)GetFloat(gardenMap, "waterRequired"),
                            manaCost = GetFloat(gardenMap, "manaCost")
                        };
                    }
                }
            }

            // Flame config
            if (root.TryGetValue("flameConfig", out var flameObj) && flameObj is Dictionary<string, object> flame)
            {
                _flameConfig = new ServerFlameConfig
                {
                    base_mana_per_second = GetFloat(flame, "base_mana_per_second"),
                    mana_per_level = GetFloat(flame, "mana_per_level"),
                    max_flame_level = (int)GetFloat(flame, "max_flame_level"),
                    entity_caps = GetIntList(flame, "entity_caps"),
                    grid_sizes = GetIntList(flame, "grid_sizes")
                };

                if (flame.TryGetValue("upgrade_recipes", out var recipesObj) && recipesObj is List<object> recipes)
                {
                    _flameUpgradeRecipes = new List<List<Dictionary<string, object>>>();
                    foreach (var r in recipes)
                    {
                        if (r is Dictionary<string, object> recipeEntry &&
                            recipeEntry.TryGetValue("ingredients", out var ingredientsObj) &&
                            ingredientsObj is List<object> ingredients)
                        {
                            var list = new List<Dictionary<string, object>>();
                            foreach (var ing in ingredients)
                                if (ing is Dictionary<string, object> d) list.Add(d);
                            _flameUpgradeRecipes.Add(list);
                        }
                    }
                }
            }

            // Vase config
            if (root.TryGetValue("vaseConfig", out var vaseObj) && vaseObj is Dictionary<string, object> vase)
            {
                _vaseConfig = new ServerVaseConfig
                {
                    craft_cost_mana = GetFloat(vase, "craft_cost_mana"),
                    default_capacity = (int)GetFloat(vase, "default_capacity"),
                    fill_duration_minutes = GetFloat(vase, "fill_duration_minutes"),
                    capacity_tiers = GetIntList(vase, "capacity_tiers"),
                    upgrade_costs = GetFloatList(vase, "upgrade_costs")
                };
            }

            // Mallum house config
            if (root.TryGetValue("mallumHouseConfig", out var mallumObj) && mallumObj is Dictionary<string, object> mallum)
            {
                _mallumHouseConfig = new ServerMallumHouseConfig
                {
                    mallums_per_house = (int)GetFloat(mallum, "mallums_per_house")
                };

                if (mallum.TryGetValue("house_costs", out var costsObj) && costsObj is List<object> costs)
                {
                    _houseCosts = new List<Dictionary<string, object>>();
                    foreach (var c in costs)
                        if (c is Dictionary<string, object> d) _houseCosts.Add(d);
                }
            }

            // Building cost config
            if (root.TryGetValue("buildingCostConfig", out var buildObj) && buildObj is Dictionary<string, object> build)
            {
                _buildingCostConfig = build;
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var val) && val is string s) return s;
            return null;
        }

        private static List<int> GetIntList(Dictionary<string, object> dict, string key)
        {
            var result = new List<int>();
            if (dict.TryGetValue(key, out var val) && val is List<object> list)
                foreach (var item in list)
                {
                    if (item is double d) result.Add((int)d);
                    else if (item is long l) result.Add((int)l);
                }
            return result;
        }

        private static List<float> GetFloatList(Dictionary<string, object> dict, string key)
        {
            var result = new List<float>();
            if (dict.TryGetValue(key, out var val) && val is List<object> list)
                foreach (var item in list)
                {
                    if (item is double d) result.Add((float)d);
                    else if (item is long l) result.Add(l);
                }
            return result;
        }
    }
}
