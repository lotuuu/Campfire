using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class FlameManager : MonoBehaviour
    {
        public static FlameManager Instance { get; private set; }

        [SerializeField] private FlameConfig config;

        public FlameConfig Config => config;
        public int Level => SaveManager.Instance.Data.flameLevel;
        public float ManaPerSecond => config.GetManaPerSecond(Level);
        public int MaxEntities => config.GetMaxEntities(Level);

        public int CurrentEntityCount
        {
            get
            {
                var data = SaveManager.Instance.Data;
                return data.plots.Count + data.vases.Count + data.gardens.Count + data.mallumHouses.Count + 1; // +1 for apotheke
            }
        }

        public bool CanPlaceEntity => CurrentEntityCount < MaxEntities;

        public event Action OnFlameUpgraded;

        private float _manaCollectTimer;
        private const float ManaCollectIntervalSeconds = 60f;

        public FlameUpgradeRecipe GetUpgradeRecipe() => config.GetUpgradeRecipe(Level);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            SaveManager.Instance.Data.mana = AccumulateMana(
                SaveManager.Instance.Data.mana, ManaPerSecond, Time.deltaTime);

            _manaCollectTimer += Time.deltaTime;
            if (_manaCollectTimer >= ManaCollectIntervalSeconds)
            {
                _manaCollectTimer = 0f;
                _ = EconomyService.Instance?.CollectMana();
            }
        }

        public static float AccumulateMana(float currentMana, float manaPerSecond, float deltaTime)
        {
            return currentMana + manaPerSecond * deltaTime;
        }

        public bool CanUpgrade()
        {
            var recipe = config.GetUpgradeRecipe(Level);
            if (recipe == null) return false;
            return FlameConfig.CanAffordUpgrade(recipe, SaveManager.Instance.Data.items);
        }

        public bool UpgradeFlame()
        {
            var recipe = config.GetUpgradeRecipe(Level);
            if (recipe == null) return false;
            if (!FlameConfig.CanAffordUpgrade(recipe, SaveManager.Instance.Data.items)) return false;
            FlameConfig.ConsumeIngredients(recipe, SaveManager.Instance.Data.items);
            SaveManager.Instance.Data.flameLevel++;
            SaveManager.Instance.Save();
            if (EconomyService.Instance != null)
            {
                var items = new List<SpendItemEntry>();
                foreach (var ing in recipe.ingredients)
                    items.Add(new SpendItemEntry { item_name = ing.itemName, count = ing.count });
                EconomyService.Instance.Enqueue("upgrade-flame",
                    JsonUtility.ToJson(new UpgradeFlameRequest { items = items }));
            }
            OnFlameUpgraded?.Invoke();
            return true;
        }
    }
}
