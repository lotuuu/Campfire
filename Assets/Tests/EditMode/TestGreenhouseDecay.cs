using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestGreenhouseDecay
    {
        [Test] public void GetStepMinutes_D_Returns20() =>
            Assert.AreEqual(20f, GreenhouseManager.GetStepMinutes(QualityTier.D));
        [Test] public void GetStepMinutes_C_Returns40() =>
            Assert.AreEqual(40f, GreenhouseManager.GetStepMinutes(QualityTier.C));
        [Test] public void GetStepMinutes_B_Returns60() =>
            Assert.AreEqual(60f, GreenhouseManager.GetStepMinutes(QualityTier.B));
        [Test] public void GetStepMinutes_A_Returns240() =>
            Assert.AreEqual(240f, GreenhouseManager.GetStepMinutes(QualityTier.A));
        [Test] public void GetStepMinutes_S_Returns360() =>
            Assert.AreEqual(360f, GreenhouseManager.GetStepMinutes(QualityTier.S));

        [Test]
        public void ComputeDecayProgress_HalfwayThroughS_Returns0Point5()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-180); // 180 of 360 min elapsed
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.S, now);
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_AtStart_Returns0()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            float progress = GreenhouseManager.ComputeDecayProgress(now, QualityTier.D, now);
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_PastThreshold_ReturnsAbove1()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-30); // 30 of 20 min elapsed → >1
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.D, now);
            Assert.Greater(progress, 1f);
        }
    }
}
