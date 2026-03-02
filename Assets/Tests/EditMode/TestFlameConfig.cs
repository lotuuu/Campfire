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
    }
}
