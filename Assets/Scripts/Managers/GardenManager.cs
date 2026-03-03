using System;
using System.Collections.Generic;
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
            return true;
        }

        public float GetGrowthProgress(int gardenIndex)
        {
            var data = SaveManager.Instance.Data;
            if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return 0f;
            var garden = data.gardens[gardenIndex];
            if (garden.mature) return 1f;
            if (string.IsNullOrEmpty(garden.plantTimeUtc)) return 0f;

            var plantData = LoadPlantData(garden.plantName);
            if (plantData == null) return 0f;

            var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - plantTime).TotalHours;
            return Mathf.Clamp01(elapsed / plantData.growthDurationHours);
        }

        private void CheckGrowthAndYields()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;

            for (int i = 0; i < data.gardens.Count; i++)
            {
                var garden = data.gardens[i];
                if (string.IsNullOrEmpty(garden.plantName)) continue;

                if (!garden.mature && GetGrowthProgress(i) >= 1f)
                {
                    garden.mature = true;
                    garden.lastYieldTimeUtc = GameTime.UtcNow.ToString("o");
                    changed = true;
                    OnGardenChanged?.Invoke(i);
                }

                if (garden.mature && !string.IsNullOrEmpty(garden.lastYieldTimeUtc))
                {
                    var plantData = LoadPlantData(garden.plantName);
                    if (plantData == null) continue;

                    var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    float elapsed = (float)(GameTime.UtcNow - lastYield).TotalHours;

                    if (elapsed >= plantData.yieldIntervalHours)
                    {
                        AddItem(data, plantData.yieldItem, plantData.yieldAmount);
                        garden.lastYieldTimeUtc = GameTime.UtcNow.ToString("o");
                        changed = true;
                        OnYieldCollected?.Invoke(i, plantData.yieldItem, plantData.yieldAmount);
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

        private static GardenPlantData LoadPlantData(string plantName)
        {
            if (string.IsNullOrEmpty(plantName)) return null;
            if (_plantCache != null && _plantCache.TryGetValue(plantName, out var plant))
                return plant;
            return null;
        }
    }
}
