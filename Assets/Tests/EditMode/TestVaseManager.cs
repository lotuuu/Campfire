using NUnit.Framework;

namespace Garden.Tests
{
    public class TestVaseManager
    {
        [Test]
        public void InitializeNewPlayer_Creates2Vases()
        {
            var data = new SaveData();
            VaseManager.InitializeNewPlayer(data, 5);
            Assert.AreEqual(2, data.vases.Count);
            Assert.AreEqual(VaseState.Empty, data.vases[0].state);
            Assert.AreEqual(5, data.vases[0].capacity);
        }
    }
}
