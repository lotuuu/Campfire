using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "MallumHouseConfig", menuName = "CampFire/Mallum House Config")]
    public class MallumHouseConfig : ScriptableObject
    {
        [SerializeField] private int mallumsPerHouse = 1;
        [SerializeField] private List<HouseCost> houseCosts = new();

        public int MallumsPerHouse => mallumsPerHouse;

        public int GetMaxMallums(int houseCount)
        {
            return houseCount * mallumsPerHouse;
        }

        public HouseCost GetNextHouseCost(int currentHouseCount)
        {
            if (currentHouseCount < 0 || currentHouseCount >= houseCosts.Count)
                return null;
            return houseCosts[currentHouseCount];
        }

        public bool CanBuildNextHouse(int currentHouseCount)
        {
            return GetNextHouseCost(currentHouseCount) != null;
        }
    }

    [Serializable]
    public class HouseCost
    {
        public float manaCost;
        public List<HarvestCost> harvestCosts = new();
    }

    [Serializable]
    public class HarvestCost
    {
        public string itemName;
        public int count;
    }
}
