using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumHouse
    {
        [Test]
        public void GetMaxMallums_ReturnsHouseCountTimesPerHouse()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(4, config.GetMaxMallums(2));
            Assert.AreEqual(6, config.GetMaxMallums(3));
        }

        [Test]
        public void GetMaxMallums_ReturnsZeroForZeroHouses()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.AreEqual(0, config.GetMaxMallums(0));
        }

        [Test]
        public void GetNextHouseCost_ReturnsNull_WhenNoCostsDefined()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.IsNull(config.GetNextHouseCost(0));
        }

        [Test]
        public void CanBuildNextHouse_ReturnsFalse_WhenNoCostsLeft()
        {
            var config = ScriptableObject.CreateInstance<MallumHouseConfig>();
            Assert.IsFalse(config.CanBuildNextHouse(0));
        }

        [Test]
        public void CanAffordSeeds_ReturnsTrueWhenEnough()
        {
            var inventory = new List<SeedInventoryEntry>
            {
                new() { seedName = "Basil", count = 5 }
            };
            var costs = new List<SeedCost>
            {
                new() { seedName = "Basil", count = 3 }
            };
            Assert.IsTrue(MallumManager.CanAffordSeeds(inventory, costs));
        }

        [Test]
        public void CanAffordSeeds_ReturnsFalseWhenNotEnough()
        {
            var inventory = new List<SeedInventoryEntry>
            {
                new() { seedName = "Basil", count = 1 }
            };
            var costs = new List<SeedCost>
            {
                new() { seedName = "Basil", count = 3 }
            };
            Assert.IsFalse(MallumManager.CanAffordSeeds(inventory, costs));
        }

        [Test]
        public void CanAffordSeeds_ReturnsFalseWhenSeedMissing()
        {
            var inventory = new List<SeedInventoryEntry>();
            var costs = new List<SeedCost>
            {
                new() { seedName = "Lavender", count = 1 }
            };
            Assert.IsFalse(MallumManager.CanAffordSeeds(inventory, costs));
        }

        [Test]
        public void CanAffordSeeds_ReturnsTrueWhenNoCosts()
        {
            var inventory = new List<SeedInventoryEntry>();
            var costs = new List<SeedCost>();
            Assert.IsTrue(MallumManager.CanAffordSeeds(inventory, costs));
        }
    }
}
