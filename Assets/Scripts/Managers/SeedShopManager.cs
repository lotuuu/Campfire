using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class SeedShopManager : MonoBehaviour
    {
        public static SeedShopManager Instance { get; private set; }

        public event Action<string> OnSeedPurchased;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public List<SeedData> GetShopSeeds()
        {
            var result = new List<SeedData>();
            foreach (var seed in SeedRegistry.Instance.AllSeeds)
                result.Add(seed);
            return result;
        }

        public bool CanBuy(string seedName)
        {
            var seed = SeedRegistry.Instance.GetSeed(seedName);
            if (seed == null) return false;
            return CurrencyManager.Instance.CanAfford(CurrencyType.Dewdrops, seed.buyPrice);
        }

        public bool BuySeed(string seedName)
        {
            var seed = SeedRegistry.Instance.GetSeed(seedName);
            if (seed == null) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, seed.buyPrice))
                return false;

            SeedRegistry.Instance.AddSeed(seedName);
            OnSeedPurchased?.Invoke(seedName);
            return true;
        }
    }
}
