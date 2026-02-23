using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Garden/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public string environmentName;
        public int slotCount = 2;
        public int unlockCostDewdrops;

        [Header("Growth Bonus")]
        [Range(0f, 0.5f)] public float growthSpeedBonus;
        public TriggerCondition bonusCondition;

        [Header("Features")]
        public bool allowsCrossPollination;
    }
}
