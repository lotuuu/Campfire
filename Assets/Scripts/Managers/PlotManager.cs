using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class PlotManager : MonoBehaviour
    {
        public static PlotManager Instance { get; private set; }

        public event Action<int> OnPlotChanged;
        public event Action<int, HarvestResult> OnHarvested;

        public static readonly float ManualWaterCooldownHours = 2f;
        public static readonly float RainWaterCooldownHours = 6f;
        public static readonly float RainTriggerMinutes = 15f;

        public static bool CanWaterPlot(PlotSave plot, DateTime utcNow, float cooldownHours)
        {
            if (plot.state != PlotState.Growing) return false;
            if (string.IsNullOrEmpty(plot.lastWateredUtc)) return true;
            var lastWatered = DateTime.Parse(plot.lastWateredUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            return (utcNow - lastWatered).TotalHours >= cooldownHours;
        }

        public static void ApplyWatering(PlotSave plot, string utcNow)
        {
            plot.waterCount++;
            plot.lastWateredUtc = utcNow;
        }

        public static int RainWaterAllPlots(List<PlotSave> plots, DateTime utcNow)
        {
            int count = 0;
            string nowStr = utcNow.ToString("o");
            foreach (var plot in plots)
            {
                if (CanWaterPlot(plot, utcNow, RainWaterCooldownHours))
                {
                    ApplyWatering(plot, nowStr);
                    count++;
                }
            }
            return count;
        }

        public static bool CheckRainEvent(SaveData data, WeatherCondition condition, DateTime utcNow)
        {
            bool isRaining = condition == WeatherCondition.Rain || condition == WeatherCondition.Storm;

            if (!isRaining)
            {
                data.rainStartTimeUtc = null;
                return false;
            }

            if (string.IsNullOrEmpty(data.rainStartTimeUtc))
            {
                data.rainStartTimeUtc = utcNow.ToString("o");
                return false;
            }

            var rainStart = DateTime.Parse(data.rainStartTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            if ((utcNow - rainStart).TotalMinutes < RainTriggerMinutes)
                return false;

            if (!string.IsNullOrEmpty(data.lastRainEffectTimeUtc))
            {
                var lastEffect = DateTime.Parse(data.lastRainEffectTimeUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                if (lastEffect >= rainStart)
                    return false;
            }

            data.lastRainEffectTimeUtc = utcNow.ToString("o");
            return true;
        }

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
            ScheduleAllPlantNotifications();
        }

        /// When true, growth timers are frozen (tutorial uses this to let the player water in time).
        public bool GrowthPaused { get; set; }
        private DateTime? _lastPauseTime;

        private void Update()
        {
            if (SaveManager.Instance?.Data == null || ConfigService.Instance == null) return;
            if (GrowthPaused)
            {
                // Push all plantTimeUtc forward so elapsed time stays frozen
                var now = GameTime.UtcNow;
                if (_lastPauseTime.HasValue)
                {
                    var delta = now - _lastPauseTime.Value;
                    if (delta.TotalSeconds > 0)
                    {
                        var data = SaveManager.Instance.Data;
                        foreach (var plot in data.plots)
                        {
                            if (plot.state == PlotState.Growing && !string.IsNullOrEmpty(plot.plantTimeUtc))
                            {
                                var pt = DateTime.Parse(plot.plantTimeUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
                                plot.plantTimeUtc = (pt + delta).ToString("o");
                            }
                        }
                    }
                }
                _lastPauseTime = now;
                return;
            }

            if (_lastPauseTime.HasValue)
                _lastPauseTime = null;

            CheckGrowthCompletion();
        }

        public List<PlotSave> Plots => SaveManager.Instance.Data.plots;

        public BuildingCost GetNextPlotCost()
        {
            // Subtract 1 for the free starter plot so cost index is based on purchased plots
            return ConfigService.Instance?.GetPlotCost(SaveManager.Instance.Data.plots.Count - 1);
        }

        public bool CraftPlot(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = GetNextPlotCost();
            if (cost == null) return false;

            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;
            if (!MallumManager.CanAffordHarvests(data.inventory, cost.harvestCosts)) return false;

            CurrencyManager.Instance.SpendMana(cost.manaCost);

            if (!CurrencyManager.FreeMode)
            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.inventory.Find(i => i.itemName == hc.itemName);
                if (entry == null) continue;
                entry.count -= hc.count;
                if (entry.count <= 0) data.inventory.Remove(entry);
            }

            if (EconomyService.Instance != null && !CurrencyManager.FreeMode
                && !(GameService.Instance != null && GameService.Instance.IsOnline))
            {
                foreach (var hc in cost.harvestCosts)
                {
                    var spendItems = new SpendItemsRequest
                    {
                        items = new List<SpendItemEntry> { new SpendItemEntry { item_name = hc.itemName, count = hc.count } },
                        freeMode = CurrencyManager.FreeMode
                    };
                    EconomyService.Instance.Enqueue("spend-items", JsonUtility.ToJson(spendItems));
                }
            }

            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = gridX, gridY = gridY });
            SaveManager.Instance.Save();
            int newIndex = data.plots.Count - 1;
            OnPlotChanged?.Invoke(newIndex);
            AudioManager.Instance?.PlaySFX("plot_craft");

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = NotifyServerCraftPlot(newIndex, gridX, gridY);
            }

            return true;
        }

        private async Task NotifyServerCraftPlot(int plotIndex, int gridX, int gridY)
        {
            var result = await GameService.Instance.CraftPlot(gridX, gridY);
            if (result != null)
            {
                var data = SaveManager.Instance.Data;
                if (plotIndex < data.plots.Count)
                {
                    data.plots[plotIndex].serverId = result.id;
                    SaveManager.Instance.Save();
                }
            }
        }

        public bool Plant(int plotIndex, string seedName)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Empty) return false;

            var seedItemName = seedName + "_Seed";
            var seedEntry = data.inventory.Find(i => i.itemName == seedItemName);
            if (!CurrencyManager.FreeMode)
            {
                if (seedEntry == null || seedEntry.count <= 0) return false;
                seedEntry.count--;
                if (seedEntry.count <= 0) data.inventory.Remove(seedEntry);
                if (!(GameService.Instance != null && GameService.Instance.IsOnline))
                    EconomyService.Instance?.Enqueue("spend-items",
                        JsonUtility.ToJson(new SpendItemsRequest
                        {
                            items = new List<SpendItemEntry> { new SpendItemEntry { item_name = seedItemName, count = 1 } },
                            freeMode = CurrencyManager.FreeMode
                        }));
            }

            plot.seedName = seedName;
            plot.state = PlotState.Growing;
            plot.plantTimeUtc = GameTime.UtcNow.ToString("o");
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();
            plot.lastWateredUtc = null;

            // Record initial weather snapshot so even fast-growing plants get scored
            // Only record if weather has actually been fetched (avoid zero-value snapshots)
            if (WeatherService.Instance != null && WeatherService.Instance.HasWeather)
                plot.snapshots.RecordSnapshot(WeatherService.Instance.CurrentWeather);

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            AudioManager.Instance?.PlaySFX("plot_plant");

            var remaining = GetRemainingSeconds(plotIndex);
            NotificationService.Instance?.SchedulePlantNotification(plotIndex, seedName, remaining);

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline && plot.serverId > 0)
            {
                _ = GameService.Instance.PlantSeed(plot.serverId, seedName);
            }

            return true;
        }

        public bool Water(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (!CanWaterPlot(plot, GameTime.UtcNow, ManualWaterCooldownHours)) return false;

            // Identify which vase will supply the water (SpendWater deducts from first with water)
            int sourceVaseServerId = 0;
            for (int i = 0; i < data.vases.Count; i++)
            {
                if (data.vases[i].currentWater > 0 && data.vases[i].serverId > 0)
                { sourceVaseServerId = data.vases[i].serverId; break; }
            }

            if (!CurrencyManager.Instance.SpendWater(1)) return false;

            ApplyWatering(plot, GameTime.UtcNow.ToString("o"));

            if (plot.subscribeWater)
            {
                double cooldownSeconds = ManualWaterCooldownHours * 3600.0;
                NotificationService.Instance?.ScheduleWaterNotification(plotIndex, plot.seedName, cooldownSeconds);
            }

            SaveManager.Instance.Save();
            OnPlotChanged?.Invoke(plotIndex);
            AudioManager.Instance?.PlaySFX("plot_water");

            // Notify server with the vase that actually supplied the water
            if (GameService.Instance != null && GameService.Instance.IsOnline && plot.serverId > 0
                && sourceVaseServerId > 0)
                _ = GameService.Instance.WaterPlot(plot.serverId, sourceVaseServerId);

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

            // Fallback: use current weather if no snapshots were recorded during growth
            if ((plot.snapshots == null || plot.snapshots.snapshotCount == 0)
                && WeatherService.Instance != null && WeatherService.Instance.HasWeather)
            {
                if (plot.snapshots == null) plot.snapshots = new GrowthSnapshots();
                plot.snapshots.RecordSnapshot(WeatherService.Instance.CurrentWeather);
            }

            // Local fallback calculation — randomized drops scaled by quality
            float score = 1f;
            if (seed.recipe != null)
                score = seed.recipe.Evaluate(plot.snapshots ?? new GrowthSnapshots(), plot.waterCount);
            int drops = CalculateDrops(score, seed.minDrops, seed.maxDrops);

            var result = new HarvestResult
            {
                seedName = seed.seedName,
                drops = drops,
                recipeScore = score,
                snapshots = plot.snapshots ?? new GrowthSnapshots(),
                waterCount = plot.waterCount,
                recipe = seed.recipe
            };

            int serverId = plot.serverId;

            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.waterCount = 0;
            plot.snapshots = new GrowthSnapshots();
            plot.lastWateredUtc = null;
            plot.state = PlotState.Empty;

            // Add items locally as fallback (server response will be authoritative)
            AddItem(data, seed.seedName, drops);
            if (!(GameService.Instance != null && GameService.Instance.IsOnline))
                EconomyService.Instance?.Enqueue("add-items",
                    JsonUtility.ToJson(new AddItemRequest { item_name = seed.seedName, count = drops }));

            SaveManager.Instance.Save();

            OnPlotChanged?.Invoke(plotIndex);
            OnHarvested?.Invoke(plotIndex, result);
            AudioManager.Instance?.PlaySFX("plot_harvest");

            NotificationService.Instance?.CancelPlantNotification(plotIndex);
            NotificationService.Instance?.CancelWaterNotification(plotIndex);

            // Notify server for authoritative harvest
            if (GameService.Instance != null && GameService.Instance.IsOnline && serverId > 0)
            {
                _ = NotifyServerHarvest(serverId, result);
            }

            return result;
        }

        private async Task NotifyServerHarvest(int serverId, HarvestResult localResult)
        {
            var resp = await GameService.Instance.Harvest(serverId);
            if (resp != null)
            {
                // Server response is authoritative — sync economy
                await EconomyService.Instance.SyncFromServer();
            }
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

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline && plot.serverId > 0)
                _ = GameService.Instance.InstantFinishPlot(plot.serverId);

            return true;
        }

        public bool SpeedUpGrowth(int plotIndex)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
            var plot = data.plots[plotIndex];
            if (plot.state != PlotState.Growing) return false;

            // Check potion availability first
            if (!CurrencyManager.FreeMode)
            {
                var potion = data.inventory.Find(i => i.itemName == "Speed_Potion");
                if (potion == null || potion.count <= 0) return false;
                potion.count--;
                if (potion.count <= 0) data.inventory.Remove(potion);
            }

            return InstantFinish(plotIndex);
        }

        public int GetSpeedPotionCount()
        {
            var item = SaveManager.Instance.Data.inventory.Find(i => i.itemName == "Speed_Potion");
            return item?.count ?? 0;
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

            if (CheckRainEvent(data, weather.condition, GameTime.UtcNow))
            {
                VaseManager.RainFillAllVases(data.vases);

                foreach (var mallum in data.mallums)
                {
                    if (mallum.state == MallumState.FetchingWater)
                        MallumManager.FreeMallumFromWater(mallum);
                }

                RainWaterAllPlots(data.plots, GameTime.UtcNow);
                changed = true;

                VaseManager.Instance?.NotifyChanged();
                MallumManager.Instance?.NotifyChanged();
            }

            if (changed) SaveManager.Instance.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ScheduleAllPlantNotifications();
        }

        private void ScheduleAllPlantNotifications()
        {
            var ns = NotificationService.Instance;
            if (ns == null) return;

            // Cancel only plot-specific notifications (plant growth + water cooldown).
            // Other managers handle their own notifications in OnApplicationPause.
            var data = SaveManager.Instance.Data;
            for (int i = 0; i < data.plots.Count; i++)
            {
                ns.CancelPlantNotification(i);
                ns.CancelWaterNotification(i);
            }

            for (int i = 0; i < data.plots.Count; i++)
            {
                var plot = data.plots[i];
                if (plot.state != PlotState.Growing) continue;
                var remaining = GetRemainingSeconds(i);
                ns.SchedulePlantNotification(i, plot.seedName, remaining);

                if (plot.subscribeWater)
                {
                    double waterRemaining = GetWaterCooldownRemaining(plot);
                    ns.ScheduleWaterNotification(i, plot.seedName, waterRemaining);
                }
            }
        }

        public void SetWaterSubscription(int plotIndex, bool subscribe)
        {
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return;
            var plot = data.plots[plotIndex];
            plot.subscribeWater = subscribe;

            if (subscribe && plot.state == PlotState.Growing)
            {
                double remaining = GetWaterCooldownRemaining(plot);
                NotificationService.Instance?.ScheduleWaterNotification(plotIndex, plot.seedName, remaining);
            }
            else
            {
                NotificationService.Instance?.CancelWaterNotification(plotIndex);
            }

            SaveManager.Instance.Save();
        }

        public static double GetWaterCooldownRemaining(PlotSave plot)
        {
            if (plot.state != PlotState.Growing) return 0;
            if (string.IsNullOrEmpty(plot.lastWateredUtc)) return 0;
            var lastWatered = DateTime.Parse(plot.lastWateredUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            double elapsed = (GameTime.UtcNow - lastWatered).TotalSeconds;
            double total = ManualWaterCooldownHours * 3600.0;
            return Math.Max(0, total - elapsed);
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
                    // Backfill a snapshot if weather arrived after planting but before maturity
                    if (plot.snapshots != null && plot.snapshots.snapshotCount == 0
                        && WeatherService.Instance != null && WeatherService.Instance.HasWeather)
                    {
                        plot.snapshots.RecordSnapshot(WeatherService.Instance.CurrentWeather);
                    }
                    plot.state = PlotState.Mature;
                    changed = true;
                    OnPlotChanged?.Invoke(i);
                }
            }
            if (changed) SaveManager.Instance.Save();
        }

        private static void AddItem(SaveData data, string itemName, int count)
        {
            var existing = data.inventory.Find(i => i.itemName == itemName);
            if (existing != null)
                existing.count += count;
            else
                data.inventory.Add(new InventoryItem { itemName = itemName, count = count });
        }

        private static ServerSeedConfig LoadSeed(string seedName)
        {
            if (string.IsNullOrEmpty(seedName)) return null;
            return ConfigService.Instance?.GetSeed(seedName);
        }

        /// <summary>
        /// Returns the display name for a seed given its asset name.
        /// Falls back to the asset name if config is not found.
        /// </summary>
        public static string GetSeedDisplayName(string assetName)
        {
            var seed = LoadSeed(assetName);
            return seed != null ? seed.seedName : assetName;
        }

        public static int CalculateDrops(float score, int minDrops, int maxDrops)
        {
            float center = minDrops + score * (maxDrops - minDrops);
            float spread = (maxDrops - minDrops) * 0.3f;
            int low = Mathf.Max(minDrops, Mathf.RoundToInt(center - spread));
            int high = Mathf.Min(maxDrops, Mathf.RoundToInt(center + spread));
            return UnityEngine.Random.Range(low, high + 1);
        }
    }

    [Serializable]
    public class HarvestResult
    {
        public string seedName;
        public int drops;
        public float recipeScore;
        public GrowthSnapshots snapshots;
        public int waterCount;
        public GrowthRecipe recipe;
    }
}
