using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public enum RecipeCategory
    {
        Pigment = 0,
        Consumable = 1,
        Material = 2
    }

    [CreateAssetMenu(fileName = "NewRecipe", menuName = "CampFire/Recipe Data")]
    public class RecipeData : ScriptableObject
    {
        public string recipeName;
        public List<IngredientEntry> ingredients = new();
        public string result;
        public int resultQuantity = 1;

        [Header("Category")]
        public RecipeCategory category;

        public static string FormatItemName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            // Try server-authoritative display name first
            if (ConfigService.Instance != null)
            {
                var displayName = ConfigService.Instance.GetItemDisplayName(key);
                if (displayName != key) return displayName;
            }
            // Fallback: format the raw key
            string name = key;
            if (name.EndsWith("_Seed")) name = name[..^5];
            return name.Replace('_', ' ');
        }
    }

    [Serializable]
    public class IngredientEntry
    {
        public string itemKey;
        public int quantity;
    }
}
