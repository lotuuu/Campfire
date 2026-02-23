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

            dustAccumulator += Time.deltaTime;
            if (dustAccumulator >= 3600f)
            {
                dustAccumulator -= 3600f;
                int totalDust = Mathf.RoundToInt(GetTotalDustPerHour());
                if (totalDust > 0)
                    CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);
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
                harvestTime = GameTime.UtcNow
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
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, value);

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return value;
        }

        public bool ExpandSlots()
        {
            var config = CurrencyManager.Instance.Config;
            if (!CurrencyManager.Instance.Spend(CurrencyType.SunShards, config.slotCostSunShards))
                return false;
            SaveManager.Instance.Data.greenhouseSlots++;
            SaveManager.Instance.Save();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public float GetTotalDustPerHour()
        {
            float total = 0;
            var config = CurrencyManager.Instance.Config;
            foreach (var p in Plants)
                total += config.GetDustPerHourForPlant(p.rarity, p.qualityTier);
            return total;
        }

        public void DebugAdvanceTime(float hours)
        {
            if (Plants.Count == 0) return;
            int totalDust = Mathf.RoundToInt(GetTotalDustPerHour() * hours);
            if (totalDust > 0)
                CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);
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
                    qualityTier = p.qualityTier
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
                    harvestTime = DateTime.Parse(ps.harvestTimeUtc).ToUniversalTime()
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
        public DateTime harvestTime;
    }
}
