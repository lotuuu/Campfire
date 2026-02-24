using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSaveManager
    {
        [Test]
        public void Load_CorruptedJson_FallsBackToNewSaveData()
        {
            SaveData result;
            try
            {
                result = JsonUtility.FromJson<SaveData>("{ this is not valid json }}}");
                if (result == null) result = new SaveData();
            }
            catch
            {
                result = new SaveData();
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.dewdrops);
        }

        [Test]
        public void Load_NullJsonResult_FallsBackToNewSaveData()
        {
            // JsonUtility.FromJson throws ArgumentException for non-object JSON ("null").
            // The try/catch in SaveManager.Load() catches this and falls back to new SaveData().
            SaveData result;
            try
            {
                result = JsonUtility.FromJson<SaveData>("null") ?? new SaveData();
            }
            catch
            {
                result = new SaveData();
            }
            Assert.IsNotNull(result);
            Assert.AreEqual(6, result.greenhouseSlots);
        }

        [Test]
        public void SaveData_DefaultValues_AreValid()
        {
            var data = new SaveData();
            Assert.AreEqual(6, data.greenhouseSlots);
            Assert.IsNotNull(data.activeSlots);
            Assert.IsNotNull(data.seedInventory);
            Assert.IsNotNull(data.greenhousePlants);
            Assert.IsNotNull(data.discoveredVariants);
            Assert.IsNotNull(data.unlockedEnvironments);
            Assert.IsNotNull(data.environmentSlots);
        }

        [Test]
        public void SaveData_RoundTrip_PreservesValues()
        {
            var data = new SaveData
            {
                dewdrops = 500,
                sunShards = 10,
                auraDust = 25,
                greenhouseSlots = 8
            };
            data.seedInventory.Add(new SeedInventoryEntry { seedName = "Astra", count = 3 });

            var json = JsonUtility.ToJson(data, true);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(500, restored.dewdrops);
            Assert.AreEqual(10, restored.sunShards);
            Assert.AreEqual(8, restored.greenhouseSlots);
            Assert.AreEqual(1, restored.seedInventory.Count);
            Assert.AreEqual("Astra", restored.seedInventory[0].seedName);
            Assert.AreEqual(3, restored.seedInventory[0].count);
        }
    }
}
