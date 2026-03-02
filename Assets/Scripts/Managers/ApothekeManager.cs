using System.Collections.Generic;
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
        public List<SeedInventoryEntry> Seeds => SaveManager.Instance.Data.seedInventory;
        public List<InventoryItem> Items => SaveManager.Instance.Data.items;

        public bool CanMix(RecipeData recipe)
        {
            var data = SaveManager.Instance.Data;
            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                if (item == null || item.count < ing.quantity) return false;
            }
            return true;
        }

        public bool Mix(RecipeData recipe)
        {
            if (!CanMix(recipe)) return false;
            var data = SaveManager.Instance.Data;

            foreach (var ing in recipe.ingredients)
            {
                var item = data.items.Find(i => i.itemName == ing.itemName);
                item.count -= ing.quantity;
                if (item.count <= 0) data.items.Remove(item);
            }

            var existing = data.items.Find(i => i.itemName == recipe.result);
            if (existing != null)
                existing.count += recipe.resultQuantity;
            else
                data.items.Add(new InventoryItem { itemName = recipe.result, count = recipe.resultQuantity });

            SaveManager.Instance.Save();
            return true;
        }

        public void AddSeed(string seedName, int count = 1)
        {
            var data = SaveManager.Instance.Data;
            var entry = data.seedInventory.Find(s => s.seedName == seedName);
            if (entry != null)
                entry.count += count;
            else
                data.seedInventory.Add(new SeedInventoryEntry { seedName = seedName, count = count });
            SaveManager.Instance.Save();
        }
    }
}
