using System;
using System.Collections.Generic;

namespace Garden
{
    public class PlantSlot
    {
        public int environmentIndex;
        public int slotIndex;
        public PlantState state = PlantState.Empty;
        public SeedData seed;
        public VariantData variant;
        public DateTime plantTime;
        public float growthSpeedMultiplier = 1f;
        public float growthProgress;
        // Runtime-only: refreshed on weather change, not persisted
        public float cachedEnvBonus;
        // Slot-scoped consumables (Fertilizer, QualityDirt); cleared on harvest
        public List<ConsumableData> appliedConsumables = new();
    }
}
