# Building Skins Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let players paint individual buildings (plots, vases, mallum houses) with plant-themed color skins purchased via pigment items.

**Architecture:** New `SkinData` ScriptableObject defines per-building-type skins with fill/border colors. `SkinManager` singleton loads all skins, handles purchase (consume pigment from inventory) and application (set `skinName` on save entry). Hex cell rendering checks `skinName` and overrides colors via `userData` on the VisualElement. Skin selector UI reuses the existing interaction panel pattern.

**Tech Stack:** Unity 6, UI Toolkit, ScriptableObjects, C#

---

### Task 1: SkinData ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/SkinData.cs`

**Step 1: Create SkinData.cs**

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSkin", menuName = "CampFire/Skin Data")]
    public class SkinData : ScriptableObject
    {
        public string skinName;
        public CampBuildingType buildingType;
        public Color hexFillColor;
        public Color hexBorderColor;
        public string costItemName;
        public int costQuantity = 1;
    }
}
```

**Step 2: Compile and check console**

Run: `read_console` to verify no compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/Data/SkinData.cs Assets/Scripts/Data/SkinData.cs.meta
git commit -m "feat: add SkinData ScriptableObject"
```

---

### Task 2: Add skinName to save data

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs` (PlotSave at line 54, VaseSave at line 40)
- Modify: `Assets/Scripts/Data/MallumHouseSave.cs` (line 9)

**Step 1: Add skinName field to PlotSave**

In `SaveData.cs`, add after `public bool subscribeWater;` (line 54):

```csharp
        public string skinName;
```

**Step 2: Add skinName field to VaseSave**

In `SaveData.cs`, add after `public int gridY;` (line 40, inside VaseSave):

```csharp
        public string skinName;
```

**Step 3: Add skinName field to MallumHouseSave**

In `MallumHouseSave.cs`, add after `public int gridY;` (line 9):

```csharp
        public string skinName;
```

**Step 4: Compile and check console**

Run: `read_console` to verify no compilation errors.

**Step 5: Commit**

```
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/MallumHouseSave.cs
git commit -m "feat: add skinName field to PlotSave, VaseSave, MallumHouseSave"
```

---

### Task 3: SkinManager singleton

**Files:**
- Create: `Assets/Scripts/Managers/SkinManager.cs`

**Step 1: Create SkinManager.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Garden
{
    public class SkinManager : MonoBehaviour
    {
        public static SkinManager Instance { get; private set; }

        private SkinData[] allSkins;
        private Dictionary<string, SkinData> skinLookup;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allSkins = Resources.LoadAll<SkinData>("Skins");
            skinLookup = new Dictionary<string, SkinData>();
            foreach (var skin in allSkins)
                skinLookup[skin.skinName] = skin;
        }

        public SkinData GetSkin(string skinName)
        {
            if (string.IsNullOrEmpty(skinName)) return null;
            skinLookup.TryGetValue(skinName, out var skin);
            return skin;
        }

        public List<SkinData> GetSkinsForBuilding(CampBuildingType type)
        {
            return allSkins.Where(s => s.buildingType == type).ToList();
        }

        public bool CanAffordSkin(SkinData skin)
        {
            var items = SaveManager.Instance.Data.items;
            var item = items.Find(i => i.itemName == skin.costItemName);
            return item != null && item.count >= skin.costQuantity;
        }

        public bool ApplySkin(CampBuildingType type, int index, SkinData skin)
        {
            if (!CanAffordSkin(skin)) return false;

            var data = SaveManager.Instance.Data;
            var item = data.items.Find(i => i.itemName == skin.costItemName);
            item.count -= skin.costQuantity;
            if (item.count <= 0) data.items.Remove(item);

            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].skinName = skin.skinName;
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].skinName = skin.skinName;
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].skinName = skin.skinName;
                    break;
            }

            SaveManager.Instance.Save();
            return true;
        }

        public void RemoveSkin(CampBuildingType type, int index)
        {
            var data = SaveManager.Instance.Data;
            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].skinName = null;
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].skinName = null;
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].skinName = null;
                    break;
            }
            SaveManager.Instance.Save();
        }
    }
}
```

**Step 2: Add SkinManager to the scene**

Add `SkinManager` component to the `"--- Managers ---"` or equivalent manager GameObject in the scene. (Check the scene hierarchy first for the correct GameObject name.)

**Step 3: Compile and check console**

Run: `read_console` to verify no compilation errors.

**Step 4: Commit**

```
git add Assets/Scripts/Managers/SkinManager.cs Assets/Scripts/Managers/SkinManager.cs.meta
git commit -m "feat: add SkinManager singleton for skin loading and application"
```

---

### Task 4: Skin color rendering in hex cells

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

This task makes skinned buildings render with their custom colors. The approach: store `(Color, Color)` on the cell's `userData` in `PopulateOccupiedCell`, then read it in `DrawHexCell`.

**Step 1: Modify DrawHexCell to check userData**

In `CampsiteViewUI.cs`, find `DrawHexCell` (line 834). Replace the color resolution block (lines 841-844):

```csharp
            Color fillColor = new Color(0.16f, 0.1f, 0.05f, 0.3f);
            Color borderColor = new Color(0.55f, 0.39f, 0.2f, 0.15f);
            el.customStyle.TryGetValue(s_HexFill, out fillColor);
            el.customStyle.TryGetValue(s_HexBorder, out borderColor);
```

with:

```csharp
            Color fillColor = new Color(0.16f, 0.1f, 0.05f, 0.3f);
            Color borderColor = new Color(0.55f, 0.39f, 0.2f, 0.15f);
            if (el.userData is (Color skinFill, Color skinBorder))
            {
                fillColor = skinFill;
                borderColor = skinBorder;
            }
            else
            {
                el.customStyle.TryGetValue(s_HexFill, out fillColor);
                el.customStyle.TryGetValue(s_HexBorder, out borderColor);
            }
```

**Step 2: Add skin color override helper method**

Add a private method after `DrawHexCell`:

```csharp
        private static void ApplySkinColors(VisualElement cell, string skinName)
        {
            if (string.IsNullOrEmpty(skinName) || SkinManager.Instance == null) return;
            var skin = SkinManager.Instance.GetSkin(skinName);
            if (skin != null)
                cell.userData = (skin.hexFillColor, skin.hexBorderColor);
        }
```

**Step 3: Call ApplySkinColors in PopulateOccupiedCell**

In `PopulateOccupiedCell`, add `ApplySkinColors` calls after the `AddToClassList` lines for Plot, Vase, and MallumHouse:

For Plot (after line 359 `cell.AddToClassList("grid-cell--plot");`):
```csharp
                    ApplySkinColors(cell, SaveManager.Instance.Data.plots[index].skinName);
```

For Vase (after line 373 `cell.AddToClassList("grid-cell--vase");`):
```csharp
                    ApplySkinColors(cell, SaveManager.Instance.Data.vases[index].skinName);
```

For MallumHouse (after line 398 `cell.AddToClassList("grid-cell--mallum-house");`):
```csharp
                    ApplySkinColors(cell, SaveManager.Instance.Data.mallumHouses[index].skinName);
```

**Step 4: Compile and check console**

Run: `read_console` to verify no compilation errors.

**Step 5: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: render skin colors on hex cells via userData override"
```

---

### Task 5: Skin selector UI in interaction panel

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

Add a `ShowSkinSelector` method and "Paint" buttons to each building interaction.

**Step 1: Add ShowSkinSelector method**

Add this method near the other ShowXxxInteraction methods (after `ShowMallumHouseInteraction`):

```csharp
        private void ShowSkinSelector(CampBuildingType type, int index)
        {
            if (SkinManager.Instance == null) return;
            interactionBody.Clear();
            interactionActions.Clear();

            string typeName = type switch
            {
                CampBuildingType.Plot => "Plot",
                CampBuildingType.Vase => "Vase",
                CampBuildingType.MallumHouse => "Mallum House",
                _ => "Building"
            };
            interactionTitle.text = $"Paint {typeName}";

            var skins = SkinManager.Instance.GetSkinsForBuilding(type);
            if (skins.Count == 0)
            {
                var noSkins = new Label("No skins available");
                noSkins.AddToClassList("interaction-info");
                interactionBody.Add(noSkins);
                AddCloseButton();
                return;
            }

            // Get current skin name
            string currentSkin = type switch
            {
                CampBuildingType.Plot => SaveManager.Instance.Data.plots[index].skinName,
                CampBuildingType.Vase => SaveManager.Instance.Data.vases[index].skinName,
                CampBuildingType.MallumHouse => SaveManager.Instance.Data.mallumHouses[index].skinName,
                _ => null
            };

            // Carousel container (horizontal scroll)
            var carousel = new ScrollView(ScrollViewMode.Horizontal);
            carousel.AddToClassList("skin-carousel");
            var carouselContent = carousel.contentContainer;
            carouselContent.style.flexDirection = FlexDirection.Row;

            // Detail area (updated when a swatch is selected)
            var detailArea = new VisualElement();
            detailArea.AddToClassList("skin-detail");

            // Track selected skin
            SkinData selectedSkin = null;
            VisualElement selectedSwatch = null;

            foreach (var skin in skins)
            {
                var swatch = new VisualElement();
                swatch.AddToClassList("skin-swatch");

                // Mini hex color preview
                var preview = new VisualElement();
                preview.AddToClassList("skin-swatch-preview");
                preview.style.backgroundColor = skin.hexFillColor;
                preview.style.borderTopColor = skin.hexBorderColor;
                preview.style.borderBottomColor = skin.hexBorderColor;
                preview.style.borderLeftColor = skin.hexBorderColor;
                preview.style.borderRightColor = skin.hexBorderColor;
                swatch.Add(preview);

                var swatchLabel = new Label(skin.skinName.Replace("_", " "));
                swatchLabel.AddToClassList("skin-swatch-label");
                swatch.Add(swatchLabel);

                bool isEquipped = skin.skinName == currentSkin;
                if (isEquipped)
                    swatch.AddToClassList("skin-swatch--equipped");

                var capturedSkin = skin;
                var capturedSwatch = swatch;
                swatch.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    // Deselect previous
                    selectedSwatch?.RemoveFromClassList("skin-swatch--selected");
                    // Select new
                    capturedSwatch.AddToClassList("skin-swatch--selected");
                    selectedSwatch = capturedSwatch;
                    selectedSkin = capturedSkin;
                    UpdateSkinDetail(detailArea, capturedSkin, type, index, currentSkin);
                });

                carouselContent.Add(swatch);
            }

            interactionBody.Add(carousel);
            interactionBody.Add(detailArea);

            // Select first skin by default
            if (skins.Count > 0)
            {
                selectedSkin = skins[0];
                selectedSwatch = carouselContent[0];
                selectedSwatch.AddToClassList("skin-swatch--selected");
                UpdateSkinDetail(detailArea, skins[0], type, index, currentSkin);
            }

            // Remove skin button (if currently skinned)
            if (!string.IsNullOrEmpty(currentSkin))
            {
                var removeBtn = new Button(() =>
                {
                    SkinManager.Instance.RemoveSkin(type, index);
                    CloseInteractionPanel();
                    RebuildGrid();
                }) { text = "Remove Skin" };
                interactionActions.Add(removeBtn);
            }

            // Back button to return to building interaction
            var backBtn = new Button(() => ShowInteraction(type, index)) { text = "Back" };
            interactionActions.Add(backBtn);
        }

        private void UpdateSkinDetail(VisualElement detailArea, SkinData skin,
            CampBuildingType type, int index, string currentSkin)
        {
            detailArea.Clear();

            var nameLabel = new Label(skin.skinName.Replace("_", " "));
            nameLabel.AddToClassList("skin-detail-name");
            detailArea.Add(nameLabel);

            var items = SaveManager.Instance.Data.items;
            var pigmentItem = items.Find(i => i.itemName == skin.costItemName);
            int have = pigmentItem?.count ?? 0;
            string costName = skin.costItemName.Replace("_", " ");

            var costLabel = new Label($"Cost: {skin.costQuantity}x {costName} (have: {have})");
            costLabel.AddToClassList("skin-detail-cost");
            detailArea.Add(costLabel);

            bool isEquipped = skin.skinName == currentSkin;
            if (isEquipped)
            {
                var equippedLabel = new Label("Currently applied");
                equippedLabel.AddToClassList("skin-detail-equipped");
                detailArea.Add(equippedLabel);
            }
            else
            {
                bool canAfford = SkinManager.Instance.CanAffordSkin(skin);
                var paintBtn = new Button(() =>
                {
                    if (SkinManager.Instance.ApplySkin(type, index, skin))
                    {
                        CloseInteractionPanel();
                        RebuildGrid();
                    }
                }) { text = $"Paint ({skin.costQuantity}x {costName})" };
                paintBtn.AddToClassList("interaction-btn-primary");
                paintBtn.SetEnabled(canAfford);
                detailArea.Add(paintBtn);
            }
        }
```

**Step 2: Add "Paint" button to ShowPlotInteraction**

In `ShowPlotInteraction`, before `AddCloseButton()` (line 1052), add:

```csharp
            // Paint button (available in all states)
            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.Plot, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }
```

**Step 3: Add "Paint" button to ShowVaseInteraction**

In `ShowVaseInteraction`, before `AddCloseButton()` (line 1314), add the same pattern:

```csharp
            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.Vase, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }
```

**Step 4: Add "Paint" button to ShowMallumHouseInteraction**

In `ShowMallumHouseInteraction`, before `AddCloseButton()` (line 1339), add:

```csharp
            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.MallumHouse, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }
```

**Step 5: Compile and check console**

Run: `read_console` to verify no compilation errors.

**Step 6: Commit**

```
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add skin selector popup and paint buttons to building interactions"
```

---

### Task 6: Skin selector USS styles

**Files:**
- Modify: `Assets/UI/Styles/CampsiteGrid.uss`

**Step 1: Add skin selector styles**

Append to `CampsiteGrid.uss`:

```css
/* ── Skin Selector ── */
.skin-carousel {
    max-height: 120px;
    margin-bottom: var(--spacing-sm);
}

.skin-swatch {
    width: 80px;
    min-width: 80px;
    height: 100px;
    margin-right: var(--spacing-xs);
    padding: var(--spacing-xxs);
    border-radius: var(--radius-sm);
    border-width: 2px;
    border-color: rgba(140, 100, 50, 0.2);
    align-items: center;
    justify-content: center;
    background-color: rgba(40, 25, 12, 0.3);
}

.skin-swatch--selected {
    border-color: rgba(255, 200, 80, 0.8);
    background-color: rgba(60, 40, 20, 0.5);
}

.skin-swatch--equipped {
    border-color: rgba(120, 180, 60, 0.6);
}

.skin-swatch-preview {
    width: 48px;
    height: 48px;
    border-radius: 8px;
    border-width: 2px;
    margin-bottom: var(--spacing-xxs);
}

.skin-swatch-label {
    font-size: 16px;
    color: var(--color-text-dim);
    -unity-text-align: upper-center;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 76px;
}

.skin-detail {
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-sm);
}

.skin-detail-name {
    font-size: var(--font-md);
    color: var(--color-text);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xxs);
}

.skin-detail-cost {
    font-size: var(--font-sm);
    color: var(--color-text-dim);
    margin-bottom: var(--spacing-xs);
}

.skin-detail-equipped {
    font-size: var(--font-sm);
    color: rgba(120, 180, 60, 0.9);
    -unity-font-style: italic;
}
```

**Step 2: Commit**

```
git add Assets/UI/Styles/CampsiteGrid.uss
git commit -m "feat: add skin selector carousel USS styles"
```

---

### Task 7: Create pigment recipes

**Files:**
- Create: 12 RecipeData `.asset` files in `Assets/Resources/Recipes/`

Create one `.asset` file per seed type. The RecipeData script GUID is `b7df30f7d51a844f298b44c1b8850329`.

Each recipe follows this pattern (example for Basil):
- `recipeName`: "Basil Pigment"
- `ingredients`: [{itemName: "Basil_harvest", quantity: 2}]
- `result`: "Basil_pigment"
- `resultQuantity`: 1

Create these 12 files by writing the YAML directly. Use the Fertilizer.asset as template:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: b7df30f7d51a844f298b44c1b8850329, type: 3}
  m_Name: Basil_Pigment
  m_EditorClassIdentifier:
  recipeName: Basil Pigment
  ingredients:
  - itemName: Basil_harvest
    quantity: 2
  result: Basil_pigment
  resultQuantity: 1
  icon: {fileID: 0}
```

Create all 12: Basil, Chamomile, Dahlia, Jasmine, Lavender, Marigold, Mint, Moonflower, Pansy, Poppy, Rosemary, Snowdrop.

**Step 1: Create all 12 pigment recipe assets**

Write each file using the YAML template above, substituting the plant name.

**Step 2: Commit**

```
git add Assets/Resources/Recipes/*_Pigment.asset Assets/Resources/Recipes/*_Pigment.asset.meta
git commit -m "feat: add 12 pigment recipes to Apotheke"
```

---

### Task 8: Create skin assets

**Files:**
- Create: 36 SkinData `.asset` files in `Assets/Resources/Skins/{Plot,Vase,MallumHouse}/`

First, get the SkinData script GUID from its `.meta` file (created in Task 1).

Create 12 skins per building type. Each skin follows this pattern (example for Basil Plot):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <SKINDATA_GUID>, type: 3}
  m_Name: Basil_plot
  m_EditorClassIdentifier:
  skinName: Basil_plot
  buildingType: 2
  hexFillColor: {r: 0.196, g: 0.471, b: 0.196, a: 0.6}
  hexBorderColor: {r: 0.314, g: 0.706, b: 0.314, a: 0.3}
  costItemName: Basil_pigment
  costQuantity: 1
```

**CampBuildingType enum values for buildingType field:**
- Plot = 2
- Vase = 3
- MallumHouse = 6

**Color table (rgba 0-1 range):**

| Plant | Fill (r,g,b,a) | Border (r,g,b,a) |
|-------|---------------|-------------------|
| Basil | 0.196, 0.471, 0.196, 0.6 | 0.314, 0.706, 0.314, 0.3 |
| Chamomile | 0.902, 0.824, 0.510, 0.6 | 1.0, 0.941, 0.667, 0.3 |
| Dahlia | 0.706, 0.196, 0.314, 0.6 | 0.863, 0.353, 0.471, 0.3 |
| Jasmine | 0.941, 0.922, 0.824, 0.6 | 1.0, 0.980, 0.902, 0.3 |
| Lavender | 0.549, 0.392, 0.706, 0.6 | 0.706, 0.549, 0.863, 0.3 |
| Marigold | 0.863, 0.588, 0.118, 0.6 | 1.0, 0.745, 0.235, 0.3 |
| Mint | 0.235, 0.667, 0.510, 0.6 | 0.392, 0.824, 0.667, 0.3 |
| Moonflower | 0.314, 0.275, 0.549, 0.6 | 0.510, 0.471, 0.784, 0.3 |
| Pansy | 0.471, 0.235, 0.627, 0.6 | 0.667, 0.392, 0.824, 0.3 |
| Poppy | 0.784, 0.235, 0.157, 0.6 | 0.941, 0.392, 0.275, 0.3 |
| Rosemary | 0.275, 0.431, 0.314, 0.6 | 0.431, 0.627, 0.471, 0.3 |
| Snowdrop | 0.824, 0.882, 0.941, 0.6 | 0.902, 0.941, 0.980, 0.3 |

**Skin naming convention:**
- Plot skins: `{Plant}_plot` (skinName), `Assets/Resources/Skins/Plot/{Plant}_plot.asset`
- Vase skins: `{Plant}_vase` (skinName), `Assets/Resources/Skins/Vase/{Plant}_vase.asset`
- MallumHouse skins: `{Plant}_house` (skinName), `Assets/Resources/Skins/MallumHouse/{Plant}_house.asset`

**Step 1: Create folder structure**

```bash
mkdir -p Assets/Resources/Skins/Plot Assets/Resources/Skins/Vase Assets/Resources/Skins/MallumHouse
```

**Step 2: Create all 36 skin assets**

Write each file using the YAML template, substituting plant name, buildingType enum value, colors, and skin name.

**Step 3: Commit**

```
git add Assets/Resources/Skins/
git commit -m "feat: add 36 skin assets (12 per building type)"
```

---

### Task 9: Wire SkinManager into the scene

**Files:**
- Modify: `Assets/Scenes/Garden.unity` (add SkinManager component)

**Step 1: Find the managers GameObject**

Search the scene hierarchy for the GameObject that holds other manager components (GameManager, PlotManager, etc.).

**Step 2: Add SkinManager component**

Use `manage_components` to add the `SkinManager` component to that GameObject.

**Step 3: Verify in play mode**

Enter play mode briefly, check console for errors. Exit play mode.

**Step 4: Commit**

```
git add Assets/Scenes/Garden.unity
git commit -m "feat: wire SkinManager into scene"
```

---

### Task 10: Integration test

**Files:**
- Create: `Assets/Tests/EditMode/SkinManagerTests.cs`

**Step 1: Write tests**

```csharp
using NUnit.Framework;
using Garden;
using UnityEngine;

namespace Garden.Tests
{
    public class SkinManagerTests
    {
        [Test]
        public void SkinData_HasCorrectFields()
        {
            var skin = ScriptableObject.CreateInstance<SkinData>();
            skin.skinName = "Test_plot";
            skin.buildingType = CampBuildingType.Plot;
            skin.hexFillColor = Color.red;
            skin.hexBorderColor = Color.blue;
            skin.costItemName = "Test_pigment";
            skin.costQuantity = 1;

            Assert.AreEqual("Test_plot", skin.skinName);
            Assert.AreEqual(CampBuildingType.Plot, skin.buildingType);
            Assert.AreEqual(1, skin.costQuantity);

            Object.DestroyImmediate(skin);
        }

        [Test]
        public void PlotSave_HasSkinName()
        {
            var plot = new PlotSave();
            Assert.IsNull(plot.skinName);
            plot.skinName = "Basil_plot";
            Assert.AreEqual("Basil_plot", plot.skinName);
        }

        [Test]
        public void VaseSave_HasSkinName()
        {
            var vase = new VaseSave();
            Assert.IsNull(vase.skinName);
            vase.skinName = "Basil_vase";
            Assert.AreEqual("Basil_vase", vase.skinName);
        }

        [Test]
        public void MallumHouseSave_HasSkinName()
        {
            var house = new MallumHouseSave();
            Assert.IsNull(house.skinName);
            house.skinName = "Basil_house";
            Assert.AreEqual("Basil_house", house.skinName);
        }
    }
}
```

**Step 2: Run tests**

Run: `run_tests` with mode "EditMode"
Expected: All 4 tests pass.

**Step 3: Commit**

```
git add Assets/Tests/EditMode/SkinManagerTests.cs Assets/Tests/EditMode/SkinManagerTests.cs.meta
git commit -m "test: add skin system unit tests"
```
