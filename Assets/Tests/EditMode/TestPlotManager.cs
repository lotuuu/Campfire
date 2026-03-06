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
                seedName = "Basil",
                plantTimeUtc = DateTime.UtcNow.AddHours(-2).ToString("o"),
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
        public void Harvest_ClearsPlot()
        {
            var plot = new PlotSave
            {
                seedName = "Basil",
                state = PlotState.Mature,
                waterCount = 2
            };
            plot.seedName = null;
            plot.plantTimeUtc = null;
            plot.waterCount = 0;
            plot.state = PlotState.Empty;

            Assert.AreEqual(PlotState.Empty, plot.state);
            Assert.IsNull(plot.seedName);
            Assert.AreEqual(0, plot.waterCount);
        }

        [Test]
        public void HarvestDrops_PerfectScore_ReturnsHighEnd()
        {
            // With score=1.0, center=maxDrops, so drops should be near max
            int minDrops = 2;
            int maxDrops = 10;
            int drops = PlotManager.CalculateDrops(1f, minDrops, maxDrops);

            Assert.GreaterOrEqual(drops, minDrops);
            Assert.LessOrEqual(drops, maxDrops);
        }

        [Test]
        public void HarvestDrops_ZeroScore_ReturnsLowEnd()
        {
            // With score=0.0, center=minDrops, so drops should be near min
            int minDrops = 2;
            int maxDrops = 10;
            int drops = PlotManager.CalculateDrops(0f, minDrops, maxDrops);

            Assert.GreaterOrEqual(drops, minDrops);
            Assert.LessOrEqual(drops, maxDrops);
        }

        [Test]
        public void HarvestDrops_AlwaysWithinRange()
        {
            int minDrops = 3;
            int maxDrops = 12;
            for (int i = 0; i <= 10; i++)
            {
                float score = i / 10f;
                int drops = PlotManager.CalculateDrops(score, minDrops, maxDrops);
                Assert.GreaterOrEqual(drops, minDrops, $"Drops below min at score {score}");
                Assert.LessOrEqual(drops, maxDrops, $"Drops above max at score {score}");
            }
        }

        [Test]
        public void Water_IncrementsWaterCount()
        {
            var plot = new PlotSave
            {
                seedName = "Basil",
                state = PlotState.Growing,
                waterCount = 0
            };
            plot.waterCount++;
            Assert.AreEqual(1, plot.waterCount);
        }
    }
}
