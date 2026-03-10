using NUnit.Framework;

namespace Garden.Tests
{
    public class TestSeedData
    {
        [Test]
        public void ServerSeedConfig_HasExpectedFields()
        {
            var seed = new ServerSeedConfig
            {
                seedName = "TestSeed",
                growthDurationHours = 4f,
                minDrops = 2,
                maxDrops = 6,
                tier = 1,
                manaCost = 10f
            };

            Assert.AreEqual("TestSeed", seed.seedName);
            Assert.AreEqual(4f, seed.growthDurationHours);
            Assert.AreEqual(2, seed.minDrops);
            Assert.AreEqual(6, seed.maxDrops);
        }

        [Test]
        public void ServerSeedConfig_RecipeField_IsAssignable()
        {
            var seed = new ServerSeedConfig
            {
                recipe = new GrowthRecipe
                {
                    useHeat = true,
                    idealTempMin = 20f,
                    idealTempMax = 30f
                }
            };

            Assert.IsNotNull(seed.recipe);
            Assert.IsTrue(seed.recipe.useHeat);
        }
    }
}
