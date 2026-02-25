using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class GreenhouseManager : MonoBehaviour
    {
        public static GreenhouseManager Instance { get; private set; }

        public List<GreenhousePlant> Plants { get; private set; } = new();
        public int MaxSlots => SaveManager.Instance.Data.greenhouseSlots;

        public event Action OnGreenhouseChanged;

        private float dustAccumulator;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            RestoreFromSave();
        }

        private void Update()
        {
            if (Plants.Count == 0) return;

            dustAccumulator += GetTotalDustPerSecond() * Time.deltaTime;
            int toAward = Mathf.FloorToInt(dustAccumulator);
            if (toAward > 0)
            {
                dustAccumulator -= toAward;
                CurrencyManager.Instance.Add(CurrencyType.AuraDust, toAward);
            }

            for (int i = 0; i < Plants.Count; i++)
            {
                var p = Plants[i];
                if (p.isWithered) continue;

                float progress = ComputeDecayProgress(p.tierStartTime, p.qualityTier, p.baseGrowthHours, GameTime.UtcNow);
                if (progress < 1f) continue;

                if (p.qualityTier == QualityTier.D)
                {
                    p.isWithered = true;
                }
                else
                {
                    p.qualityTier = p.qualityTier - 1;
                    p.tierStartTime = GameTime.UtcNow;
                }

                SaveGreenhouse();
                OnGreenhouseChanged?.Invoke();
            }
        }

        public bool AddPlant(SeedData seed, VariantData variant, QualityTier tier = QualityTier.C)
        {
            if (Plants.Count >= MaxSlots) return false;

            Plants.Add(new GreenhousePlant
            {
                seedName = seed.seedName,
                variantName = variant.variantName,
                rarity = variant.rarity,
                qualityTier = tier,
                primaryColor = variant.primaryColor,
                baseGrowthHours = seed.baseGrowthHours,
                harvestTime = GameTime.UtcNow,
                tierStartTime = GameTime.UtcNow,
                isWithered = false
            });

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public bool AddPlant(SeedData seed, VariantData variant)
        {
            return AddPlant(seed, variant, QualityTier.C);
        }

        public int SellPlant(int index)
        {
            if (index < 0 || index >= Plants.Count) return 0;

            var plant = Plants[index];
            var seed = SeedRegistry.Instance.GetSeed(plant.seedName);
            int baseSell = seed != null ? seed.baseSellPrice : 100;
            int value = CurrencyManager.Instance.Config.GetSellValue(baseSell, plant.qualityTier);

            Plants.RemoveAt(index);
            CurrencyManager.Instance.Add(CurrencyType.Gold, value);

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return value;
        }

        public void TrashPlant(int index)
        {
            if (index < 0 || index >= Plants.Count) return;
            Plants.RemoveAt(index);
            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
        }

        public float GetDecayProgress(int index)
        {
            if (index < 0 || index >= Plants.Count) return 0f;
            var p = Plants[index];
            if (p.isWithered) return 1f;
            return Mathf.Clamp01(ComputeDecayProgress(p.tierStartTime, p.qualityTier, p.baseGrowthHours, GameTime.UtcNow));
        }

        public bool ExpandSlots()
        {
            var config = CurrencyManager.Instance.Config;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, config.greenhouseExpandCostGold))
                return false;
            SaveManager.Instance.Data.greenhouseSlots++;
            SaveManager.Instance.Save();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public float GetTotalDustPerSecond()
        {
            float total = 0;
            var config = CurrencyManager.Instance.Config;
            foreach (var p in Plants)
                if (!p.isWithered)
                    total += config.GetDustPerSecondForPlant(p.rarity, p.qualityTier);
            return total;
        }


        public static float GetStepMinutes(QualityTier tier, float baseGrowthHours)
        {
            float factor = tier switch
            {
                QualityTier.S => 4.00f,
                QualityTier.A => 2.00f,
                QualityTier.B => 1.00f,
                QualityTier.C => 0.50f,
                QualityTier.D => 0.25f,
                _ => 0.25f
            };
            return factor * baseGrowthHours * 60f;
        }

        public static float ComputeDecayProgress(DateTime tierStartTime, QualityTier tier, float baseGrowthHours, DateTime now)
        {
            float elapsedMinutes = (float)(now - tierStartTime).TotalMinutes;
            return elapsedMinutes / GetStepMinutes(tier, baseGrowthHours);
        }

        public void DebugAdvanceTime(float hours)
        {
            if (Plants.Count == 0) return;
            int totalDust = Mathf.RoundToInt(GetTotalDustPerSecond() * hours * 3600f);
            if (totalDust > 0)
                CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);

            foreach (var p in Plants)
            {
                if (!p.isWithered)
                    p.tierStartTime = p.tierStartTime.AddHours(-hours);
            }
        }

        private void SaveGreenhouse()
        {
            var save = SaveManager.Instance.Data;
            save.greenhousePlants.Clear();
            foreach (var p in Plants)
            {
                save.greenhousePlants.Add(new GreenhousePlantSave
                {
                    seedName = p.seedName,
                    variantName = p.variantName,
                    harvestTimeUtc = p.harvestTime.ToString("O"),
                    qualityTier = p.qualityTier,
                    tierStartTimeUtc = p.tierStartTime.ToString("O"),
                    isWithered = p.isWithered,
                    baseGrowthHours = p.baseGrowthHours
                });
            }
            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;
            Plants.Clear();

            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            foreach (var ps in save.greenhousePlants)
            {
                Rarity rarity = Rarity.Common;
                Color color = Color.green;
                foreach (var seed in allSeeds)
                {
                    if (seed.seedName != ps.seedName) continue;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName != ps.variantName) continue;
                        rarity = v.rarity;
                        color = v.primaryColor;
                        break;
                    }
                    break;
                }

                Plants.Add(new GreenhousePlant
                {
                    seedName = ps.seedName,
                    variantName = ps.variantName,
                    rarity = rarity,
                    qualityTier = ps.qualityTier,
                    primaryColor = color,
                    baseGrowthHours = ps.baseGrowthHours,
                    harvestTime = DateTime.Parse(ps.harvestTimeUtc).ToUniversalTime(),
                    tierStartTime = string.IsNullOrEmpty(ps.tierStartTimeUtc)
                        ? GameTime.UtcNow
                        : DateTime.Parse(ps.tierStartTimeUtc).ToUniversalTime(),
                    isWithered = ps.isWithered
                });
            }
        }
    }

    public class GreenhousePlant
    {
        public string seedName;
        public string variantName;
        public Rarity rarity;
        public QualityTier qualityTier;
        public Color primaryColor;
        public float baseGrowthHours;
        public DateTime harvestTime;
        public DateTime tierStartTime;  // when current tier began decaying
        public bool isWithered;         // true once plant has passed below D
    }
}
