using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewQuest", menuName = "CampFire/Quest Data")]
    public class QuestData : ScriptableObject
    {
        public string questName;
        [TextArea] public string description;
        public int durationMinutes = 30;
        public int requiredFlameLevel = 1;
        public int rewardRolls = 2;
        public List<QuestReward> rewardPool = new();
    }

    [Serializable]
    public class QuestReward
    {
        public SeedData seed;
        public float weight = 1f;
        public int minCount = 1;
        public int maxCount = 1;
    }
}
