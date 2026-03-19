using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class ApothekeManager : MonoBehaviour
    {
        public static ApothekeManager Instance { get; private set; }

        public ServerRecipeConfig[] AllRecipes { get; private set; } = System.Array.Empty<ServerRecipeConfig>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void LoadRecipesFromConfig()
        {
            var all = ConfigService.Instance?.GetAllRecipes();
            if (all != null)
                AllRecipes = new List<ServerRecipeConfig>(all.Values).ToArray();
            else
                AllRecipes = System.Array.Empty<ServerRecipeConfig>();
        }

        public List<InventoryItem> Seeds =>
            SaveManager.Instance.Data.inventory.FindAll(i =>
                ConfigService.Instance?.GetItem(i.itemKey)?.category == "seed");

        public List<InventoryItem> Items => SaveManager.Instance.Data.inventory;

        public bool CanMix(ServerRecipeConfig recipe)
        {
            if (CurrencyManager.FreeMode) return true;
            var data = SaveManager.Instance.Data;
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                if (item == null || item.count < ing.count) return false;
            }
            return true;
        }

        public bool Mix(ServerRecipeConfig recipe)
        {
            if (!CanMix(recipe)) return false;
            var data = SaveManager.Instance.Data;

            if (!CurrencyManager.FreeMode)
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                if (item == null) continue;
                item.count -= ing.count;
                if (item.count <= 0) data.inventory.Remove(item);
            }

            var existing = data.inventory.Find(i => i.itemKey == recipe.resultItem);
            if (existing != null)
                existing.count += recipe.resultQuantity;
            else
                data.inventory.Add(new InventoryItem { itemKey = recipe.resultItem, count = recipe.resultQuantity });

            SaveManager.Instance.Save();
            AudioManager.Instance?.PlaySFX("apotheke_mix");
            if (EconomyService.Instance != null && !CurrencyManager.FreeMode)
            {
                foreach (var ing in recipe.ingredients)
                {
                    var spendItems = new SpendItemsRequest
                    {
                        items = new List<SpendItemEntry> { new SpendItemEntry { item_key = ing.itemKey, count = ing.count } },
                        freeMode = CurrencyManager.FreeMode
                    };
                    EconomyService.Instance.Enqueue("spend-items", JsonUtility.ToJson(spendItems));
                }
                EconomyService.Instance.Enqueue("add-items",
                    JsonUtility.ToJson(new AddItemRequest { item_key = recipe.resultItem, count = recipe.resultQuantity }));
            }
            return true;
        }

        public async Task<bool> CraftOnServer(ServerRecipeConfig recipe)
        {
            if (!CanMix(recipe)) return false;

            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                var result = await GameService.Instance.CraftApotheke(recipe.name);
                if (result == null) return false;

                if (EconomyService.Instance != null)
                    EconomyService.Instance.Initialize();

                return true;
            }

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
