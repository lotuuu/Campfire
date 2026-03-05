using NUnit.Framework;
using Garden;

[TestFixture]
public class TestGameService
{
    [Test]
    public void GameStateResponse_DeserializesPlots()
    {
        string json = @"{""economy"":{""mana"":50,""gems"":5,""flameLevel"":1,""seeds"":[],""items"":[]},""plots"":[{""id"":1,""seedName"":""Basil"",""state"":""growing"",""gridX"":1,""gridY"":0,""waterCount"":2}],""vases"":[],""gardens"":[],""mallums"":[]}";
        var state = UnityEngine.JsonUtility.FromJson<GameStateResponse>(json);
        Assert.AreEqual(1, state.plots.Count);
        Assert.AreEqual("Basil", state.plots[0].seedName);
        Assert.AreEqual("growing", state.plots[0].state);
    }

    [Test]
    public void HarvestResponse_Deserializes()
    {
        string json = @"{""score"":0.85,""drops"":3,""itemName"":""Basil_harvest""}";
        var resp = UnityEngine.JsonUtility.FromJson<HarvestResponse>(json);
        Assert.AreEqual(0.85f, resp.score, 0.01f);
        Assert.AreEqual(3, resp.drops);
    }

    [Test]
    public void CraftRequest_Serializes()
    {
        var req = new CraftRequest { gridX = 2, gridY = -1 };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("2"));
        Assert.IsTrue(json.Contains("-1"));
    }

    [Test]
    public void QuestRequest_Serializes()
    {
        var req = new QuestRequest { questName = "SwampForage" };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("SwampForage"));
    }

    [Test]
    public void ServerMallum_DeserializesRewards()
    {
        string json = @"{""id"":1,""state"":""quest_complete"",""assignedQuestName"":""SwampForage"",""pendingRewards"":[{""seed_name"":""Basil"",""count"":2}]}";
        var mallum = UnityEngine.JsonUtility.FromJson<ServerMallum>(json);
        Assert.AreEqual("quest_complete", mallum.state);
        Assert.AreEqual(1, mallum.pendingRewards.Count);
    }

    [Test]
    public void LocationRequest_Serializes()
    {
        var req = new LocationRequest { lat = 51.5f, lon = -0.12f };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("51.5"));
    }

    [Test]
    public void GameStateResponse_DeserializesVases()
    {
        string json = @"{""economy"":{""mana"":0,""gems"":0,""flameLevel"":1,""seeds"":[],""items"":[]},""plots"":[],""vases"":[{""id"":1,""capacity"":5,""currentWater"":3,""state"":""full"",""gridX"":0,""gridY"":-1}],""gardens"":[],""mallums"":[]}";
        var state = UnityEngine.JsonUtility.FromJson<GameStateResponse>(json);
        Assert.AreEqual(1, state.vases.Count);
        Assert.AreEqual(5, state.vases[0].capacity);
        Assert.AreEqual("full", state.vases[0].state);
    }

    [Test]
    public void GameStateResponse_DeserializesGardens()
    {
        string json = @"{""economy"":{""mana"":0,""gems"":0,""flameLevel"":1,""seeds"":[],""items"":[]},""plots"":[],""vases"":[],""gardens"":[{""id"":1,""plantName"":""OakTree"",""mature"":true,""gridX"":2,""gridY"":1}],""mallums"":[]}";
        var state = UnityEngine.JsonUtility.FromJson<GameStateResponse>(json);
        Assert.AreEqual(1, state.gardens.Count);
        Assert.AreEqual("OakTree", state.gardens[0].plantName);
        Assert.IsTrue(state.gardens[0].mature);
    }

    [Test]
    public void CollectGardenResponse_Deserializes()
    {
        string json = @"{""garden"":{""id"":1,""plantName"":""OakTree"",""mature"":true,""gridX"":2,""gridY"":1},""yieldItem"":""Oak_fruit"",""yieldAmount"":3}";
        var resp = UnityEngine.JsonUtility.FromJson<CollectGardenResponse>(json);
        Assert.AreEqual("Oak_fruit", resp.yieldItem);
        Assert.AreEqual(3, resp.yieldAmount);
    }

    [Test]
    public void WaterRequest_Serializes()
    {
        var req = new WaterRequest { plotId = 5, vaseId = 2 };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("\"plotId\":5"));
        Assert.IsTrue(json.Contains("\"vaseId\":2"));
    }
}
