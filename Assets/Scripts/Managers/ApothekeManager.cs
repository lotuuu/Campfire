using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class ApothekeManager : MonoBehaviour
    {
        public static ApothekeManager Instance { get; private set; }

        private RecipeData[] allRecipes;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allRecipes = Resources.LoadAll<RecipeData>("Recipes");
        }

        public RecipeData[] AllRecipes => allRecipes;

        public List<InventoryItem> Seeds =>
            SaveManager.Instance.Data.inventory.FindAll(i =>
                ConfigService.Instance?.GetItem(i.itemKey)?.category == "seed");

        public List<InventoryItem> Items => SaveManager.Instance.Data.inventory;

        public bool CanMix(RecipeData recipe)
        {
            if (CurrencyManager.FreeMode) return true;
            var data = SaveManager.Instance.Data;
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                if (item == null || item.count < ing.quantity) return false;
            }
            return true;
        }

        public bool Mix(RecipeData recipe)
        {
            if (!CanMix(recipe)) return false;
            var data = SaveManager.Instance.Data;

            if (!CurrencyManager.FreeMode)
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                if (item == null) continue;
                item.count -= ing.quantity;
                if (item.count <= 0) data.inventory.Remove(item);
            }

            var existing = data.inventory.Find(i => i.itemKey == recipe.result);
            if (existing != null)
                existing.count += recipe.resultQuantity;
            else
                data.inventory.Add(new InventoryItem { itemKey = recipe.result, count = recipe.resultQuantity });

            SaveManager.Instance.Save();
            AudioManager.Instance?.PlaySFX("apotheke_mix");
            if (EconomyService.Instance != null && !CurrencyManager.FreeMode)
            {
                // Report consumed ingredients
                foreach (var ing in recipe.ingredients)
                {
                    var spendItems = new SpendItemsRequest
                    {
                        items = new List<SpendItemEntry> { new SpendItemEntry { item_key = ing.itemKey, count = ing.quantity } },
                        freeMode = CurrencyManager.FreeMode
                    };
                    EconomyService.Instance.Enqueue("spend-items", JsonUtility.ToJson(spendItems));
                }
                // Report produced result
                EconomyService.Instance.Enqueue("add-items",
                    JsonUtility.ToJson(new AddItemRequest { item_key = recipe.result, count = recipe.resultQuantity }));
            }
            return true;
        }

        public async Task<bool> CraftOnServer(RecipeData recipe)
        {
            if (!CanMix(recipe)) return false;

            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                var result = await GameService.Instance.CraftApotheke(recipe.recipeName);
                if (result == null) return false;

                // Server succeeded — sync inventory from economy service
                if (EconomyService.Instance != null)
                    EconomyService.Instance.Initialize();

                return true;
            }

            // Offline fallback: use existing local Mix
            return Mix(recipe);
        }

        public void AddItem(string itemKey, int count = 1)
        {
            var data = SaveManager.Instance.Data;
            var entry = data.inventory.Find(i => i.itemKey == itemKey);
            if (entry != null)
                entry.count += count;
            else
                data.inventory.Add(new InventoryItem { itemKey = itemKey, count = count });

            // Discover seeds automatically
            if (ConfigService.Instance?.GetItem(itemKey)?.category == "seed")
                DiscoverSeed(data, itemKey);

            SaveManager.Instance.Save();
            EconomyService.Instance?.Enqueue("add-items",
                JsonUtility.ToJson(new AddItemRequest { item_key = itemKey, count = count }));
        }

        public static void DiscoverSeed(SaveData data, string itemKey)
        {
            if (!data.discoveredSeeds.Contains(itemKey))
                data.discoveredSeeds.Add(itemKey);
        }

        public static bool IsSeedDiscovered(string itemKey)
        {
            return SaveManager.Instance?.Data?.discoveredSeeds?.Contains(itemKey) ?? false;
        }
    }
}
