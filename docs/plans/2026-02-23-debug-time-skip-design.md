# Debug Time Skip Design

**Date:** 2026-02-23
**Status:** Approved

## Summary

Add a debug tool to skip time forward by a user-specified number of hours. Affects both plant growth and greenhouse dust payouts.

## Mechanism

- **Plant growth**: Shift `PlantManager.PlantTime` backwards by N hours. The existing `Update()` loop recalculates progress from `DateTime.UtcNow - PlantTime`, so no other changes needed.
- **Greenhouse dust**: Directly calculate and add the dust that would have accumulated over N hours based on current greenhouse contents.
- **Persistence**: Save state after both operations so the skip persists across sessions.

## UI

Add a row to the existing Debug panel (after preset buttons, before Apply):
- IntegerField labeled "Hours" (default 1, min 1)
- "Skip Time" button

## Files Changed

- `Assets/UI/Documents/GardenRoot.uxml` — Add debug-row with IntegerField + button
- `Assets/Scripts/Debug/DebugWeatherPanel.cs` — Wire Skip button
- `Assets/Scripts/Managers/PlantManager.cs` — Add `DebugAdvanceTime(float hours)`
- `Assets/Scripts/Managers/GreenhouseManager.cs` — Add `DebugAdvanceTime(float hours)`
