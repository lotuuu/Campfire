# Design: One Environment-Scoped Consumable Per Environment

**Date**: 2026-02-26
**Status**: Approved

## Problem

Currently, an environment can have multiple different env-scoped consumables active simultaneously (e.g. Fan + Cloud on Backyard). The intended behaviour is one at a time per environment.

## Behaviour

- Each environment may have at most one env-scoped consumable active.
- Applying a second env consumable to an environment that already has one prompts for confirmation before replacing.
- On confirmation: the old consumable is discarded (no refund), the new one is applied.
- On cancel: nothing changes.
- Slot-scoped consumables (Fertilizer, QualityDirt) are unaffected.

## Changes

### 1. Logic — `ConsumableManager.ApplyToEnvironment()`

Change the `RemoveAll` guard from "same type for this env" to "any type for this env":

```csharp
// Before
envList.RemoveAll(e => e.envIndex == envIndex && e.consumableType == type.ToString());

// After
envList.RemoveAll(e => e.envIndex == envIndex);
```

No save format changes — `environmentConsumables` already supports at-most-one-per-env naturally.

### 2. Visual indicator — herb button active state

When the picker opens (or active env changes), call `GetEnvConsumables(ActiveEnv)`. If non-empty, add CSS class `.consumable-picker-btn--occupied` to the herb button. This class shows a small colored dot/ring. Remove the class when empty.

Style in `Backyard.uss`:
```uss
.consumable-picker-btn--occupied {
    border-width: 2px;
    border-color: var(--color-highlight);
    border-radius: 66px;
}
```
(or a pseudo-element dot — exact visual TBD in implementation)

### 3. Confirmation UX — inline in picker widget

When an env-scoped consumable is tapped and the current env already has one active:

1. Close the icon list
2. Show an inline confirmation row inside the picker:
   - Label: "Replace `[OldType]`?"
   - Confirm button → calls `ApplyToEnvironment()` + `SpawnEnvConsumableVisual()` + `ClearEnvConsumableVisuals()`
   - Cancel button → reverts picker to closed state
3. On either action, picker closes normally

This keeps the interaction contained to the picker widget — no modal overlay needed.

### 4. Visual — `BackyardIsometricView.SpawnEnvConsumableVisual()`

Change from "destroy same-type GO only" to "destroy all env-scoped GOs" before spawning:

```csharp
// Before: destroy only matching type
if (_envConsumableGOs.TryGetValue(type, out var existing)) { Destroy(existing); ... }

// After: destroy all env consumable GOs first
foreach (var kvp in _envConsumableGOs) if (kvp.Value) Destroy(kvp.Value);
_envConsumableGOs.Clear();
// then spawn new one
```

## Files Affected

- `Assets/Scripts/Managers/ConsumableManager.cs`
- `Assets/Scripts/UI/BackyardViewUI.cs`
- `Assets/Scripts/UI/BackyardIsometricView.cs`
- `Assets/UI/Styles/Backyard.uss`
