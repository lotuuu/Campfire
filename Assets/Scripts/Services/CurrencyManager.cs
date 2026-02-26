using System;
using UnityEngine;

namespace Garden
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        [SerializeField] private CurrencyConfig config;

        public CurrencyConfig Config => config;
        public int Gold => SaveManager.Instance.Data.gold;
        public int SunShards => SaveManager.Instance.Data.sunShards;
        public int Pollen => SaveManager.Instance.Data.pollen;

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
                case CurrencyType.Gold:
                    old = data.gold; data.gold = (int)System.Math.Clamp((long)data.gold + amount, 0L, (long)int.MaxValue);
                    OnCurrencyChanged?.Invoke(type, old, data.gold); break;
                case CurrencyType.SunShards:
                    old = data.sunShards; data.sunShards = (int)System.Math.Clamp((long)data.sunShards + amount, 0L, (long)int.MaxValue);
                    OnCurrencyChanged?.Invoke(type, old, data.sunShards); break;
                case CurrencyType.Pollen:
                    old = data.pollen; data.pollen = (int)System.Math.Clamp((long)data.pollen + amount, 0L, (long)int.MaxValue);
                    OnCurrencyChanged?.Invoke(type, old, data.pollen); break;
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
                CurrencyType.Gold => Gold >= amount,
                CurrencyType.SunShards => SunShards >= amount,
                CurrencyType.Pollen => Pollen >= amount,
                _ => false
            };
        }
    }
}