using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestFlameConfig
    {
        private ServerFlameConfig config;

        [SetUp]
        public void SetUp()
        {
            config = new ServerFlameConfig
            {
                mana_rates = new List<float> { 0.5f, 0.8f, 1.1f, 1.4f, 1.7f, 2.0f, 2.3f, 2.6f, 2.9f, 3.2f },
                mana_caps = new List<int> { 300, 500, 750, 1000, 1500, 2000, 3000, 4000, 5000, 7000, 9000, 12000 },
                entity_caps = new List<int> { 6, 8, 12, 15, 18, 22, 26, 30, 35, 40 },
                grid_sizes = new List<int> { 2, 2, 3, 3, 3, 4, 4, 4, 5, 5 },
                upgradeRecipes = new List<FlameUpgradeRecipe>()
            };
        }

        [Test]
        public void GetMaxEntities_ReturnsCorrectForLevel()
        {
            Assert.AreEqual(6, config.GetMaxEntities(1));
            Assert.AreEqual(8, config.GetMaxEntities(2));
            Assert.AreEqual(15, config.GetMaxEntities(4));
        }

        [Test]
        public void GetMaxEntities_ClampsToLastEntry()
        {
            Assert.AreEqual(40, config.GetMaxEntities(99));
        }

        [Test]
        public void GetUpgradeRecipe_ReturnsNullAtMaxLevel()
        {
            // Default config has empty upgradeRecipes list, so MaxLevel = 1
            Assert.IsNull(config.GetUpgradeRecipe(1));
        }

        [Test]
        public void MaxLevel_EqualsRecipeCountPlusOne()
        {
            Assert.AreEqual(1, config.MaxLevel);
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
                    new() { itemKey = "Basil", count = 3 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemKey = "Basil", count = 5 }
            };
            Assert.IsTrue(FlameManager.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void CanAffordUpgrade_ReturnsFalseWhenItemsInsufficient()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemKey = "Basil", count = 3 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemKey = "Basil", count = 2 }
            };
            Assert.IsFalse(FlameManager.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void CanAffordUpgrade_ReturnsFalseWhenItemMissing()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemKey = "Basil", count = 3 }
                }
            };
            var items = new List<InventoryItem>();
            Assert.IsFalse(FlameManager.CanAffordUpgrade(recipe, items));
        }

        [Test]
        public void ConsumeIngredients_SubtractsFromInventory()
        {
            var recipe = new FlameUpgradeRecipe
            {
                ingredients = new List<FlameIngredient>
                {
                    new() { itemKey = "Basil", count = 3 },
                    new() { itemKey = "Chamomile", count = 2 }
                }
            };
            var items = new List<InventoryItem>
            {
                new() { itemKey = "Basil", count = 5 },
                new() { itemKey = "Chamomile", count = 4 }
            };
            FlameManager.ConsumeIngredients(recipe, items);
            Assert.AreEqual(2, items.Find(i => i.itemKey == "Basil").count);
            Assert.AreEqual(2, items.Find(i => i.itemKey == "Chamomile").count);
        }
    }
}
