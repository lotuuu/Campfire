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
            seed.waterRequired = 2;
            seed.baseYield = 3;

            Assert.AreEqual("TestSeed", seed.seedName);
            Assert.AreEqual(4f, seed.growthDurationHours);
            Assert.AreEqual(2, seed.waterRequired);
            Assert.AreEqual(3, seed.baseYield);
        }

        [Test]
        public void SeedData_WeatherMatch_UsesTrigerCondition()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.preferredWeather = new TriggerCondition
            {
                useWeatherCondition = true,
                requiredConditions = new[] { WeatherCondition.Rain }
            };

            var rainyWeather = new WeatherData { condition = WeatherCondition.Rain };
            var clearWeather = new WeatherData { condition = WeatherCondition.Clear };

            Assert.IsTrue(seed.preferredWeather.Evaluate(rainyWeather));
            Assert.IsFalse(seed.preferredWeather.Evaluate(clearWeather));
        }
    }
}
