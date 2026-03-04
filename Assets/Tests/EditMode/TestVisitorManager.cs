using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestVisitorManager
    {
        [SetUp]
        public void SetUp()
        {
            CurrencyManager.FreeMode = false;
        }

        // --- IsVisitorHour ---

        [Test]
        public void IsVisitorHour_Before22_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 4, 21, 59, 0);
            Assert.IsFalse(VisitorManager.IsVisitorHour(time));
        }

        [Test]
        public void IsVisitorHour_At22_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 4, 22, 0, 0);
            Assert.IsTrue(VisitorManager.IsVisitorHour(time));
        }

        [Test]
        public void IsVisitorHour_At23_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 4, 23, 30, 0);
            Assert.IsTrue(VisitorManager.IsVisitorHour(time));
        }

        [Test]
        public void IsVisitorHour_AtMidnight_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 5, 0, 0, 0);
            Assert.IsFalse(VisitorManager.IsVisitorHour(time));
        }

        // --- DismissVisitor ---

        [Test]
        public void DismissVisitor_ClearsCurrentVisitor()
        {
            var data = new SaveData();
            data.currentVisitor = new VisitorSave { visitorName = "Test" };
            VisitorManager.DismissVisitor(data);
            Assert.IsNull(data.currentVisitor);
        }

        // --- CleanExpiredQuests ---

        [Test]
        public void CleanExpiredQuests_RemovesOldQuests()
        {
            var data = new SaveData();
            var oldReturn = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
            data.activeQuests.Add(new ActiveVisitorQuest
            {
                visitorName = "Old",
                returnDateUtc = oldReturn.ToString("o")
            });

            // utcNow is more than 1 day after returnDate
            var now = new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc);
            VisitorManager.CleanExpiredQuests(data, now);

            Assert.AreEqual(0, data.activeQuests.Count, "Quest past expiry should be removed");
        }

        [Test]
        public void CleanExpiredQuests_KeepsFutureQuests()
        {
            var data = new SaveData();
            var futureReturn = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
            data.activeQuests.Add(new ActiveVisitorQuest
            {
                visitorName = "Future",
                returnDateUtc = futureReturn.ToString("o")
            });

            var now = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
            VisitorManager.CleanExpiredQuests(data, now);

            Assert.AreEqual(1, data.activeQuests.Count, "Future quest should be kept");
        }

        [Test]
        public void CleanExpiredQuests_RemovesEmptyReturnDate()
        {
            var data = new SaveData();
            data.activeQuests.Add(new ActiveVisitorQuest
            {
                visitorName = "NoDate",
                returnDateUtc = ""
            });

            var now = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
            VisitorManager.CleanExpiredQuests(data, now);

            Assert.AreEqual(0, data.activeQuests.Count, "Quest with empty returnDateUtc should be removed");
        }

        // --- ApplyGift: Water ---

        [Test]
        public void ApplyGift_Water_DistributesAcrossVases()
        {
            var visitor = new VisitorSave { giftType = "water", giftAmount = 5 };
            var data = new SaveData();
            data.vases.Add(new VaseSave { currentWater = 0, capacity = 3 });
            data.vases.Add(new VaseSave { currentWater = 0, capacity = 3 });

            VisitorManager.ApplyGift(visitor, data);

            Assert.AreEqual(3, data.vases[0].currentWater, "First vase should be filled to capacity");
            Assert.AreEqual(2, data.vases[1].currentWater, "Second vase should get the remaining 2");
            Assert.IsTrue(visitor.giftClaimed);
        }

        [Test]
        public void ApplyGift_Water_CapsAtVaseCapacity()
        {
            var visitor = new VisitorSave { giftType = "water", giftAmount = 100 };
            var data = new SaveData();
            data.vases.Add(new VaseSave { currentWater = 2, capacity = 5 });

            VisitorManager.ApplyGift(visitor, data);

            Assert.AreEqual(5, data.vases[0].currentWater, "Vase should not exceed capacity");
            Assert.IsTrue(visitor.giftClaimed);
        }

        // --- ApplyGift: Item ---

        [Test]
        public void ApplyGift_Item_AddsNewItem()
        {
            var visitor = new VisitorSave { giftType = "item", giftName = "Feather", giftAmount = 3 };
            var data = new SaveData();

            VisitorManager.ApplyGift(visitor, data);

            Assert.AreEqual(1, data.items.Count);
            Assert.AreEqual("Feather", data.items[0].itemName);
            Assert.AreEqual(3, data.items[0].count);
            Assert.IsTrue(visitor.giftClaimed);
        }

        [Test]
        public void ApplyGift_Item_IncrementsExistingItem()
        {
            var visitor = new VisitorSave { giftType = "item", giftName = "Feather", giftAmount = 2 };
            var data = new SaveData();
            data.items.Add(new InventoryItem { itemName = "Feather", count = 5 });

            VisitorManager.ApplyGift(visitor, data);

            Assert.AreEqual(1, data.items.Count);
            Assert.AreEqual(7, data.items[0].count);
            Assert.IsTrue(visitor.giftClaimed);
        }

        // --- ApplyGift guard ---

        [Test]
        public void ApplyGift_AlreadyClaimed_DoesNothing()
        {
            var visitor = new VisitorSave
            {
                giftType = "item",
                giftName = "Feather",
                giftAmount = 5,
                giftClaimed = true
            };
            var data = new SaveData();

            VisitorManager.ApplyGift(visitor, data);

            Assert.AreEqual(0, data.items.Count, "No items should be added when gift already claimed");
        }

        // --- CanAffordOffer ---

        [Test]
        public void CanAffordOffer_Sufficient_ReturnsTrue()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 2 }
                },
                rewardSeedName = "Lavender",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Petal", count = 5 }
            };

            Assert.IsTrue(VisitorManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_Insufficient_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 10 }
                },
                rewardSeedName = "Lavender",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Petal", count = 3 }
            };

            Assert.IsFalse(VisitorManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_MissingItem_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 1 }
                },
                rewardSeedName = "Lavender",
                rewardCount = 1
            };
            var items = new List<InventoryItem>();

            Assert.IsFalse(VisitorManager.CanAffordOffer(offer, items));
        }

        // --- ExecuteTrade ---

        [Test]
        public void ExecuteTrade_ConsumesItemsAndAddsSeed()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 2 },
                    new TradeCost { itemName = "Root", count = 1 }
                },
                rewardSeedName = "Dahlia",
                rewardCount = 3
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Petal", count = 5 },
                new InventoryItem { itemName = "Root", count = 2 }
            };
            var seeds = new List<SeedInventoryEntry>();

            VisitorManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(3, items[0].count, "Petal count should be reduced by 2");
            Assert.AreEqual(1, items[1].count, "Root count should be reduced by 1");
            Assert.AreEqual(1, seeds.Count);
            Assert.AreEqual("Dahlia", seeds[0].seedName);
            Assert.AreEqual(3, seeds[0].count);
        }

        [Test]
        public void ExecuteTrade_AddsToExistingSeedEntry()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 1 }
                },
                rewardSeedName = "Basil",
                rewardCount = 2
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Petal", count = 5 }
            };
            var seeds = new List<SeedInventoryEntry>
            {
                new SeedInventoryEntry { seedName = "Basil", count = 3 }
            };

            VisitorManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(1, seeds.Count, "Should not create a duplicate entry");
            Assert.AreEqual(5, seeds[0].count, "Existing seed count should be incremented");
        }

        [Test]
        public void ExecuteTrade_RemovesZeroCountItems()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Petal", count = 3 }
                },
                rewardSeedName = "Mint",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Petal", count = 3 }
            };
            var seeds = new List<SeedInventoryEntry>();

            VisitorManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(0, items.Count, "Item with zero count should be removed");
        }

        // --- BuildVisitorSave ---

        [Test]
        public void BuildVisitorSave_Merchant_SetsTypeAndOffers()
        {
            var response = new VisitorManager.VisitorResponse
            {
                visitor_type = "merchant",
                visitor_id = "m1",
                name = "Peddler",
                portrait_id = "portrait_merchant",
                dialogue = new List<string> { "Hello!" },
                offers = new List<VisitorManager.OfferResponse>
                {
                    new VisitorManager.OfferResponse
                    {
                        costs = new List<TradeCost>
                        {
                            new TradeCost { itemName = "Petal", count = 2 }
                        },
                        rewardSeedName = "Lavender",
                        rewardCount = 1
                    }
                }
            };

            var save = VisitorManager.BuildVisitorSave(response, 2, -1, "2026-03-04T00:00:00Z");

            Assert.AreEqual(VisitorType.Merchant, save.type);
            Assert.AreEqual("m1", save.visitorId);
            Assert.AreEqual("Peddler", save.visitorName);
            Assert.AreEqual("portrait_merchant", save.portraitId);
            Assert.AreEqual(1, save.offers.Count);
            Assert.AreEqual("Lavender", save.offers[0].rewardSeedName);
            Assert.AreEqual(1, save.offers[0].rewardCount);
            Assert.AreEqual(1, save.offers[0].costs.Count);
            Assert.AreEqual("Petal", save.offers[0].costs[0].itemName);
            Assert.AreEqual(1, save.dialogueLines.Count);
            Assert.AreEqual("Hello!", save.dialogueLines[0]);
        }

        [Test]
        public void BuildVisitorSave_Gifter_SetsGiftFields()
        {
            var response = new VisitorManager.VisitorResponse
            {
                visitor_type = "gifter",
                visitor_id = "g1",
                name = "Generous Gnome",
                portrait_id = "portrait_gifter",
                dialogue = new List<string> { "A gift for you!" },
                gift = new VisitorManager.GiftResponse
                {
                    type = "water",
                    name = "",
                    amount = 5
                }
            };

            var save = VisitorManager.BuildVisitorSave(response, 0, 1, "2026-03-04T00:00:00Z");

            Assert.AreEqual(VisitorType.Gifter, save.type);
            Assert.AreEqual("water", save.giftType);
            Assert.AreEqual(5, save.giftAmount);
            Assert.IsFalse(save.giftClaimed);
        }

        [Test]
        public void BuildVisitorSave_Quester_SetsQuestFields()
        {
            var response = new VisitorManager.VisitorResponse
            {
                visitor_type = "quester",
                visitor_id = "q1",
                name = "Wanderer",
                portrait_id = "portrait_quester",
                dialogue = new List<string> { "I need help!" },
                quest = new VisitorManager.QuestResponse
                {
                    quest_id = 42,
                    request_item = "Petal",
                    request_count = 3,
                    return_days = 1,
                    reward = new VisitorManager.QuestReward { type = "seed", name = "Moonflower", count = 2 },
                    return_dialogue = new List<string> { "Thank you!" },
                    is_return = false
                }
            };

            var save = VisitorManager.BuildVisitorSave(response, -1, 1, "2026-03-04T00:00:00Z");

            Assert.AreEqual(VisitorType.Quester, save.type);
            Assert.AreEqual("Petal", save.requestItem);
            Assert.AreEqual(3, save.requestCount);
            Assert.IsFalse(save.isReturnVisit);
            Assert.IsNotNull(save.rewardJson);
            Assert.IsTrue(save.rewardJson.Contains("Moonflower"));
            Assert.AreEqual(1, save.returnDialogue.Count);
            Assert.AreEqual("Thank you!", save.returnDialogue[0]);
        }

        [Test]
        public void BuildVisitorSave_SetsGridPosition()
        {
            var response = new VisitorManager.VisitorResponse
            {
                visitor_type = "gifter",
                visitor_id = "g2",
                name = "Sprite",
                portrait_id = "portrait_sprite",
                gift = new VisitorManager.GiftResponse { type = "item", name = "Feather", amount = 1 }
            };

            var save = VisitorManager.BuildVisitorSave(response, 3, -2, "2026-03-04T12:00:00Z");

            Assert.AreEqual(3, save.gridX);
            Assert.AreEqual(-2, save.gridY);
            Assert.AreEqual("2026-03-04T12:00:00Z", save.appearedAtUtc);
            Assert.AreEqual("2026-03-04T12:00:00Z", save.fetchedDateUtc);
        }
    }
}
