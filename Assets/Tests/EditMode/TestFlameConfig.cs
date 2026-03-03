using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestFlameConfig
    {
        private FlameConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<FlameConfig>();
        }

        [Test]
        public void GetMaxEntities_ReturnsCorrectForLevel()
        {
            Assert.AreEqual(3, config.GetMaxEntities(1));
            Assert.AreEqual(5, config.GetMaxEntities(2));
            Assert.AreEqual(12, config.GetMaxEntities(4));
        }

        [Test]
        public void GetMaxEntities_ClampsToLastEntry()
        {
            Assert.AreEqual(18, config.GetMaxEntities(99));
        }

        [Test]
        public void GetUpgradeCost_ReturnsCorrectForLevel()
        {
            Assert.Greater(config.GetUpgradeCost(1), 0f);
        }

        [Test]
        public void GetManaRate_ScalesWithLevel()
        {
            float rate1 = config.GetManaPerSecond(1);
            float rate2 = config.GetManaPerSecond(2);
            Assert.Greater(rate2, rate1);
        }

        [Test]
        public void FlameUpgradeRecipe_IngredientsDefaultEmpty()
        {
            var recipe = new FlameUpgradeRecipe();
            Assert.IsNotNull(recipe.ingredients);
            Assert.AreEqual(0, recipe.ingredients.Count);
        }

        [Test]
        public void CanAffordUpgrade_ReturnsTrueWhenItemsSufficient()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemName = "Basil_harvest", count = 3 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil_harvest", count = 5 }
            };
            Assert.IsTrue(FlameConfig.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void CanAffordUpgrade_ReturnsFalseWhenItemsInsufficient()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemName = "Basil_harvest", count = 3 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil_harvest", count = 2 }
            };
            Assert.IsFalse(FlameConfig.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void CanAffordUpgrade_ReturnsFalseWhenItemMissing()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemName = "Basil_harvest", count = 3 }
                }
            };
            var items = new List<InventoryItem>();
            Assert.IsFalse(FlameConfig.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void ConsumeIngredients_SubtractsFromInventory()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemName = "Basil_harvest", count = 3 },
                    new() { itemName = "Chamomile_harvest", count = 2 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemName = "Basil_harvest", count = 5 },
                new() { itemName = "Chamomile_harvest", count = 4 }
            };
            FlameConfig.ConsumeIngredients(recipe, items);
            Assert.AreEqual(2, items.Find(i => i.itemName == "Basil_harvest").count);
            Assert.AreEqual(2, items.Find(i => i.itemName == "Chamomile_harvest").count);
        }
    }
}
