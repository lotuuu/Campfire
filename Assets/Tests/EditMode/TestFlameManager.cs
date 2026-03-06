using NUnit.Framework;

namespace Garden.Tests
{
    public class TestFlameManager
    {
        [Test]
        public void AccumulateMana_AddsCorrectAmount()
        {
            float result = FlameManager.AccumulateMana(100f, 2f, 0.5f);
            Assert.AreEqual(101f, result, 0.001f);
        }

        [Test]
        public void AccumulateMana_ZeroDelta_NoChange()
        {
            float result = FlameManager.AccumulateMana(50f, 5f, 0f);
            Assert.AreEqual(50f, result, 0.001f);
        }

        [Test]
        public void AccumulateMana_ClampsToManaCap()
        {
            float result = FlameManager.AccumulateMana(290f, 100f, 1f, 300f);
            Assert.AreEqual(300f, result, 0.001f);
        }

        [Test]
        public void AccumulateMana_AlreadyAtCap_StaysAtCap()
        {
            float result = FlameManager.AccumulateMana(300f, 100f, 1f, 300f);
            Assert.AreEqual(300f, result, 0.001f);
        }

        [Test]
        public void GetManaCap_ReturnsCorrectPerLevel()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<FlameConfig>();
            // Uses default values from code: 300, 500, 750, ...
            Assert.AreEqual(300f, config.GetManaCap(1));
        }
    }
}
