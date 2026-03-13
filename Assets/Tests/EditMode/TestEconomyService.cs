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
        queue.actions.Add(new EconomyAction { type = "add-items", jsonBody = "{\"item_key\":\"basil_seed\",\"count\":3}" });

        string json = UnityEngine.JsonUtility.ToJson(queue);
        var restored = UnityEngine.JsonUtility.FromJson<EconomyQueue>(json);

        Assert.AreEqual(2, restored.actions.Count);
        Assert.AreEqual("spend-mana", restored.actions[0].type);
        Assert.AreEqual("add-items", restored.actions[1].type);
    }

    [Test]
    public void EconomyState_DeserializesFromServerJson()
    {
        string json = @"{""mana"":42.5,""gems"":10,""flameLevel"":3,""lastManaCollectUtc"":""2026-03-05T12:00:00Z"",""inventory"":[{""itemKey"":""basil_seed"",""count"":5},{""itemKey"":""speed_potion"",""count"":2}]}";
        var state = UnityEngine.JsonUtility.FromJson<EconomyState>(json);

        Assert.AreEqual(42.5f, state.mana, 0.01f);
        Assert.AreEqual(10, state.gems);
        Assert.AreEqual(3, state.flameLevel);
        Assert.AreEqual(2, state.inventory.Count);
        Assert.AreEqual("basil_seed", state.inventory[0].itemKey);
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
    public void AddItemRequest_SerializesCorrectly()
    {
        var req = new AddItemRequest { item_key = "basil_seed", count = 3 };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("basil_seed"));
        Assert.IsTrue(json.Contains("3"));
    }

    [Test]
    public void UpgradeFlameRequest_SerializesWithItems()
    {
        var req = new UpgradeFlameRequest
        {
            items = new System.Collections.Generic.List<SpendItemEntry>
            {
                new SpendItemEntry { item_key = "sprouts", count = 1 }
            }
        };
        string json = UnityEngine.JsonUtility.ToJson(req);
        Assert.IsTrue(json.Contains("sprouts"));
    }
}
