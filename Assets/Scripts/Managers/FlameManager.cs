using System;
using System.Collections.Generic;
using System.Reflection;
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
        public float ManaCap => config.GetManaCap(Level);

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

        public FlameUpgradeRecipe GetUpgradeRecipe() => config.GetUpgradeRecipe(Level);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ApplyServerFlameConfig();
        }

        private void ApplyServerFlameConfig()
        {
            var cs = ConfigService.Instance;
            if (cs == null || !cs.IsLoaded || cs.FlameConfig == null) return;

            var sf = cs.FlameConfig;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(FlameConfig);

            type.GetField("baseManaPerSecond", flags)?.SetValue(config, sf.base_mana_per_second);
            type.GetField("manaPerLevel", flags)?.SetValue(config, sf.mana_per_level);

            if (sf.entity_caps != null && sf.entity_caps.Count > 0)
                type.GetField("maxEntitiesPerLevel", flags)?.SetValue(config, sf.entity_caps.ToArray());

            if (sf.grid_sizes != null && sf.grid_sizes.Count > 0)
                type.GetField("gridSizePerLevel", flags)?.SetValue(config, sf.grid_sizes.ToArray());

            // Overlay upgrade recipes
            var serverRecipes = cs.FlameUpgradeRecipes;
            if (serverRecipes != null && serverRecipes.Count > 0)
            {
                var recipes = new List<FlameUpgradeRecipe>();
                foreach (var sr in serverRecipes)
                {
                    var recipe = new FlameUpgradeRecipe();
                    foreach (var ing in sr)
                    {
                        string itemName = ing.TryGetValue("itemName", out var n) && n is string s ? s : null;
                        int count = ing.TryGetValue("count", out var c) ? ToInt(c) : 0;
                        if (itemName != null)
                            recipe.ingredients.Add(new FlameIngredient { itemName = itemName, count = count });
                    }
                    recipes.Add(recipe);
                }
                type.GetField("upgradeRecipes", flags)?.SetValue(config, recipes);
            }
        }

        private static int ToInt(object val)
        {
            if (val is double d) return (int)d;
            if (val is long l) return (int)l;
            if (val is int i) return i;
            return 0;
        }

        private void Update()
        {
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
                var req = new UpgradeFlameRequest { items = items, freeMode = CurrencyManager.FreeMode };
                EconomyService.Instance.Enqueue("upgrade-flame", JsonUtility.ToJson(req));
            }
            OnFlameUpgraded?.Invoke();
            return true;
        }
    }
}
