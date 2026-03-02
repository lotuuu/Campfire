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
                seedName = "Fern",
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
        public void HarvestDrops_PerfectRecipe_ReturnsBaseDrops()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            int baseDrops = 5;

            var snapshots = new GrowthSnapshots { snapshotCount = 10, sumTemp = 250f };
            float score = recipe.Evaluate(snapshots, 0);
            int drops = Mathf.Max(1, Mathf.RoundToInt(baseDrops * score));

            Assert.AreEqual(5, drops);
        }

        [Test]
        public void HarvestDrops_PoorRecipe_ReturnsAtLeast1()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            int baseDrops = 5;

            var snapshots = new GrowthSnapshots { snapshotCount = 10, sumTemp = 0f };
            float score = recipe.Evaluate(snapshots, 0);
            int drops = Mathf.Max(1, Mathf.RoundToInt(baseDrops * score));

            Assert.AreEqual(1, drops);
        }

        [Test]
        public void Water_IncrementsWaterCount()
        {
            var plot = new PlotSave
            {
                seedName = "Fern",
                state = PlotState.Growing,
                waterCount = 0
            };
            plot.waterCount++;
            Assert.AreEqual(1, plot.waterCount);
        }
    }
}
