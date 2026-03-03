using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class MerchantManager : MonoBehaviour
    {
        public static MerchantManager Instance { get; private set; }

        public event Action OnMerchantArrived;
        public event Action OnMerchantDeparted;

        private List<MerchantData> allMerchants;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allMerchants = new List<MerchantData>(Resources.LoadAll<MerchantData>("Merchants"));
        }

        private void Start()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            int before = data.merchants.Count;
            CleanStaleMerchants(data, GameTime.UtcNow);
            if (data.merchants.Count < before)
            {
                SaveManager.Instance.Save();
                OnMerchantDeparted?.Invoke();
            }
        }

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null || allMerchants.Count == 0) return;

            int gridRadius = FlameManager.Instance != null
                ? FlameManager.Instance.Config.GetGridSize(data.flameLevel)
                : 2;

            var now = GameTime.Now;
            var utcNow = GameTime.UtcNow;

            // Departure: remove merchants if outside 10 PM–midnight window
            if (data.merchants.Count > 0 && !IsNightMerchantHour(now))
            {
                DismissAllMerchants(data);
                SaveManager.Instance.Save();
                OnMerchantDeparted?.Invoke();
                return;
            }

            // Arrival: spawn if in window, no active merchants, and not already spawned today
            if (IsNightMerchantHour(now) && data.merchants.Count == 0)
            {
                string todayUtc = utcNow.Date.ToString("o");
                if (data.lastMerchantDateUtc == todayUtc) return;

                var merchant = allMerchants[UnityEngine.Random.Range(0, allMerchants.Count)];
                bool spawned = TrySpawnMerchant(data, merchant, gridRadius, data.flameLevel, utcNow);
                if (spawned)
                {
                    data.lastMerchantDateUtc = todayUtc;
                    SaveManager.Instance.Save();
                    OnMerchantArrived?.Invoke();
                }
            }
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static bool IsNightMerchantHour(DateTime localTime)
        {
            return localTime.Hour >= 22;
        }

        public static void DismissAllMerchants(SaveData data)
        {
            data.merchants.Clear();
        }

        public static void CleanStaleMerchants(SaveData data, DateTime utcNow)
        {
            string todayUtc = utcNow.Date.ToString("o");
            data.merchants.RemoveAll(m =>
            {
                if (string.IsNullOrEmpty(m.appearedAtUtc)) return true;
                var appeared = DateTime.Parse(m.appearedAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                return appeared.Date.ToString("o") != todayUtc;
            });
        }

        public static bool TrySpawnMerchant(SaveData data, MerchantData merchantData,
            int gridRadius, int flameLevel, DateTime utcNow)
        {
            var freeTiles = BirdManager.GetFreeTiles(data, gridRadius);
            if (freeTiles.Count == 0) return false;

            var offers = RollOffers(merchantData, flameLevel);
            if (offers.Count == 0) return false;

            var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];
            var save = new MerchantSave
            {
                gridX = tile.q,
                gridY = tile.r,
                merchantName = merchantData.merchantName,
                offers = offers,
                appearedAtUtc = utcNow.ToString("o")
            };
            data.merchants.Add(save);
            return true;
        }

        public static List<MerchantOfferSave> RollOffers(MerchantData merchantData, int flameLevel)
        {
            var eligible = new List<MerchantOffer>();
            foreach (var offer in merchantData.offerPool)
            {
                if (offer.requiredFlameLevel <= flameLevel)
                    eligible.Add(offer);
            }

            if (eligible.Count == 0) return new List<MerchantOfferSave>();

            float totalWeight = 0f;
            foreach (var o in eligible) totalWeight += o.weight;

            int count = Mathf.Min(merchantData.offerCount, eligible.Count);
            var picked = new List<MerchantOfferSave>();
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;
                for (int j = 0; j < eligible.Count; j++)
                {
                    if (usedIndices.Contains(j)) continue;
                    cumulative += eligible[j].weight;
                    if (roll < cumulative)
                    {
                        var offer = eligible[j];
                        var save = new MerchantOfferSave
                        {
                            rewardSeedName = offer.rewardSeed.seedName,
                            rewardCount = offer.rewardCount,
                            costs = new List<TradeCost>(offer.costs)
                        };
                        picked.Add(save);
                        usedIndices.Add(j);
                        totalWeight -= eligible[j].weight;
                        break;
                    }
                }
            }

            return picked;
        }

        public static bool CanAffordOffer(MerchantOfferSave offer, List<InventoryItem> items)
        {
            foreach (var cost in offer.costs)
            {
                var item = items.Find(i => i.itemName == cost.itemName);
                if (item == null || item.count < cost.count) return false;
            }
            return true;
        }

        public static void ExecuteTrade(MerchantOfferSave offer, List<InventoryItem> items,
            List<SeedInventoryEntry> seedInventory)
        {
            // Consume items
            foreach (var cost in offer.costs)
            {
                var item = items.Find(i => i.itemName == cost.itemName);
                item.count -= cost.count;
                if (item.count <= 0) items.Remove(item);
            }

            // Add seeds
            var entry = seedInventory.Find(s => s.seedName == offer.rewardSeedName);
            if (entry != null)
                entry.count += offer.rewardCount;
            else
                seedInventory.Add(new SeedInventoryEntry
                    { seedName = offer.rewardSeedName, count = offer.rewardCount });
        }
    }
}
