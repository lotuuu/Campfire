using System;

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
    }
}
