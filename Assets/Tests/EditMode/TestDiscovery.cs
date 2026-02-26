using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestDiscovery
    {
        private VariantData CreateVariant(string name)
        {
            var v = ScriptableObject.CreateInstance<VariantData>();
            v.variantName = name;
            return v;
        }

        [Test]
        public void CheckAndMarkDiscovered_ReturnsTrueForNewVariant()
        {
            var variant = CreateVariant("Celestial");
            var save = new SaveData();

            bool result = PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.IsTrue(result);
            Assert.IsTrue(save.discoveredVariants.Contains("Celestial"));
        }

        [Test]
        public void CheckAndMarkDiscovered_ReturnsFalseForAlreadyDiscoveredVariant()
        {
            var variant = CreateVariant("Celestial");
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial");

            bool result = PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.IsFalse(result);
            Assert.AreEqual(1, save.discoveredVariants.Count); // not added twice
        }

        [Test]
        public void CheckAndMarkDiscovered_AddsVariantToDiscoveredList()
        {
            var variant = CreateVariant("Storm");
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial"); // pre-existing

            PlantManager.CheckAndMarkDiscovered(variant, save);

            Assert.AreEqual(2, save.discoveredVariants.Count);
            Assert.IsTrue(save.discoveredVariants.Contains("Storm"));
        }
    }
}
