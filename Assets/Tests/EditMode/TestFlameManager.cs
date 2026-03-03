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
    }
}
