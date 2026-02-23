using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestHarvestEngine
    {
        private SeedData CreateTestSeed(WeatherCondition preferred = WeatherCondition.Clear)
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.seedName = "TestSeed";
            seed.baseSellPrice = 100;
            seed.preferredWeather = preferred;
            seed.specialConditions = new();
            return seed;
        }

        private VariantData CreateTestVariant()
        {
            var variant = ScriptableObject.CreateInstance<VariantData>();
            variant.variantName = "TestVariant";
            variant.rarity = Rarity.Common;
            return variant;
        }

        [Test]
        public void Roll_ReturnsValidQualityTier()
        {
            var seed = CreateTestSeed();
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Cloudy };

            var result = HarvestEngine.Roll(seed, variant, weather);

            Assert.IsTrue(
                result.tier == QualityTier.D || result.tier == QualityTier.C ||
                result.tier == QualityTier.B || result.tier == QualityTier.A ||
                result.tier == QualityTier.S, $"Unexpected tier: {result.tier}");
            Assert.AreEqual(seed, result.seed);
            Assert.AreEqual(variant, result.variant);
        }

        [Test]
        public void Roll_NoSyncShield_DewdropValueUsesBaseSellPrice()
        {
            var seed = CreateTestSeed(WeatherCondition.Rain);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Clear };

            var result = HarvestEngine.Roll(seed, variant, weather);

            float expectedMultiplier = CurrencyConfig.GetQualityMultiplier(result.tier);
            int expected = Mathf.RoundToInt(100 * expectedMultiplier);
            Assert.AreEqual(expected, result.dewdropValue);
            Assert.IsFalse(result.syncShieldActive);
        }

        [Test]
        public void Roll_SyncShieldActive_WhenWeatherMatchesPreferred()
        {
            var seed = CreateTestSeed(WeatherCondition.Storm);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Storm };

            var result = HarvestEngine.Roll(seed, variant, weather);

            Assert.IsTrue(result.syncShieldActive);
            Assert.AreNotEqual(QualityTier.D, result.tier);
        }

        [Test]
        public void Roll_SyncShieldActive_NeverReturnsD()
        {
            var seed = CreateTestSeed(WeatherCondition.Rain);
            var variant = CreateTestVariant();
            var weather = new WeatherData { condition = WeatherCondition.Rain };

            for (int i = 0; i < 200; i++)
            {
                var result = HarvestEngine.Roll(seed, variant, weather);
                Assert.AreNotEqual(QualityTier.D, result.tier,
                    $"D tier appeared on roll {i} with sync shield active");
            }
        }

        [Test]
        public void GetBaseProbabilities_ReturnsCorrectValues()
        {
            var probs = HarvestEngine.GetBaseProbabilities();

            Assert.AreEqual(0.15f, probs[QualityTier.D], 0.001f);
            Assert.AreEqual(0.55f, probs[QualityTier.C], 0.001f);
            Assert.AreEqual(0.20f, probs[QualityTier.B], 0.001f);
            Assert.AreEqual(0.08f, probs[QualityTier.A], 0.001f);
            Assert.AreEqual(0.02f, probs[QualityTier.S], 0.001f);
        }

        [Test]
        public void GetSyncShieldProbabilities_ReturnsCorrectValues()
        {
            var probs = HarvestEngine.GetSyncShieldProbabilities();

            Assert.AreEqual(0f, probs[QualityTier.D], 0.001f);
            Assert.AreEqual(0.50f, probs[QualityTier.C], 0.001f);
            Assert.AreEqual(0.30f, probs[QualityTier.B], 0.001f);
            Assert.AreEqual(0.15f, probs[QualityTier.A], 0.001f);
            Assert.AreEqual(0.05f, probs[QualityTier.S], 0.001f);
        }

        [Test]
        public void Roll_SpecialCondition_ModifiesProbabilities()
        {
            var seed = CreateTestSeed(WeatherCondition.Clear);
            seed.specialConditions.Add(new SeedSpecialCondition
            {
                targetTier = QualityTier.S,
                bonusPercent = 0.10f,
                condition = new TriggerCondition
                {
                    useTemperature = true,
                    minTemp = 25f,
                    maxTemp = 60f
                }
            });
            var variant = CreateTestVariant();
            var weather = new WeatherData { temperature = 30f, condition = WeatherCondition.Cloudy };

            int sCount = 0;
            int totalRolls = 1000;
            for (int i = 0; i < totalRolls; i++)
            {
                var result = HarvestEngine.Roll(seed, variant, weather);
                if (result.tier == QualityTier.S) sCount++;
            }

            Assert.Greater(sCount, 50, "S-tier should appear more often with +10% bonus");
        }
    }
}
