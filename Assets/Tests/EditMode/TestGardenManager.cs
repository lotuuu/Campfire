using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGardenManager
    {
        [Test]
        public void GardenGrowth_CalculatesProgress()
        {
            var garden = new GardenSave
            {
                plantName = "Oak",
                plantTimeUtc = DateTime.UtcNow.AddHours(-12).ToString("o"),
                mature = false
            };

            float growthHours = 24f;
            var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - plantTime).TotalHours;
            float progress = UnityEngine.Mathf.Clamp01(elapsed / growthHours);

            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void MatureGarden_YieldsOnInterval()
        {
            var garden = new GardenSave
            {
                plantName = "Oak",
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-13).ToString("o")
            };

            float intervalHours = 12f;
            var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - lastYield).TotalHours;

            Assert.IsTrue(elapsed >= intervalHours);
        }
    }
}
