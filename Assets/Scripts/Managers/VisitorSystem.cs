using System;
using UnityEngine;

namespace Garden
{
    public class VisitorSystem : MonoBehaviour
    {
        public static VisitorSystem Instance { get; private set; }

        public event Action<VisitorGift> OnVisitorArrived;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckForVisitor();
        }

        private void CheckForVisitor()
        {
            if (WeatherService.Instance == null) return;
            if (!WeatherService.Instance.CurrentWeather.isNight) return;

            var data = SaveManager.Instance.Data;
            var today = GameTime.UtcNow.Date.ToString("o");

            if (data.lastVisitorDateUtc == today) return;

            data.lastVisitorDateUtc = today;

            var gift = DetermineGift(data);
            ApplyGift(data, gift);

            SaveManager.Instance.Save();
            OnVisitorArrived?.Invoke(gift);
        }

        private VisitorGift DetermineGift(SaveData data)
        {
            int totalWater = 0;
            foreach (var v in data.vases) totalWater += v.currentWater;
            if (totalWater <= 2)
            {
                return new VisitorGift { type = VisitorGiftType.Water, amount = 3 };
            }
            return new VisitorGift { type = VisitorGiftType.Seed, seedName = "Chamomile", amount = 1 };
        }

        private void ApplyGift(SaveData data, VisitorGift gift)
        {
            switch (gift.type)
            {
                case VisitorGiftType.Water:
                    foreach (var vase in data.vases)
                    {
                        int space = vase.capacity - vase.currentWater;
                        if (space > 0)
                        {
                            int fill = Math.Min(space, gift.amount);
                            vase.currentWater += fill;
                            gift.amount -= fill;
                            if (gift.amount <= 0) break;
                        }
                    }
                    break;
                case VisitorGiftType.Seed:
                    ApothekeManager.Instance?.AddSeed(gift.seedName, gift.amount);
                    break;
            }
        }
    }

    public enum VisitorGiftType { Seed, Water }

    [Serializable]
    public class VisitorGift
    {
        public VisitorGiftType type;
        public string seedName;
        public int amount;
    }
}
