# Construction Tab Design
**Date:** 2026-02-24
**Status:** Approved

## Summary

Add a "Construction" tab (replacing the locked placeholder tab) that lets players unlock new farm locations and expand existing ones. Unifies all farm growth purchases under one tab. Also redesigns the currency economy so Dewdrops fund construction and AuraDust funds seeds.

---

## Economy Changes

| Currency | Before | After |
|---|---|---|
| Dewdrops | Seeds + slot unlocks | All construction (slots, environments, greenhouse) |
| AuraDust | Unused spend mechanic | Seeds in the Shop tab |
| SunShards | Greenhouse expansion | Reserved for future premium/IAP — no spend mechanic |

**Specific changes:**
- `SeedShopManager.BuySeed()`: `CurrencyType.Dewdrops` → `CurrencyType.AuraDust`
- `GreenhouseManager.ExpandSlots()`: `CurrencyType.SunShards` → `CurrencyType.Dewdrops`
- `CurrencyConfig.slotCostSunShards` renamed to `greenhouseExpandCostDewdrops`
- Greenhouse expand button removed from `GreenhouseUI`, moved to Construction tab

---

## Construction Tab

### Tab Wiring
- `locked-page` / `tab-locked` in GardenRoot.uxml renamed to `construction-page` / `tab-construction`
- Tab re-enabled in `BottomNavUI`
- `HortusUI` wires up `ConstructionUI.Show()` on page change to index 4

### Layout

Scrollable list of location cards, Greenhouse card pinned last.

```
┌─────────────────────────────────────┐
│  Construction                       │
│─────────────────────────────────────│
│  ┌─────────────────────────────┐    │
│  │ Sunny Windowsill        ✓   │    │
│  │ Slots: ██████░░  3 / 4     │    │
│  │  [+ Add Slot  💧 500]       │    │
│  └─────────────────────────────┘    │
│                                     │
│  ┌─────────────────────────────┐    │
│  │ Misty Greenhouse        🔒  │    │
│  │ Unlock for 💧 1200          │    │
│  │  [Unlock Location]          │    │
│  └─────────────────────────────┘    │
│                                     │
│  ┌─────────────────────────────┐    │
│  │ Greenhouse Storage          │    │
│  │ Capacity: 6 slots           │    │
│  │  [+ Expand  💧 300]         │    │
│  └─────────────────────────────┘    │
└─────────────────────────────────────┘
```

### Card States

| State | Condition | Display |
|---|---|---|
| **Unlocked** | In `unlockedEnvironments` | Slot progress + upgrade buttons |
| **Locked-visible** | Previous environment fully upgraded (all slots at max) | Single "Unlock" button with cost |
| **Hidden** | Not yet revealed by progression gate | Card not added to DOM |

The Greenhouse card is always visible (no unlock requirement).

### Progression Gate

A locked environment card becomes **visible** (locked-visible state) only after all slots in the previous environment have been purchased. This gates information and creates natural progression pacing.

---

## New Files

| File | Purpose |
|---|---|
| `Assets/Scripts/UI/ConstructionUI.cs` | Tab controller, Initialize(root) pattern |
| `Assets/Resources/UI/Templates/ConstructionLocationCard.uxml` | Card template |
| `Assets/Resources/UI/Templates/ConstructionUpgradeButton.uxml` | Per-upgrade button row |

## Modified Files

| File | Change |
|---|---|
| `Assets/UI/Documents/GardenRoot.uxml` | Rename locked → construction page/tab |
| `Assets/Scripts/UI/HortusUI.cs` | Wire ConstructionUI, Show() on page 4 |
| `Assets/Scripts/UI/BottomNavUI.cs` | Enable construction tab |
| `Assets/Scripts/Managers/GreenhouseManager.cs` | SunShards → Dewdrops in ExpandSlots() |
| `Assets/Scripts/UI/GreenhouseUI.cs` | Remove expand button |
| `Assets/Scripts/Managers/SeedShopManager.cs` | Dewdrops → AuraDust in BuySeed() |
| `Assets/Scripts/UI/SeedShopUI.cs` | Update cost label currency symbol |
| `Assets/Scripts/Data/CurrencyConfig.cs` | Rename slotCostSunShards → greenhouseExpandCostDewdrops |
| `Assets/Resources/Config/CurrencyConfig.asset` | Update serialized field |

---

## ConstructionUI Logic

```
Show()
  → Load all EnvironmentData assets (already in EnvironmentManager)
  → For each environment in order:
      if unlocked → render card with upgrade buttons
      else if previous env fully upgraded → render locked-visible card
      else → skip (hidden)
  → Append Greenhouse card (always)
  → Subscribe CurrencyManager.OnCurrencyChanged → RefreshDisplay()

OnUpgradeClicked(envIndex, upgradeType)
  → EnvironmentManager.UnlockSlot(envIndex)   // for slot upgrade
  → RefreshDisplay()

OnUnlockLocationClicked(envIndex)
  → EnvironmentManager.UnlockEnvironment(envIndex)  // spend Dewdrops, add to unlockedEnvironments
  → RefreshDisplay()

OnGreenhouseExpandClicked()
  → GreenhouseManager.ExpandSlots()
  → RefreshDisplay()
```

## Upgrade Extensibility

Cards are designed to host multiple upgrade types per location. Currently only `SlotCount` is implemented. Future upgrades (dirt quality, fertilizer) can be added by:
1. Adding fields to `EnvironmentData`
2. Adding upgrade type enum values
3. Cloning additional `ConstructionUpgradeButton` rows per card

No structural changes to `ConstructionUI` or UXML templates required.
