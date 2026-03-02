using System.Collections.Generic;
using NUnit.Framework;
using Garden;

namespace Garden.Tests
{
    public class TestVillageSnapshot
    {
        [Test]
        public void VillageSnapshot_FromSaveData_MapsCorrectly()
        {
            var saveData = new SaveData { flameLevel = 3 };
            saveData.plots.Add(new PlotSave
            {
                seedName = "Fern", state = PlotState.Growing, gridX = 1, gridY = 0
            });
            saveData.vases.Add(new VaseSave
            {
                currentWater = 3, capacity = 5, state = VaseState.Full, gridX = -1, gridY = 1
            });
            saveData.gardens.Add(new GardenSave
            {
                plantName = "Oak", mature = true, gridX = 0, gridY = -1
            });

            var snapshot = VillageSnapshot.FromSaveData(saveData, 3);

            Assert.AreEqual(3, snapshot.flameLevel);
            Assert.AreEqual(1, snapshot.plots.Count);
            Assert.AreEqual("Fern", snapshot.plots[0].seedName);
            Assert.AreEqual("Growing", snapshot.plots[0].state);
            Assert.AreEqual(1, snapshot.vases.Count);
            Assert.AreEqual(3, snapshot.vases[0].currentWater);
            Assert.AreEqual(1, snapshot.gardens.Count);
            Assert.AreEqual("Oak", snapshot.gardens[0].plantName);
            Assert.IsTrue(snapshot.gardens[0].mature);
        }

        [Test]
        public void VillageSnapshot_DictionaryRoundTrip()
        {
            var snapshot = new VillageSnapshot { flameLevel = 2 };
            snapshot.plots.Add(new SnapshotPlot
            {
                seedName = "Sunflower", state = "Mature", gridX = 1, gridY = 0
            });

            var dict = snapshot.ToDictionary();
            var loaded = VillageSnapshot.FromDictionary(dict);

            Assert.AreEqual(2, loaded.flameLevel);
            Assert.AreEqual(1, loaded.plots.Count);
            Assert.AreEqual("Sunflower", loaded.plots[0].seedName);
        }

        [Test]
        public void GiftItem_StoresTypeNameCount()
        {
            var item = new GiftItem { type = "seed", name = "Moonvine", count = 2 };
            Assert.AreEqual("seed", item.type);
            Assert.AreEqual("Moonvine", item.name);
            Assert.AreEqual(2, item.count);
        }

        [Test]
        public void VillageSnapshot_EmptySaveData_ProducesEmptySnapshot()
        {
            var saveData = new SaveData();
            var snapshot = VillageSnapshot.FromSaveData(saveData, 1);
            Assert.AreEqual(1, snapshot.flameLevel);
            Assert.AreEqual(0, snapshot.plots.Count);
            Assert.AreEqual(0, snapshot.vases.Count);
            Assert.AreEqual(0, snapshot.gardens.Count);
        }

        [Test]
        public void GiftItem_SeedAndItemTypes()
        {
            var seed = new GiftItem { type = "seed", name = "Fern", count = 3 };
            var item = new GiftItem { type = "item", name = "Fertilizer", count = 1 };
            Assert.AreEqual("seed", seed.type);
            Assert.AreEqual("item", item.type);
        }
    }
}
