using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

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

        [Test]
        public void TryClaimDiscoveryReward_ReturnsTrueForUnclaimed()
        {
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial");
            // Not in claimedDiscoveryRewards → should succeed
            bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
            Assert.IsTrue(result);
            Assert.IsTrue(save.claimedDiscoveryRewards.Contains("Celestial"));
        }

        [Test]
        public void TryClaimDiscoveryReward_ReturnsFalseForAlreadyClaimed()
        {
            var save = new SaveData();
            save.discoveredVariants.Add("Celestial");
            save.claimedDiscoveryRewards.Add("Celestial");

            bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
            Assert.IsFalse(result);
            Assert.AreEqual(1, save.claimedDiscoveryRewards.Count);
        }

        [Test]
        public void TryClaimDiscoveryReward_ReturnsFalseForUndiscovered()
        {
            var save = new SaveData();
            // Not in discoveredVariants → should fail
            bool result = CodexUI.TryClaimDiscoveryReward("Celestial", save);
            Assert.IsFalse(result);
            Assert.AreEqual(0, save.claimedDiscoveryRewards.Count);
        }

        [Test]
        public void IsDiscoveryRewardUnclaimed_TrueWhenDiscoveredButNotClaimed()
        {
            var save = new SaveData();
            save.discoveredVariants.Add("Storm");

            Assert.IsTrue(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
        }

        [Test]
        public void IsDiscoveryRewardUnclaimed_FalseWhenClaimed()
        {
            var save = new SaveData();
            save.discoveredVariants.Add("Storm");
            save.claimedDiscoveryRewards.Add("Storm");

            Assert.IsFalse(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
        }

        [Test]
        public void IsDiscoveryRewardUnclaimed_FalseWhenNotDiscovered()
        {
            var save = new SaveData();

            Assert.IsFalse(CodexUI.IsDiscoveryRewardUnclaimed("Storm", save));
        }
    }
}
