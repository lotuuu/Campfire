using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestMoonPhase
    {
        [Test]
        public void KnownNewMoon_ReturnsNewMoon()
        {
            var result = MoonPhaseCalculator.Calculate(new DateTime(2025, 1, 29));
            Assert.AreEqual(MoonPhase.NewMoon, result);
        }

        [Test]
        public void KnownFullMoon_ReturnsFullMoon()
        {
            var result = MoonPhaseCalculator.Calculate(new DateTime(2025, 2, 12));
            Assert.AreEqual(MoonPhase.FullMoon, result);
        }
    }
}