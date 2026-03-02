# Apotheke UI Remake Design

## Problem

The Apotheke panel is a flat list of seeds and recipes with no organization. Seeds don't show their growth recipes (the new recipe system from the seed recipe redesign). The "Craft" bottom nav name is confusing since it currently handles building placement, not crafting/mixing.

## Solution

Apotheke gets a tab bar (Inventory | Craft). The current "Craft" bottom nav becomes "Build". Seeds show expandable cards with growth recipe details and harvest item info.

## Inventory Tab

Lists all seeds as expandable cards. Collapsed state shows:
- Seed name, count, harvest item name, harvest item sprite

Expanded state (on tap) reveals active growth recipe dimensions:
- Only dimensions where `use*` is true are shown
- Each dimension shows its ideal range (e.g. "Heat: 15-25°C")
- Weight shown if != 1.0 (e.g. "Rain: 30-80% (x1.5)")

SeedData is loaded from `Resources.LoadAll<SeedData>("Seeds")` to access the `recipe` field for display.

## Craft Tab

Shows mixing recipes (currently just Fertilizer). Same layout as current recipe list: recipe name, ingredients with quantities from inventory, result, Mix button. Reuses existing RecipeCard template and ApothekeManager.Mix() logic.

## Renaming: Craft → Build

- Bottom nav button text: "Craft" → "Build"
- UXML element IDs: `btn-craft` → `btn-build`, `craft-panel` → `build-panel`, `craft-list` → `build-list`
- C# class: `CraftUI` → `BuildUI`
- USS file: `Craft.uss` → `Build.uss` (class names: `.craft-*` → `.build-*`)
- `BottomNavUI`: `OnCraftClicked` → `OnBuildClicked`
- `CampFireUI`: `craft` → `build`, wire `OnBuildClicked`

## Tab Bar

Reuses the `.letters-tab` style pattern from the Letters panel for visual consistency. Active tab gets `.letters-tab--active` class.

## UXML Structure

```xml
#apotheke-panel
  #apotheke-tabs  (.letters-tab style)
    Button#tab-inventory "Inventory" (.letters-tab .letters-tab--active)
    Button#tab-craft "Craft" (.letters-tab)
  #apotheke-inventory
    ScrollView#seed-list (expandable seed cards)
  #apotheke-craft
    ScrollView#recipe-list (recipe cards with Mix buttons)
```

## Expanded Seed Card Layout

```
.seed-card
  .seed-card-header (row — always visible, clickable)
    .seed-icon (sprite)
    .seed-info (column)
      .seed-name "Fern"
      .seed-count "x3"
    .seed-outcome (column, right side)
      .seed-outcome-name "Fern Harvest"
  .seed-card-details (column — shown only when expanded)
    .seed-recipe-row "Heat: 15-25°C"
    .seed-recipe-row "Humidity: 60-90%"
    .seed-recipe-row "Rain: 30-80% (x1.5)"
    .seed-recipe-row "Waterings: 2"
```

## Files Changed

- `Assets/UI/Documents/CampFireRoot.uxml` — tab bar in apotheke-panel, rename craft→build
- `Assets/Scripts/UI/ApothekeUI.cs` — rewrite: tab switching, expandable cards, recipe display
- `Assets/UI/Styles/Apotheke.uss` — tab reuse, expandable card styles, recipe dimension styles
- `Assets/Scripts/UI/BottomNavUI.cs` — rename OnCraftClicked → OnBuildClicked, btn-craft → btn-build
- `Assets/Scripts/UI/CampFireUI.cs` — rename craft → build references
- `Assets/Scripts/UI/CraftUI.cs` → rename to `Assets/Scripts/UI/BuildUI.cs`
- `Assets/UI/Styles/Craft.uss` → rename to `Assets/UI/Styles/Build.uss`
- `Assets/Resources/UI/Templates/SeedCard.uxml` — new expandable layout
