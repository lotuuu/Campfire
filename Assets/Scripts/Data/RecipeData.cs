using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "CampFire/Recipe Data")]
    public class RecipeData : ScriptableObject
    {
        public string recipeName;
        public List<IngredientEntry> ingredients = new();
        public string result;
        public int resultQuantity = 1;

        [Header("Visuals")]
        public Sprite icon;
    }

    [Serializable]
    public class IngredientEntry
    {
        public string itemName;
        public int quantity;
    }
}
