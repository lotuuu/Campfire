using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSeedData
    {
        [Test]
        public void SeedData_HasExpectedFields()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.seedName = "TestSeed";
            seed.growthDurationHours = 4f;
            seed.baseDrops = 3;

            Assert.AreEqual("TestSeed", seed.seedName);
            Assert.AreEqual(4f, seed.growthDurationHours);
            Assert.AreEqual(3, seed.baseDrops);
        }

        [Test]
        public void SeedData_RecipeField_IsAssignable()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f,
                idealTempMax = 30f
            };

            Assert.IsNotNull(seed.recipe);
            Assert.IsTrue(seed.recipe.useHeat);
        }
    }
}
