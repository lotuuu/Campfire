using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class FlameIngredient
    {
        public string itemKey;
        public int count;
    }

    [Serializable]
    public class FlameUpgradeRecipe
    {
        public List<FlameIngredient> ingredients = new();
    }

    [Serializable]
    public class HarvestCost
    {
        public string itemKey;
        public int count;
    }

    [Serializable]
    public class BuildingCost
    {
        public float manaCost;
        public List<HarvestCost> harvestCosts = new();
    }

    [Serializable]
    public class GardenCostTier
    {
        public float manaCost;
        public int seedCost;
    }
}
