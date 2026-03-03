# Seed Roster Redesign

Replace the 3 placeholder seeds (Fern, Sunflower, Moonvine) with 10 real-world flowers and herbs. Growth times and conditions are realistic but adapted to game progression.

## Roster

| # | Seed | Type | Growth | Base Drops | Mana Cost | Harvest Item | Recipe Dimensions |
|---|------|------|--------|------------|-----------|--------------|-------------------|
| 1 | Basil | Herb | 1h | 1 | 0 | Basil Leaves | Heat 20-30°C, Waterings 1 |
| 2 | Chamomile | Herb | 1.5h | 2 | 0 | Chamomile Flowers | Heat 15-25°C, Sunlight 50-90% |
| 3 | Marigold | Flower | 2h | 2 | 10 | Marigold Petals | Heat 20-35°C, Sunlight 60-100%, Waterings 2 |
| 4 | Snowdrop | Flower | 2.5h | 2 | 10 | Snowdrop Bells | Heat 0-10°C, Humidity 50-80%, Waterings 1 |
| 5 | Mint | Herb | 3h | 3 | 15 | Mint Leaves | Humidity 50-80%, Rain 20-60%, Waterings 2 |
| 6 | Lavender | Flower | 5h | 3 | 25 | Lavender Flowers | Heat 25-35°C, Sunlight 70-100%, Wind 5-15 m/s |
| 7 | Pansy | Flower | 6h | 3 | 30 | Pansy Petals | Heat 5-15°C, Rain 20-60%, Sunlight 40-80% |
| 8 | Poppy | Flower | 8h | 4 | 40 | Poppy Petals | Heat 15-25°C, Rain 30-70%, Humidity 40-75% |
| 9 | Jasmine | Flower | 12h | 4 | 60 | Jasmine Flowers | Heat 20-30°C, Humidity 60-90%, Waterings 3 |
| 10 | Rosemary | Herb | 18h | 5 | 80 | Rosemary Sprigs | Heat 20-35°C, Sunlight 60-100%, Wind 5-20 m/s |
| 11 | Dahlia | Flower | 30h | 6 | 120 | Dahlia Blooms | Heat 18-28°C, Humidity 50-80%, Sunlight 50-90%, Waterings 4 |
| 12 | Moonflower | Flower | 48h | 8 | 200 | Moonflower Blossoms | Humidity 60-90%, Moon: Full Moon (x3), Waterings 3 |

## Design Rationale

**Early game hook**: Basil (1h) and Chamomile (1.5h) are free. Players get quick wins and learn the loop fast.

**Progression**: Cost and time ramp together. Each tier asks for more recipe dimensions, rewarding players who pay attention to weather.

**Recipe dimension coverage**: Early seeds use 1-2 dimensions, mid-tier 2-3, late-game 3-4. Every GrowthRecipe dimension is used across the roster:
- Heat: 7 seeds (most common, easy to understand)
- Sunlight: 4 seeds
- Humidity: 5 seeds
- Rain: 2 seeds
- Wind: 2 seeds
- Moon: 1 seed (Moonflower)
- Waterings: 6 seeds

**Realistic mapping**: Conditions match real horticulture. Lavender loves hot, sunny, windy Mediterranean conditions. Mint thrives in humid, rainy spots. Moonflower is a real night-blooming plant (Ipomoea alba).

## Code Changes

- Delete `Assets/Resources/Seeds/{Fern,Sunflower,Moonvine}.asset`
- Create 10 new `.asset` files in `Assets/Resources/Seeds/`
- Update `GameManager.Start()`: starter grant becomes 3x Basil (was 3x Fern)
- Update `VisitorSystem`: gift seed becomes Chamomile (was Fern)
- Update tests: replace "Fern"/"Sunflower"/"Moonvine" references with new seed names
- Fertilizer recipe is independent (uses Berry/Acorn), no changes needed

## GrowthRecipe Details

All seeds use default tolerance values unless noted. Weights default to 1.

### Basil
- Heat: 20-30°C, tolerance 10, weight 1
- Waterings: 1, weight 0.5

### Chamomile
- Heat: 15-25°C, tolerance 10, weight 1
- Sunlight: 50-90%, tolerance 20, weight 1

### Marigold
- Heat: 20-35°C, tolerance 10, weight 1
- Sunlight: 60-100%, tolerance 20, weight 1
- Waterings: 2, weight 0.5

### Mint
- Humidity: 50-80%, tolerance 20, weight 1
- Rain: 0.2-0.6, tolerance 0.3, weight 1.5
- Waterings: 2, weight 0.5

### Lavender
- Heat: 25-35°C, tolerance 10, weight 1.5
- Sunlight: 70-100%, tolerance 20, weight 1.5
- Wind: 5-15 m/s, tolerance 5, weight 1

### Poppy
- Heat: 15-25°C, tolerance 10, weight 1
- Rain: 0.3-0.7, tolerance 0.3, weight 1.5
- Humidity: 40-75%, tolerance 20, weight 1

### Jasmine
- Heat: 20-30°C, tolerance 10, weight 1
- Humidity: 60-90%, tolerance 20, weight 1.5
- Waterings: 3, weight 1

### Rosemary
- Heat: 20-35°C, tolerance 10, weight 1
- Sunlight: 60-100%, tolerance 20, weight 1.5
- Wind: 5-20 m/s, tolerance 5, weight 1

### Dahlia
- Heat: 18-28°C, tolerance 8, weight 1
- Humidity: 50-80%, tolerance 20, weight 1
- Sunlight: 50-90%, tolerance 20, weight 1
- Waterings: 4, weight 1

### Moonflower
- Humidity: 60-90%, tolerance 20, weight 1
- Moon: Full Moon (phase 4), weight 3
- Waterings: 3, weight 1
