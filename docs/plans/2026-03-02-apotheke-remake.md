# Apotheke UI Remake Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remake the Apotheke overlay with Inventory/Craft tabs, expandable seed cards showing growth recipes, and rename the bottom nav "Craft" button to "Build".

**Architecture:** Apotheke panel gets a tab bar (reusing Letters tab styles). Inventory tab shows expandable seed cards with recipe details loaded from SeedData. Craft tab shows mixing recipes. The existing CraftUI (building actions) is renamed to BuildUI throughout.

**Tech Stack:** Unity 6, UI Toolkit (UXML/USS/C#), NUnit EditMode tests

---

### Task 1: Rename CraftUI to BuildUI

This task renames all "Craft" references (for the building panel) to "Build" to free up the "Craft" name for the Apotheke mixing tab.

**Files:**
- Rename: `Assets/Scripts/UI/CraftUI.cs` → `Assets/Scripts/UI/BuildUI.cs`
- Rename: `Assets/UI/Styles/Craft.uss` → `Assets/UI/Styles/Build.uss`
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`
- Modify: `Assets/Scripts/UI/CampFireUI.cs`
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`

**Step 1: Create BuildUI.cs (replacing CraftUI.cs)**

Write `Assets/Scripts/UI/BuildUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BuildUI : MonoBehaviour
    {
        private VisualElement buildList;
        private VisualTreeAsset buildTemplate;

        public event Action<CampBuildingType> OnRequestPlacement;

        public void Initialize(VisualElement root)
        {
            buildList = root.Q("build-list");
            buildTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/CraftItem");
            Refresh();
        }

        public void Refresh()
        {
            if (buildList == null) return;
            buildList.Clear();

            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool canAfford = PlotManager.Instance.Plots.Count < FlameManager.Instance.MaxPlots;
                AddBuildItem("New Plot", canAfford ? "Place on grid" : "Max plots reached", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Plot);
                });
            }

            if (VaseManager.Instance != null)
            {
                float cost = VaseManager.Instance.Config.CraftCostMana;
                bool canAfford = CurrencyManager.Instance.CanAffordMana(cost);
                AddBuildItem("New Vase", $"{cost:F0} Mana", () =>
                {
                    if (canAfford)
                        OnRequestPlacement?.Invoke(CampBuildingType.Vase);
                });
            }

            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                var cost = FlameManager.Instance.Config.GetUpgradeCost(FlameManager.Instance.Level);
                AddBuildItem("Upgrade Flame", $"{cost:F0} Mana", () =>
                {
                    FlameManager.Instance.UpgradeFlame();
                    Refresh();
                });
            }
        }

        private void AddBuildItem(string name, string cost, Action onClick)
        {
            var el = buildTemplate.CloneTree();
            var nameLabel = el.Q<Label>(className: "craft-name");
            var costLabel = el.Q<Label>(className: "craft-cost");
            var actionBtn = el.Q<Button>(className: "craft-action");

            if (nameLabel != null) nameLabel.text = name;
            if (costLabel != null) costLabel.text = cost;
            if (actionBtn != null) actionBtn.clicked += onClick;

            buildList.Add(el);
        }
    }
}
```

Delete the old `Assets/Scripts/UI/CraftUI.cs` file.

**Step 2: Create Build.uss (replacing Craft.uss)**

Write `Assets/UI/Styles/Build.uss` — identical to current Craft.uss but with `build` replacing `craft` in element IDs:

```css
/* Build.uss — Build overlay styling */

#build-panel {
    flex-direction: column;
}

#build-list {
    flex-grow: 1;
}

#build-list .unity-scroll-view__content-container {
    flex-direction: column;
    padding: var(--spacing-xs);
}

/* Reuse .craft-item class names from CraftItem.uxml template — no rename needed */
.craft-item {
    flex-direction: row;
    align-items: center;
    background-color: rgba(50, 38, 22, 0.6);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.25);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    transition-property: background-color, border-color;
    transition-duration: 0.15s;
}

.craft-item:hover {
    background-color: rgba(65, 48, 28, 0.7);
    border-color: rgba(180, 120, 60, 0.4);
}

.craft-name {
    font-size: var(--font-md);
    color: rgb(230, 210, 180);
    -unity-font-style: bold;
    flex-grow: 1;
}

.craft-cost {
    font-size: var(--font-sm);
    color: rgb(255, 170, 60);
    margin-right: var(--spacing-md);
    flex-shrink: 0;
}

.craft-action {
    min-width: 120px;
    min-height: 72px;
    background-color: rgba(140, 80, 20, 0.5);
    border-width: 1px;
    border-color: rgba(200, 140, 50, 0.4);
    border-radius: var(--radius-sm);
    color: rgb(255, 220, 150);
    font-size: var(--font-sm);
    -unity-text-align: middle-center;
    flex-shrink: 0;
}

.craft-action:hover {
    background-color: rgba(160, 95, 25, 0.65);
    border-color: rgba(220, 160, 60, 0.6);
}

.craft-action:disabled {
    background-color: rgba(40, 30, 18, 0.4);
    border-color: rgba(100, 70, 35, 0.2);
    color: rgb(120, 100, 75);
}
```

Delete the old `Assets/UI/Styles/Craft.uss` file.

**Step 3: Update BottomNavUI.cs**

Replace `Assets/Scripts/UI/BottomNavUI.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnApothekeClicked;
        public event Action OnLettersClicked;
        public event Action OnBuildClicked;

        public void Initialize(VisualElement root)
        {
            var btnApotheke = root.Q<Button>("btn-apotheke");
            var btnLetters = root.Q<Button>("btn-letters");
            var btnBuild = root.Q<Button>("btn-build");

            btnApotheke?.RegisterCallback<ClickEvent>(_ => OnApothekeClicked?.Invoke());
            btnLetters?.RegisterCallback<ClickEvent>(_ => OnLettersClicked?.Invoke());
            btnBuild?.RegisterCallback<ClickEvent>(_ => OnBuildClicked?.Invoke());
        }
    }
}
```

**Step 4: Update CampFireUI.cs**

In `Assets/Scripts/UI/CampFireUI.cs`, make these changes:

a) Field declarations — change:
```csharp
    private CraftUI craft;
```
to:
```csharp
    private BuildUI build;
```

b) In `craftPanel` field:
```csharp
    private VisualElement craftPanel;
```
to:
```csharp
    private VisualElement buildPanel;
```

c) In Start(), change:
```csharp
            craft = GetComponent<CraftUI>();
```
to:
```csharp
            build = GetComponent<BuildUI>();
```

d) Change:
```csharp
            craft?.Initialize(root);
```
to:
```csharp
            build?.Initialize(root);
```

e) Change:
```csharp
            craftPanel = root.Q("craft-panel");
```
to:
```csharp
            buildPanel = root.Q("build-panel");
```

f) In bottom nav wiring, change:
```csharp
                bottomNav.OnCraftClicked += () => OpenOverlay("Craft", craftPanel);
```
to:
```csharp
                bottomNav.OnBuildClicked += () => OpenOverlay("Build", buildPanel);
```

g) In craft placement wiring, change:
```csharp
            if (craft != null && campsiteView != null)
            {
                craft.OnRequestPlacement += type =>
```
to:
```csharp
            if (build != null && campsiteView != null)
            {
                build.OnRequestPlacement += type =>
```

h) In HideAllPanels(), change:
```csharp
            if (craftPanel != null) craftPanel.style.display = DisplayStyle.None;
```
to:
```csharp
            if (buildPanel != null) buildPanel.style.display = DisplayStyle.None;
```

**Step 5: Update CampFireRoot.uxml**

In `Assets/UI/Documents/CampFireRoot.uxml`:

a) Change stylesheet reference:
```xml
    <Style src="project://database/Assets/UI/Styles/Craft.uss" />
```
to:
```xml
    <Style src="project://database/Assets/UI/Styles/Build.uss" />
```

b) Change bottom nav button:
```xml
            <ui:Button name="btn-craft" class="nav-btn" text="Craft" />
```
to:
```xml
            <ui:Button name="btn-build" class="nav-btn" text="Build" />
```

c) Change craft panel:
```xml
                    <!-- Craft panel -->
                    <ui:VisualElement name="craft-panel">
                        <ui:ScrollView name="craft-list" />
                    </ui:VisualElement>
```
to:
```xml
                    <!-- Build panel -->
                    <ui:VisualElement name="build-panel">
                        <ui:ScrollView name="build-list" />
                    </ui:VisualElement>
```

**Step 6: Verify compilation**

Refresh Unity and check console for 0 errors. The Build panel should work identically to the old Craft panel.

**Step 7: Commit**

```bash
git add -A
git commit -m "refactor: rename Craft panel to Build throughout"
```

---

### Task 2: Add tab bar to Apotheke panel (UXML + USS)

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`
- Modify: `Assets/UI/Styles/Apotheke.uss`

**Step 1: Update apotheke-panel in CampFireRoot.uxml**

Replace the `apotheke-panel` section:

```xml
                    <!-- Apotheke panel -->
                    <ui:VisualElement name="apotheke-panel">
                        <!-- Tab bar -->
                        <ui:VisualElement name="apotheke-tabs">
                            <ui:Button name="tab-inventory" class="letters-tab letters-tab--active" text="Inventory" />
                            <ui:Button name="tab-craft" class="letters-tab" text="Craft" />
                        </ui:VisualElement>

                        <!-- Inventory view -->
                        <ui:VisualElement name="apotheke-inventory">
                            <ui:Label name="inventory-empty" text="No seeds yet" class="letters-empty-text" />
                            <ui:ScrollView name="seed-list" />
                        </ui:VisualElement>

                        <!-- Craft view (mixing recipes) -->
                        <ui:VisualElement name="apotheke-craft">
                            <ui:ScrollView name="recipe-list" />
                        </ui:VisualElement>
                    </ui:VisualElement>
```

**Step 2: Add tab and inventory styles to Apotheke.uss**

Replace the entire `Assets/UI/Styles/Apotheke.uss` with:

```css
/* Apotheke.uss — Apotheke overlay (Inventory + Craft tabs) */

#apotheke-panel {
    flex-direction: column;
    flex-grow: 1;
}

/* ── Tab bar (reuses .letters-tab from Letters.uss) ── */
#apotheke-tabs {
    flex-direction: row;
    margin-bottom: var(--spacing-md);
    flex-shrink: 0;
}

/* ── Tab content views ── */
#apotheke-inventory,
#apotheke-craft {
    flex-grow: 1;
    flex-direction: column;
}

#seed-list {
    flex-grow: 1;
}

#seed-list .unity-scroll-view__content-container {
    flex-direction: column;
    padding: var(--spacing-xs);
}

/* ── Seed card (expandable) ── */
.seed-card {
    flex-direction: column;
    background-color: rgba(50, 35, 20, 0.6);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.25);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    transition-property: background-color, border-color;
    transition-duration: 0.15s;
}

.seed-card:hover {
    background-color: rgba(65, 45, 25, 0.7);
    border-color: rgba(180, 120, 60, 0.4);
}

.seed-card--expanded {
    border-color: rgba(220, 150, 50, 0.4);
}

.seed-card-header {
    flex-direction: row;
    align-items: center;
}

.seed-icon {
    width: 72px;
    height: 72px;
    border-radius: var(--radius-sm);
    background-color: rgba(40, 28, 15, 0.6);
    margin-right: var(--spacing-sm);
    flex-shrink: 0;
}

.seed-info {
    flex-grow: 1;
    flex-direction: column;
    justify-content: center;
}

.seed-name {
    font-size: var(--font-md);
    color: rgb(230, 210, 180);
    -unity-font-style: bold;
}

.seed-count {
    font-size: var(--font-sm);
    color: rgb(170, 145, 110);
    margin-top: var(--spacing-xxs);
}

.seed-outcome {
    flex-direction: column;
    align-items: flex-end;
    justify-content: center;
    flex-shrink: 0;
    margin-left: var(--spacing-sm);
}

.seed-outcome-name {
    font-size: var(--font-sm);
    color: rgb(180, 220, 120);
}

/* ── Expandable recipe details ── */
.seed-card-details {
    flex-direction: column;
    margin-top: var(--spacing-sm);
    padding-top: var(--spacing-sm);
    border-top-width: 1px;
    border-top-color: rgba(140, 100, 50, 0.15);
    display: none;
}

.seed-card--expanded .seed-card-details {
    display: flex;
}

.seed-recipe-title {
    font-size: var(--font-sm);
    color: rgb(200, 180, 140);
    -unity-font-style: bold;
    margin-bottom: var(--spacing-xs);
}

.seed-recipe-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-xxs);
}

.seed-recipe-label {
    font-size: var(--font-xs);
    color: rgb(170, 145, 110);
    min-width: 160px;
    flex-shrink: 0;
}

.seed-recipe-value {
    font-size: var(--font-xs);
    color: rgb(220, 200, 165);
    flex-grow: 1;
}

.seed-recipe-weight {
    font-size: 20px;
    color: rgb(140, 120, 90);
    flex-shrink: 0;
    margin-left: var(--spacing-xs);
}

/* ── Recipe section (Craft tab) ── */
#recipe-list {
    flex-grow: 1;
}

#recipe-list .unity-scroll-view__content-container {
    flex-direction: column;
    padding: var(--spacing-xs);
}

.recipe-card {
    flex-direction: row;
    align-items: center;
    background-color: rgba(45, 35, 22, 0.6);
    border-width: 1px;
    border-color: rgba(120, 90, 45, 0.25);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
}

.recipe-card:hover {
    background-color: rgba(55, 42, 26, 0.7);
    border-color: rgba(160, 110, 50, 0.4);
}

.recipe-name {
    font-size: var(--font-md);
    color: rgb(220, 200, 165);
    -unity-font-style: bold;
    min-width: 120px;
    flex-shrink: 0;
}

.recipe-ingredients {
    flex-grow: 1;
    flex-direction: row;
    flex-wrap: wrap;
    margin-left: var(--spacing-sm);
    margin-right: var(--spacing-sm);
}

.recipe-ingredient {
    font-size: var(--font-xs);
    color: rgb(160, 140, 110);
    margin-right: var(--spacing-xs);
}

.recipe-result {
    font-size: var(--font-sm);
    color: rgb(180, 220, 120);
    flex-shrink: 0;
    margin-right: var(--spacing-sm);
}

.recipe-action {
    min-width: 100px;
    min-height: 64px;
    background-color: rgba(80, 120, 40, 0.5);
    border-width: 1px;
    border-color: rgba(120, 180, 60, 0.4);
    border-radius: var(--radius-sm);
    color: rgb(200, 240, 150);
    font-size: var(--font-sm);
    -unity-text-align: middle-center;
    flex-shrink: 0;
}

.recipe-action:hover {
    background-color: rgba(95, 140, 48, 0.65);
    border-color: rgba(140, 200, 70, 0.6);
}

.recipe-action:disabled {
    background-color: rgba(40, 50, 22, 0.4);
    border-color: rgba(80, 100, 40, 0.2);
    color: rgb(100, 120, 75);
}
```

**Step 3: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml Assets/UI/Styles/Apotheke.uss
git commit -m "feat: add tab bar and expandable card styles to Apotheke"
```

---

### Task 3: Rewrite ApothekeUI with tabs and expandable seed cards

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`

**Step 1: Rewrite ApothekeUI.cs**

Replace `Assets/Scripts/UI/ApothekeUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ApothekeUI : MonoBehaviour
    {
        private VisualElement inventoryView;
        private VisualElement craftView;
        private Label inventoryEmpty;
        private VisualElement seedList;
        private VisualElement recipeList;
        private Button tabInventory;
        private Button tabCraft;
        private VisualTreeAsset recipeTemplate;

        private SeedData[] allSeeds;
        private int expandedIndex = -1;

        public void Initialize(VisualElement root)
        {
            tabInventory = root.Q<Button>("tab-inventory");
            tabCraft = root.Q<Button>("tab-craft");
            inventoryView = root.Q("apotheke-inventory");
            craftView = root.Q("apotheke-craft");
            inventoryEmpty = root.Q<Label>("inventory-empty");
            seedList = root.Q("seed-list");
            recipeList = root.Q("recipe-list");
            recipeTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/RecipeCard");

            allSeeds = Resources.LoadAll<SeedData>("Seeds");

            tabInventory?.RegisterCallback<ClickEvent>(_ => ShowTab(0));
            tabCraft?.RegisterCallback<ClickEvent>(_ => ShowTab(1));

            ShowTab(0);
        }

        public void Refresh()
        {
            RefreshSeeds();
            RefreshRecipes();
        }

        private void ShowTab(int index)
        {
            bool isInventory = index == 0;

            tabInventory?.EnableInClassList("letters-tab--active", isInventory);
            tabCraft?.EnableInClassList("letters-tab--active", !isInventory);

            if (inventoryView != null)
                inventoryView.style.display = isInventory ? DisplayStyle.Flex : DisplayStyle.None;
            if (craftView != null)
                craftView.style.display = isInventory ? DisplayStyle.None : DisplayStyle.Flex;

            if (isInventory) RefreshSeeds();
            else RefreshRecipes();
        }

        private void RefreshSeeds()
        {
            if (seedList == null) return;
            seedList.Clear();

            var seeds = SaveManager.Instance?.Data?.seedInventory;
            if (seeds == null || seeds.Count == 0)
            {
                if (inventoryEmpty != null) inventoryEmpty.style.display = DisplayStyle.Flex;
                return;
            }
            if (inventoryEmpty != null) inventoryEmpty.style.display = DisplayStyle.None;

            for (int i = 0; i < seeds.Count; i++)
            {
                var entry = seeds[i];
                if (entry.count <= 0) continue;

                var seedData = FindSeedData(entry.seedName);
                var card = BuildSeedCard(entry, seedData, i);
                seedList.Add(card);
            }
        }

        private VisualElement BuildSeedCard(SeedInventoryEntry entry, SeedData seedData, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("seed-card");
            if (index == expandedIndex) card.AddToClassList("seed-card--expanded");

            // Header row (always visible)
            var header = new VisualElement();
            header.AddToClassList("seed-card-header");

            var icon = new VisualElement();
            icon.AddToClassList("seed-icon");
            if (seedData != null && seedData.icon != null)
                icon.style.backgroundImage = new StyleBackground(seedData.icon);
            header.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("seed-info");
            var nameLabel = new Label(entry.seedName);
            nameLabel.AddToClassList("seed-name");
            info.Add(nameLabel);
            var countLabel = new Label($"x{entry.count}");
            countLabel.AddToClassList("seed-count");
            info.Add(countLabel);
            header.Add(info);

            var outcome = new VisualElement();
            outcome.AddToClassList("seed-outcome");
            string harvestName = entry.seedName + " Harvest";
            var outcomeName = new Label(harvestName);
            outcomeName.AddToClassList("seed-outcome-name");
            outcome.Add(outcomeName);
            header.Add(outcome);

            card.Add(header);

            // Details section (shown when expanded)
            var details = new VisualElement();
            details.AddToClassList("seed-card-details");

            if (seedData != null && seedData.recipe != null)
            {
                var title = new Label("Growth Recipe");
                title.AddToClassList("seed-recipe-title");
                details.Add(title);

                AddRecipeDimensions(details, seedData.recipe);

                var dropsRow = new VisualElement();
                dropsRow.AddToClassList("seed-recipe-row");
                var dropsLabel = new Label("Max drops");
                dropsLabel.AddToClassList("seed-recipe-label");
                dropsRow.Add(dropsLabel);
                var dropsValue = new Label($"{seedData.baseDrops}");
                dropsValue.AddToClassList("seed-recipe-value");
                dropsRow.Add(dropsValue);
                details.Add(dropsRow);

                var durationRow = new VisualElement();
                durationRow.AddToClassList("seed-recipe-row");
                var durLabel = new Label("Growth time");
                durLabel.AddToClassList("seed-recipe-label");
                durationRow.Add(durLabel);
                var durValue = new Label($"{seedData.growthDurationHours}h");
                durValue.AddToClassList("seed-recipe-value");
                durationRow.Add(durValue);
                details.Add(durationRow);
            }
            else
            {
                var noRecipe = new Label("No recipe data");
                noRecipe.AddToClassList("seed-recipe-label");
                details.Add(noRecipe);
            }

            card.Add(details);

            // Tap to expand/collapse
            int idx = index;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                expandedIndex = expandedIndex == idx ? -1 : idx;
                RefreshSeeds();
            });

            return card;
        }

        private static void AddRecipeDimensions(VisualElement container, GrowthRecipe recipe)
        {
            if (recipe.useHeat)
                AddDimensionRow(container, "Heat", $"{recipe.idealTempMin}-{recipe.idealTempMax}\u00b0C", recipe.heatWeight);
            if (recipe.useWind)
                AddDimensionRow(container, "Wind", $"{recipe.idealWindMin}-{recipe.idealWindMax} m/s", recipe.windWeight);
            if (recipe.useHumidity)
                AddDimensionRow(container, "Humidity", $"{recipe.idealHumidityMin}-{recipe.idealHumidityMax}%", recipe.humidityWeight);
            if (recipe.useSunlight)
                AddDimensionRow(container, "Sunlight", $"{recipe.idealSunlightMin}-{recipe.idealSunlightMax}%", recipe.sunlightWeight);
            if (recipe.useRain)
            {
                int minPct = Mathf.RoundToInt(recipe.idealRainMin * 100f);
                int maxPct = Mathf.RoundToInt(recipe.idealRainMax * 100f);
                AddDimensionRow(container, "Rain", $"{minPct}-{maxPct}%", recipe.rainWeight);
            }
            if (recipe.useMoon)
                AddDimensionRow(container, "Moon", recipe.requiredMoonPhase.ToString(), recipe.moonWeight);
            if (recipe.useWaterings)
                AddDimensionRow(container, "Waterings", $"{recipe.idealWaterings}", recipe.wateringsWeight);
        }

        private static void AddDimensionRow(VisualElement container, string label, string value, float weight)
        {
            var row = new VisualElement();
            row.AddToClassList("seed-recipe-row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("seed-recipe-label");
            row.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("seed-recipe-value");
            row.Add(valueEl);

            if (!Mathf.Approximately(weight, 1f))
            {
                var weightEl = new Label($"x{weight:G3}");
                weightEl.AddToClassList("seed-recipe-weight");
                row.Add(weightEl);
            }

            container.Add(row);
        }

        private void RefreshRecipes()
        {
            if (recipeList == null || ApothekeManager.Instance == null) return;
            recipeList.Clear();
            foreach (var recipe in ApothekeManager.Instance.AllRecipes)
            {
                var el = recipeTemplate.CloneTree();
                var nameLabel = el.Q<Label>(className: "recipe-name");
                var resultLabel = el.Q<Label>(className: "recipe-result");
                var mixBtn = el.Q<Button>(className: "recipe-action");

                if (nameLabel != null) nameLabel.text = recipe.recipeName;
                if (resultLabel != null) resultLabel.text = $"\u2192 {recipe.result}";
                if (mixBtn != null)
                {
                    bool canMix = ApothekeManager.Instance.CanMix(recipe);
                    mixBtn.SetEnabled(canMix);
                    var r = recipe;
                    mixBtn.clicked += () =>
                    {
                        ApothekeManager.Instance.Mix(r);
                        Refresh();
                    };
                }
                recipeList.Add(el);
            }
        }

        private SeedData FindSeedData(string seedName)
        {
            if (allSeeds == null || string.IsNullOrEmpty(seedName)) return null;
            foreach (var s in allSeeds)
                if (s.seedName == seedName) return s;
            return null;
        }
    }
}
```

**Step 2: Verify compilation and visual result**

Refresh Unity, check console for 0 errors. Open Apotheke overlay — should show Inventory tab (default) with expandable seed cards. Click "Craft" tab to see recipes.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/ApothekeUI.cs
git commit -m "feat: rewrite ApothekeUI with Inventory/Craft tabs and expandable seed cards"
```

---

### Task 4: Update SeedCard.uxml template (cleanup)

The SeedCard.uxml template is no longer used by ApothekeUI (which now builds cards programmatically). Check if anything else uses it — if not, it can be deleted or left as-is for potential future use.

**Step 1: Search for SeedCard.uxml usage**

Search codebase for `SeedCard` references. If only the old ApothekeUI loaded it, it's now unused.

**Step 2: Delete if unused**

If unused, delete `Assets/Resources/UI/Templates/SeedCard.uxml` and its `.meta`.

**Step 3: Commit**

```bash
git add -A
git commit -m "chore: remove unused SeedCard template"
```

---

### Task 5: Final verification

**Step 1: Run all EditMode tests**

Run Unity Test Runner EditMode. All tests should pass.

**Step 2: Visual verification**

- Open Apotheke overlay: should show "Inventory" tab active by default
- Inventory tab shows seed cards with name, count, harvest item name
- Tap a seed card: it expands to show growth recipe dimensions (only active ones)
- Tap again: it collapses
- Click "Craft" tab: shows recipe list with Mix buttons
- Bottom nav says "Build" (not "Craft")
- Click "Build": opens Build overlay with plot/vase/flame actions
- Build placement mode still works

**Step 3: Check console**

No errors or warnings.
