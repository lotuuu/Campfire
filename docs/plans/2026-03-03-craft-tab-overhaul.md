# Craft Tab Visual Overhaul

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the flat, broken Craft tab with expandable recipe cards grouped by category, showing ingredient counts and craftability status.

**Architecture:** Add a `RecipeCategory` enum to `RecipeData`, rewrite `ApothekeUI.RefreshRecipes()` to build expandable cards programmatically (like seed cards), group by category with section headers, and display ingredient owned/needed counts. A static `FormatItemName()` utility replaces underscores with spaces for display.

**Tech Stack:** Unity UI Toolkit (C#, USS), ScriptableObject YAML assets

---

### Task 1: Add RecipeCategory enum to RecipeData

**Files:**
- Modify: `Assets/Scripts/Data/RecipeData.cs`

**Step 1: Add enum and field**

Add `RecipeCategory` enum above the class and a `category` field to `RecipeData`:

```csharp
public enum RecipeCategory
{
    Pigment = 0,
    Potion = 1,
    Material = 2
}
```

Add inside `RecipeData` class, above the `[Header("Visuals")]` line:

```csharp
[Header("Category")]
public RecipeCategory category;
```

**Step 2: Add FormatItemName utility**

Add a static method to `RecipeData`:

```csharp
public static string FormatItemName(string internalName)
{
    if (string.IsNullOrEmpty(internalName)) return "";
    return internalName.Replace('_', ' ');
}
```

**Step 3: Compile and verify**

Run: `read_console` — expect no compilation errors.

**Step 4: Commit**

```
feat: add RecipeCategory enum and FormatItemName utility to RecipeData
```

---

### Task 2: Set category on all recipe assets

**Files:**
- Modify: All 14 files in `Assets/Resources/Recipes/*.asset`

**Step 1: Set category field in YAML**

For each recipe asset, add `category: N` after the `icon` line. Unity serializes enums as ints.

- **Pigment = 0**: Basil_Pigment, Chamomile_Pigment, Dahlia_Pigment, Jasmine_Pigment, Lavender_Pigment, Marigold_Pigment, Mint_Pigment, Moonflower_Pigment, Pansy_Pigment, Poppy_Pigment, Rosemary_Pigment, Snowdrop_Pigment
- **Potion = 1**: Speed_Potion
- **Material = 2**: Fertilizer

Each asset file: add `category: N` as a new line after the `icon:` line. Example for Basil_Pigment.asset:
```yaml
  icon: {fileID: 0}
  category: 0
```

Example for Speed_Potion.asset:
```yaml
  icon: {fileID: 0}
  category: 1
```

Example for Fertilizer.asset:
```yaml
  icon: {fileID: 0}
  category: 2
```

**Step 2: Commit**

```
chore: set RecipeCategory on all 14 recipe assets
```

---

### Task 3: Update Apotheke.uss with new recipe card styles

**Files:**
- Modify: `Assets/UI/Styles/Apotheke.uss`

**Step 1: Replace recipe card styles**

Remove the existing `.recipe-card`, `.recipe-name`, `.recipe-ingredients`, `.recipe-ingredient`, `.recipe-result`, `.recipe-action` styles (lines 151-218). Replace with new styles for the expandable card system:

```css
/* ── Category headers ── */
.recipe-category-header {
    font-size: var(--font-sm);
    color: rgb(200, 180, 140);
    -unity-font-style: bold;
    margin-top: var(--spacing-md);
    margin-bottom: var(--spacing-xs);
    padding-left: var(--spacing-xxs);
}

/* ── Recipe cards (expandable, mirrors seed-card pattern) ── */
.recipe-card {
    flex-direction: column;
    background-color: rgba(45, 35, 22, 0.6);
    border-width: 1px;
    border-color: rgba(120, 90, 45, 0.25);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    transition-property: background-color, border-color;
    transition-duration: 0.15s;
}

.recipe-card:hover {
    background-color: rgba(55, 42, 26, 0.7);
    border-color: rgba(160, 110, 50, 0.4);
}

.recipe-card--expanded {
    border-color: rgba(220, 150, 50, 0.4);
}

.recipe-card--craftable {
    border-color: rgba(120, 180, 60, 0.35);
}

/* Collapsed header row */
.recipe-card-header {
    flex-direction: row;
    align-items: center;
}

.recipe-card-name {
    font-size: var(--font-md);
    color: rgb(220, 200, 165);
    -unity-font-style: bold;
    flex-grow: 1;
}

.recipe-card-status {
    width: 16px;
    height: 16px;
    border-radius: 8px;
    flex-shrink: 0;
    margin-left: var(--spacing-sm);
}

.recipe-card-status--ready {
    background-color: rgb(120, 180, 60);
}

.recipe-card-status--missing {
    background-color: rgba(120, 90, 45, 0.4);
}

/* Expanded details area */
.recipe-card-details {
    flex-direction: column;
    margin-top: var(--spacing-sm);
    padding-top: var(--spacing-sm);
    border-top-width: 1px;
    border-top-color: rgba(140, 100, 50, 0.15);
    display: none;
}

.recipe-card--expanded .recipe-card-details {
    display: flex;
}

/* Ingredient rows */
.recipe-ingredients-title {
    font-size: var(--font-xs);
    color: rgb(170, 145, 110);
    margin-bottom: var(--spacing-xxs);
}

.recipe-ingredient-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: var(--spacing-xxs);
    padding-left: var(--spacing-xs);
}

.recipe-ingredient-name {
    font-size: var(--font-xs);
    color: rgb(200, 180, 150);
    flex-grow: 1;
}

.recipe-ingredient-count {
    font-size: var(--font-xs);
    flex-shrink: 0;
    margin-left: var(--spacing-xs);
}

.recipe-ingredient-count--satisfied {
    color: rgb(140, 200, 80);
}

.recipe-ingredient-count--missing {
    color: rgb(200, 120, 60);
}

/* Result row */
.recipe-result-row {
    flex-direction: row;
    align-items: center;
    margin-top: var(--spacing-sm);
    padding-top: var(--spacing-xs);
    border-top-width: 1px;
    border-top-color: rgba(140, 100, 50, 0.1);
}

.recipe-result-label {
    font-size: var(--font-xs);
    color: rgb(170, 145, 110);
    margin-right: var(--spacing-xs);
}

.recipe-result-name {
    font-size: var(--font-sm);
    color: rgb(180, 220, 120);
    flex-grow: 1;
}

/* Mix button */
.recipe-action {
    min-height: 64px;
    margin-top: var(--spacing-sm);
    background-color: rgba(80, 120, 40, 0.5);
    border-width: 1px;
    border-color: rgba(120, 180, 60, 0.4);
    border-radius: var(--radius-sm);
    color: rgb(200, 240, 150);
    font-size: var(--font-sm);
    -unity-text-align: middle-center;
    -unity-font-style: bold;
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

**Step 2: Commit**

```
style: add expandable recipe card and category header styles
```

---

### Task 4: Rewrite ApothekeUI.RefreshRecipes with expandable grouped cards

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs`

**Step 1: Add expandedRecipeIndex field**

Add next to the existing `expandedIndex` field:

```csharp
private int expandedRecipeIndex = -1;
```

**Step 2: Remove recipeTemplate field and its loading**

Delete the `recipeTemplate` field declaration and the `Resources.Load<VisualTreeAsset>("UI/Templates/RecipeCard")` line from `Initialize()`.

**Step 3: Rewrite RefreshRecipes**

Replace the entire `RefreshRecipes()` method with:

```csharp
private void RefreshRecipes()
{
    if (recipeList == null || ApothekeManager.Instance == null) return;
    recipeList.Clear();

    var recipes = ApothekeManager.Instance.AllRecipes;
    if (recipes == null || recipes.Length == 0) return;

    var items = SaveManager.Instance?.Data?.items;

    // Group by category, craftable first within each group
    var grouped = new System.Collections.Generic.SortedDictionary<RecipeCategory,
        System.Collections.Generic.List<RecipeData>>();
    foreach (var r in recipes)
    {
        if (!grouped.ContainsKey(r.category))
            grouped[r.category] = new System.Collections.Generic.List<RecipeData>();
        grouped[r.category].Add(r);
    }

    foreach (var kvp in grouped)
    {
        // Sort: craftable first, then alphabetical
        kvp.Value.Sort((a, b) =>
        {
            bool canA = ApothekeManager.Instance.CanMix(a);
            bool canB = ApothekeManager.Instance.CanMix(b);
            if (canA != canB) return canA ? -1 : 1;
            return string.Compare(a.recipeName, b.recipeName, System.StringComparison.Ordinal);
        });

        // Category header
        var header = new Label(CategoryLabel(kvp.Key));
        header.AddToClassList("recipe-category-header");
        recipeList.Add(header);

        foreach (var recipe in kvp.Value)
        {
            var card = BuildRecipeCard(recipe, items);
            recipeList.Add(card);
        }
    }
}

private static string CategoryLabel(RecipeCategory cat)
{
    return cat switch
    {
        RecipeCategory.Pigment => "Pigments",
        RecipeCategory.Potion => "Potions",
        RecipeCategory.Material => "Materials",
        _ => cat.ToString()
    };
}
```

**Step 4: Add BuildRecipeCard method**

Add after `RefreshRecipes()`:

```csharp
private VisualElement BuildRecipeCard(RecipeData recipe, System.Collections.Generic.List<InventoryItem> items)
{
    bool canMix = ApothekeManager.Instance.CanMix(recipe);
    int recipeIndex = System.Array.IndexOf(ApothekeManager.Instance.AllRecipes, recipe);
    bool isExpanded = recipeIndex == expandedRecipeIndex;

    var card = new VisualElement();
    card.AddToClassList("recipe-card");
    if (isExpanded) card.AddToClassList("recipe-card--expanded");
    if (canMix) card.AddToClassList("recipe-card--craftable");

    // Header row (always visible)
    var header = new VisualElement();
    header.AddToClassList("recipe-card-header");

    var nameLabel = new Label(recipe.recipeName);
    nameLabel.AddToClassList("recipe-card-name");
    header.Add(nameLabel);

    var status = new VisualElement();
    status.AddToClassList("recipe-card-status");
    status.AddToClassList(canMix ? "recipe-card-status--ready" : "recipe-card-status--missing");
    header.Add(status);

    card.Add(header);

    // Details (shown when expanded)
    var details = new VisualElement();
    details.AddToClassList("recipe-card-details");

    var ingTitle = new Label("Needs:");
    ingTitle.AddToClassList("recipe-ingredients-title");
    details.Add(ingTitle);

    foreach (var ing in recipe.ingredients)
    {
        var row = new VisualElement();
        row.AddToClassList("recipe-ingredient-row");

        var ingName = new Label(RecipeData.FormatItemName(ing.itemName));
        ingName.AddToClassList("recipe-ingredient-name");
        row.Add(ingName);

        int owned = 0;
        if (items != null)
        {
            var item = items.Find(i => i.itemName == ing.itemName);
            if (item != null) owned = item.count;
        }
        bool satisfied = owned >= ing.quantity;

        var countLabel = new Label($"{owned}/{ing.quantity}");
        countLabel.AddToClassList("recipe-ingredient-count");
        countLabel.AddToClassList(satisfied
            ? "recipe-ingredient-count--satisfied"
            : "recipe-ingredient-count--missing");
        row.Add(countLabel);

        details.Add(row);
    }

    // Result row
    var resultRow = new VisualElement();
    resultRow.AddToClassList("recipe-result-row");
    var resultLbl = new Label("Makes:");
    resultLbl.AddToClassList("recipe-result-label");
    resultRow.Add(resultLbl);
    var resultName = new Label($"{recipe.resultQuantity}x {RecipeData.FormatItemName(recipe.result)}");
    resultName.AddToClassList("recipe-result-name");
    resultRow.Add(resultName);
    details.Add(resultRow);

    // Mix button
    var mixBtn = new Button(() =>
    {
        ApothekeManager.Instance.Mix(recipe);
        Refresh();
    });
    mixBtn.text = "Mix";
    mixBtn.AddToClassList("recipe-action");
    mixBtn.SetEnabled(canMix);
    details.Add(mixBtn);

    card.Add(details);

    // Click to expand/collapse
    int idx = recipeIndex;
    header.RegisterCallback<ClickEvent>(evt =>
    {
        evt.StopPropagation();
        expandedRecipeIndex = expandedRecipeIndex == idx ? -1 : idx;
        RefreshRecipes();
    });

    return card;
}
```

**Step 5: Compile and verify**

Run: `read_console` — expect no compilation errors.

**Step 6: Commit**

```
feat: rewrite Craft tab with expandable grouped recipe cards
```

---

### Task 5: Clean up unused RecipeCard template

**Files:**
- Delete: `Assets/Resources/UI/Templates/RecipeCard.uxml`
- Delete: `Assets/Resources/UI/Templates/RecipeCard.uxml.meta`

**Step 1: Delete the files**

The template is no longer used since we build cards programmatically.

**Step 2: Compile and verify**

Run: `read_console` — expect no errors.

**Step 3: Commit**

```
chore: remove unused RecipeCard.uxml template
```

---

### Task 6: Visual verification

**Step 1: Take screenshot**

Open the Seeds overlay, switch to Craft tab, and take a screenshot to verify:
- Category headers appear (Pigments, Potions, Materials)
- Recipe cards show name + status dot
- Tapping a card expands to show ingredients with owned/needed counts
- Craftable recipes sort to top of their category
- Mix button is enabled only when all ingredients are met
- Display names show spaces instead of underscores

**Step 2: Run tests**

Run existing EditMode tests to verify no regressions.
