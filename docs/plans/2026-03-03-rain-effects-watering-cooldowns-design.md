# Rain Effects & Watering Cooldowns Design

## Summary

After 15 minutes of continuous rain, all vases fill to capacity and all growing plants receive a free watering. Manual and rain waterings have cooldowns (2h and 6h respectively) sharing a single timestamp per plot.

## Rain Event Detection

Tracked via two fields on `SaveData`:
- `rainStartTimeUtc` — when continuous rain began (null if not raining)
- `lastRainEffectTimeUtc` — when last rain effect fired (prevents re-triggering during same rain event)

On each `WeatherService.OnWeatherUpdated`:
1. If condition is `Rain` or `Storm`:
   - If `rainStartTimeUtc` is null, set it to now
   - Else if 15+ min elapsed since `rainStartTimeUtc` AND no rain effect fired in this rain window (`lastRainEffectTimeUtc` is null or before `rainStartTimeUtc`), trigger rain effects and set `lastRainEffectTimeUtc = now`
2. If condition is NOT rain/storm, clear `rainStartTimeUtc`

## Rain Effects (triggered once per rain event)

1. **Fill all vases**: Set every vase to `Full` (`currentWater = capacity`), clear `fillStartTimeUtc`. Free any Mallums in `FetchingWater` state via `FreeMallumFromWater()`.
2. **Water all growing plots**: For each `Growing` plot where 6+ hours have passed since `lastWateredUtc` (or it's null), increment `waterCount` and set `lastWateredUtc = now`. No Water currency spent.

## Watering Cooldowns

New field on `PlotSave`: `lastWateredUtc`.

- **Manual watering** (2h cooldown): `PlotManager.Water()` checks `lastWateredUtc`. If < 2h ago, reject. Otherwise spend 1 Water, increment `waterCount`, set `lastWateredUtc`.
- **Rain watering** (6h cooldown): Same `lastWateredUtc` field, 6h threshold. Free (no Water currency).
- Shared timestamp means any watering resets the clock for both sources.

## Constants (code, not serialized)

- `RainTriggerMinutes = 15`
- `ManualWaterCooldownHours = 2`
- `RainWaterCooldownHours = 6`

## Data Changes

**SaveData**: add `string rainStartTimeUtc`, `string lastRainEffectTimeUtc`
**PlotSave**: add `string lastWateredUtc`

## Decisions

- Rain bypasses Mallum requirement (free water from the sky)
- Rain fills mid-fill vases and frees the assigned Mallum
- Rain waterings are free (no Water currency spent)
- Manual and rain cooldowns share a single `lastWateredUtc` timestamp
