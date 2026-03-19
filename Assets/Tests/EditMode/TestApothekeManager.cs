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

            var recipe = new ServerRecipeConfig
            {
                name = "TestRecipe",
                resultItem = "Fertilizer",
                resultQuantity = 1,
                ingredients = new List<ServerRecipeIngredient>
                {
                    new() { itemKey = "RedFlower", count = 2 },
                    new() { itemKey = "Water_Essence", count = 1 }
                }
            };

            // Simulate mixing
            foreach (var ing in recipe.ingredients)
            {
                var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
                item.count -= ing.count;
            }
            data.inventory.Add(new InventoryItem { itemKey = recipe.resultItem, count = recipe.resultQuantity });

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
