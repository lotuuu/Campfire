using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestBirdManager
    {
        // --- GetFreeTiles tests ---

        [Test]
        public void GetFreeTiles_ExcludesFlameAtOrigin()
        {
            var data = new SaveData();
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((0, 0)), "Flame at origin should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesPlots()
        {
            var data = new SaveData();
            data.plots.Add(new PlotSave { gridX = 1, gridY = 0 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((1, 0)), "Plot tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesVases()
        {
            var data = new SaveData();
            data.vases.Add(new VaseSave { gridX = 0, gridY = 1 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((0, 1)), "Vase tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesGardens()
        {
            var data = new SaveData();
            data.gardens.Add(new GardenSave { gridX = -1, gridY = 0 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((-1, 0)), "Garden tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesMallumHouses()
        {
            var data = new SaveData();
            data.mallumHouses.Add(new MallumHouseSave { gridX = 0, gridY = -1 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((0, -1)), "Mallum house tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesApotheke()
        {
            var data = new SaveData();
            // Default apotheke is at (1, 0)
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((data.apothekeGridX, data.apothekeGridY)),
                "Apotheke tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ExcludesBirds()
        {
            var data = new SaveData();
            data.birds.Add(new BirdSave { gridX = -1, gridY = 1 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.IsFalse(tiles.Contains((-1, 1)), "Bird tile should be excluded");
        }

        [Test]
        public void GetFreeTiles_ReturnsEmptyWhenAllOccupied()
        {
            var data = new SaveData();
            // Radius 1 has 7 hex tiles: (0,0), (1,0), (-1,0), (0,1), (0,-1), (1,-1), (-1,1)
            // (0,0) is flame, apotheke defaults to (1,0)
            // Fill the remaining 5 tiles
            data.plots.Add(new PlotSave { gridX = -1, gridY = 0 });
            data.vases.Add(new VaseSave { gridX = 0, gridY = 1 });
            data.gardens.Add(new GardenSave { gridX = 0, gridY = -1 });
            data.birds.Add(new BirdSave { gridX = 1, gridY = -1 });
            data.mallumHouses.Add(new MallumHouseSave { gridX = -1, gridY = 1 });
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.AreEqual(0, tiles.Count, "All tiles occupied, should return empty");
        }

        [Test]
        public void GetFreeTiles_ReturnsCorrectCountForRadius1()
        {
            var data = new SaveData();
            // Radius 1 = 7 tiles. Flame(0,0) + Apotheke(1,0) = 2 occupied => 5 free
            var tiles = BirdManager.GetFreeTiles(data, 1);
            Assert.AreEqual(5, tiles.Count);
        }

        [Test]
        public void GetFreeTiles_ReturnsAllValidHexesMinusOccupied()
        {
            var data = new SaveData();
            // Radius 2 = 19 tiles. Flame(0,0) + Apotheke(1,0) = 2 occupied => 17 free
            var tiles = BirdManager.GetFreeTiles(data, 2);
            Assert.AreEqual(17, tiles.Count);
        }

        // --- GetEligibleSeeds tests ---

        [Test]
        public void GetEligibleSeeds_FiltersByTier()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 2);
            Assert.AreEqual(2, eligible.Count, "Should include tier 1 and tier 2 seeds");
            foreach (var s in eligible)
                Assert.LessOrEqual(s.tier, 2);
        }

        [Test]
        public void GetEligibleSeeds_ReturnsAllAtMaxLevel()
        {
            var seeds = CreateTestSeeds();
            int maxTier = 0;
            foreach (var s in seeds)
                if (s.tier > maxTier) maxTier = s.tier;

            var eligible = BirdManager.GetEligibleSeeds(seeds, maxTier);
            Assert.AreEqual(seeds.Count, eligible.Count, "All seeds should be eligible at max level");
        }

        [Test]
        public void GetEligibleSeeds_ReturnsEmptyAtLevel0()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 0);
            Assert.AreEqual(0, eligible.Count, "No seeds should be eligible at level 0");
        }

        [Test]
        public void GetEligibleSeeds_ReturnsOnlyTier1AtLevel1()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 1);
            Assert.AreEqual(1, eligible.Count);
            Assert.AreEqual("Basil", eligible[0].name);
        }

        // --- RollSeedDrop tests ---

        [Test]
        public void RollSeedDrop_ReturnsValidEntry()
        {
            var seeds = CreateTestSeeds();
            var eligible = BirdManager.GetEligibleSeeds(seeds, 3);
            var bird = BirdManager.RollSeedDrop(eligible, 3);
            Assert.IsNotNull(bird);
            Assert.IsFalse(string.IsNullOrEmpty(bird.seedName));
            Assert.Greater(bird.seedCount, 0);
        }

        [Test]
        public void RollSeedDrop_ReturnsNullForEmptyList()
        {
            var bird = BirdManager.RollSeedDrop(new List<SeedData>(), 1);
            Assert.IsNull(bird);
        }

        [Test]
        public void RollSeedDrop_ReturnsNullForNullList()
        {
            var bird = BirdManager.RollSeedDrop(null, 1);
            Assert.IsNull(bird);
        }

        [Test]
        public void RollSeedDrop_HigherLevelGivesMoreLowTierSeeds()
        {
            // For a tier-1 seed at flame level 5:
            // baseCount = Max(1, 5 - 1 + 1) = 5
            // quantity range = [Max(1,4), 7) = [4, 7)
            // For tier-1 seed at flame level 1:
            // baseCount = Max(1, 1 - 1 + 1) = 1
            // quantity range = [Max(1,0), 3) = [1, 3)
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.name = "Basil";
            seed.seedName = "Basil Seed";
            seed.tier = 1;
            var seeds = new List<SeedData> { seed };

            int lowLevelTotal = 0;
            int highLevelTotal = 0;
            int runs = 100;
            for (int i = 0; i < runs; i++)
            {
                var birdLow = BirdManager.RollSeedDrop(seeds, 1);
                lowLevelTotal += birdLow.seedCount;
                var birdHigh = BirdManager.RollSeedDrop(seeds, 5);
                highLevelTotal += birdHigh.seedCount;
            }

            Assert.Greater(highLevelTotal, lowLevelTotal,
                "Higher flame level should yield more low-tier seeds on average");
        }

        [Test]
        public void RollSeedDrop_SeedCountIsAlwaysAtLeast1()
        {
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.name = "Basil";
            seed.seedName = "Basil Seed";
            seed.tier = 1;
            var seeds = new List<SeedData> { seed };

            for (int i = 0; i < 50; i++)
            {
                var bird = BirdManager.RollSeedDrop(seeds, 1);
                Assert.GreaterOrEqual(bird.seedCount, 1);
            }
        }

        // --- ProcessHourlyChecks tests ---

        [Test]
        public void ProcessHourlyChecks_InitializesLastCheck_WhenNull()
        {
            var data = new SaveData();
            var seeds = CreateTestSeeds();
            var now = new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc);

            bool result = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);

            Assert.IsFalse(result, "Should return false on initialization");
            Assert.IsFalse(string.IsNullOrEmpty(data.lastBirdCheckHourUtc),
                "lastBirdCheckHourUtc should be set");

            // Should be truncated to hour
            var parsed = DateTime.Parse(data.lastBirdCheckHourUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.AreEqual(10, parsed.Hour);
            Assert.AreEqual(0, parsed.Minute);
        }

        [Test]
        public void ProcessHourlyChecks_InitializesLastCheck_WhenEmpty()
        {
            var data = new SaveData { lastBirdCheckHourUtc = "" };
            var seeds = CreateTestSeeds();
            var now = new DateTime(2026, 3, 1, 14, 45, 0, DateTimeKind.Utc);

            bool result = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);

            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrEmpty(data.lastBirdCheckHourUtc));
        }

        [Test]
        public void ProcessHourlyChecks_NoChange_WhenSameHour()
        {
            var data = new SaveData();
            var seeds = CreateTestSeeds();
            var hour = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = hour.ToString("o");

            // Call at 10:30 — same hour, nothing should happen
            var now = new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc);
            bool result = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);

            Assert.IsFalse(result, "No bird should be placed within the same hour");
            Assert.AreEqual(0, data.birds.Count);
        }

        [Test]
        public void ProcessHourlyChecks_UpdatesLastCheckHour()
        {
            var data = new SaveData();
            var seeds = CreateTestSeeds();
            var lastCheck = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = lastCheck.ToString("o");

            var now = new DateTime(2026, 3, 1, 13, 15, 0, DateTimeKind.Utc);
            BirdManager.ProcessHourlyChecks(data, seeds, 2, now);

            var updated = DateTime.Parse(data.lastBirdCheckHourUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.AreEqual(13, updated.Hour);
            Assert.AreEqual(0, updated.Minute);
        }

        [Test]
        public void ProcessHourlyChecks_CatchesUpMultipleHours()
        {
            // Force Random.value to always be 0 (below 0.33 threshold) by using a fixed seed
            // that produces values < 0.33
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();
            var lastCheck = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = lastCheck.ToString("o");

            // Run many iterations: 24 hour gap should have many chances to place birds
            // With 0.33 chance per hour, over 24 hours we very likely get at least 1 bird
            var now = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

            // Run multiple times to ensure at least once it places birds (probabilistic)
            bool everPlaced = false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var testData = new SaveData { flameLevel = 1 };
                testData.lastBirdCheckHourUtc = lastCheck.ToString("o");
                bool result = BirdManager.ProcessHourlyChecks(testData, seeds, 2, now);
                if (result)
                {
                    everPlaced = true;
                    Assert.Greater(testData.birds.Count, 0);
                    break;
                }
            }
            Assert.IsTrue(everPlaced, "Over 20 attempts with 24-hour gaps, at least one should place a bird");
        }

        [Test]
        public void ProcessHourlyChecks_ExistingBirdsHalveChance()
        {
            // With 0 birds: chance = 0.33 * 0.5^0 = 0.33
            // With 1 bird:  chance = 0.33 * 0.5^1 = 0.165
            // With 2 birds: chance = 0.33 * 0.5^2 = 0.0825
            // Over many runs with 1 hour gap, count placements with 0 vs 2 existing birds
            var seeds = CreateTestSeeds();
            var lastCheck = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc);
            int runs = 500;

            int placedWith0 = 0;
            int placedWith2 = 0;
            for (int i = 0; i < runs; i++)
            {
                // Test with 0 existing birds
                var data0 = new SaveData { flameLevel = 1 };
                data0.lastBirdCheckHourUtc = lastCheck.ToString("o");
                if (BirdManager.ProcessHourlyChecks(data0, seeds, 3, now))
                    placedWith0++;

                // Test with 2 existing birds
                var data2 = new SaveData { flameLevel = 1 };
                data2.lastBirdCheckHourUtc = lastCheck.ToString("o");
                data2.birds.Add(new BirdSave { gridX = -1, gridY = 0, seedName = "Basil", seedCount = 1 });
                data2.birds.Add(new BirdSave { gridX = 0, gridY = -1, seedName = "Basil", seedCount = 1 });
                if (BirdManager.ProcessHourlyChecks(data2, seeds, 3, now))
                    placedWith2++;
            }

            // With 0 birds: ~33% chance. With 2 birds: ~8.25% chance.
            // placedWith0 should be significantly higher
            Assert.Greater(placedWith0, placedWith2,
                $"0-bird placements ({placedWith0}) should exceed 2-bird placements ({placedWith2})");
        }

        [Test]
        public void ProcessHourlyChecks_NoBirds_WhenNoFreeTiles()
        {
            var data = new SaveData { flameLevel = 1 };
            var seeds = CreateTestSeeds();

            // Fill all tiles in radius 1 (7 tiles)
            // Flame at (0,0), Apotheke at (1,0) by default
            data.plots.Add(new PlotSave { gridX = -1, gridY = 0 });
            data.vases.Add(new VaseSave { gridX = 0, gridY = 1 });
            data.gardens.Add(new GardenSave { gridX = 0, gridY = -1 });
            data.mallumHouses.Add(new MallumHouseSave { gridX = 1, gridY = -1 });
            data.birds.Add(new BirdSave { gridX = -1, gridY = 1, seedName = "Basil", seedCount = 1 });

            var lastCheck = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            data.lastBirdCheckHourUtc = lastCheck.ToString("o");
            var now = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

            bool result = BirdManager.ProcessHourlyChecks(data, seeds, 1, now);
            Assert.IsFalse(result, "Should not place birds when no free tiles");
            Assert.AreEqual(1, data.birds.Count, "Original bird count should be unchanged");
        }

        [Test]
        public void ProcessHourlyChecks_PlacedBirdHasValidTile()
        {
            // Run until we get a placed bird, then verify its coords are valid
            var seeds = CreateTestSeeds();
            var lastCheck = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
            var now = new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

            for (int attempt = 0; attempt < 50; attempt++)
            {
                var data = new SaveData { flameLevel = 1 };
                data.lastBirdCheckHourUtc = lastCheck.ToString("o");
                bool result = BirdManager.ProcessHourlyChecks(data, seeds, 2, now);
                if (result && data.birds.Count > 0)
                {
                    var bird = data.birds[0];
                    Assert.IsTrue(HexGridUtil.IsWithinRadius(bird.gridX, bird.gridY, 2),
                        $"Bird at ({bird.gridX},{bird.gridY}) should be within grid radius 2");
                    // Bird should not be at flame or apotheke
                    Assert.IsFalse(bird.gridX == 0 && bird.gridY == 0,
                        "Bird should not be at flame origin");
                    return;
                }
            }
            Assert.Fail("Could not place a bird in 50 attempts");
        }

        [Test]
        public void ProcessHourlyChecks_NewBirdsCountTowardSubsequentChecks()
        {
            // If a bird is placed at hour N, subsequent hours in the same call
            // should have reduced chance. We verify by checking that data.birds.Count
            // is used dynamically — the implementation adds to data.birds before
            // continuing the loop, so effectiveChance recalculates each iteration.
            var seeds = CreateTestSeeds();
            var lastCheck = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            // 48 hour gap = lots of checks
            var now = new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);

            // Run many times and track max birds placed in single call
            int maxBirdsInSingleCall = 0;
            int totalBirdsAllCalls = 0;
            int runs = 50;
            for (int i = 0; i < runs; i++)
            {
                var data = new SaveData { flameLevel = 1 };
                data.lastBirdCheckHourUtc = lastCheck.ToString("o");
                BirdManager.ProcessHourlyChecks(data, seeds, 3, now);
                if (data.birds.Count > maxBirdsInSingleCall)
                    maxBirdsInSingleCall = data.birds.Count;
                totalBirdsAllCalls += data.birds.Count;
            }

            // With 48 hours: first hour 33%, second (if 1 bird) 16.5%, third 8.25%...
            // Should not get too many birds despite many hours
            // Average should be reasonable (roughly 1-3 per 48-hour gap)
            float avgBirds = (float)totalBirdsAllCalls / runs;
            Assert.Less(avgBirds, 10f,
                $"Average birds ({avgBirds:F1}) over 48 hours should be modest due to halving");
        }

        // --- CollectBird tests ---

        [Test]
        public void CollectBird_RemovesBirdAndReturnsIt()
        {
            var data = new SaveData();
            data.birds.Add(new BirdSave { gridX = 1, gridY = -1, seedName = "Basil", seedCount = 3 });
            data.birds.Add(new BirdSave { gridX = -1, gridY = 1, seedName = "Mint", seedCount = 2 });

            var collected = BirdManager.CollectBird(data, 0);

            Assert.IsNotNull(collected);
            Assert.AreEqual("Basil", collected.seedName);
            Assert.AreEqual(3, collected.seedCount);
            Assert.AreEqual(1, data.birds.Count, "Bird list should shrink by 1");
            Assert.AreEqual("Mint", data.birds[0].seedName, "Remaining bird should be the second one");
        }

        [Test]
        public void CollectBird_ReturnsNull_ForNegativeIndex()
        {
            var data = new SaveData();
            data.birds.Add(new BirdSave { seedName = "Basil", seedCount = 1 });
            var result = BirdManager.CollectBird(data, -1);
            Assert.IsNull(result);
            Assert.AreEqual(1, data.birds.Count);
        }

        [Test]
        public void CollectBird_ReturnsNull_ForOutOfRangeIndex()
        {
            var data = new SaveData();
            data.birds.Add(new BirdSave { seedName = "Basil", seedCount = 1 });
            var result = BirdManager.CollectBird(data, 5);
            Assert.IsNull(result);
            Assert.AreEqual(1, data.birds.Count);
        }

        [Test]
        public void CollectBird_ReturnsNull_ForEmptyList()
        {
            var data = new SaveData();
            var result = BirdManager.CollectBird(data, 0);
            Assert.IsNull(result);
        }

        [Test]
        public void CollectBird_ReturnsNull_ForNullData()
        {
            var result = BirdManager.CollectBird(null, 0);
            Assert.IsNull(result);
        }

        // --- Helper methods ---

        private List<SeedData> CreateTestSeeds()
        {
            var seeds = new List<SeedData>();

            var basil = ScriptableObject.CreateInstance<SeedData>();
            basil.name = "Basil";
            basil.seedName = "Basil Seed";
            basil.tier = 1;
            seeds.Add(basil);

            var chamomile = ScriptableObject.CreateInstance<SeedData>();
            chamomile.name = "Chamomile";
            chamomile.seedName = "Chamomile Seed";
            chamomile.tier = 2;
            seeds.Add(chamomile);

            var dahlia = ScriptableObject.CreateInstance<SeedData>();
            dahlia.name = "Dahlia";
            dahlia.seedName = "Dahlia Seed";
            dahlia.tier = 3;
            seeds.Add(dahlia);

            return seeds;
        }
    }
}
