using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "VaseConfig", menuName = "CampFire/Vase Config")]
    public class VaseConfig : ScriptableObject
    {
        [SerializeField] private int baseCapacity = 5;
        [SerializeField] private float craftCostMana = 100f;
        [SerializeField] private float fillDurationMinutes = 30f;
        [SerializeField] private int[] capacityPerTier = { 5, 8, 12, 20 };
        [SerializeField] private float[] upgradeCosts = { 75f, 200f, 500f };

        public int BaseCapacity => baseCapacity;
        public float CraftCostMana => craftCostMana;
        public float FillDurationMinutes => fillDurationMinutes;

        public int GetCapacity(int tier)
        {
            int index = Mathf.Clamp(tier, 0, capacityPerTier.Length - 1);
            return capacityPerTier[index];
        }

        public float GetUpgradeCost(int currentTier)
        {
            int index = Mathf.Clamp(currentTier, 0, upgradeCosts.Length - 1);
            return upgradeCosts[index];
        }

        public int MaxTier => capacityPerTier.Length - 1;
    }
}
