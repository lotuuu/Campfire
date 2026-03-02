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

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
        }

        private void Start()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
        }

        private void Update()
        {
            CheckGrowthCompletion();
        }

        public List<PlotSave> Plots => SaveManager.Instance.Data.plots;

        public bool CraftPlot(int gridX, int gridY)
        {
            if (Plots.Count >= FlameManager.Instance.MaxPlots) return false;
            SaveManager.Instance.Data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = gridX, gridY = gridY });
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
            plot.state = PlotState.Growing;
            plot.plantTimeUtc = GameTime.UtcNow.ToString("o");
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        public bool Water(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return false;

            if (!CurrencyManager.Instance.SpendWater(1)) return false;

            plot.waterCount++;

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
            return Mathf.Clamp01(elapsed / seed.growthDurationHours);
        }

        public float GetRemainingSeconds(int plotIndex)
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
            float elapsedSeconds = (float)(GameTime.UtcNow - plantTime).TotalSeconds;
            float totalSeconds = seed.growthDurationHours * 3600f;
            return Mathf.Max(0f, totalSeconds - elapsedSeconds);
        }

        public HarvestResult Harvest(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return null;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Mature) return null;

            var seed = LoadSeed(plot.seedName);
            if (seed == null) return null;

            float score = 1f;
            if (seed.recipe != null)
                score = seed.recipe.Evaluate(plot.snapshots ?? new GrowthSnapshots(), plot.waterCount);

            int drops = Mathf.Max(1, Mathf.RoundToInt(seed.baseDrops * score));

            AddItem(data, seed.seedName + "_harvest", drops);

            var result = new HarvestResult
            {
                seedName = seed.seedName,
                drops = drops,
                recipeScore = score
            };

            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();
            plot.state = PlotState.Empty;

            SaveManager.Instance.Save();

            OnPlotChanged?.Invoke(plotIndex);
            OnHarvested?.Invoke(plotIndex, result);
            return result;
        }

        public bool InstantFinish(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return false;
            plot.state = PlotState.Mature;
            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            return true;
        }

        private void OnWeatherUpdated(WeatherData weather)
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            for (int i = 0; i < data.plots.Count; i++)
            {
                var plot = data.plots[i];
                if (plot.state != PlotState.Growing) continue;
                if (plot.snapshots == null) plot.snapshots = new GrowthSnapshots();
                plot.snapshots.RecordSnapshot(weather);
                changed = true;
            }
            if (changed) SaveManager.Instance.Save();
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
        public int drops;
        public float recipeScore;
    }
}
