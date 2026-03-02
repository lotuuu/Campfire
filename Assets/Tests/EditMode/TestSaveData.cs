using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSaveData
    {
        [Test]
        public void NewSaveData_HasCorrectDefaults()
        {
            var data = new SaveData();
            Assert.AreEqual(1, data.version);
            Assert.AreEqual(0f, data.mana);
            Assert.AreEqual(1, data.flameLevel);
            Assert.AreEqual(0, data.vases.Count);
            Assert.AreEqual(0, data.plots.Count);
            Assert.AreEqual(0, data.gardens.Count);
            Assert.AreEqual(0, data.seedInventory.Count);
            Assert.AreEqual(0, data.items.Count);
            Assert.AreEqual(0f, data.lastManaCollectTime);
        }

        [Test]
        public void SaveData_RoundTrips_ThroughJson()
        {
            var data = new SaveData
            {
                mana = 123.5f,
                flameLevel = 3,
                lastManaCollectTime = 1000f,
            };
            data.seedInventory.Add(new SeedInventoryEntry { seedName = "Fern", count = 5 });
            data.vases.Add(new VaseSave { capacity = 10, currentWater = 7 });
            data.plots.Add(new PlotSave { seedName = "Fern", watered = true });
            data.gardens.Add(new GardenSave { plantName = "Oak", mature = true });
            data.items.Add(new InventoryItem { itemName = "Acorn", count = 3 });

            var json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(3, restored.flameLevel);
            Assert.AreEqual(123.5f, restored.mana);
            Assert.AreEqual(1, restored.seedInventory.Count);
            Assert.AreEqual("Fern", restored.seedInventory[0].seedName);
            Assert.AreEqual(1, restored.vases.Count);
            Assert.AreEqual(7, restored.vases[0].currentWater);
            Assert.AreEqual(1, restored.plots.Count);
            Assert.IsTrue(restored.plots[0].watered);
            Assert.AreEqual(1, restored.gardens.Count);
            Assert.IsTrue(restored.gardens[0].mature);
            Assert.AreEqual(1, restored.items.Count);
            Assert.AreEqual("Acorn", restored.items[0].itemName);
        }
    }
}
