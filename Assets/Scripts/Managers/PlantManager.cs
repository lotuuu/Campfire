using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class PlantManager : MonoBehaviour
    {
        public static PlantManager Instance { get; private set; }

        private List<PlantSlot> slots = new();

        // Legacy single-plant compatibility
        public PlantState State => GetFirstSlotInState(PlantState.Mature)?.state
            ?? GetFirstSlotInState(PlantState.Growing)?.state
            ?? PlantState.Empty;
        public SeedData CurrentSeed => GetFeaturedSlot()?.seed;
        public VariantData CurrentVariant => GetFeaturedSlot()?.variant;
        public float GrowthProgress => GetFeaturedSlot()?.growthProgress ?? 0f;
        public float GrowthSpeedMultiplier => GetFeaturedSlot()?.growthSpeedMultiplier ?? 1f;
        public DateTime PlantTime => GetFeaturedSlot()?.plantTime ?? DateTime.MinValue;

        public event Action OnPlantStateChanged;
        public event Action<int, int, PlantState> OnSlotStateChanged;
        public event Action<int, int, float> OnSlotGrowthUpdated;

        private const float GrowthTickInterval = 5f;
        private float _growthTickTimer;

        public IReadOnlyList<PlantSlot> Slots => slots;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            InitializeSlots();
            RestoreFromSave();
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += RefreshMultipliers;
                RefreshMultipliers(WeatherService.Instance.CurrentWeather);
            }
            // Re-schedule notifications after restore, in case OnApplicationPause(false)
            // fired at startup and cancelled them before plant data was loaded.
            NotificationService.Instance?.RescheduleAll();
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= RefreshMultipliers;
        }

        private void RefreshMultipliers(WeatherData weather)
        {
            foreach (var slot in slots)
            {
                if (slot.state != PlantState.Growing) continue;
                var envConsumables = ConsumableManager.Instance != null
                    ? ConsumableManager.Instance.GetEnvConsumables(slot.environmentIndex)
                    : new List<ConsumableData>();
                var effective = ApplyConsumableOverrides(envConsumables, weather);
                slot.growthSpeedMultiplier = (slot.variant?.trigger != null
                    && slot.variant.trigger.Evaluate(effective)) ? 1.25f : 1f;
                slot.cachedEnvBonus = EnvironmentManager.Instance != null
                    ? EnvironmentManager.Instance.GetGrowthBonus(slot.environmentIndex, effective)
                    : 0f;
            }
        }

        public void ForceRefreshMultipliers()
        {
            if (WeatherService.Instance != null)
                RefreshMultipliers(WeatherService.Instance.CurrentWeather);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (NotificationService.Instance == null) return;

            if (pauseStatus)
                NotificationService.Instance.RescheduleAll();
            else
                NotificationService.Instance.CancelAll();
        }

        private void Update()
        {
            bool anyMatured = false;

            // Update progress every frame for smooth UI; transition to Mature immediately when done
            foreach (var slot in slots)
            {
                if (slot.state != PlantState.Growing) continue;

                bool hasFertilizer = slot.appliedConsumables != null &&
                    slot.appliedConsumables.Exists(c => c.type == ConsumableType.Fertilizer);
                float fertilizerBonus = hasFertilizer ? 1f : 0f;
                float totalMultiplier = Mathf.Max(
                    slot.growthSpeedMultiplier + slot.cachedEnvBonus + fertilizerBonus, 0.01f);
                float totalHours = slot.seed.baseGrowthHours / totalMultiplier;
                float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);

                if (slot.growthProgress >= 1f)
                {
                    slot.state = PlantState.Mature;
                    OnSlotStateChanged?.Invoke(slot.environmentIndex, slot.slotIndex, PlantState.Mature);
                    OnPlantStateChanged?.Invoke();
                    anyMatured = true;
                }
                else
                {
                    OnSlotGrowthUpdated?.Invoke(slot.environmentIndex, slot.slotIndex, slot.growthProgress);
                }
            }

            // Tick only for periodic save
            _growthTickTimer += Time.deltaTime;
            if (anyMatured || _growthTickTimer >= GrowthTickInterval)
            {
                _growthTickTimer = 0f;
                SaveState();
            }
        }

        public PlantSlot GetSlot(int envIndex, int slotIndex)
        {
            return slots.Find(s => s.environmentIndex == envIndex && s.slotIndex == slotIndex);
        }

        public List<PlantSlot> GetSlotsForEnvironment(int envIndex)
        {
            return slots.FindAll(s => s.environmentIndex == envIndex);
        }

        public bool Plant(SeedData seed, int environmentIndex, int slotIndex)
        {
            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Empty) return false;

            var weather = WeatherService.Instance.CurrentWeather;
            var result = GeneticsEngine.Resolve(seed, weather);

            slot.seed = seed;
            slot.variant = result.variant;
            slot.growthSpeedMultiplier = result.growthSpeedMultiplier;
            slot.plantTime = GameTime.UtcNow;
            slot.growthProgress = 0f;
            slot.state = PlantState.Growing;

            var save = SaveManager.Instance.Data;
            if (!save.discoveredVariants.Contains(result.variant.variantName))
                save.discoveredVariants.Add(result.variant.variantName);

            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
            if (entry != null && !seed.infinite) entry.count--;

            if (NotificationService.Instance != null)
            {
                float envBonus = 0f;
                if (EnvironmentManager.Instance != null && WeatherService.Instance != null)
                    envBonus = EnvironmentManager.Instance.GetGrowthBonus(
                        environmentIndex, WeatherService.Instance.CurrentWeather);
                float totalMultiplier = result.growthSpeedMultiplier + envBonus;
                double remainingSeconds = (seed.baseGrowthHours / totalMultiplier) * 3600.0;
                NotificationService.Instance.SchedulePlantNotification(
                    environmentIndex, slotIndex, seed.seedName, remainingSeconds);
            }

            OnSlotStateChanged?.Invoke(environmentIndex, slotIndex, PlantState.Growing);
            OnPlantStateChanged?.Invoke();
            SaveState();
            return true;
        }

        public void Plant(SeedData seed)
        {
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Empty)
                {
                    Plant(seed, slot.environmentIndex, slot.slotIndex);
                    return;
                }
            }
        }

        public HarvestResult Harvest(int environmentIndex, int slotIndex)
        {
            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Mature)
                return default;

            var globalWeather = WeatherService.Instance.CurrentWeather;
            var envConsumables = ConsumableManager.Instance != null
                ? ConsumableManager.Instance.GetEnvConsumables(environmentIndex)
                : new List<ConsumableData>();
            var effectiveWeather = ApplyConsumableOverrides(envConsumables, globalWeather);
            bool qualityBoosted = slot.appliedConsumables != null &&
                slot.appliedConsumables.Exists(c => c.type == ConsumableType.QualityDirt);
            var result = HarvestEngine.Roll(slot.seed, slot.variant, effectiveWeather, qualityBoosted);

            ClearSlot(slot);
            return result;
        }

        public void Harvest()
        {
            var slot = GetFirstSlotInState(PlantState.Mature);
            if (slot == null) return;

            var result = Harvest(slot.environmentIndex, slot.slotIndex);
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
            CurrencyManager.Instance.Add(CurrencyType.Gold, result.goldValue);
        }

        public void SellHarvest(HarvestResult result)
        {
            CurrencyManager.Instance.Add(CurrencyType.Gold, result.goldValue);
        }

        public void KeepHarvest(HarvestResult result)
        {
            GreenhouseManager.Instance.AddPlant(result.seed, result.variant, result.tier);
        }

        public static bool CheckAndMarkDiscovered(VariantData variant, SaveData save)
        {
            if (variant == null || string.IsNullOrEmpty(variant.variantName)) return false;
            if (save.discoveredVariants.Contains(variant.variantName)) return false;
            save.discoveredVariants.Add(variant.variantName);
            return true;
        }

        public void DebugAdvanceTime(float hours)
        {
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Growing)
                    slot.plantTime = slot.plantTime.AddHours(-hours);
            }
            SaveState();
        }

        public float GetRemainingHours()
        {
            var slot = GetFeaturedSlot();
            if (slot == null || slot.state != PlantState.Growing) return 0f;
            bool hasFertilizer = slot.appliedConsumables != null &&
                slot.appliedConsumables.Exists(c => c.type == ConsumableType.Fertilizer);
            float fertilizerBonus = hasFertilizer ? 1f : 0f;
            float totalMultiplier = Mathf.Max(
                slot.growthSpeedMultiplier + slot.cachedEnvBonus + fertilizerBonus, 0.01f);
            float totalHours = slot.seed.baseGrowthHours / totalMultiplier;
            float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        public float GetRemainingHours(int envIndex, int slotIndex)
        {
            var slot = GetSlot(envIndex, slotIndex);
            if (slot == null || slot.state != PlantState.Growing) return 0f;
            bool hasFertilizer = slot.appliedConsumables != null &&
                slot.appliedConsumables.Exists(c => c.type == ConsumableType.Fertilizer);
            float fertilizerBonus = hasFertilizer ? 1f : 0f;
            float totalMultiplier = Mathf.Max(
                slot.growthSpeedMultiplier + slot.cachedEnvBonus + fertilizerBonus, 0.01f);
            float totalHours = slot.seed.baseGrowthHours / totalMultiplier;
            float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        public int GetMatureCount()
        {
            int count = 0;
            foreach (var slot in slots)
                if (slot.state == PlantState.Mature) count++;
            return count;
        }

        public int GetGrowingCount()
        {
            int count = 0;
            foreach (var slot in slots)
                if (slot.state == PlantState.Growing) count++;
            return count;
        }

        public PlantSlot GetFirstSlotInState(PlantState state)
        {
            foreach (var slot in slots)
                if (slot.state == state) return slot;
            return null;
        }

        private PlantSlot GetFeaturedSlot()
        {
            return GetFirstSlotInState(PlantState.Mature)
                ?? GetFirstSlotInState(PlantState.Growing);
        }

        public void AddSlot(int envIndex, int slotIndex)
        {
            slots.Add(new PlantSlot { environmentIndex = envIndex, slotIndex = slotIndex });
            OnPlantStateChanged?.Invoke();
        }

        /// <summary>
        /// Spends one slot-scoped consumable (Fertilizer or QualityDirt) and applies it to the slot.
        /// Returns false if: consumable is env-scoped, slot is empty, already has this type, or out of stock.
        /// </summary>
        public bool ApplyConsumable(ConsumableType type, int environmentIndex, int slotIndex)
        {
            var consumableData = ConsumableManager.Instance?.GetConsumableData(type);
            if (consumableData == null || consumableData.isEnvironmentScoped) return false;

            var slot = GetSlot(environmentIndex, slotIndex);
            if (slot == null || slot.state == PlantState.Empty) return false;
            if (slot.appliedConsumables.Exists(c => c.type == type)) return false;

            if (!ConsumableManager.Instance.Spend(type)) return false;

            slot.appliedConsumables.Add(consumableData);
            SaveState();
            return true;
        }

        private void InitializeSlots()
        {
            slots.Clear();
            if (EnvironmentManager.Instance == null) return;

            var envs = EnvironmentManager.Instance.Environments;
            for (int e = 0; e < envs.Count; e++)
            {
                int count = EnvironmentManager.Instance.GetActiveSlotCount(e);
                for (int s = 0; s < count; s++)
                {
                    slots.Add(new PlantSlot
                    {
                        environmentIndex = e,
                        slotIndex = s
                    });
                }
            }
        }

        private void ClearSlot(PlantSlot slot)
        {
            if (NotificationService.Instance != null)
                NotificationService.Instance.CancelPlantNotification(slot.environmentIndex, slot.slotIndex);

            slot.seed = null;
            slot.variant = null;
            slot.growthProgress = 0f;
            slot.state = PlantState.Empty;
            slot.appliedConsumables.Clear();

            OnSlotStateChanged?.Invoke(slot.environmentIndex, slot.slotIndex, PlantState.Empty);
            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        private void SaveState()
        {
            var save = SaveManager.Instance.Data;
            save.activeSlots.Clear();
            foreach (var slot in slots)
            {
                if (slot.state == PlantState.Empty) continue;
                save.activeSlots.Add(new PlantSlotSave
                {
                    environmentIndex = slot.environmentIndex,
                    slotIndex = slot.slotIndex,
                    seedName = slot.seed.seedName,
                    variantName = slot.variant.variantName,
                    plantTimeUtc = slot.plantTime.ToString("O"),
                    growthSpeedMultiplier = slot.growthSpeedMultiplier,
                    appliedConsumables = slot.appliedConsumables
                        .ConvertAll(c => c.type.ToString())
                });
            }
            var featured = GetFeaturedSlot();
            save.activePlant = featured != null
                ? new ActivePlantSave
                {
                    isActive = true,
                    seedName = featured.seed.seedName,
                    variantName = featured.variant.variantName,
                    plantTimeUtc = featured.plantTime.ToString("O"),
                    growthSpeedMultiplier = featured.growthSpeedMultiplier
                }
                : new ActivePlantSave { isActive = false };

            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;

            if (save.activeSlots != null && save.activeSlots.Count > 0)
            {
                foreach (var ps in save.activeSlots)
                {
                    var slot = GetSlot(ps.environmentIndex, ps.slotIndex);
                    if (slot == null) continue;

                    var seed = SeedRegistry.Instance.GetSeed(ps.seedName);
                    if (seed == null) continue;
                    VariantData variant = null;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName == ps.variantName) { variant = v; break; }
                    }
                    if (variant == null) continue;

                    slot.seed = seed;
                    slot.variant = variant;
                    slot.plantTime = DateTime.Parse(ps.plantTimeUtc).ToUniversalTime();
                    slot.growthSpeedMultiplier = ps.growthSpeedMultiplier;

                    if (ps.appliedConsumables != null && ConsumableManager.Instance != null)
                    {
                        foreach (var typeName in ps.appliedConsumables)
                        {
                            if (System.Enum.TryParse<ConsumableType>(typeName, out var ctype))
                            {
                                var cd = ConsumableManager.Instance.GetConsumableData(ctype);
                                if (cd != null) slot.appliedConsumables.Add(cd);
                            }
                        }
                    }

                    float totalHours = seed.baseGrowthHours / slot.growthSpeedMultiplier;
                    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                    slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);
                    slot.state = slot.growthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
                }
                OnPlantStateChanged?.Invoke();
                return;
            }

            if (save.activePlant != null && save.activePlant.isActive && slots.Count > 0)
            {
                var slot = slots[0];
                var seeds = Resources.LoadAll<SeedData>("Seeds");
                foreach (var seed in seeds)
                {
                    if (seed.seedName != save.activePlant.seedName) continue;
                    slot.seed = seed;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName == save.activePlant.variantName)
                        { slot.variant = v; break; }
                    }
                    break;
                }
                if (slot.seed != null && slot.variant != null)
                {
                    slot.plantTime = DateTime.Parse(save.activePlant.plantTimeUtc).ToUniversalTime();
                    slot.growthSpeedMultiplier = save.activePlant.growthSpeedMultiplier;
                    float totalHours = slot.seed.baseGrowthHours / slot.growthSpeedMultiplier;
                    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
                    slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);
                    slot.state = slot.growthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
                }
                OnPlantStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Returns a copy of globalWeather with env-scoped consumable overrides applied.
        /// Only Fan/Igloo/Heater/Cloud modify the weather struct.
        /// WeatherData is a struct so assignment gives a copy — globalWeather is never mutated.
        /// </summary>
        internal static WeatherData ApplyConsumableOverrides(
            List<ConsumableData> consumables, WeatherData globalWeather)
        {
            var w = globalWeather;
            foreach (var c in consumables)
            {
                switch (c.type)
                {
                    case ConsumableType.Fan:    w.windSpeed   += c.magnitude; break;
                    case ConsumableType.Igloo:  w.temperature -= c.magnitude; break;
                    case ConsumableType.Heater: w.temperature += c.magnitude; break;
                    case ConsumableType.Cloud:  w.condition    = WeatherCondition.Rain; break;
                }
            }
            return w;
        }
    }
}
