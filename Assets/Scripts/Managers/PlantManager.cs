using System;
using UnityEngine;

namespace Garden
{
    public class PlantManager : MonoBehaviour
    {
        public static PlantManager Instance { get; private set; }

        public PlantState State { get; private set; } = PlantState.Empty;
        public SeedData CurrentSeed { get; private set; }
        public VariantData CurrentVariant { get; private set; }
        public float GrowthProgress { get; private set; }
        public float GrowthSpeedMultiplier { get; private set; } = 1f;
        public DateTime PlantTime { get; private set; }

        public event Action OnPlantStateChanged;
        public event Action<float> OnGrowthUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RestoreFromSave();
        }

        private void Update()
        {
            if (State != PlantState.Growing) return;

            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            GrowthProgress = Mathf.Clamp01(elapsed / totalHours);
            OnGrowthUpdated?.Invoke(GrowthProgress);

            if (WeatherService.Instance != null && CurrentVariant.trigger != null)
            {
                if (CurrentVariant.trigger.Evaluate(WeatherService.Instance.CurrentWeather))
                    GrowthSpeedMultiplier = 1.25f;
                else
                    GrowthSpeedMultiplier = 1f;
            }

            if (GrowthProgress >= 1f)
            {
                State = PlantState.Mature;
                OnPlantStateChanged?.Invoke();
                SaveState();
            }
        }

        public void Plant(SeedData seed)
        {
            if (State != PlantState.Empty) return;

            var weather = WeatherService.Instance.CurrentWeather;
            var result = GeneticsEngine.Resolve(seed, weather);

            CurrentSeed = seed;
            CurrentVariant = result.variant;
            GrowthSpeedMultiplier = result.growthSpeedMultiplier;
            PlantTime = DateTime.UtcNow;
            GrowthProgress = 0f;
            State = PlantState.Growing;

            var save = SaveManager.Instance.Data;
            if (!save.discoveredVariants.Contains(result.variant.variantName))
                save.discoveredVariants.Add(result.variant.variantName);

            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
            if (entry != null) entry.count--;

            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        public void Harvest()
        {
            if (State != PlantState.Mature) return;

            GreenhouseManager.Instance.AddPlant(CurrentSeed, CurrentVariant);
            int dewdrops = CurrencyManager.Instance.Config.GetDewdropsForRarity(CurrentVariant.rarity);
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, dewdrops);

            CurrentSeed = null;
            CurrentVariant = null;
            GrowthProgress = 0f;
            State = PlantState.Empty;

            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        public float GetRemainingHours()
        {
            if (State != PlantState.Growing) return 0f;
            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        private void SaveState()
        {
            var save = SaveManager.Instance.Data;
            if (State == PlantState.Empty)
            {
                save.activePlant = new ActivePlantSave { isActive = false };
            }
            else
            {
                save.activePlant = new ActivePlantSave
                {
                    isActive = true,
                    seedName = CurrentSeed.seedName,
                    variantName = CurrentVariant.variantName,
                    plantTimeUtc = PlantTime.ToString("O"),
                    growthSpeedMultiplier = GrowthSpeedMultiplier
                };
            }
            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;
            if (save.activePlant == null || !save.activePlant.isActive) return;

            var seeds = Resources.LoadAll<SeedData>("Seeds");
            foreach (var seed in seeds)
            {
                if (seed.seedName != save.activePlant.seedName) continue;
                CurrentSeed = seed;
                foreach (var v in seed.variants)
                {
                    if (v.variantName != save.activePlant.variantName) continue;
                    CurrentVariant = v;
                    break;
                }
                break;
            }

            if (CurrentSeed == null || CurrentVariant == null) return;

            PlantTime = DateTime.Parse(save.activePlant.plantTimeUtc).ToUniversalTime();
            GrowthSpeedMultiplier = save.activePlant.growthSpeedMultiplier;

            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            GrowthProgress = Mathf.Clamp01(elapsed / totalHours);

            State = GrowthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
            OnPlantStateChanged?.Invoke();
        }
    }
}