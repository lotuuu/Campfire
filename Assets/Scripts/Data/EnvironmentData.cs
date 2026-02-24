using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Garden/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public string environmentName;
        public int slotCount = 1;
        public int maxSlotCount = 4;
        public int unlockCostDewdrops;
        public int slotUnlockCostDewdrops = 500;

        [Header("Growth Bonus")]
        [Range(0f, 0.5f)] public float growthSpeedBonus;
        public TriggerCondition bonusCondition;

        [Header("Features")]
        public bool allowsCrossPollination;
    }
}
