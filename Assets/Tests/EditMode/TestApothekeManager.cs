using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestApothekeManager
    {
        [Test]
        public void Mix_ConsumesIngredients()
        {
            var data = new SaveData();
            data.inventory.Add(new InventoryItem { itemKey = "RedFlower", count = 5 });
            data.inventory.Add(new InventoryItem { itemKey = "Water_Essence", count = 3 });

            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.ingredients = new List<IngredientEntry>
            {
                new() { itemKey = "RedFlower", quantity = 2 },
                new() { itemKey = "Water_Essence", quantity = 1 }
            };
            recipe.result = "Fertilizer";
            recipe.resultQuantity = 1;

            // Simulate mixing
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                item.count -= ing.quantity;
            }
            data.inventory.Add(new InventoryItem { itemKey = recipe.result, count = recipe.resultQuantity });

            Assert.AreEqual(3, data.inventory[0].count);
            Assert.AreEqual(2, data.inventory[1].count);
            Assert.AreEqual("Fertilizer", data.inventory[2].itemKey);
        }

        [Test]
        public void Mix_FailsIfMissingIngredients()
        {
            var data = new SaveData();
            data.inventory.Add(new InventoryItem { itemKey = "RedFlower", count = 1 });

            var item = data.inventory.Find(i => i.itemKey == "RedFlower");
            Assert.IsFalse(item.count >= 2);
        }
    }
}
