using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class BuildingCost
    {
        public float manaCost;
        public List<SeedCost> seedCosts = new();
    }

    [CreateAssetMenu(fileName = "BuildingCostConfig", menuName = "CampFire/Building Cost Config")]
    public class BuildingCostConfig : ScriptableObject
    {
        [SerializeField] private List<BuildingCost> plotCosts = new();
        [SerializeField] private List<BuildingCost> vaseCosts = new();

        public BuildingCost GetPlotCost(int currentPlotCount)
        {
            if (plotCosts.Count == 0) return null;
            int index = Mathf.Clamp(currentPlotCount, 0, plotCosts.Count - 1);
            return plotCosts[index];
        }

        public BuildingCost GetVaseCost(int currentVaseCount)
        {
            if (vaseCosts.Count == 0) return null;
            int index = Mathf.Clamp(currentVaseCount, 0, vaseCosts.Count - 1);
            return vaseCosts[index];
        }
    }
}
