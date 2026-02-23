using System;
using UnityEngine;

namespace Garden
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        [SerializeField] private CurrencyConfig config;

        public CurrencyConfig Config => config;
        public int Dewdrops => SaveManager.Instance.Data.dewdrops;
        public int SunShards => SaveManager.Instance.Data.sunShards;
        public int AuraDust => SaveManager.Instance.Data.auraDust;

        public event Action<CurrencyType, int, int> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Add(CurrencyType type, int amount)
        {
            var data = SaveManager.Instance.Data;
            int old;
            switch (type)
            {
                case CurrencyType.Dewdrops:
                    old = data.dewdrops; data.dewdrops += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.dewdrops); break;
                case CurrencyType.SunShards:
                    old = data.sunShards; data.sunShards += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.sunShards); break;
                case CurrencyType.AuraDust:
                    old = data.auraDust; data.auraDust += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.auraDust); break;
            }
            SaveManager.Instance.Save();
        }

        public bool Spend(CurrencyType type, int amount)
        {
            if (!CanAfford(type, amount)) return false;
            Add(type, -amount);
            return true;
        }

        public bool CanAfford(CurrencyType type, int amount)
        {
            return type switch
            {
                CurrencyType.Dewdrops => Dewdrops >= amount,
                CurrencyType.SunShards => SunShards >= amount,
                CurrencyType.AuraDust => AuraDust >= amount,
                _ => false
            };
        }
    }
}