using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMerchantManager
    {
        private MerchantData CreateTestMerchant()
        {
            var seed1 = ScriptableObject.CreateInstance<SeedData>();
            seed1.seedName = "Moonflower";
            seed1.tier = 2;

            var seed2 = ScriptableObject.CreateInstance<SeedData>();
            seed2.seedName = "Dahlia";
            seed2.tier = 3;

            var merchant = ScriptableObject.CreateInstance<MerchantData>();
            merchant.merchantName = "Night Merchant";
            merchant.flavorText = "Rare seeds for trade...";
            merchant.offerCount = 3;
            merchant.offerPool = new List<MerchantOffer>
            {
                new MerchantOffer
                {
                    requiredFlameLevel = 1,
                    costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                    rewardSeed = seed1,
                    rewardCount = 1,
                    weight = 1f
                },
                new MerchantOffer
                {
                    requiredFlameLevel = 2,
                    costs = new List<TradeCost> { new TradeCost { itemName = "Chamomile_harvest", count = 5 } },
                    rewardSeed = seed2,
                    rewardCount = 1,
                    weight = 1f
                }
            };
            return merchant;
        }

        [Test]
        public void IsNightMerchantHour_At22_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 3, 22, 0, 0);
            Assert.IsTrue(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At23_ReturnsTrue()
        {
            var time = new DateTime(2026, 3, 3, 23, 30, 0);
            Assert.IsTrue(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At21_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 3, 21, 59, 0);
            Assert.IsFalse(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void IsNightMerchantHour_At0_ReturnsFalse()
        {
            var time = new DateTime(2026, 3, 4, 0, 0, 0);
            Assert.IsFalse(MerchantManager.IsNightMerchantHour(time));
        }

        [Test]
        public void RollOffers_FiltersByFlameLevel()
        {
            var merchant = CreateTestMerchant();

            // Flame level 1: only first offer eligible
            var offers = MerchantManager.RollOffers(merchant, 1);
            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual("Moonflower", offers[0].rewardSeedName);
        }

        [Test]
        public void RollOffers_HigherFlameLevelUnlocksMore()
        {
            var merchant = CreateTestMerchant();

            var offers = MerchantManager.RollOffers(merchant, 3);
            Assert.AreEqual(2, offers.Count);
        }

        [Test]
        public void RollOffers_RespectsOfferCount()
        {
            var merchant = CreateTestMerchant();
            merchant.offerCount = 1;

            var offers = MerchantManager.RollOffers(merchant, 3);
            Assert.AreEqual(1, offers.Count);
        }

        [Test]
        public void RollOffers_EmptyPoolReturnsEmpty()
        {
            var merchant = ScriptableObject.CreateInstance<MerchantData>();
            merchant.offerPool = new List<MerchantOffer>();

            var offers = MerchantManager.RollOffers(merchant, 5);
            Assert.AreEqual(0, offers.Count);
        }

        [Test]
        public void CanAffordOffer_WithSufficientItems_ReturnsTrue()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };

            Assert.IsTrue(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_WithInsufficientItems_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 2 }
            };

            Assert.IsFalse(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void CanAffordOffer_MissingItem_ReturnsFalse()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>();

            Assert.IsFalse(MerchantManager.CanAffordOffer(offer, items));
        }

        [Test]
        public void ExecuteTrade_ConsumesItemsAndAddsSeed()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 2
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(2, items[0].count);
            Assert.AreEqual(1, seeds.Count);
            Assert.AreEqual("Moonflower", seeds[0].seedName);
            Assert.AreEqual(2, seeds[0].count);
        }

        [Test]
        public void ExecuteTrade_RemovesItemWhenCountReachesZero()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 3 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 3 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void ExecuteTrade_AddsToExistingSeedEntry()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost> { new TradeCost { itemName = "Basil_harvest", count = 1 } },
                rewardSeedName = "Moonflower",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 }
            };
            var seeds = new List<SeedInventoryEntry>
            {
                new SeedInventoryEntry { seedName = "Moonflower", count = 3 }
            };

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(1, seeds.Count);
            Assert.AreEqual(4, seeds[0].count);
        }

        [Test]
        public void TrySpawnMerchant_PlacesMerchantOnFreeTile()
        {
            var data = new SaveData();
            var merchant = CreateTestMerchant();
            var utcNow = new DateTime(2026, 3, 3, 22, 0, 0, DateTimeKind.Utc);

            bool result = MerchantManager.TrySpawnMerchant(data, merchant, 2, 1, utcNow);

            Assert.IsTrue(result);
            Assert.AreEqual(1, data.merchants.Count);
            Assert.AreEqual("Night Merchant", data.merchants[0].merchantName);
            Assert.IsTrue(data.merchants[0].offers.Count > 0);
        }

        [Test]
        public void DismissAllMerchants_ClearsList()
        {
            var data = new SaveData();
            data.merchants.Add(new MerchantSave { merchantName = "Test" });

            MerchantManager.DismissAllMerchants(data);

            Assert.AreEqual(0, data.merchants.Count);
        }

        [Test]
        public void CleanStaleMerchants_RemovesOldMerchants()
        {
            var data = new SaveData();
            var yesterday = new DateTime(2026, 3, 2, 22, 0, 0, DateTimeKind.Utc);
            data.merchants.Add(new MerchantSave
            {
                merchantName = "Stale",
                appearedAtUtc = yesterday.ToString("o")
            });

            var today = new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc);
            MerchantManager.CleanStaleMerchants(data, today);

            Assert.AreEqual(0, data.merchants.Count);
        }

        [Test]
        public void CleanStaleMerchants_KeepsTodayMerchants()
        {
            var data = new SaveData();
            var todayEvening = new DateTime(2026, 3, 3, 22, 0, 0, DateTimeKind.Utc);
            data.merchants.Add(new MerchantSave
            {
                merchantName = "Fresh",
                appearedAtUtc = todayEvening.ToString("o")
            });

            var todayLater = new DateTime(2026, 3, 3, 23, 0, 0, DateTimeKind.Utc);
            MerchantManager.CleanStaleMerchants(data, todayLater);

            Assert.AreEqual(1, data.merchants.Count);
        }

        [Test]
        public void ExecuteTrade_MultipleCosts_ConsumesAll()
        {
            var offer = new MerchantOfferSave
            {
                costs = new List<TradeCost>
                {
                    new TradeCost { itemName = "Basil_harvest", count = 2 },
                    new TradeCost { itemName = "Mint_harvest", count = 1 }
                },
                rewardSeedName = "Dahlia",
                rewardCount = 1
            };
            var items = new List<InventoryItem>
            {
                new InventoryItem { itemName = "Basil_harvest", count = 5 },
                new InventoryItem { itemName = "Mint_harvest", count = 3 }
            };
            var seeds = new List<SeedInventoryEntry>();

            MerchantManager.ExecuteTrade(offer, items, seeds);

            Assert.AreEqual(3, items[0].count);
            Assert.AreEqual(2, items[1].count);
            Assert.AreEqual("Dahlia", seeds[0].seedName);
        }

        // --- Dialogue rolling tests ---

        private MerchantData CreateMerchantWithDialogues()
        {
            var merchant = CreateTestMerchant();
            merchant.dialoguePool = new List<MerchantDialogue>
            {
                new MerchantDialogue { lines = new List<string> { "Hello.", "Let's trade." } },
                new MerchantDialogue { lines = new List<string> { "Good evening.", "What do you have?" } },
                new MerchantDialogue { lines = new List<string> { "Nice night.", "See my wares." } }
            };
            return merchant;
        }

        [Test]
        public void RollDialogue_ReturnsLinesFromPool()
        {
            var merchant = CreateMerchantWithDialogues();
            var seen = new List<int>();

            var lines = MerchantManager.RollDialogue(merchant, seen);

            Assert.IsTrue(lines.Count > 0);
            Assert.AreEqual(1, seen.Count);
        }

        [Test]
        public void RollDialogue_PrefersUnseenDialogues()
        {
            var merchant = CreateMerchantWithDialogues();
            var seen = new List<int> { 0, 1 };

            var lines = MerchantManager.RollDialogue(merchant, seen);

            // Only index 2 was unseen, so it must be picked
            Assert.AreEqual(3, seen.Count);
            Assert.AreEqual(2, seen[2]);
            Assert.AreEqual("Nice night.", lines[0]);
        }

        [Test]
        public void RollDialogue_ResetsWhenAllSeen()
        {
            var merchant = CreateMerchantWithDialogues();
            var seen = new List<int> { 0, 1, 2 };

            var lines = MerchantManager.RollDialogue(merchant, seen);

            // seen should have been cleared then one added back
            Assert.AreEqual(1, seen.Count);
            Assert.IsTrue(lines.Count > 0);
        }

        [Test]
        public void RollDialogue_EmptyPoolReturnsEmptyLines()
        {
            var merchant = CreateTestMerchant();
            merchant.dialoguePool = new List<MerchantDialogue>();
            var seen = new List<int>();

            var lines = MerchantManager.RollDialogue(merchant, seen);

            Assert.AreEqual(0, lines.Count);
            Assert.AreEqual(0, seen.Count);
        }

        [Test]
        public void RollDialogue_NullPoolReturnsEmptyLines()
        {
            var merchant = CreateTestMerchant();
            merchant.dialoguePool = null;
            var seen = new List<int>();

            var lines = MerchantManager.RollDialogue(merchant, seen);

            Assert.AreEqual(0, lines.Count);
        }
    }
}
