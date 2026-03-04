using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewMerchant", menuName = "CampFire/Merchant Data")]
    public class MerchantData : ScriptableObject
    {
        public string merchantName;
        [TextArea] public string flavorText;
        public Texture2D portrait;
        public int offerCount = 3;
        public List<MerchantOffer> offerPool = new();
        public List<MerchantDialogue> dialoguePool = new();
    }

    [Serializable]
    public class MerchantDialogue
    {
        [TextArea] public List<string> lines = new();
    }

    [Serializable]
    public class MerchantOffer
    {
        public int requiredFlameLevel = 1;
        public List<TradeCost> costs = new();
        public SeedData rewardSeed;
        public int rewardCount = 1;
        public float weight = 1f;
    }

    [Serializable]
    public class TradeCost
    {
        public string itemName;
        public int count;
    }
}
