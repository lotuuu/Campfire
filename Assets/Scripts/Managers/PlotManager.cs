using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class PlotManager : MonoBehaviour
    {
        public static PlotManager Instance { get; private set; }

        public event Action<int> OnPlotChanged;
        public event Action<int, HarvestResult> OnHarvested;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckGrowthCompletion();
        }

        public List<PlotSave> Plots => SaveManager.Instance.Data.plots;

        public bool CraftPlot()
        {
            if (Plots.Count >= FlameManager.Instance.MaxPlots) return false;
            SaveManager.Instance.Data.plots.Add(new PlotSave { state = PlotState.Empty });
            SaveManager.Instance.Save();
            return true;
        }

        public bool Plant(int plotIndex, string seedName)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Empty) return false;

            var seedEntry = data.seedInventory.Find(s => s.seedName == seedName);
            if (seedEntry == null || seedEntry.count <= 0) return false;

            seedEntry.count--;
            if (seedEntry.count <= 0) data.seedInventory.Remove(seedEntry);

            plot.seedName = seedName;
            plot.state = PlotState.Planted;
            plot.watered = false;
            plot.plantTimeUtc = null;

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public bool Water(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Planted) return false;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return false;

            if (!CurrencyManager.Instance.SpendWater(seed.waterRequired)) return false;

            plot.watered = true;
            plot.state = PlotState.Growing;
            plot.plantTimeUtc = GameTime.UtcNow.ToString("o");

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public float GetGrowthProgress(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return 0f;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing || string.IsNullOrEmpty(plot.plantTimeUtc))
                return 0f;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return 0f;

            var plantTime = DateTime.Parse(plot.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - plantTime).TotalHours;
            float effectiveHours = GetEffectiveGrowthHours(seed);
            return Mathf.Clamp01(elapsed / effectiveHours);
        }

        public HarvestResult Harvest(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return null;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Mature) return null;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return null;

            bool weatherMatch = seed.preferredWeather != null &&
                                WeatherService.Instance != null &&
                                seed.preferredWeather.Evaluate(WeatherService.Instance.CurrentWeather);

            float quality = CalculateQuality(weatherMatch);
            int yield = Mathf.RoundToInt(seed.baseYield * quality);

            AddItem(data, seed.seedName + "_harvest", yield);

            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.watered = false;
            plot.state = PlotState.Empty;

            SaveManager.Instance.Save();

            var result = new HarvestResult
            {
                seedName = seed.seedName,
                yield = yield,
                qualityMultiplier = quality,
                weatherMatched = weatherMatch
            };

            OnPlotChanged?.Invoke(plotIndex);
            OnHarvested?.Invoke(plotIndex, result);
            return result;
        }

        private void CheckGrowthCompletion()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            for (int i = 0; i < data.plots.Count; i++)
            {
                var plot = data.plots[i];
                if (plot.state != PlotState.Growing) continue;
                if (GetGrowthProgress(i) >= 1f)
                {
                    plot.state = PlotState.Mature;
                    changed = true;
                    OnPlotChanged?.Invoke(i);
                }
            }
            if (changed) SaveManager.Instance.Save();
        }

        private float GetEffectiveGrowthHours(SeedData seed)
        {
            float hours = seed.growthDurationHours;
            if (WeatherService.Instance != null &&
                seed.preferredWeather != null &&
                seed.preferredWeather.Evaluate(WeatherService.Instance.CurrentWeather))
            {
                hours /= (1f + SeedData.WeatherMatchBonus);
            }
            return hours;
        }

        private static float CalculateQuality(bool weatherMatch)
        {
            float base_ = 1.0f;
            float roll = UnityEngine.Random.Range(-0.2f, 0.2f);
            if (weatherMatch) roll += 0.5f;
            return Mathf.Clamp(base_ + roll, SeedData.MinQualityMultiplier, SeedData.MaxQualityMultiplier);
        }

        private static void AddItem(SaveData data, string itemName, int count)
        {
            var existing = data.items.Find(i => i.itemName == itemName);
            if (existing != null) existing.count += count;
            else data.items.Add(new InventoryItem { itemName = itemName, count = count });
        }

        private static SeedData LoadSeed(string seedName)
        {
            if (string.IsNullOrEmpty(seedName)) return null;
            var all = Resources.LoadAll<SeedData>("Seeds");
            foreach (var s in all)
                if (s.seedName == seedName) return s;
            return null;
        }
    }

    [Serializable]
    public class HarvestResult
    {
        public string seedName;
        public int yield;
        public float qualityMultiplier;
        public bool weatherMatched;
    }
}
