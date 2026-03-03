using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class FlameIngredient
    {
        public string itemName;
        public int count;
    }

    [Serializable]
    public class FlameUpgradeRecipe
    {
        public List<FlameIngredient> ingredients = new();
    }

    [CreateAssetMenu(fileName = "FlameConfig", menuName = "CampFire/Flame Config")]
    public class FlameConfig : ScriptableObject
    {
        [Header("Mana Generation")]
        [SerializeField] private float baseManaPerSecond = 0.5f;
        [SerializeField] private float manaPerLevel = 0.3f;

        [Header("Entity Capacity (plots + vases + gardens)")]
        [SerializeField] private int[] maxEntitiesPerLevel = { 3, 5, 8, 12, 18 };

        [Header("Upgrade Costs (Mana)")]
        [SerializeField] private float[] upgradeCosts = { 50f, 150f, 400f, 1000f };

        [Header("Grid")]
        [SerializeField] private int[] gridSizePerLevel = { 5, 5, 7, 7, 9 };

        public float GetManaPerSecond(int flameLevel)
        {
            return baseManaPerSecond + (flameLevel - 1) * manaPerLevel;
        }

        public int GetMaxEntities(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, maxEntitiesPerLevel.Length - 1);
            return maxEntitiesPerLevel[index];
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
