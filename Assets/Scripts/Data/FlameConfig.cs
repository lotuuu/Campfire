using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class FlameIngredient
    {
        public string itemName;
        public int count;
    }

    [Serializable]
    public class FlameUpgradeRecipe
    {
        public List<FlameIngredient> ingredients = new();
    }

    [CreateAssetMenu(fileName = "FlameConfig", menuName = "CampFire/Flame Config")]
    public class FlameConfig : ScriptableObject
    {
        [Header("Mana Generation")]
        [SerializeField] private float baseManaPerSecond = 0.5f;
        [SerializeField] private float manaPerLevel = 0.3f;

        [Header("Mana Cap")]
        [SerializeField] private int[] manaCapPerLevel = { 300, 500, 750, 1000, 1500, 2000, 3000, 4000, 5000, 7000, 9000, 12000 };

        [Header("Entity Capacity (apotheke + plots + vases + gardens + houses)")]
        [SerializeField] private int[] maxEntitiesPerLevel = { 6, 8, 12, 15, 18, 22, 26, 30, 35, 40 };

        [Header("Upgrade Recipes")]
        [SerializeField] private List<FlameUpgradeRecipe> upgradeRecipes = new();

        [Header("Grid")]
        [SerializeField] private int[] gridSizePerLevel = { 2, 2, 3, 3, 3, 4, 4, 4, 5, 5 };

        public float GetManaPerSecond(int flameLevel)
        {
            return baseManaPerSecond + (flameLevel - 1) * manaPerLevel;
        }

        public float GetManaCap(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, manaCapPerLevel.Length - 1);
            return manaCapPerLevel[index];
        }

        public int GetMaxEntities(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, maxEntitiesPerLevel.Length - 1);
            return maxEntitiesPerLevel[index];
        }

        public int MaxLevel => upgradeRecipes.Count + 1;

        public FlameUpgradeRecipe GetUpgradeRecipe(int currentLevel)
        {
            int index = currentLevel - 1;
            if (index < 0 || index >= upgradeRecipes.Count) return null;
            return upgradeRecipes[index];
        }

        public int GetGridSize(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, gridSizePerLevel.Length - 1);
            return gridSizePerLevel[index];
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
