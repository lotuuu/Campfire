# Hearth Isometric View Design

**Date:** 2026-02-24
**Status:** Approved

## Summary

Replace the current CSS-only rotated-square hearth visualization with real isometric voxel tile sprites (voxelTile_55) rendered in Unity world space. Transparent UI Toolkit buttons overlay the tiles for interaction. The grid grows dynamically as the player unlocks additional farm slots.

## Architecture

### New: `HearthIsometricView` MonoBehaviour

Lives on a new "HearthIso" GameObject in the scene, sorted between `LivingCanvas` and `WeatherOverlay`.

**Responsibilities:**
- Spawns SpriteRenderer tile GameObjects (voxelTile_55) for each currently-unlocked slot
- Arranges tiles in an isometric east-row: each successive tile offset by `+(tileWidth/2)` X, `-(tileHeight/4)` Y in world space
- Each tile has a child SpriteRenderer (Unity built-in circle sprite) for the plant visual
- Exposes `Vector2 GetTileScreenCenter(int slotIndex)` (world → screen pixels)
- Exposes `void SetPlantVisual(int slotIndex, PlantState state, Color color)`
- Subscribes to `EnvironmentManager.OnSlotUnlocked` → spawns new tile at runtime

**SerializeFields:**
- `Sprite tileSprite` → voxelTile_55
- `string sortingLayerName` (default "Default")
- `int baseSortingOrder` (tiles render behind plant visuals)
- `Vector3 gridOrigin` (world-space anchor, centered on screen)

### Updated: `HearthViewUI`

**Changes:**
- Add `[SerializeField] HearthIsometricView isometricView`
- Drop `const int SlotCount = 2`; all slot arrays become `List<>`
- `Initialize()` reads `EnvironmentManager.GetActiveSlotCount(HearthEnvIndex)`, creates that many overlay buttons
- Each `Update()` re-projects `isometricView.GetTileScreenCenter(i)` → UI panel space and repositions buttons (handles screen resize / layout changes)
- Subscribes to `EnvironmentManager.OnSlotUnlocked` → adds a new button for the new slot
- Delegates plant visual updates to `isometricView.SetPlantVisual()` instead of manipulating UXML swatches

### UXML / USS

**GardenRoot.uxml:**
- Remove the static `hearth-plot` div and its 2 hardcoded `hearth-slot-*` buttons
- `hearth-view` becomes a full-fill absolute container (transparent), overlay buttons added at runtime by `HearthViewUI`

**Hearth.uss:**
- Remove: `#hearth-plot`, `.hearth-slot`, `.hearth-slot-inner`, `.hearth-soil`, `.hearth-plant-swatch`
- Keep: `#hearth-view`, `#hearth-title`, `.hearth-slot-label`, `.hearth-progress-bar`, `.hearth-progress-fill`, `.hearth-slot-mature`
- Add: `.hearth-slot-overlay` — absolute positioned, transparent background, no border by default

## Isometric Layout

```
Tile index:  0          1          2
World pos:  (0, 0)    (+W/2, -H/4)  (+W, -H/2)
```

Where W = sprite pixel width / PPU, H = sprite pixel height / PPU.
Tiles are centered around `gridOrigin` (shifted left by `(n-1) * W/4` so the group stays centered as slots are added).

## Plant Visual

- Empty: tile sprite only, plant child inactive
- Growing: plant child active, colored by `variant.primaryColor`, scale 0.4× tile width
- Mature: same + pulsing scale animation (driven in `HearthViewUI.Update`)

## Sorting

| Layer | Object |
|-------|--------|
| Background | LivingCanvas |
| Default (order 0) | Isometric tiles (voxelTile_55) |
| Default (order 1) | Plant visual discs |
| Overlay | WeatherOverlay particles |
| UI | UIDocument (always on top) |

## Files Touched

| File | Change |
|------|--------|
| `Assets/Scripts/UI/HearthIsometricView.cs` | **New** |
| `Assets/Scripts/UI/HearthViewUI.cs` | Updated |
| `Assets/UI/Documents/GardenRoot.uxml` | Remove static hearth-plot |
| `Assets/UI/Styles/Hearth.uss` | Remove obsolete rules, add overlay class |
| Scene `SampleScene.unity` | Add HearthIso GameObject, wire HearthViewUI ref |
