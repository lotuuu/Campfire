using NUnit.Framework;

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
    }
}
