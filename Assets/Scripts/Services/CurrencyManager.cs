using System;
using UnityEngine;

namespace Garden
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        public float Mana => SaveManager.Instance.Data.mana;
        public int Gems => SaveManager.Instance.Data.gems;

        public int TotalWater
        {
            get
            {
                int total = 0;
                foreach (var v in SaveManager.Instance.Data.vases)
                    total += v.currentWater;
                return total;
            }
        }

        public event Action<CurrencyType, float, float> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddMana(float amount)
        {
            var data = SaveManager.Instance.Data;
            float old = data.mana;
            data.mana = Mathf.Max(0f, data.mana + amount);
            OnCurrencyChanged?.Invoke(CurrencyType.Mana, old, data.mana);
            SaveManager.Instance.Save();
        }

        public bool SpendMana(float amount)
        {
            if (!CanAffordMana(amount)) return false;
            AddMana(-amount);
            return true;
        }

        public bool CanAffordMana(float amount)
        {
            return SaveManager.Instance.Data.mana >= amount;
        }

        public bool SpendWater(int amount)
        {
            if (TotalWater < amount) return false;
            var data = SaveManager.Instance.Data;
            float oldTotal = TotalWater;
            int remaining = amount;
            for (int i = 0; i < data.vases.Count && remaining > 0; i++)
            {
                int take = Math.Min(data.vases[i].currentWater, remaining);
                data.vases[i].currentWater -= take;
                remaining -= take;
            }
            OnCurrencyChanged?.Invoke(CurrencyType.Water, oldTotal, TotalWater);
            SaveManager.Instance.Save();
            return true;
        }

        public bool CanAffordWater(int amount)
        {
            return TotalWater >= amount;
        }

        public void AddGems(int amount)
        {
            var data = SaveManager.Instance.Data;
            float old = data.gems;
            data.gems = Mathf.Max(0, data.gems + amount);
            OnCurrencyChanged?.Invoke(CurrencyType.Gems, old, data.gems);
            SaveManager.Instance.Save();
        }

        public bool SpendGems(int amount)
        {
            if (amount <= 0 || SaveManager.Instance.Data.gems < amount) return false;
            AddGems(-amount);
            return true;
        }

        public bool CanAffordGems(int amount) => SaveManager.Instance.Data.gems >= amount;

        public void GrantInfiniteGems()
        {
            var data = SaveManager.Instance.Data;
            float old = data.gems;
            data.gems = 999999;
            OnCurrencyChanged?.Invoke(CurrencyType.Gems, old, data.gems);
            SaveManager.Instance.Save();
        }
    }
}
