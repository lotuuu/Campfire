# Environment Switcher Design

**Date:** 2026-02-25
**Status:** Approved

## Problem

Purchased environments (The Balcony, The Wild Patch, Deep Conservatory) are accessible in the Construction tab for purchasing and slot upgrades, but there is no way to navigate to them from the main gameplay view. `BackyardViewUI` and `BackyardIsometricView` are hardcoded to environment index 0.

## Solution

Re-tapping the terrarium (middle) tab while already on it toggles a secondary environment switcher bar that slides up from above the main nav. Each unlocked environment appears as a pill button. Tapping a pill switches the active environment and dismisses the bar.

## Architecture

### Data Layer

- **`EnvironmentData.cs`**: Add `public Sprite tileSprite` field for data-driven tile rendering.
- **`SaveData`**: Add `public int activeEnvironmentIndex` (defaults to 0).
- **`EnvironmentManager`**:
  - Add `public int ActiveEnvironmentIndex { get; private set; }`
  - Add `public event Action<int> OnActiveEnvironmentChanged`
  - Add `SetActiveEnvironment(int index)` — validates env is unlocked, updates `SaveData.activeEnvironmentIndex`, saves, fires event.
  - On load, restore `ActiveEnvironmentIndex` from save; validate still unlocked, else fall back to 0.

### Rendering

- **`BackyardIsometricView`**: Remove hardcoded `tileSprite` Inspector field and `BackyardEnvIndex = 0` constant. Add `SetEnvironment(int envIndex)` that reads `EnvironmentManager.Environments[envIndex].tileSprite` and calls `RebuildGrid`. Subscribe to `EnvironmentManager.OnActiveEnvironmentChanged`.
- **`BackyardViewUI`**: Remove `BackyardEnvIndex = 0` constant. Subscribe to `OnActiveEnvironmentChanged`, rebuild slot buttons for the new env index on change.

### Switcher Bar UI

- **New `EnvironmentSwitcherBar.cs`**: Standalone MonoBehaviour controller initialized by `HortusUI` with the `#env-switcher-bar` root element.
  - `Show()` / `Hide()` methods with CSS translate slide animation (~250ms ease-out).
  - Builds one pill button per unlocked environment on `Show()`.
  - Marks the active env pill with `env-pill-active` USS class.
  - Refreshes pill list when `EnvironmentManager.OnEnvironmentUnlocked` fires.
  - Fires `OnEnvironmentSelected(int index)` when a pill is tapped.
- **`BottomNavUI`**: Track currently active tab index. If terrarium tab (index 2) is tapped while already active, fire `OnTerrariumReactivated` event instead of calling `GoToPage`. No behavior change for all other tabs.
- **`HortusUI`**:
  - Subscribe to `BottomNavUI.OnTerrariumReactivated` → toggle the switcher bar (only if >1 env unlocked).
  - Subscribe to `EnvironmentSwitcherBar.OnEnvironmentSelected` → call `EnvironmentManager.SetActiveEnvironment(index)`, hide bar.
  - On `pageView.OnPageChanged` — if navigating away from terrarium page, auto-hide the bar.

### UXML / USS

- **`GardenRoot.uxml`**: Add `#env-switcher-bar` as a sibling directly above `#bottom-nav` inside the nav container. Starts hidden.
- **USS**: Add styles for `.env-switcher-bar`, `.env-pill`, `.env-pill-active`, and the slide-in/out transition.

### Asset Updates

- Assign `tileSprite` on all 4 `EnvironmentData` assets: `Hearth.asset`, `Balcony.asset`, `WildPatch.asset`, `Conservatory.asset`.

## UX Behaviour

- Re-tap terrarium tab → bar slides up (only if >1 env unlocked; otherwise no-op).
- Tap env pill → switch active env, bar slides down.
- Re-tap terrarium tab while bar is open → bar slides down (no env change).
- Navigate to any other page → bar auto-hides.
- Active env pill is visually distinct (filled style vs outlined).

## Persistence

Active environment index is saved to `SaveData.activeEnvironmentIndex`. On load, `EnvironmentManager.Awake()` restores it; if the saved index is out of range or the env is not unlocked, falls back to 0.

## Out of Scope

- Animated transition between environments in the iso view (grid rebuilds immediately).
- Per-environment plant prefab sets (all envs share the same plant prefab pool).
- Environment icons in the switcher bar (name labels only for now).
