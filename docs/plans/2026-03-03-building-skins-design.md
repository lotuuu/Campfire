# Building Skins Design

## Overview

Players can paint individual buildings (plots, vases, mallum houses) with plant-themed color skins. Skins are purchased per building instance using pigment items crafted in the Apotheke.

## Economy Flow

```
Harvest 2x [Plant] --> Apotheke: mix [Plant] Pigment recipe --> 1x [Plant]_pigment
[Plant]_pigment --> Skin Selector on building --> consumes 1 pigment --> building painted
```

- 12 pigment recipes (one per seed type), each costs 2 of that plant's harvest
- 36 skin definitions total: 12 seeds x 3 building types
- Skins are consumed per instance (each building needs its own pigment)

## Data Model

### SkinData (new ScriptableObject)

```csharp
public class SkinData : ScriptableObject
{
    public string skinName;           // e.g. "Basil Plot"
    public CampBuildingType buildingType; // Plot, Vase, MallumHouse
    public Color hexFillColor;        // overrides --hex-fill
    public Color hexBorderColor;      // overrides --hex-border
    public string costItemName;       // e.g. "Basil_pigment"
    public int costQuantity = 1;
}
```

Assets at `Assets/Resources/Skins/{Plot,Vase,MallumHouse}/*.asset`.

### RecipeData (pigment recipes)

12 new RecipeData assets at `Assets/Resources/Recipes/`:
- `Basil_Pigment.asset`: ingredients = [{Basil_harvest, 2}], result = "Basil_pigment"
- Same pattern for all 12 seeds

### Save Data Changes

Add `skinName` field (string, nullable) to:
- `PlotSave`
- `VaseSave`
- `MallumHouseSave`

When set, it references a `SkinData` asset name. When null/empty, default USS colors apply.

## Color Palette

Plant-inspired fill/border colors (all at 0.6 fill alpha, 0.3 border alpha to match existing style):

| Plant | Fill Color | Border Color |
|-------|-----------|--------------|
| Basil | rgba(50, 120, 50, 0.6) | rgba(80, 180, 80, 0.3) |
| Chamomile | rgba(230, 210, 130, 0.6) | rgba(255, 240, 170, 0.3) |
| Dahlia | rgba(180, 50, 80, 0.6) | rgba(220, 90, 120, 0.3) |
| Jasmine | rgba(240, 235, 210, 0.6) | rgba(255, 250, 230, 0.3) |
| Lavender | rgba(140, 100, 180, 0.6) | rgba(180, 140, 220, 0.3) |
| Marigold | rgba(220, 150, 30, 0.6) | rgba(255, 190, 60, 0.3) |
| Mint | rgba(60, 170, 130, 0.6) | rgba(100, 210, 170, 0.3) |
| Moonflower | rgba(80, 70, 140, 0.6) | rgba(130, 120, 200, 0.3) |
| Pansy | rgba(120, 60, 160, 0.6) | rgba(170, 100, 210, 0.3) |
| Poppy | rgba(200, 60, 40, 0.6) | rgba(240, 100, 70, 0.3) |
| Rosemary | rgba(70, 110, 80, 0.6) | rgba(110, 160, 120, 0.3) |
| Snowdrop | rgba(210, 225, 240, 0.6) | rgba(230, 240, 250, 0.3) |

Same colors used for all 3 building types of a given plant.

## Rendering

In `CampsiteViewUI.PopulateOccupiedCell()`, after adding the USS building class, check if the save entry has a `skinName`. If set, load the corresponding `SkinData` and apply inline style overrides:

```csharp
if (!string.IsNullOrEmpty(save.skinName))
{
    var skin = SkinManager.Instance.GetSkin(save.skinName);
    if (skin != null)
    {
        cell.style.SetCustomProperty("--hex-fill", skin.hexFillColor);
        cell.style.SetCustomProperty("--hex-border", skin.hexBorderColor);
    }
}
```

Since USS custom properties can't be set via inline style directly, we'll instead set them via `cell.style.unityBackgroundColor` or, more practically, store the colors on the element's userData and read them in `DrawHexCell`. The approach: if `userData` contains a `(Color fill, Color border)` tuple, use those instead of the USS custom property values.

## SkinManager (new singleton)

Loads all `SkinData` from `Resources/Skins/` at Awake. Provides:
- `GetSkinsForBuilding(CampBuildingType type)` - list of all skins for a building type
- `GetSkin(string skinName)` - lookup by name
- `CanAffordSkin(SkinData skin)` - checks inventory for pigment
- `ApplySkin(CampBuildingType type, int index, SkinData skin)` - consumes pigment, sets skinName on save entry
- `RemoveSkin(CampBuildingType type, int index)` - clears skinName (free, returns nothing)

## UI: Skin Selector Popup

Triggered by a "Paint" button in the building interaction panel (ShowPlotInteraction, ShowVaseInteraction, ShowMallumHouseInteraction).

### Layout

```
+---------------------------------------------+
|              Paint Plot                       |
|                                               |
|  [ Basil ] [ Chamomile ] [ Dahlia ] ...      |  <-- horizontal scroll
|                                               |
|  +---------------------------------------+   |
|  |     [Hex color preview swatch]        |   |
|  |     "Basil"                           |   |
|  |     Cost: 1x Basil Pigment (have: 3) |   |
|  +---------------------------------------+   |
|                                               |
|  [   Paint (1x Basil Pigment)   ]            |  <-- purchase button
|  [         Remove Skin          ]            |  <-- if already skinned
|  [           Close              ]            |
+---------------------------------------------+
```

- Carousel: horizontal ScrollView with skin swatches (colored hex thumbnails)
- Selecting a swatch updates the detail area and purchase button
- Purchase button disabled if can't afford
- "Remove Skin" button shown only if building currently has a skin

### Template

New UXML template: `Assets/Resources/UI/Templates/SkinSelector.uxml`

### Controller

New `SkinSelectorUI` MonoBehaviour (or method group within CampsiteViewUI) that:
- Receives building type + index
- Populates carousel from SkinManager
- Handles purchase flow (consume pigment -> set skin -> rebuild grid)

Given this is a popup within the existing interaction panel system, it will be implemented as a method in CampsiteViewUI that replaces the interaction panel content, similar to ShowPlotInteraction etc.

## Files to Create

- `Assets/Scripts/Data/SkinData.cs` - ScriptableObject definition
- `Assets/Scripts/Managers/SkinManager.cs` - singleton manager
- `Assets/Resources/Skins/Plot/*.asset` - 12 plot skin assets
- `Assets/Resources/Skins/Vase/*.asset` - 12 vase skin assets
- `Assets/Resources/Skins/MallumHouse/*.asset` - 12 mallum house skin assets
- `Assets/Resources/Recipes/Basil_Pigment.asset` (and 11 more) - pigment recipes
- `Assets/Resources/UI/Templates/SkinSelector.uxml` - skin selector template

## Files to Modify

- `Assets/Scripts/Data/SaveData.cs` - add skinName to PlotSave, VaseSave, MallumHouseSave
- `Assets/Scripts/UI/CampsiteViewUI.cs` - skin color overrides in rendering, "Paint" button in interactions, skin selector popup
- `Assets/UI/Styles/CampsiteGrid.uss` - possible skin-related styles (minimal)
