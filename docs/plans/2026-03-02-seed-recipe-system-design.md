# Seed Recipe System Design

## Problem

The current harvest system gates growth behind watering and uses a binary weather pass/fail for quality. This makes planting feel mechanical — water it, wait, harvest. There's no ongoing relationship between the plant and its environment.

## Solution

Each seed has a **GrowthRecipe**: a set of ideal environmental conditions over its lifespan. Growth starts immediately on planting (no watering gate). During growth, the system records periodic weather snapshots (every 15 min, aligned with WeatherService polls). At harvest, actual accumulated conditions are compared against the recipe using a weighted average to determine drop count.

## Recipe Dimensions

Each dimension is optional (has a `use*` flag). Unused dimensions are excluded from scoring. Every active dimension has a configurable `weight` (default 1.0).

| Dimension | Recipe fields | Tracked as | Score logic |
|-----------|--------------|------------|-------------|
| Heat | `idealTempMin`, `idealTempMax` (°C) | avg temperature | 1.0 inside range, linear falloff outside (reaches 0 at `tolerance` distance) |
| Wind | `idealWindMin`, `idealWindMax` (m/s) | avg windSpeed | same |
| Humidity | `idealHumidityMin`, `idealHumidityMax` (%) | avg humidity | same |
| Sunlight | `idealSunlightMin`, `idealSunlightMax` (%) | avg (100 - cloudCover) | same |
| Rain | `idealRainMin`, `idealRainMax` (fraction 0-1) | fraction of snapshots with Rain/Storm condition | same |
| Moon | `requiredMoonPhase` (MoonPhase enum) | fraction of snapshots in that phase | score = fraction |
| Waterings | `idealWaterings` (int) | actual watering count | 1.0 if exact, -0.25 per unit off, floor 0 |

For range-based dimensions, each also has a `tolerance` field (how far outside the ideal range before score hits 0). Default tolerance: half the range width, minimum 5 units.

## Score Calculation

```
activeWeightSum = 0
scoreSum = 0

for each dimension where use* is true:
    score = computeDimensionScore(actual, ideal)  // 0.0 to 1.0
    scoreSum += score * weight
    activeWeightSum += weight

finalScore = scoreSum / activeWeightSum   // 0.0 to 1.0, NaN guarded
drops = max(1, round(baseDrops * finalScore))
```

Minimum 1 drop guaranteed regardless of score.

## Data Model Changes

### GrowthRecipe (new serializable class)

Replaces `TriggerCondition preferredWeather` on SeedData. Lives on SeedData as `public GrowthRecipe recipe`.

```csharp
[Serializable]
public class GrowthRecipe
{
    // Heat
    public bool useHeat;
    public float idealTempMin, idealTempMax;
    public float heatTolerance = 10f;
    public float heatWeight = 1f;

    // Wind
    public bool useWind;
    public float idealWindMin, idealWindMax;
    public float windTolerance = 5f;
    public float windWeight = 1f;

    // Humidity
    public bool useHumidity;
    public float idealHumidityMin, idealHumidityMax;
    public float humidityTolerance = 20f;
    public float humidityWeight = 1f;

    // Sunlight
    public bool useSunlight;
    public float idealSunlightMin, idealSunlightMax;
    public float sunlightTolerance = 20f;
    public float sunlightWeight = 1f;

    // Rain
    public bool useRain;
    public float idealRainMin, idealRainMax;  // fraction 0-1
    public float rainTolerance = 0.3f;
    public float rainWeight = 1f;

    // Moon
    public bool useMoon;
    public MoonPhase requiredMoonPhase;
    public float moonWeight = 1f;

    // Waterings
    public bool useWaterings;
    public int idealWaterings;
    public float wateringsWeight = 1f;
}
```

### SeedData changes

- Remove: `preferredWeather`, `waterRequired`, `baseYield`, weather match constants
- Add: `GrowthRecipe recipe`, `int baseDrops`

### PlotSave changes

- Remove: `watered` (bool)
- Add: `int waterCount`, `int snapshotCount`, `float sumTemp`, `float sumWind`, `float sumHumidity`, `float sumSunlight`, `int rainSnapshots`, `int[] moonPhaseSnapshots` (8 entries, one per MoonPhase)

### PlotState simplification

Remove `Watered` state. Flow: `Empty -> Growing -> Mature`. `Planted` state is also removed — planting goes directly to `Growing`.

## PlotManager Changes

### Plant()
Sets state to `Growing`, records `plantTimeUtc`, initializes all snapshot accumulators to zero.

### Water()
Increments `waterCount` on the plot. Still costs water from vases via CurrencyManager. Can be called any time during `Growing` state. Does not affect growth speed.

### RecordSnapshot()
Called when WeatherService fires `OnWeatherUpdated`. For every plot in `Growing` state:
- Increment `snapshotCount`
- Add current temp, wind, humidity, sunlight (100 - cloudCover) to running sums
- If condition is Rain or Storm, increment `rainSnapshots`
- Increment the appropriate `moonPhaseSnapshots[phase]` entry

### Harvest()
1. Compute averages: avgTemp = sumTemp / snapshotCount, etc.
2. Compute rainFraction = rainSnapshots / snapshotCount
3. Find dominant moon phase = argmax(moonPhaseSnapshots), moonFraction = max / snapshotCount
4. For each active recipe dimension, compute score (0-1)
5. Weighted average -> finalScore
6. drops = max(1, round(baseDrops * finalScore))
7. Return HarvestResult with per-dimension breakdown

### Growth speed
`growthDurationHours` remains as-is. No weather speed bonus (removed). Growth is purely time-based.

## What Stays

- `TriggerCondition` remains for non-recipe systems (visitor triggers, calendar events)
- WeatherService polling unchanged (15 min)
- Growth duration from `SeedData.growthDurationHours`
- Save/load via SaveManager JSON serialization
- HarvestResult struct (extended with recipe score breakdown)

## What Gets Removed

- `TriggerCondition preferredWeather` from SeedData
- `waterRequired` from SeedData (watering is now optional/recipe-based)
- `GetEffectiveGrowthHours()` weather speed bonus
- `CalculateQuality()` random roll + weather match
- `PlotState.Planted` and `PlotState.Watered`
- `watered` bool from PlotSave
