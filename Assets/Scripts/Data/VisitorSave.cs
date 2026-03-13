using System;
using System.Collections.Generic;

namespace Garden
{
    public enum VisitorType { Merchant, Gifter, Quester }

    [Serializable]
    public class VisitorSave
    {
        public int gridX;
        public int gridY;
        public string visitorId;
        public string visitorName;
        public string portraitId;
        public VisitorType type;
        public List<string> dialogueLines = new();
        public bool dialogueSeen;
        public string appearedAtUtc;
        public string fetchedDateUtc;

        // Merchant
        public List<MerchantOfferSave> offers = new();

        // Gifter
        public string giftType; // "seed", "water", "item"
        public string giftName;
        public int giftAmount;
        public bool giftClaimed;

        // Quester
        public int serverQuestId;
        public string requestItem;
        public int requestCount;
        public int returnDays;
        public string returnDateUtc;
        public string rewardJson;
        public List<string> returnDialogue = new();
        public bool isReturnVisit;
        public bool questFulfilled;
    }

    [Serializable]
    public class ActiveVisitorQuest
    {
        public int serverQuestId;
        public string visitorId;
        public string visitorName;
        public string portraitId;
        public string requestItem;
        public int requestCount;
        public string returnDateUtc;
        public string rewardJson;
        public List<string> returnDialogue = new();
    }

    [Serializable]
    public class TradeCost
    {
        public string itemKey;
        public int count;
    }

    [Serializable]
    public class MerchantOfferSave
    {
        public List<TradeCost> costs = new();
        public string rewardSeedName;
        public int rewardCount;
    }
}
