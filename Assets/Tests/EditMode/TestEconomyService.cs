using NUnit.Framework;
using Garden;

[TestFixture]
public class TestEconomyService
{
    [Test]
    public void EconomyQueue_SerializesAndDeserializes()
    {
        var queue = new EconomyQueue();
        queue.actions.Add(new EconomyAction { type = "spend-mana", jsonBody = "{\"amount\":20}" });
        queue.actions.Add(new EconomyAction { type = "add-items", jsonBody = "{\"item_name\":\"Basil_Seed\",\"count\":3}" });

        string json = UnityEngine.JsonUtility.ToJson(queue);
        var restored = UnityEngine.JsonUtility.FromJson<EconomyQueue>(json);

        Assert.AreEqual(2, restored.actions.Count);
        Assert.AreEqual("spend-mana", restored.actions[0].type);
        Assert.AreEqual("add-items", restored.actions[1].type);
    }

    [Test]
    public void EconomyState_DeserializesFromServerJson()
    {
        string json = @"{""mana"":42.5,""gems"":10,""flameLevel"":3,""lastManaCollectUtc"":""2026-03-05T12:00:00Z"",""inventory"":[{""itemName"":""Basil_Seed"",""count"":5},{""itemName"":""Speed_Potion"",""count"":2}]}";
        var state = UnityEngine.JsonUtility.FromJson<EconomyState>(json);

        Assert.AreEqual(42.5f, state.mana, 0.01f);
        Assert.AreEqual(10, state.gems);
        Assert.AreEqual(3, state.flameLevel);
        Assert.AreEqual(2, state.inventory.Count);
        Assert.AreEqual("Basil_Seed", state.inventory[0].itemName);
        Assert.AreEqual(5, state.inventory[0].count);
    }

    [Test]
    public void SpendManaRequest_SerializesCorrectly()
    {
        var req = new SpendManaRequest { amount = 25.5f };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("25.5"));
    }

    [Test]
    public void AddSeedRequest_SerializesCorrectly()
    {
        var req = new AddSeedRequest { seed_name = "Basil", count = 3 };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("Basil"));
        Assert.IsTrue(json.Contains("3"));
    }

    [Test]
    public void UpgradeFlameRequest_SerializesWithItems()
    {
        var req = new UpgradeFlameRequest
        {
            items = new System.Collections.Generic.List<SpendItemEntry>
            {
                new SpendItemEntry { item_name = "Sprouts", count = 1 }
            }
        };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("Sprouts"));
    }
}
