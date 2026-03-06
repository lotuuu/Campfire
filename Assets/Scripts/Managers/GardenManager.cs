using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class GardenManager : MonoBehaviour
    {
        public static GardenManager Instance { get; private set; }

        public event Action<int> OnGardenChanged;
        public event Action<int, string, int> OnYieldCollected;

        private static Dictionary<string, GardenPlantData> _plantCache;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_plantCache == null)
            {
                _plantCache = new Dictionary<string, GardenPlantData>();
                foreach (var plant in Resources.LoadAll<GardenPlantData>("GardenPlants"))
                    _plantCache[plant.plantName] = plant;
            }

            ApplyServerGardenConfigs();
        }

        private static void ApplyServerGardenConfigs()
        {
            if (_plantCache == null) return;
            var cs = ConfigService.Instance;
            if (cs == null || !cs.IsLoaded) return;

            foreach (var kv in _plantCache)
            {
                var serverGarden = cs.GetGarden(kv.Key);
                if (serverGarden == null) continue;

                var plant = kv.Value;
                plant.growthDurationHours = serverGarden.growthDurationHours;
                plant.yieldItem = serverGarden.yieldItem;
                plant.yieldAmount = serverGarden.yieldAmount;
                plant.yieldIntervalHours = serverGarden.yieldIntervalHours;
                plant.waterRequired = serverGarden.waterRequired;
            }
        }

        private void Update()
        {
            CheckGrowthAndYields();
        }

        public List<GardenSave> Gardens => SaveManager.Instance.Data.gardens;

        public bool Plant(int gardenIndex, string plantName)
        {
            var data = SaveManager.Instance.Data;
            if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return false;
            var garden = data.gardens[gardenIndex];
            if (!string.IsNullOrEmpty(garden.plantName)) return false;

            var plantData = LoadPlantData(plantName);
            if (plantData == null) return false;

            if (!CurrencyManager.Instance.SpendWater(plantData.waterRequired)) return false;

            garden.plantName = plantName;
            garden.plantTimeUtc = GameTime.UtcNow.ToString("o");
            garden.mature = false;
            garden.lastYieldTimeUtc = null;

            SaveManager.Instance.Save();
            OnGardenChanged?.Invoke(gardenIndex);

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = GameService.Instance.PlantGarden(plantName, garden.gridX, garden.gridY);
            }

            return true;
        }

        public float GetGrowthProgress(int gardenIndex)
        {
            var data = SaveManager.Instance.Data;
            if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return 0f;
            var garden = data.gardens[gardenIndex];

            var plantData = LoadPlantData(garden.plantName);
            if (plantData == null && !garden.mature) return 0f;

            float durationHours = plantData != null ? plantData.growthDurationHours : 1f;
            return GetGrowthProgress(garden, durationHours, GameTime.UtcNow);
        }

        public static float GetGrowthProgress(GardenSave garden, float growthDurationHours, DateTime utcNow)
        {
            if (garden.mature) return 1f;
            if (string.IsNullOrEmpty(garden.plantTimeUtc)) return 0f;

            var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(utcNow - plantTime).TotalHours;
            return Mathf.Clamp01(elapsed / growthDurationHours);
        }

        public static bool CheckYieldReady(GardenSave garden, float yieldIntervalHours, DateTime utcNow)
        {
            if (!garden.mature) return false;
            if (string.IsNullOrEmpty(garden.lastYieldTimeUtc)) return false;

            var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(utcNow - lastYield).TotalHours;
            return elapsed >= yieldIntervalHours;
        }

        private void CheckGrowthAndYields()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            var now = GameTime.UtcNow;

            for (int i = 0; i < data.gardens.Count; i++)
            {
                var garden = data.gardens[i];
                if (string.IsNullOrEmpty(garden.plantName)) continue;

                var plantData = LoadPlantData(garden.plantName);
                if (plantData == null) continue;

                if (!garden.mature && GetGrowthProgress(garden, plantData.growthDurationHours, now) >= 1f)
                {
                    garden.mature = true;
                    garden.lastYieldTimeUtc = now.ToString("o");
                    changed = true;
                    OnGardenChanged?.Invoke(i);
                }

                if (CheckYieldReady(garden, plantData.yieldIntervalHours, now))
                {
                    AddItem(data, plantData.yieldItem, plantData.yieldAmount);
                    garden.lastYieldTimeUtc = now.ToString("o");
                    changed = true;
                    OnYieldCollected?.Invoke(i, plantData.yieldItem, plantData.yieldAmount);

                    // Notify server
                    if (GameService.Instance != null && GameService.Instance.IsOnline && garden.serverId > 0)
                    {
                        _ = GameService.Instance.CollectGarden(garden.serverId);
                    }
                }
            }

            if (changed) SaveManager.Instance.Save();
        }

        private static void AddItem(SaveData data, string itemName, int count)
        {
            var existing = data.items.Find(it => it.itemName == itemName);
            if (existing != null) existing.count += count;
            else data.items.Add(new InventoryItem { itemName = itemName, count = count });
        }

        // ── Garden Building ──────────────────────────────────────────

        private static BuildingCostConfig _buildingCostConfig;

        private static BuildingCostConfig LoadBuildingCostConfig()
        {
            if (_buildingCostConfig == null)
                _buildingCostConfig = Resources.Load<BuildingCostConfig>("Config/BuildingCostConfig");
            return _buildingCostConfig;
        }

        public BuildingCost GetNextGardenCost()
        {
            return LoadBuildingCostConfig()?.GetGardenCost(SaveManager.Instance.Data.gardens.Count);
        }

        public bool CraftEmptyGarden(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = GetNextGardenCost();
            if (cost == null) return false;

            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;
            if (!MallumManager.CanAffordHarvests(data.items, cost.harvestCosts)) return false;

            CurrencyManager.Instance.SpendMana(cost.manaCost);

            if (!CurrencyManager.FreeMode)
            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.items.Find(i => i.itemName == hc.itemName);
                entry.count -= hc.count;
                if (entry.count <= 0) data.items.Remove(entry);
            }

            data.gardens.Add(new GardenSave
            {
                gridX = gridX,
                gridY = gridY
            });

            SaveManager.Instance.Save();
            OnGardenChanged?.Invoke(data.gardens.Count - 1);
            return true;
        }

        // ── Helpers ─────────────────────────────────────────────────

        private static GardenPlantData LoadPlantData(string plantName)
        {
            if (string.IsNullOrEmpty(plantName)) return null;
            if (_plantCache != null && _plantCache.TryGetValue(plantName, out var plant))
                return plant;
            return null;
        }
    }
}
