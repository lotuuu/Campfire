using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class MerchantSave
    {
        public int gridX;
        public int gridY;
        public string merchantName;
        public List<MerchantOfferSave> offers = new();
        public string appearedAtUtc;
        public List<string> dialogueLines = new();
        public bool dialogueSeen;
    }

    [Serializable]
    public class MerchantOfferSave
    {
        public List<TradeCost> costs = new();
        public string rewardSeedName;
        public int rewardCount;
    }
}
