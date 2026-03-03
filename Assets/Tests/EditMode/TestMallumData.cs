using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumData
    {
        [Test]
        public void MallumSave_DefaultState_IsIdle()
        {
            var mallum = new MallumSave();
            Assert.AreEqual(MallumState.Idle, mallum.state);
        }

        [Test]
        public void MallumSave_PendingRewards_StartsEmpty()
        {
            var mallum = new MallumSave();
            Assert.IsNotNull(mallum.pendingRewards);
            Assert.AreEqual(0, mallum.pendingRewards.Count);
        }

        [Test]
        public void SaveData_Mallums_StartsEmpty()
        {
            var data = new SaveData();
            Assert.IsNotNull(data.mallums);
            Assert.AreEqual(0, data.mallums.Count);
        }

        [Test]
        public void MallumConfig_GetMaxMallums_Level1_Returns1()
        {
            var config = ScriptableObject.CreateInstance<MallumConfig>();
            Assert.AreEqual(1, config.GetMaxMallums(1));
        }
    }
}
