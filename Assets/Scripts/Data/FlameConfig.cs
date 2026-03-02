using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "FlameConfig", menuName = "CampFire/Flame Config")]
    public class FlameConfig : ScriptableObject
    {
        [Header("Mana Generation")]
        [SerializeField] private float baseManaPerSecond = 0.5f;
        [SerializeField] private float manaPerLevel = 0.3f;

        [Header("Plot Capacity")]
        [SerializeField] private int[] plotsPerLevel = { 1, 2, 3, 5, 8 };

        [Header("Upgrade Costs (Mana)")]
        [SerializeField] private float[] upgradeCosts = { 50f, 150f, 400f, 1000f };

        [Header("Grid")]
        [SerializeField] private int[] gridSizePerLevel = { 5, 5, 7, 7, 9 };

        public float GetManaPerSecond(int flameLevel)
        {
            return baseManaPerSecond + (flameLevel - 1) * manaPerLevel;
        }

        public int GetMaxPlots(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, plotsPerLevel.Length - 1);
            return plotsPerLevel[index];
        }

        public float GetUpgradeCost(int currentLevel)
        {
            int index = Mathf.Clamp(currentLevel - 1, 0, upgradeCosts.Length - 1);
            return upgradeCosts[index];
        }

        public int MaxLevel => upgradeCosts.Length + 1;

        public int GetGridSize(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, gridSizePerLevel.Length - 1);
            return gridSizePerLevel[index];
        }
    }
}
