using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestGreenhouseDecay
    {
        [Test] public void GetStepMinutes_D_Returns15() =>
            Assert.AreEqual(15f, GreenhouseManager.GetStepMinutes(QualityTier.D, 1f));
        [Test] public void GetStepMinutes_C_Returns30() =>
            Assert.AreEqual(30f, GreenhouseManager.GetStepMinutes(QualityTier.C, 1f));
        [Test] public void GetStepMinutes_B_Returns60() =>
            Assert.AreEqual(60f, GreenhouseManager.GetStepMinutes(QualityTier.B, 1f));
        [Test] public void GetStepMinutes_A_Returns120() =>
            Assert.AreEqual(120f, GreenhouseManager.GetStepMinutes(QualityTier.A, 1f));
        [Test] public void GetStepMinutes_S_Returns240() =>
            Assert.AreEqual(240f, GreenhouseManager.GetStepMinutes(QualityTier.S, 1f));

        [Test]
        public void ComputeDecayProgress_HalfwayThroughS_Returns0Point5()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-120); // 120 of 240 min elapsed (S tier, 1h base)
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.S, 1f, now);
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_AtStart_Returns0()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            float progress = GreenhouseManager.ComputeDecayProgress(now, QualityTier.D, 1f, now);
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void ComputeDecayProgress_PastThreshold_ReturnsAbove1()
        {
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var start = now.AddMinutes(-20); // 20 of 15 min elapsed → >1 (D tier, 1h base)
            float progress = GreenhouseManager.ComputeDecayProgress(start, QualityTier.D, 1f, now);
            Assert.Greater(progress, 1f);
        }
    }
}
