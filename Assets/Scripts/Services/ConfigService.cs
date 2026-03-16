using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Garden
{
    // ── Server response DTOs (JsonUtility-compatible) ──

    [Serializable]
    public class ItemConfig
    {
        public string displayName;
        public string category;
        public string spriteKey;
    }

    [Serializable]
    public class ServerSeedConfig
    {
        public string seedName;
        public string item_key;
        public string harvest_item_key;
        public float growthDurationHours;
        public int minDrops;
        public int maxDrops;
        public float manaCost;
        public int tier;
        public GrowthRecipe recipe;
    }

    [Serializable]
    public class ServerQuestReward
    {
        public string itemKey;
        public float weight = 1f;
        public int minCount = 1;
        public int maxCount = 1;
    }

    [Serializable]
    public class ServerQuestConfig
    {
        public string questName;
        public string description;
        public int durationMinutes;
        public int requiredFlameLevel;
        public int rewardRolls;
        public List<ServerQuestReward> rewardPool = new();
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
        public int max_flame_level;
        public List<float> mana_rates = new();
        public List<int> mana_caps = new();
        public List<int> entity_caps = new();
        public List<int> grid_sizes = new();
        public List<FlameUpgradeRecipe> upgradeRecipes = new();

        public float GetManaPerSecond(int flameLevel)
        {
            if (mana_rates.Count == 0) return 0f;
            int index = Mathf.Clamp(flameLevel - 1, 0, mana_rates.Count - 1);
            return mana_rates[index];
        }

        public float GetManaCap(int flameLevel)
        {
            if (mana_caps.Count == 0) return 0f;
            int index = Mathf.Clamp(flameLevel - 1, 0, mana_caps.Count - 1);
            return mana_caps[index];
        }

        public int GetMaxEntities(int flameLevel)
        {
            if (entity_caps.Count == 0) return 0;
            int index = Mathf.Clamp(flameLevel - 1, 0, entity_caps.Count - 1);
            return entity_caps[index];
        }

        public int GetGridSize(int flameLevel)
        {
            if (grid_sizes.Count == 0) return 2;
            int index = Mathf.Clamp(flameLevel - 1, 0, grid_sizes.Count - 1);
            return grid_sizes[index];
        }

        public int MaxLevel => upgradeRecipes.Count + 1;

        public FlameUpgradeRecipe GetUpgradeRecipe(int currentLevel)
        {
            int index = currentLevel - 1;
            if (index < 0 || index >= upgradeRecipes.Count) return null;
            return upgradeRecipes[index];
        }
    }

    [Serializable]
    public class ServerVaseConfig
    {
        public float craft_cost_mana;
        public int default_capacity;
        public float fill_duration_minutes;
        public string speed_item;
    }

    [Serializable]
    public class ServerMallumHouseConfig
    {
        public int mallums_per_house;
        public string quest_speed_item;
        public List<BuildingCost> houseCosts = new();

        public int MallumsPerHouse => mallums_per_house;
        public int GetMaxMallums(int houseCount) => houseCount * mallums_per_house;
    }

    [Serializable]
    public class ServerBirdConfig
    {
        public float spawn_base_chance;
        public float spawn_decay;
    }

    [Serializable]
    public class ServerPlotConfig
    {
        public int water_cooldown_seconds;
        public int rain_water_cooldown_seconds;
        public int rain_trigger_minutes;
        public float drop_spread_factor;
        public string speed_item;
    }

    [Serializable]
    public class NewPlayerItemGrant
    {
        public string itemKey;
        public int count;
    }

    [Serializable]
    public class ServerNewPlayerConfig
    {
        public float mana;
        public int gems;
        public int startingWater;
        public List<NewPlayerItemGrant> seeds = new();
        public List<NewPlayerItemGrant> items = new();
    }

    public class ConfigService : MonoBehaviour
    {
        public static ConfigService Instance { get; private set; }
        public bool IsLoaded { get; private set; }

        private Dictionary<string, ServerSeedConfig> _seedConfigs = new();
        private Dictionary<string, ServerQuestConfig> _questConfigs = new();
        private Dictionary<string, ServerGardenConfig> _gardenConfigs = new();
        private ServerFlameConfig _flameConfig;
        private ServerVaseConfig _vaseConfig;
        private ServerMallumHouseConfig _mallumHouseConfig;
        private ServerBirdConfig _birdConfig;
        private ServerPlotConfig _plotConfig;
        private ServerNewPlayerConfig _newPlayerConfig;
        private Dictionary<string, List<BuildingCost>> _buildingCosts = new();
        private Dictionary<string, string> _spriteManifest = new();

        public Dictionary<string, ItemConfig> Items { get; private set; } = new();

        public ItemConfig GetItem(string itemKey)
        {
            return Items.TryGetValue(itemKey, out var config) ? config : null;
        }

        public string GetItemDisplayName(string itemKey)
        {
            if (Items.TryGetValue(itemKey, out var config))
                return config.displayName;
            return itemKey; // fallback to raw key
        }

        public List<KeyValuePair<string, ItemConfig>> GetItemsByCategory(string category)
        {
            return Items.Where(kv => kv.Value.category == category).ToList();
        }

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private const long SlowStepMs = 500;

        public async Task<bool> FetchConfigs(string locale = "en")
        {
            var totalSw = Stopwatch.StartNew();

            try
            {
                var url = ServerBaseUrl + $"/game/configs?locale={locale}";
                using var req = UnityWebRequest.Get(url);
                req.downloadHandler = new DownloadHandlerBuffer();
                var token = SocialSaveManager.Instance?.Data?.authToken;
                if (!string.IsNullOrEmpty(token))
                    req.SetRequestHeader("Authorization", $"Bearer {token}");

                var tcs = new TaskCompletionSource<bool>();
                var op = req.SendWebRequest();
                op.completed += _ => tcs.SetResult(true);
                await tcs.Task;

                var networkMs = totalSw.ElapsedMilliseconds;
                if (networkMs > SlowStepMs)
                    Debug.LogWarning($"[INIT SLOW] GET /game/configs network took {networkMs}ms");

                if (req.responseCode != 200)
                {
                    Debug.LogWarning($"ConfigService: fetch failed (HTTP {req.responseCode})");
                    return false;
                }

                var parseSw = Stopwatch.StartNew();
                ParseResponse(req.downloadHandler.text, locale);
                if (parseSw.ElapsedMilliseconds > SlowStepMs)
                    Debug.LogWarning($"[INIT SLOW] ConfigService.ParseResponse took {parseSw.ElapsedMilliseconds}ms");

                if (_seedConfigs.Count == 0)
                {
                    Debug.LogWarning("ConfigService: response missing seeds section, treating as failed.");
                    return false;
                }
                IsLoaded = true;
                Debug.Log($"[INIT] ConfigService.FetchConfigs total: {totalSw.ElapsedMilliseconds}ms ({_seedConfigs.Count} seeds, {_questConfigs.Count} quests, {_gardenConfigs.Count} gardens, {_spriteManifest.Count} sprites in manifest)");
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

        public ServerQuestConfig GetQuest(string name) =>
            _questConfigs.TryGetValue(name, out var q) ? q : null;

        public ServerGardenConfig GetGarden(string name) =>
            _gardenConfigs.TryGetValue(name, out var g) ? g : null;

        public List<ServerSeedConfig> GetAllSeeds() => new(_seedConfigs.Values);
        public List<ServerQuestConfig> GetAllQuests() => new(_questConfigs.Values);
        public List<ServerGardenConfig> GetAllGardens() => new(_gardenConfigs.Values);

        public ServerFlameConfig FlameConfig => _flameConfig;
        public ServerVaseConfig VaseConfig => _vaseConfig;
        public ServerMallumHouseConfig MallumHouseConfig => _mallumHouseConfig;
        public ServerBirdConfig BirdConfig => _birdConfig;
        public ServerPlotConfig PlotConfig => _plotConfig;
        public ServerNewPlayerConfig NewPlayerConfig => _newPlayerConfig;
        public Dictionary<string, string> SpriteManifest => _spriteManifest;

        // ── Legacy accessors (kept until managers are migrated) ──

        public Dictionary<string, object> GetSeedRecipe(string name)
        {
            // Legacy: returns null; managers should use GetSeed(name).recipe instead
            return null;
        }

        public List<Dictionary<string, object>> GetQuestRewardPool(string name)
        {
            // Legacy: returns null; managers should use GetQuest(name).rewardPool instead
            return null;
        }

        public List<List<Dictionary<string, object>>> FlameUpgradeRecipes
        {
            get
            {
                // Legacy: returns null; managers should use FlameConfig.upgradeRecipes instead
                return null;
            }
        }

        public List<Dictionary<string, object>> HouseCosts
        {
            get
            {
                // Legacy: returns null; managers should use MallumHouseConfig.houseCosts instead
                return null;
            }
        }


        // ── Building Cost Accessors ──

        private List<BuildingCost> GetBuildingCostList(string key) =>
            _buildingCosts.TryGetValue(key, out var list) ? list : null;

        public BuildingCost GetPlotCost(int currentCount)
        {
            var list = GetBuildingCostList("plot_costs");
            if (list == null || list.Count == 0) return null;
            int index = Mathf.Clamp(currentCount, 0, list.Count - 1);
            return list[index];
        }

        public BuildingCost GetVaseCost(int currentCount)
        {
            var list = GetBuildingCostList("vase_costs");
            if (list == null || list.Count == 0) return null;
            int index = Mathf.Clamp(currentCount, 0, list.Count - 1);
            return list[index];
        }

        public BuildingCost GetGardenCost(int currentCount)
        {
            var list = GetBuildingCostList("garden_costs");
            if (list == null || list.Count == 0) return null;
            int index = Mathf.Clamp(currentCount, 0, list.Count - 1);
            return list[index];
        }

        public BuildingCost GetHouseCost(int currentCount)
        {
            var list = _mallumHouseConfig?.houseCosts;
            if (list == null || currentCount < 0 || currentCount >= list.Count) return null;
            return list[currentCount];
        }

        public bool CanBuildNextHouse(int currentCount) => GetHouseCost(currentCount) != null;

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

        private void ParseResponse(string json, string locale = "en")
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
                            item_key = GetString(seedMap, "itemKey"),
                            harvest_item_key = GetString(seedMap, "harvestItemKey"),
                            growthDurationHours = GetFloat(seedMap, "growthDurationHours"),
                            minDrops = (int)GetFloat(seedMap, "minDrops"),
                            maxDrops = (int)GetFloat(seedMap, "maxDrops"),
                            manaCost = GetFloat(seedMap, "manaCost"),
                            tier = (int)GetFloat(seedMap, "tier")
                        };

                        if (seedMap.TryGetValue("recipe", out var recipeObj) && recipeObj is Dictionary<string, object> recipeMap)
                            config.recipe = ConvertRecipe(recipeMap);

                        _seedConfigs[kv.Key] = config;
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
                            description = GetString(questMap, "description"),
                            durationMinutes = (int)GetFloat(questMap, "durationMinutes"),
                            requiredFlameLevel = (int)GetFloat(questMap, "requiredFlameLevel"),
                            rewardRolls = (int)GetFloat(questMap, "rewardRolls")
                        };

                        if (questMap.TryGetValue("rewardPool", out var poolObj) && poolObj is List<object> pool)
                        {
                            foreach (var item in pool)
                            {
                                if (item is Dictionary<string, object> r)
                                {
                                    config.rewardPool.Add(new ServerQuestReward
                                    {
                                        itemKey = GetString(r, "itemKey"),
                                        weight = GetFloat(r, "weight", 1f),
                                        minCount = (int)GetFloat(r, "minCount", 1f),
                                        maxCount = (int)GetFloat(r, "maxCount", 1f)
                                    });
                                }
                            }
                        }

                        _questConfigs[kv.Key] = config;
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
                    max_flame_level = (int)GetFloat(flame, "max_flame_level"),
                    mana_rates = GetFloatList(flame, "mana_rates"),
                    mana_caps = GetIntList(flame, "mana_caps"),
                    entity_caps = GetIntList(flame, "entity_caps"),
                    grid_sizes = GetIntList(flame, "grid_sizes")
                };

                if (flame.TryGetValue("upgrade_recipes", out var recipesObj) && recipesObj is List<object> recipes)
                {
                    foreach (var r in recipes)
                    {
                        if (r is Dictionary<string, object> recipeEntry &&
                            recipeEntry.TryGetValue("ingredients", out var ingredientsObj) &&
                            ingredientsObj is List<object> ingredients)
                        {
                            var recipe = new FlameUpgradeRecipe();
                            foreach (var ing in ingredients)
                            {
                                if (ing is Dictionary<string, object> d)
                                {
                                    recipe.ingredients.Add(new FlameIngredient
                                    {
                                        itemKey = GetString(d, "itemKey"),
                                        count = (int)GetFloat(d, "count")
                                    });
                                }
                            }
                            _flameConfig.upgradeRecipes.Add(recipe);
                        }
                    }
                }

                // Building costs (inside flame config)
                if (flame.TryGetValue("plot_costs", out var pcObj) && pcObj is List<object> plotCosts)
                    _buildingCosts["plot_costs"] = ParseBuildingCostList(plotCosts);

                if (flame.TryGetValue("vase_costs", out var vcObj) && vcObj is List<object> vaseCosts)
                    _buildingCosts["vase_costs"] = ParseBuildingCostList(vaseCosts);

                if (flame.TryGetValue("garden_costs", out var gcObj) && gcObj is List<object> gardenCosts)
                    _buildingCosts["garden_costs"] = ParseBuildingCostList(gardenCosts);
            }

            // Vase config
            if (root.TryGetValue("vaseConfig", out var vaseObj) && vaseObj is Dictionary<string, object> vase)
            {
                _vaseConfig = new ServerVaseConfig
                {
                    craft_cost_mana = GetFloat(vase, "craft_cost_mana"),
                    default_capacity = (int)GetFloat(vase, "default_capacity"),
                    fill_duration_minutes = GetFloat(vase, "fill_duration_minutes"),
                    speed_item = GetString(vase, "speed_item")
                };
            }

            // Mallum house config
            if (root.TryGetValue("mallumHouseConfig", out var mallumObj) && mallumObj is Dictionary<string, object> mallum)
            {
                _mallumHouseConfig = new ServerMallumHouseConfig
                {
                    mallums_per_house = (int)GetFloat(mallum, "mallums_per_house"),
                    quest_speed_item = GetString(mallum, "quest_speed_item")
                };

                if (mallum.TryGetValue("house_costs", out var costsObj) && costsObj is List<object> costs)
                {
                    _mallumHouseConfig.houseCosts = ParseBuildingCostList(costs);
                }
            }

            // Bird config
            if (root.TryGetValue("birdConfig", out var birdObj) && birdObj is Dictionary<string, object> bird)
            {
                _birdConfig = new ServerBirdConfig
                {
                    spawn_base_chance = GetFloat(bird, "spawn_base_chance"),
                    spawn_decay = GetFloat(bird, "spawn_decay")
                };
            }

            // Plot config
            if (root.TryGetValue("plotConfig", out var plotObj) && plotObj is Dictionary<string, object> plot)
            {
                _plotConfig = new ServerPlotConfig
                {
                    water_cooldown_seconds = (int)GetFloat(plot, "water_cooldown_seconds"),
                    rain_water_cooldown_seconds = (int)GetFloat(plot, "rain_water_cooldown_seconds"),
                    rain_trigger_minutes = (int)GetFloat(plot, "rain_trigger_minutes"),
                    drop_spread_factor = GetFloat(plot, "drop_spread_factor"),
                    speed_item = GetString(plot, "speed_item")
                };
            }

            // New player config
            if (root.TryGetValue("newPlayerConfig", out var npObj) && npObj is Dictionary<string, object> np)
            {
                _newPlayerConfig = new ServerNewPlayerConfig
                {
                    mana = GetFloat(np, "mana"),
                    gems = (int)GetFloat(np, "gems"),
                    startingWater = (int)GetFloat(np, "starting_water")
                };

                if (np.TryGetValue("seeds", out var seedsListObj) && seedsListObj is List<object> seedsList)
                {
                    foreach (var item in seedsList)
                    {
                        if (item is Dictionary<string, object> d)
                            _newPlayerConfig.seeds.Add(new NewPlayerItemGrant
                            {
                                itemKey = GetString(d, "itemKey"),
                                count = (int)GetFloat(d, "count")
                            });
                    }
                }

                if (np.TryGetValue("items", out var itemsListObj) && itemsListObj is List<object> itemsList)
                {
                    foreach (var item in itemsList)
                    {
                        if (item is Dictionary<string, object> d)
                            _newPlayerConfig.items.Add(new NewPlayerItemGrant
                            {
                                itemKey = GetString(d, "itemKey"),
                                count = (int)GetFloat(d, "count")
                            });
                    }
                }
            }

            // Sprite manifest
            if (root.TryGetValue("sprites", out var spritesObj) && spritesObj is Dictionary<string, object> sprites)
            {
                _spriteManifest.Clear();
                foreach (var kv in sprites)
                {
                    if (kv.Value is string hash)
                        _spriteManifest[kv.Key] = hash;
                }
            }

            // Items
            if (root.TryGetValue("items", out var itemsObj) && itemsObj is Dictionary<string, object> itemsDict)
            {
                Items = new Dictionary<string, ItemConfig>();
                foreach (var kv in itemsDict)
                {
                    if (kv.Value is Dictionary<string, object> itemData)
                    {
                        Items[kv.Key] = new ItemConfig
                        {
                            displayName = itemData.TryGetValue("displayName", out var dn) ? dn as string : kv.Key,
                            category = itemData.TryGetValue("category", out var cat) ? cat as string : "harvest",
                            spriteKey = itemData.TryGetValue("spriteKey", out var sk) ? sk as string : null
                        };
                    }
                }
            }

            // Translations
            if (root.TryGetValue("translations", out var transObj) && transObj is Dictionary<string, object> trans)
            {
                var dict = new Dictionary<string, string>();
                foreach (var kv in trans)
                    dict[kv.Key] = kv.Value as string ?? "";
                LocalizationService.Instance?.LoadTranslations(dict, locale);
            }

            if (root.TryGetValue("supportedLocales", out var localesObj) && localesObj is List<object> locales)
            {
                var list = locales.Select(l => l as string).Where(l => l != null).ToList();
                LocalizationService.Instance?.SetSupportedLocales(list);
            }
        }

        public void ApplyLocaleOverrides(Dictionary<string, object> overrides)
        {
            if (overrides == null) return;

            if (overrides.TryGetValue("items", out var itemsObj) && itemsObj is Dictionary<string, object> items)
            {
                foreach (var kv in items)
                {
                    if (Items.TryGetValue(kv.Key, out var item) && kv.Value is Dictionary<string, object> fields)
                    {
                        if (fields.TryGetValue("displayName", out var dn) && dn is string name)
                            item.displayName = name;
                    }
                }
            }

            if (overrides.TryGetValue("quests", out var questsObj) && questsObj is Dictionary<string, object> quests)
            {
                foreach (var kv in quests)
                {
                    if (_questConfigs.TryGetValue(kv.Key, out var quest) && kv.Value is Dictionary<string, object> fields)
                    {
                        if (fields.TryGetValue("questName", out var qn) && qn is string qname)
                            quest.questName = qname;
                        if (fields.TryGetValue("description", out var desc) && desc is string d)
                            quest.description = d;
                    }
                }
            }

            if (overrides.TryGetValue("gardens", out var gardensObj) && gardensObj is Dictionary<string, object> gardens)
            {
                foreach (var kv in gardens)
                {
                    if (_gardenConfigs.TryGetValue(kv.Key, out var garden) && kv.Value is Dictionary<string, object> fields)
                    {
                        if (fields.TryGetValue("plantName", out var pn) && pn is string pname)
                            garden.plantName = pname;
                    }
                }
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
                    else if (item is long l) result.Add((float)l);
                }
            return result;
        }

        private static List<BuildingCost> ParseBuildingCostList(List<object> list)
        {
            var result = new List<BuildingCost>();
            foreach (var item in list)
            {
                if (item is not Dictionary<string, object> d) continue;
                var cost = new BuildingCost();

                if (d.TryGetValue("manaCost", out var mc))
                    cost.manaCost = mc is double dd ? (float)dd : mc is long ll ? (float)ll : 0f;

                if (d.TryGetValue("harvestCosts", out var hcObj) && hcObj is List<object> hcList)
                {
                    foreach (var hc in hcList)
                    {
                        if (hc is not Dictionary<string, object> hd) continue;
                        string itemKey = hd.TryGetValue("itemKey", out var n) && n is string s ? s : null;
                        int count = hd.TryGetValue("count", out var c) ? (c is double cd ? (int)cd : c is long cl ? (int)cl : 0) : 0;
                        if (itemKey != null)
                            cost.harvestCosts.Add(new HarvestCost { itemKey = itemKey, count = count });
                    }
                }
                result.Add(cost);
            }
            return result;
        }
    }
}
