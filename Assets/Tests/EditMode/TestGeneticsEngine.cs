using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestGeneticsEngine
    {
        [Test]
        public void Resolve_StormWeather_ReturnsHighestPriorityMatch()
        {
            var stormVariant = ScriptableObject.CreateInstance<VariantData>();
            stormVariant.variantName = "Static";
            stormVariant.priority = 2;
            stormVariant.trigger = new TriggerCondition
            {
                useWeatherCondition = true,
                requiredConditions = new[] { WeatherCondition.Storm }
            };

            var baseVariant = ScriptableObject.CreateInstance<VariantData>();
            baseVariant.variantName = "Base";
            baseVariant.priority = 4;
            baseVariant.trigger = new TriggerCondition();

            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.variants = new() { baseVariant, stormVariant };

            var weather = new WeatherData { condition = WeatherCondition.Storm };

            var result = GeneticsEngine.Resolve(seed, weather);
            Assert.AreEqual("Static", result.variant.variantName);
            Assert.AreEqual(1.25f, result.growthSpeedMultiplier);
        }

        [Test]
        public void Resolve_NoMatch_ReturnsFallback()
        {
            var rareVariant = ScriptableObject.CreateInstance<VariantData>();
            rareVariant.variantName = "Glacial";
            rareVariant.priority = 2;
            rareVariant.trigger = new TriggerCondition
            {
                useTemperature = true,
                minTemp = -50f,
                maxTemp = 5f
            };

            var baseVariant = ScriptableObject.CreateInstance<VariantData>();
            baseVariant.variantName = "Base";
            baseVariant.priority = 4;
            baseVariant.trigger = null;

            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.variants = new() { rareVariant, baseVariant };

            var weather = new WeatherData { temperature = 22f };
            var result = GeneticsEngine.Resolve(seed, weather);
            Assert.AreEqual("Base", result.variant.variantName);
            Assert.AreEqual(1.0f, result.growthSpeedMultiplier);
        }
    }
}