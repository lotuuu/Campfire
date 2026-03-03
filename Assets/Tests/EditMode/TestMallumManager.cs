using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumManager
    {
        [Test]
        public void EnsureMallumCount_AddsIdleMallums_WhenBelowCap()
        {
            var mallums = new List<MallumSave>();
            MallumManager.EnsureMallumCount(mallums, 2);
            Assert.AreEqual(2, mallums.Count);
            Assert.AreEqual(MallumState.Idle, mallums[0].state);
            Assert.AreEqual(MallumState.Idle, mallums[1].state);
        }

        [Test]
        public void EnsureMallumCount_DoesNotRemove_WhenAboveCap()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.OnQuest },
                new() { state = MallumState.Idle },
                new() { state = MallumState.Idle }
            };
            MallumManager.EnsureMallumCount(mallums, 2);
            Assert.AreEqual(3, mallums.Count);
        }

        [Test]
        public void GetAvailableCount_CountsOnlyIdle()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle },
                new() { state = MallumState.OnQuest },
                new() { state = MallumState.FetchingWater },
                new() { state = MallumState.Idle }
            };
            Assert.AreEqual(2, MallumManager.GetAvailableCount(mallums));
        }

        [Test]
        public void ClaimMallumForWater_SetsStateAndVaseIndex()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle }
            };
            bool result = MallumManager.ClaimMallumForWater(mallums, 0, "2026-01-01T00:00:00Z");
            Assert.IsTrue(result);
            Assert.AreEqual(MallumState.FetchingWater, mallums[0].state);
            Assert.AreEqual(0, mallums[0].assignedVaseIndex);
        }

        [Test]
        public void ClaimMallumForWater_FailsWhenNoneIdle()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.OnQuest }
            };
            bool result = MallumManager.ClaimMallumForWater(mallums, 0, "2026-01-01T00:00:00Z");
            Assert.IsFalse(result);
        }

        [Test]
        public void ClaimMallumForQuest_SetsStateAndQuestName()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle }
            };
            bool result = MallumManager.ClaimMallumForQuest(mallums, "Swamp Forage", "2026-01-01T00:00:00Z");
            Assert.IsTrue(result);
            Assert.AreEqual(MallumState.OnQuest, mallums[0].state);
            Assert.AreEqual("Swamp Forage", mallums[0].assignedQuestName);
        }

        [Test]
        public void RollRewards_ProducesCorrectRollCount()
        {
            var pool = new List<QuestReward>
            {
                new() { seed = ScriptableObject.CreateInstance<SeedData>(), weight = 1f, minCount = 1, maxCount = 1 }
            };
            pool[0].seed.seedName = "Basil";

            var rewards = MallumManager.RollRewards(pool, 3);
            Assert.AreEqual(3, rewards.Count);
            Assert.AreEqual("Basil", rewards[0].seedName);
        }

        [Test]
        public void CollectRewards_ClearsStateAndRewards()
        {
            var mallum = new MallumSave
            {
                state = MallumState.QuestComplete,
                assignedQuestName = "Swamp Forage",
                pendingRewards = new List<RewardEntry>
                {
                    new() { seedName = "Basil", count = 2 }
                }
            };
            var rewards = MallumManager.CollectRewards(mallum);
            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(MallumState.Idle, mallum.state);
            Assert.IsNull(mallum.assignedQuestName);
            Assert.AreEqual(0, mallum.pendingRewards.Count);
        }

        [Test]
        public void FreeMallumFromWater_ReturnsToIdle()
        {
            var mallum = new MallumSave
            {
                state = MallumState.FetchingWater,
                assignedVaseIndex = 1,
                startTimeUtc = "2026-01-01T00:00:00Z"
            };
            MallumManager.FreeMallumFromWater(mallum);
            Assert.AreEqual(MallumState.Idle, mallum.state);
            Assert.AreEqual(-1, mallum.assignedVaseIndex);
            Assert.IsNull(mallum.startTimeUtc);
        }
    }
}
