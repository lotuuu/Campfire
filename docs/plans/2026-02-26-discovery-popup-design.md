# Discovery Popup Design

**Date:** 2026-02-26
**Feature:** Special popup for new plant variant discoveries

## Overview

When a player harvests a plant whose variant they have never grown before, replace the normal harvest result popup with a dramatic "Discovery" popup that celebrates the first encounter.

## Trigger Condition

- Check happens in `PlantManager.Harvest()` before firing any UI event
- A discovery is new if the variant name is **not** in `SaveData.discoveredVariants` at the moment of harvest
- Mark the variant as discovered (add to `discoveredVariants`, save) immediately — before showing the popup
- Normal harvests (already-discovered variants) continue to use `HarvestResultUI` unchanged

## Event Flow

```
PlantManager.Harvest()
  ├─ variant already discovered → raise OnHarvestComplete(result) → HarvestResultUI.Show()
  └─ variant NEW → add to discoveredVariants → raise OnNewVariantDiscovered(variant, result) → DiscoveryPopupUI.Show()
```

`HortusUI` owns both subscriptions and routes accordingly. The two popups are mutually exclusive.

## UI Structure

Full-screen overlay (position absolute, 0/0/0/0). Tap anywhere on the scrim dismisses. Template cloned from `Resources/UI/Templates/DiscoveryPopup.uxml`.

```
discovery-overlay        (full-screen dark scrim, rgba 0,0,0,0.85)
  discovery-card         (centered card, rounded, dark surface)
    glow-bg              (radial gradient, color = variant.primaryColor, pulsing)
    sprite-container     (large square, variant sprite as background-image)
    variant-name         (large bold label)
    rarity-label         (small caps, rarity color class)
    divider              (horizontal rule, animates width)
    variant-description  (body text)
    actions-row
      share-button       ("Share Discovery ↑")
      dismiss-hint       ("Tap anywhere to continue")
```

## Animation Sequence (CSS keyframes)

| Element | Delay | Effect |
|---|---|---|
| Scrim | 0s | fade in 0.3s |
| Card | 0.1s | scale 0.85→1.0 + fade, bounce easing |
| Sprite | 0.2s | scale 0.7→1.0 + fade, bounce easing |
| Glow | 0.2s | opacity pulse loop (0.4→0.8) |
| Name | 0.5s | translate Y +16px→0 + fade, 0.3s |
| Rarity + divider | 0.7s | divider width 0%→100%, rarity fade |
| Description | 1.0s | fade in 0.4s |
| Share + dismiss hint | 1.5s | fade in |

All elements use `animation-fill-mode: both` (start invisible, hold final state).
Bounce easing: `cubic-bezier(0.34, 1.56, 0.64, 1)`.
Variant `primaryColor` applied to `glow-bg` via inline style from C# after template clone.

## Share Button

- On click: `ScreenCapture.CaptureScreenshotAsTexture()` → crop to card `worldBound` → `new NativeShare().AddFile(texture).Share()`
- Dependency: **NativeShare** (yasirkula/UnityNativeShare, MIT license) — added via UPM or manual import
- Share text: `"I just discovered {variantName} in Garden! 🌱"`

## Files

| Action | Path |
|---|---|
| New | `Assets/Scripts/UI/DiscoveryPopupUI.cs` |
| New | `Assets/Resources/UI/Templates/DiscoveryPopup.uxml` |
| New | `Assets/UI/Styles/DiscoveryPopup.uss` |
| Modify | `Assets/Scripts/Managers/PlantManager.cs` |
| Modify | `Assets/Scripts/UI/HortusUI.cs` |
| Modify | `Assets/UI/Documents/GardenRoot.uxml` |

## Dependencies

- NativeShare (yasirkula/UnityNativeShare) — must be installed before the share button is wired up
