using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class FlameManager : MonoBehaviour
    {
        public static FlameManager Instance { get; private set; }

        private ServerFlameConfig Config => ConfigService.Instance?.FlameConfig;
        public bool ConfigReady => Config != null;
        public int Level => SaveManager.Instance.Data.flameLevel;
        public float ManaPerSecond => Config?.GetManaPerSecond(Level) ?? 0f;
        public int MaxEntities => Config?.GetMaxEntities(Level) ?? 0;
        public float ManaCap => Config?.GetManaCap(Level) ?? 0f;

        public int CurrentEntityCount
        {
            get
            {
                var data = SaveManager.Instance.Data;
                return data.plots.Count + data.vases.Count + data.gardens.Count + data.mallumHouses.Count;
            }
        }

        public bool CanPlaceEntity => CurrencyManager.FreeMode || CurrentEntityCount < MaxEntities;

        public event Action OnFlameUpgraded;

        private float _manaCollectTimer;
        private const float ManaCollectIntervalSeconds = 60f;

        public FlameUpgradeRecipe GetUpgradeRecipe() => Config?.GetUpgradeRecipe(Level);
        public int GetGridSize() => Config?.GetGridSize(Level) ?? 2;
        public int GetGridSize(int flameLevel) => Config?.GetGridSize(flameLevel) ?? 2;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Config == null) return;

            SaveManager.Instance.Data.mana = AccumulateMana(
                SaveManager.Instance.Data.mana, ManaPerSecond, Time.deltaTime, ManaCap);

            _manaCollectTimer += Time.deltaTime;
            if (_manaCollectTimer >= ManaCollectIntervalSeconds)
            {
                _manaCollectTimer = 0f;
                _ = EconomyService.Instance?.CollectMana();
            }
        }

        public static float AccumulateMana(float currentMana, float manaPerSecond, float deltaTime, float manaCap = float.MaxValue)
        {
            return Mathf.Min(currentMana + manaPerSecond * deltaTime, manaCap);
        }

        public bool CanUpgrade()
        {
            var recipe = Config?.GetUpgradeRecipe(Level);
            if (recipe == null) return false;
            return CanAffordUpgrade(recipe, SaveManager.Instance.Data.items);
        }

        public bool UpgradeFlame()
        {
            var recipe = Config?.GetUpgradeRecipe(Level);
            if (recipe == null) return false;
            if (!CanAffordUpgrade(recipe, SaveManager.Instance.Data.items)) return false;
            ConsumeIngredients(recipe, SaveManager.Instance.Data.items);
            SaveManager.Instance.Data.flameLevel++;
            SaveManager.Instance.Save();
            if (EconomyService.Instance != null)
            {
                var items = new List<SpendItemEntry>();
                foreach (var ing in recipe.ingredients)
                    items.Add(new SpendItemEntry { item_name = ing.itemName, count = ing.count });
                var req = new UpgradeFlameRequest { items = items, freeMode = CurrencyManager.FreeMode };
                EconomyService.Instance.Enqueue("upgrade-flame", JsonUtility.ToJson(req));
            }
            OnFlameUpgraded?.Invoke();
            AudioManager.Instance?.PlaySFX("flame_upgrade");
            return true;
        }

        public static bool CanAffordUpgrade(FlameUpgradeRecipe recipe, List<InventoryItem> items)
        {
            if (CurrencyManager.FreeMode) return true;
            foreach (var ingredient in recipe.ingredients)
            {
                var item = items.Find(i => i.itemName == ingredient.itemName);
                if (item == null || item.count < ingredient.count)
                    return false;
            }
            return true;
        }

        public static void ConsumeIngredients(FlameUpgradeRecipe recipe, List<InventoryItem> items)
        {
            if (CurrencyManager.FreeMode) return;
            foreach (var ingredient in recipe.ingredients)
            {
                var item = items.Find(i => i.itemName == ingredient.itemName);
                item.count -= ingredient.count;
            }
        }
    }
}
