using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public enum RecipeCategory
    {
        Pigment = 0,
        Potion = 1,
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

        [Header("Visuals")]
        public Sprite icon;

        public static string FormatItemName(string internalName)
        {
            if (string.IsNullOrEmpty(internalName)) return "";
            string name = internalName;
            if (name.EndsWith("_harvest")) name = name[..^8];
            return name.Replace('_', ' ');
        }
    }

    [Serializable]
    public class IngredientEntry
    {
        public string itemName;
        public int quantity;
    }
}
