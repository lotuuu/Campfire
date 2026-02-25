using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Garden/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public string environmentName;
        public int slotCount = 1;
        public int maxSlotCount = 4;
        public int unlockCostGold;
        public int slotUnlockCostGold = 500;

        [Header("Visuals")]
        public Sprite tileSprite;

        [Header("Growth Bonus")]
        [Range(0f, 0.5f)] public float growthSpeedBonus;
        public TriggerCondition bonusCondition;

        [Header("Features")]
        public bool allowsCrossPollination;
    }
}
