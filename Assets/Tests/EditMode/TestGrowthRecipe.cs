using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGrowthRecipe
    {
        [Test]
        public void ScoreRange_PerfectMatch_Returns1()
        {
            float score = GrowthRecipe.ScoreRange(25f, 20f, 30f, 10f);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_AtEdge_Returns1()
        {
            float score = GrowthRecipe.ScoreRange(20f, 20f, 30f, 10f);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_OutsideTolerance_Returns0()
        {
            float score = GrowthRecipe.ScoreRange(5f, 20f, 30f, 10f);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void ScoreRange_HalfwayOutside_Returns0Point5()
        {
            float score = GrowthRecipe.ScoreRange(15f, 20f, 30f, 10f);
            Assert.AreEqual(0.5f, score, 0.001f);
        }

        [Test]
        public void Waterings_InRange_Returns1()
        {
            // ScoreRange(3, 2, 4, 2) — 3 is within [2,4]
            float score = GrowthRecipe.ScoreRange(3, 2, 4, 2);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Waterings_OneOutside_ReturnsHalf()
        {
            // ScoreRange(5, 2, 4, 2) — 1 past max, tol 2 → 0.5
            float score = GrowthRecipe.ScoreRange(5, 2, 4, 2);
            Assert.AreEqual(0.5f, score, 0.001f);
        }

        [Test]
        public void Waterings_BeyondTolerance_Returns0()
        {
            // ScoreRange(7, 2, 4, 2) — 3 past max, tol 2 → 0
            float score = GrowthRecipe.ScoreRange(7, 2, 4, 2);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void Evaluate_NoActiveDimensions_Returns1()
        {
            var recipe = new GrowthRecipe();
            var snapshots = new GrowthSnapshots { snapshotCount = 5 };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_HeatOnly_PerfectMatch()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 4,
                sumTemp = 100f
            };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_TwoDimensions_WeightedAverage()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 2f,
                useWaterings = true,
                idealWateringsMin = 2, idealWateringsMax = 4,
                wateringsTolerance = 2f, wateringsWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 4,
                sumTemp = 100f
            };
            int waterCount = 3;
            float score = recipe.Evaluate(snapshots, waterCount);
            Assert.AreEqual(1f, score, 0.001f);
        }

        [Test]
        public void Evaluate_MoonPhase_FractionScoring()
        {
            var recipe = new GrowthRecipe
            {
                useMoon = true,
                requiredMoonPhase = MoonPhase.FullMoon,
                moonWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 10,
                moonPhaseSnapshots = new int[8]
            };
            snapshots.moonPhaseSnapshots[(int)MoonPhase.FullMoon] = 7;
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(0.7f, score, 0.001f);
        }

        [Test]
        public void Evaluate_ZeroSnapshots_Returns0()
        {
            // With active weather axes but no snapshots, score is 0 (no data collected)
            var recipe = new GrowthRecipe { useHeat = true };
            var snapshots = new GrowthSnapshots { snapshotCount = 0 };
            float score = recipe.Evaluate(snapshots, 0);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void EvaluatePerAxis_HeatOnly_ReturnsOneResult()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f
            };
            var snapshots = new GrowthSnapshots
            {
                snapshotCount = 4,
                sumTemp = 100f // avg 25
            };
            var results = recipe.EvaluatePerAxis(snapshots, 0);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Heat", results[0].axisName);
            Assert.AreEqual(25f, results[0].actual, 0.1f);
            Assert.AreEqual(1f, results[0].score, 0.001f);
        }

        [Test]
        public void EvaluatePerAxis_TwoAxes_ReturnsBoth()
        {
            var recipe = new GrowthRecipe
            {
                useHeat = true,
                idealTempMin = 20f, idealTempMax = 30f,
                heatTolerance = 10f, heatWeight = 1f,
                useWaterings = true,
                idealWateringsMin = 2, idealWateringsMax = 4,
                wateringsTolerance = 2f, wateringsWeight = 1f
            };
            var snapshots = new GrowthSnapshots { snapshotCount = 4, sumTemp = 100f };
            var results = recipe.EvaluatePerAxis(snapshots, 5);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("Heat", results[0].axisName);
            Assert.AreEqual(1f, results[0].score, 0.001f);
            Assert.AreEqual("Waterings", results[1].axisName);
            Assert.AreEqual(0.5f, results[1].score, 0.001f);
        }

        [Test]
        public void EvaluatePerAxis_NoAxesEnabled_ReturnsEmpty()
        {
            var recipe = new GrowthRecipe();
            var snapshots = new GrowthSnapshots { snapshotCount = 5 };
            var results = recipe.EvaluatePerAxis(snapshots, 0);
            Assert.AreEqual(0, results.Count);
        }
    }
}
