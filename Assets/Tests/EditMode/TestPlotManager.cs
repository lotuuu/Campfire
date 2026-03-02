using System;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestPlotManager
    {
        [Test]
        public void GrowthProgress_CalculatesCorrectly()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                plantTimeUtc = DateTime.UtcNow.AddHours(-2).ToString("o"),
                watered = true,
                state = PlotState.Growing
            };

            float growthHours = 4f;
            var plantTime = DateTime.Parse(plot.plantTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(DateTime.UtcNow - plantTime).TotalHours;
            float progress = Mathf.Clamp01(elapsed / growthHours);

            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void WeatherMatch_BoostsGrowthSpeed()
        {
            float baseHours = 4f;
            float boostedHours = baseHours / (1f + SeedData.WeatherMatchBonus);
            Assert.AreEqual(3.2f, boostedHours, 0.01f);
        }

        [Test]
        public void Harvest_ClearsPlot()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                state = PlotState.Mature,
                watered = true
            };
            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.watered = false;
            plot.state = PlotState.Empty;

            Assert.AreEqual(PlotState.Empty, plot.state);
            Assert.IsNull(plot.seedName);
        }
    }
}
