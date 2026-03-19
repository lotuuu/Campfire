# Craft Menu Redesign Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Apotheke craft tab from text-only expandable cards to a visually rich "cozy workshop" UI, and migrate recipe data from ScriptableObjects to server-authoritative config.

**Architecture:** Server already sends recipe data under the `"recipes"` key in `/game/configs`. We add `description_key` to each recipe on the server, parse recipes into a new `ServerRecipeConfig` DTO on the client, rewrite `ApothekeManager` to use server configs, then rebuild the craft tab UI with result icons, ingredient icon chips, and expand-in-place detail.

**Tech Stack:** Unity 6 UI Toolkit (USS/C#), Elixir/Phoenix server, MiniJson parsing

**Spec:** `docs/superpowers/specs/2026-03-19-craft-menu-redesign-design.md`

---

### Task 1: Add recipe description translations to the server

Add `description_key` to each recipe in the server seed data, and seed the English translation strings. Add Spanish config translations for recipe descriptions.

**Files:**
- Modify: `server/priv/repo/seeds.exs` (lines 933-1025 — recipe_configs)
- Modify: `server/priv/repo/translation_seeds.exs` (add English recipe description keys + Spanish ConfigTranslation entries)

- [ ] **Step 1: Add `description_key` to each recipe in seeds.exs**

In `server/priv/repo/seeds.exs`, add a `"description_key"` field to every recipe in the `recipe_configs` map. Pattern:

```elixir
"basil_pigment" => %{
  "ingredients" => [%{"itemKey" => item_key!.("basil"), "count" => 3}],
  "result_item" => item_key!.("basil_pigment"),
  "result_quantity" => 1,
  "category" => "Pigment",
  "description_key" => "recipe.basil_pigment.desc"
},
```

Apply to all 14 recipes:
- `basil_pigment`, `chamomile_pigment`, `dahlia_pigment`, `jasmine_pigment`, `lavender_pigment`, `marigold_pigment`, `mint_pigment`, `moonflower_pigment`, `pansy_pigment`, `poppy_pigment`, `rosemary_pigment`, `snowdrop_pigment`
- `speed_potion`, `fertilizer`

Key pattern: `"recipe.{recipe_key}.desc"`

- [ ] **Step 2: Add English translation entries in translation_seeds.exs**

In the `en = [...]` list in `server/priv/repo/translation_seeds.exs`, add entries for each recipe description and the new UI key. Place them after the existing tutorial/UI keys:

```elixir
# Recipe descriptions
{"recipe.basil_pigment.desc", "A rich green dye extracted from fresh basil leaves. Use it to paint your camp buildings."},
{"recipe.chamomile_pigment.desc", "A soft golden pigment distilled from chamomile flowers. Perfect for warm, sunny decorations."},
{"recipe.dahlia_pigment.desc", "A deep magenta dye pressed from dahlia petals. Adds a bold splash of color."},
{"recipe.jasmine_pigment.desc", "A delicate white pigment made from jasmine blooms. Gives a clean, elegant finish."},
{"recipe.lavender_pigment.desc", "A calming purple dye brewed from lavender sprigs. Brings a peaceful touch to your camp."},
{"recipe.marigold_pigment.desc", "A bright orange pigment ground from marigold petals. Warm and cheerful."},
{"recipe.mint_pigment.desc", "A cool teal dye infused from fresh mint. Crisp and refreshing."},
{"recipe.moonflower_pigment.desc", "A rare silvery pigment harvested under moonlight. Shimmers with an ethereal glow."},
{"recipe.pansy_pigment.desc", "A vibrant violet dye from pansy petals. Bold and expressive."},
{"recipe.poppy_pigment.desc", "A vivid red pigment pressed from wild poppies. Fiery and eye-catching."},
{"recipe.rosemary_pigment.desc", "A muted sage-green dye steeped from rosemary. Earthy and grounding."},
{"recipe.snowdrop_pigment.desc", "A pale frost-white pigment from snowdrop flowers. Delicate as fresh snow."},
{"recipe.speed_potion.desc", "A zippy tonic brewed from mint and chamomile. Speeds up plant growth for a short time."},
{"recipe.fertilizer.desc", "A hearty mix of berries and acorns. Apply to a plot to boost your next harvest."},
# New UI key for craft tab
{"ui.apotheke.ingredients", "Ingredients"},
```

- [ ] **Step 3: Add Spanish translations for recipe descriptions**

In the `es = [...]` list in `translation_seeds.exs`, add entries as regular translation tuples (recipe descriptions flow through the standard `translations` map via `Loc.Get()`, not `ConfigTranslation`):

```elixir
# Recipe descriptions
{"recipe.basil_pigment.desc", "Un rico tinte verde extraido de hojas frescas de albahaca. Usalo para pintar los edificios de tu campamento."},
{"recipe.chamomile_pigment.desc", "Un suave pigmento dorado destilado de flores de manzanilla. Perfecto para decoraciones calidas."},
{"recipe.dahlia_pigment.desc", "Un profundo tinte magenta prensado de petalos de dalia. Un toque audaz de color."},
{"recipe.jasmine_pigment.desc", "Un delicado pigmento blanco hecho de flores de jazmin. Un acabado limpio y elegante."},
{"recipe.lavender_pigment.desc", "Un tinte purpura calmante preparado con ramitas de lavanda. Un toque de paz para tu campamento."},
{"recipe.marigold_pigment.desc", "Un pigmento naranja brillante molido de petalos de calendula. Calido y alegre."},
{"recipe.mint_pigment.desc", "Un tinte verde azulado infusionado con menta fresca. Fresco y revitalizante."},
{"recipe.moonflower_pigment.desc", "Un raro pigmento plateado cosechado bajo la luz de la luna. Brilla con un resplandor etereo."},
{"recipe.pansy_pigment.desc", "Un vibrante tinte violeta de petalos de pensamiento. Audaz y expresivo."},
{"recipe.poppy_pigment.desc", "Un vivido pigmento rojo prensado de amapolas silvestres. Ardiente y llamativo."},
{"recipe.rosemary_pigment.desc", "Un tinte verde salvia suave macerado con romero. Terroso y reconfortante."},
{"recipe.snowdrop_pigment.desc", "Un palido pigmento blanco helado de flores de campanilla. Delicado como nieve fresca."},
{"recipe.speed_potion.desc", "Un tonico rapido preparado con menta y manzanilla. Acelera el crecimiento de las plantas."},
{"recipe.fertilizer.desc", "Una mezcla abundante de bayas y bellotas. Aplica a una parcela para mejorar tu proxima cosecha."},
# New UI key
{"ui.apotheke.ingredients", "Ingredientes"},
```

No changes needed to `game_controller.ex` — recipe descriptions flow through the standard `translations` map, not ConfigTranslation.

- [ ] **Step 5: Reseed the database and verify**

```bash
cd server && mix run priv/repo/seeds.exs && mix run priv/repo/translation_seeds.exs
```

Verify by hitting the configs endpoint:
```bash
curl -s http://localhost:4000/game/configs?locale=en | python3 -m json.tool | grep -A5 "basil_pigment"
```

Expected: each recipe has `description_key` field. The `translations` object in the response has the `recipe.*.desc` keys.

- [ ] **Step 6: Commit**

```bash
cd server
git add priv/repo/seeds.exs priv/repo/translation_seeds.exs
git commit -m "feat(server): add localized recipe descriptions to config"
```

---

### Task 2: Parse server recipes in ConfigService

Add `ServerRecipeConfig` DTO and parse the `"recipes"` key from the server config response. Expose via `GetRecipe()` and `GetAllRecipes()`.

**Files:**
- Modify: `Assets/Scripts/Services/ConfigService.cs` (add DTO + parsing + accessors)

- [ ] **Step 1: Add the DTOs to ConfigService.cs**

Add these classes near the other `Server*Config` DTOs at the top of the file (after `ServerQuestReward`, around line 80):

```csharp
[System.Serializable]
public class ServerRecipeIngredient
{
    public string itemKey;
    public int count;
}

[System.Serializable]
public class ServerRecipeConfig
{
    public string name;
    public string category;
    public string resultItem;
    public int resultQuantity;
    public string descriptionKey;
    public List<ServerRecipeIngredient> ingredients = new();
}
```

- [ ] **Step 2: Add the backing dictionary and accessors**

Add a private field near the other config dictionaries (around line 130):

```csharp
private Dictionary<string, ServerRecipeConfig> _recipeConfigs = new();
```

Add public accessors near the other `Get*` methods:

```csharp
public ServerRecipeConfig GetRecipe(string name) =>
    _recipeConfigs.TryGetValue(name, out var c) ? c : null;

public Dictionary<string, ServerRecipeConfig> GetAllRecipes() => _recipeConfigs;
```

- [ ] **Step 3: Add recipe parsing in ParseResponse()**

In `ParseResponse()`, add a new block after the items parsing (after line ~701). Follow the exact MiniJson pattern used for seeds/quests:

```csharp
// Recipes
if (root.TryGetValue("recipes", out var recipesObj) && recipesObj is Dictionary<string, object> recipesDict)
{
    _recipeConfigs = new Dictionary<string, ServerRecipeConfig>();
    foreach (var kv in recipesDict)
    {
        if (kv.Value is Dictionary<string, object> rMap)
        {
            var config = new ServerRecipeConfig
            {
                name = kv.Key,
                category = GetString(rMap, "category"),
                resultItem = GetString(rMap, "result_item"),
                resultQuantity = (int)GetFloat(rMap, "result_quantity", 1f),
                descriptionKey = GetString(rMap, "description_key")
            };

            if (rMap.TryGetValue("ingredients", out var ingsObj) && ingsObj is List<object> ings)
            {
                foreach (var item in ings)
                {
                    if (item is Dictionary<string, object> ingMap)
                    {
                        config.ingredients.Add(new ServerRecipeIngredient
                        {
                            itemKey = GetString(ingMap, "itemKey"),
                            count = (int)GetFloat(ingMap, "count", 1f)
                        });
                    }
                }
            }

            _recipeConfigs[kv.Key] = config;
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/ConfigService.cs
git commit -m "feat: parse server recipe configs in ConfigService"
```

---

### Task 3: Migrate ApothekeManager from ScriptableObjects to server config

Replace `Resources.LoadAll<RecipeData>` with `ConfigService.Instance.GetAllRecipes()`. Update `CanMix()`, `Mix()`, and `CraftOnServer()` to use `ServerRecipeConfig`.

**Files:**
- Modify: `Assets/Scripts/Managers/ApothekeManager.cs`

- [ ] **Step 1: Replace the recipe field and loading**

Change the field and `Awake()`:

```csharp
// OLD:
private RecipeData[] allRecipes;

private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    allRecipes = Resources.LoadAll<RecipeData>("Recipes");
}

public RecipeData[] AllRecipes => allRecipes;

// NEW:
public ServerRecipeConfig[] AllRecipes { get; private set; } = System.Array.Empty<ServerRecipeConfig>();

private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
}

/// <summary>
/// Called after ConfigService has fetched configs (e.g. from GameService init).
/// </summary>
public void LoadRecipesFromConfig()
{
    var all = ConfigService.Instance?.GetAllRecipes();
    if (all != null)
        AllRecipes = new List<ServerRecipeConfig>(all.Values).ToArray();
    else
        AllRecipes = System.Array.Empty<ServerRecipeConfig>();
}
```

- [ ] **Step 2: Update CanMix() for ServerRecipeConfig**

```csharp
// OLD:
public bool CanMix(RecipeData recipe)
{
    if (CurrencyManager.FreeMode) return true;
    var data = SaveManager.Instance.Data;
    foreach (var ing in recipe.ingredients)
    {
        var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
        if (item == null || item.count < ing.quantity) return false;
    }
    return true;
}

// NEW:
public bool CanMix(ServerRecipeConfig recipe)
{
    if (CurrencyManager.FreeMode) return true;
    var data = SaveManager.Instance.Data;
    foreach (var ing in recipe.ingredients)
    {
        var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
        if (item == null || item.count < ing.count) return false;
    }
    return true;
}
```

Note: `ing.quantity` → `ing.count`

- [ ] **Step 3: Update Mix() for ServerRecipeConfig**

```csharp
public bool Mix(ServerRecipeConfig recipe)
{
    if (!CanMix(recipe)) return false;
    var data = SaveManager.Instance.Data;

    if (!CurrencyManager.FreeMode)
    foreach (var ing in recipe.ingredients)
    {
        var item = data.inventory.Find(i => i.itemKey == ing.itemKey);
        if (item == null) continue;
        item.count -= ing.count;
        if (item.count <= 0) data.inventory.Remove(item);
    }

    var existing = data.inventory.Find(i => i.itemKey == recipe.resultItem);
    if (existing != null)
        existing.count += recipe.resultQuantity;
    else
        data.inventory.Add(new InventoryItem { itemKey = recipe.resultItem, count = recipe.resultQuantity });

    SaveManager.Instance.Save();
    AudioManager.Instance?.PlaySFX("apotheke_mix");
    if (EconomyService.Instance != null && !CurrencyManager.FreeMode)
    {
        foreach (var ing in recipe.ingredients)
        {
            var spendItems = new SpendItemsRequest
            {
                items = new List<SpendItemEntry> { new SpendItemEntry { item_key = ing.itemKey, count = ing.count } },
                freeMode = CurrencyManager.FreeMode
            };
            EconomyService.Instance.Enqueue("spend-items", JsonUtility.ToJson(spendItems));
        }
        EconomyService.Instance.Enqueue("add-items",
            JsonUtility.ToJson(new AddItemRequest { item_key = recipe.resultItem, count = recipe.resultQuantity }));
    }
    return true;
}
```

Key changes: `ing.quantity` → `ing.count`, `recipe.result` → `recipe.resultItem`

- [ ] **Step 4: Update CraftOnServer() for ServerRecipeConfig**

```csharp
public async Task<bool> CraftOnServer(ServerRecipeConfig recipe)
{
    if (!CanMix(recipe)) return false;

    if (GameService.Instance != null && GameService.Instance.IsOnline)
    {
        var result = await GameService.Instance.CraftApotheke(recipe.name);
        if (result == null) return false;

        if (EconomyService.Instance != null)
            EconomyService.Instance.Initialize();

        return true;
    }

    return Mix(recipe);
}
```

Key change: `recipe.recipeName` → `recipe.name`

- [ ] **Step 5: Call LoadRecipesFromConfig() during init**

Find where `ConfigService` configs are fetched (in `GameService` initialization). After configs are loaded, call:

```csharp
ApothekeManager.Instance?.LoadRecipesFromConfig();
```

Search for where `ConfigService.Instance.FetchConfigs()` is awaited in `GameService.cs` and add the call right after. This ensures recipes are loaded after the server data is available.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Managers/ApothekeManager.cs Assets/Scripts/Services/GameService.cs
git commit -m "feat: migrate ApothekeManager to server recipe configs"
```

---

### Task 4: Delete RecipeData ScriptableObject and assets

Remove the old ScriptableObject class, enum, and all recipe asset files now that everything uses server config.

**Files:**
- Delete: `Assets/Scripts/Data/RecipeData.cs` and its `.meta`
- Delete: all `.asset` and `.meta` files in `Assets/Resources/Recipes/`
- Modify: any remaining references (should be none after Task 3, but verify)

- [ ] **Step 1: Verify no remaining references to RecipeData**

```bash
grep -rn "RecipeData\|RecipeCategory\|IngredientEntry" Assets/Scripts/ --include="*.cs" | grep -v "RecipeData.cs"
```

Expected: no results. `RecipeData`, `RecipeCategory`, `IngredientEntry`, and `FormatItemName()` are all defined in `RecipeData.cs` and have no external callers after Task 3.

- [ ] **Step 2: Delete RecipeData.cs and its meta**

```bash
rm Assets/Scripts/Data/RecipeData.cs Assets/Scripts/Data/RecipeData.cs.meta
```

- [ ] **Step 3: Delete all recipe assets**

```bash
rm -rf Assets/Resources/Recipes/
```

- [ ] **Step 4: Commit**

```bash
git add -u Assets/Scripts/Data/RecipeData.cs Assets/Resources/Recipes/
git commit -m "chore: remove RecipeData ScriptableObjects (now server-authoritative)"
```

---

### Task 5: Redesign the craft tab UI — card layout and styling

Rewrite `BuildRecipeCard()` in `ApothekeUI.cs` and update `Apotheke.uss` with the new cozy workshop card design. This is the core visual change.

**Files:**
- Modify: `Assets/Scripts/UI/ApothekeUI.cs` (rewrite `RefreshRecipes()`, `BuildRecipeCard()`, `CategoryLabel()`, category sort)
- Modify: `Assets/UI/Styles/Apotheke.uss` (replace recipe card styles)

- [ ] **Step 1: Update category sorting and label in ApothekeUI.cs**

Replace the `RecipeCategory`-based sorting with string-based categories. Replace the `CategoryLabel(RecipeCategory)` method and the `SortedDictionary` in `RefreshRecipes()`:

```csharp
// Category sort order — matches old enum order
private static readonly Dictionary<string, int> CategorySortOrder = new()
{
    { "Pigment", 0 },
    { "Consumable", 1 },
    { "Material", 2 }
};

private static int GetCategorySortKey(string category)
{
    return CategorySortOrder.TryGetValue(category, out var order) ? order : 99;
}

private static string CategoryLabel(string category) => category switch
{
    "Pigment" => Loc.Get("ui.apotheke.pigments", "Pigments"),
    "Consumable" => Loc.Get("ui.apotheke.consumables", "Consumables"),
    "Material" => Loc.Get("ui.apotheke.materials", "Materials"),
    _ => category
};
```

- [ ] **Step 2: Rewrite RefreshRecipes() to use ServerRecipeConfig**

```csharp
private void RefreshRecipes()
{
    if (recipeList == null || ApothekeManager.Instance == null) return;

    var recipes = ApothekeManager.Instance.AllRecipes;
    if (recipes == null || recipes.Length == 0)
    {
        SyncPoolCount(recipeList, recipePool, 0);
        return;
    }

    var items = SaveManager.Instance?.Data?.inventory;

    // Group by category
    var grouped = new SortedDictionary<int, (string category, List<ServerRecipeConfig> recipes)>();
    foreach (var r in recipes)
    {
        int sortKey = GetCategorySortKey(r.category);
        if (!grouped.ContainsKey(sortKey))
            grouped[sortKey] = (r.category, new List<ServerRecipeConfig>());
        grouped[sortKey].recipes.Add(r);
    }

    var newElements = new List<VisualElement>();

    foreach (var kvp in grouped)
    {
        var (category, list) = kvp.Value;

        // Sort: craftable first, then alphabetical
        list.Sort((a, b) =>
        {
            bool canA = ApothekeManager.Instance.CanMix(a);
            bool canB = ApothekeManager.Instance.CanMix(b);
            if (canA != canB) return canA ? -1 : 1;
            return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
        });

        // Decorative category header
        var header = new VisualElement();
        header.AddToClassList("recipe-category-divider");

        var lineLeft = new VisualElement();
        lineLeft.AddToClassList("recipe-category-line");
        header.Add(lineLeft);

        var label = new Label(CategoryLabel(category).ToUpper());
        label.AddToClassList("recipe-category-label");
        header.Add(label);

        var lineRight = new VisualElement();
        lineRight.AddToClassList("recipe-category-line");
        header.Add(lineRight);

        newElements.Add(header);

        foreach (var recipe in list)
            newElements.Add(BuildRecipeCard(recipe, items));
    }

    SyncPoolCount(recipeList, recipePool, newElements.Count);
    for (int i = 0; i < newElements.Count; i++)
        ReplacePoolElement(recipeList, recipePool, i, newElements[i]);
}
```

- [ ] **Step 3: Rewrite BuildRecipeCard() with the new cozy workshop layout**

```csharp
private VisualElement BuildRecipeCard(ServerRecipeConfig recipe, List<InventoryItem> items)
{
    bool canMix = ApothekeManager.Instance.CanMix(recipe);
    int recipeIndex = System.Array.IndexOf(ApothekeManager.Instance.AllRecipes, recipe);
    bool isExpanded = recipeIndex == expandedRecipeIndex;

    var card = new VisualElement();
    card.AddToClassList("recipe-card");
    if (isExpanded) card.AddToClassList("recipe-card--expanded");
    if (canMix) card.AddToClassList("recipe-card--craftable");
    else card.AddToClassList("recipe-card--uncraftable");

    // ── Header row (always visible) ──
    var headerRow = new VisualElement();
    headerRow.AddToClassList("recipe-card-header");

    // Result icon (64px)
    var resultIcon = new VisualElement();
    resultIcon.AddToClassList("recipe-card-result-icon");
    var resultSprite = LoadItemSprite(recipe.resultItem);
    if (resultSprite != null)
        resultIcon.style.backgroundImage = new StyleBackground(resultSprite);
    headerRow.Add(resultIcon);

    // Name + ingredient chips column
    var infoCol = new VisualElement();
    infoCol.AddToClassList("recipe-card-info");

    var nameLabel = new Label(ConfigService.Instance.GetItemDisplayName(recipe.resultItem));
    nameLabel.AddToClassList("recipe-card-name");
    infoCol.Add(nameLabel);

    // Ingredient chips row
    var chipsRow = new VisualElement();
    chipsRow.AddToClassList("recipe-card-chips");

    for (int i = 0; i < recipe.ingredients.Count; i++)
    {
        var ing = recipe.ingredients[i];
        if (i > 0)
        {
            var plus = new Label("+");
            plus.AddToClassList("recipe-card-chip-plus");
            chipsRow.Add(plus);
        }

        var chip = new VisualElement();
        chip.AddToClassList("recipe-card-chip");

        var chipIcon = new VisualElement();
        chipIcon.AddToClassList("recipe-card-chip-icon");
        var ingSprite = LoadItemSprite(ing.itemKey);
        if (ingSprite != null)
            chipIcon.style.backgroundImage = new StyleBackground(ingSprite);
        chip.Add(chipIcon);

        int owned = 0;
        if (items != null)
        {
            var inv = items.Find(it => it.itemKey == ing.itemKey);
            if (inv != null) owned = inv.count;
        }
        bool satisfied = owned >= ing.count;

        var chipCount = new Label(satisfied ? $"{owned}" : $"{owned}/{ing.count}");
        chipCount.AddToClassList("recipe-card-chip-count");
        chipCount.AddToClassList(satisfied ? "recipe-card-chip-count--ok" : "recipe-card-chip-count--missing");
        chip.Add(chipCount);

        if (!satisfied) chip.AddToClassList("recipe-card-chip--missing");

        chipsRow.Add(chip);
    }
    infoCol.Add(chipsRow);
    headerRow.Add(infoCol);

    // Inline craft button (collapsed state, only when craftable)
    if (canMix && !isExpanded)
    {
        var inlineCraft = new Button();
        inlineCraft.clickable = new Clickable(() =>
        {
            ApothekeManager.Instance.Mix(recipe);
            Refresh();
        });
        inlineCraft.text = Loc.Get("ui.button.craft", "Craft");
        inlineCraft.AddToClassList("recipe-card-craft-inline");
        headerRow.Add(inlineCraft);
    }

    card.Add(headerRow);

    // ── Expanded details ──
    var details = new VisualElement();
    details.AddToClassList("recipe-card-details");

    // Flavor text
    string desc = Loc.Get(recipe.descriptionKey ?? "", "");
    if (!string.IsNullOrEmpty(desc))
    {
        var flavorLabel = new Label(desc);
        flavorLabel.AddToClassList("recipe-card-flavor");
        details.Add(flavorLabel);
    }

    // Ingredients section
    var ingTitle = new Label(Loc.Get("ui.apotheke.ingredients", "Ingredients").ToUpper());
    ingTitle.AddToClassList("recipe-card-ing-title");
    details.Add(ingTitle);

    foreach (var ing in recipe.ingredients)
    {
        var row = new VisualElement();
        row.AddToClassList("recipe-card-ing-row");

        var rowIcon = new VisualElement();
        rowIcon.AddToClassList("recipe-card-ing-icon");
        var sprite = LoadItemSprite(ing.itemKey);
        if (sprite != null)
            rowIcon.style.backgroundImage = new StyleBackground(sprite);
        row.Add(rowIcon);

        var rowInfo = new VisualElement();
        rowInfo.AddToClassList("recipe-card-ing-info");

        var ingName = new Label(ConfigService.Instance.GetItemDisplayName(ing.itemKey));
        ingName.AddToClassList("recipe-card-ing-name");
        rowInfo.Add(ingName);

        var ingCat = ConfigService.Instance.GetItem(ing.itemKey)?.category ?? "";
        if (!string.IsNullOrEmpty(ingCat))
        {
            var catLabel = new Label(ingCat.Substring(0, 1).ToUpper() + ingCat.Substring(1));
            catLabel.AddToClassList("recipe-card-ing-category");
            rowInfo.Add(catLabel);
        }
        row.Add(rowInfo);

        int owned = 0;
        if (items != null)
        {
            var inv = items.Find(it => it.itemKey == ing.itemKey);
            if (inv != null) owned = inv.count;
        }
        bool satisfied = owned >= ing.count;

        var countEl = new VisualElement();
        countEl.AddToClassList("recipe-card-ing-count");

        var ownedLabel = new Label($"{owned}");
        ownedLabel.AddToClassList(satisfied ? "recipe-card-ing-owned--ok" : "recipe-card-ing-owned--missing");
        countEl.Add(ownedLabel);

        var neededLabel = new Label($" / {ing.count}");
        neededLabel.AddToClassList("recipe-card-ing-needed");
        countEl.Add(neededLabel);

        row.Add(countEl);
        details.Add(row);
    }

    // Result row
    var resultRow = new VisualElement();
    resultRow.AddToClassList("recipe-card-result-row");

    var makesLabel = new Label(Loc.Get("ui.apotheke.makes", "Makes").ToUpper());
    makesLabel.AddToClassList("recipe-card-result-label");
    resultRow.Add(makesLabel);

    var resultSmallIcon = new VisualElement();
    resultSmallIcon.AddToClassList("recipe-card-result-small-icon");
    if (resultSprite != null)
        resultSmallIcon.style.backgroundImage = new StyleBackground(resultSprite);
    resultRow.Add(resultSmallIcon);

    var resultName = new Label($"{recipe.resultQuantity}x {ConfigService.Instance.GetItemDisplayName(recipe.resultItem)}");
    resultName.AddToClassList("recipe-card-result-name");
    resultRow.Add(resultName);

    details.Add(resultRow);

    // Full-width craft button
    var craftBtn = new Button();
    craftBtn.clickable = new Clickable(() =>
    {
        ApothekeManager.Instance.Mix(recipe);
        Refresh();
    });
    craftBtn.text = Loc.Get("ui.button.craft", "Craft");
    craftBtn.AddToClassList("recipe-card-craft-full");
    craftBtn.SetEnabled(canMix);
    details.Add(craftBtn);

    card.Add(details);

    // Tap to expand/collapse
    int idx = recipeIndex;
    RegisterTapInScrollView(headerRow, () =>
    {
        expandedRecipeIndex = expandedRecipeIndex == idx ? -1 : idx;
        RefreshRecipes();
    });

    return card;
}

private static Sprite LoadItemSprite(string itemKey)
{
    string key = SpriteService.ItemToSpriteKey(itemKey);
    if (key != null)
    {
        var sprite = SpriteService.Instance?.GetSprite(key);
        if (sprite != null) return sprite;
    }
    return null;
}
```

- [ ] **Step 4: Update Apotheke.uss with new recipe card styles**

Replace the recipe card section (everything from `.recipe-category-header` through `.recipe-action:disabled`) with the new styles:

```css
/* ── Decorative category divider ── */
.recipe-category-divider {
    flex-direction: row;
    align-items: center;
    margin-top: var(--spacing-md);
    margin-bottom: var(--spacing-sm);
}

.recipe-category-line {
    flex-grow: 1;
    height: 1px;
    background-color: rgba(180, 130, 60, 0.3);
}

.recipe-category-label {
    font-size: var(--font-xs);
    color: rgb(180, 150, 100);
    -unity-text-align: middle-center;
    padding: 0 var(--spacing-sm);
}

/* ── Recipe card (cozy workshop) ── */
.recipe-card {
    flex-direction: column;
    background-color: rgba(55, 40, 22, 0.7);
    border-width: 1px;
    border-color: rgba(120, 90, 45, 0.25);
    border-radius: var(--radius-md);
    padding: var(--spacing-sm);
    margin-bottom: var(--spacing-xs);
    transition-property: background-color, border-color;
    transition-duration: 0.15s;
}

.recipe-card:hover {
    background-color: rgba(65, 48, 28, 0.8);
    border-color: rgba(160, 110, 50, 0.4);
}

.recipe-card--expanded {
    border-color: rgba(220, 170, 70, 0.5);
    background-color: rgba(55, 40, 22, 0.8);
}

.recipe-card--craftable {
    border-color: rgba(140, 200, 70, 0.35);
}

/* Uncraftable cards fade — applied via C# (no --craftable class = add --uncraftable) */
.recipe-card--uncraftable {
    opacity: 0.7;
}

/* ── Header row ── */
.recipe-card-header {
    flex-direction: row;
    align-items: center;
}

.recipe-card-result-icon {
    width: 64px;
    height: 64px;
    border-radius: 10px;
    background-color: rgba(40, 28, 15, 0.6);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.2);
    margin-right: var(--spacing-sm);
    flex-shrink: 0;
    -unity-background-scale-mode: scale-to-fit;
}

.recipe-card-info {
    flex-grow: 1;
    flex-direction: column;
    min-width: 0;
}

.recipe-card-name {
    font-size: var(--font-md);
    color: rgb(240, 220, 170);
    -unity-font-style: bold;
    margin-bottom: 4px;
}

/* ── Ingredient chips ── */
.recipe-card-chips {
    flex-direction: row;
    align-items: center;
    flex-wrap: wrap;
}

.recipe-card-chip {
    flex-direction: row;
    align-items: center;
    background-color: rgba(40, 28, 15, 0.6);
    border-width: 1px;
    border-color: rgba(140, 200, 70, 0.3);
    border-radius: 6px;
    padding: 2px 6px;
    margin-right: 4px;
    margin-bottom: 4px;
}

.recipe-card-chip--missing {
    border-color: rgba(200, 120, 60, 0.3);
}

.recipe-card-chip-icon {
    width: 28px;
    height: 28px;
    -unity-background-scale-mode: scale-to-fit;
    margin-right: 4px;
}

.recipe-card-chip-count {
    font-size: var(--font-xs);
    -unity-font-style: bold;
}

.recipe-card-chip-count--ok {
    color: rgb(140, 200, 80);
}

.recipe-card-chip-count--missing {
    color: rgb(200, 120, 60);
}

.recipe-card-chip-plus {
    font-size: var(--font-sm);
    color: rgba(180, 150, 100, 0.4);
    margin: 0 2px;
}

/* ── Inline craft button (collapsed) ── */
.recipe-card-craft-inline {
    background-color: rgba(80, 130, 40, 0.5);
    border-width: 1px;
    border-color: rgba(120, 180, 60, 0.5);
    border-radius: 8px;
    padding: 8px 16px;
    color: rgb(200, 240, 150);
    font-size: var(--font-sm);
    -unity-font-style: bold;
    flex-shrink: 0;
    margin-left: var(--spacing-xs);
}

.recipe-card-craft-inline:hover {
    background-color: rgba(95, 150, 48, 0.65);
    border-color: rgba(140, 200, 70, 0.6);
}

/* ── Expanded details ── */
.recipe-card-details {
    flex-direction: column;
    margin-top: var(--spacing-sm);
    padding-top: var(--spacing-sm);
    border-top-width: 1px;
    border-top-color: rgba(180, 130, 60, 0.2);
    display: none;
}

.recipe-card--expanded .recipe-card-details {
    display: flex;
}

/* Flavor text */
.recipe-card-flavor {
    font-size: var(--font-sm);
    color: rgb(170, 150, 115);
    -unity-font-style: italic;
    margin-bottom: var(--spacing-sm);
    white-space: normal;
}

/* Ingredients title */
.recipe-card-ing-title {
    font-size: var(--font-xs);
    color: rgb(150, 130, 100);
    margin-bottom: var(--spacing-xs);
}

/* Ingredient row */
.recipe-card-ing-row {
    flex-direction: row;
    align-items: center;
    padding: var(--spacing-xs);
    background-color: rgba(40, 30, 18, 0.5);
    border-radius: 8px;
    margin-bottom: var(--spacing-xxs);
}

.recipe-card-ing-icon {
    width: 40px;
    height: 40px;
    border-radius: 8px;
    background-color: rgba(40, 28, 15, 0.8);
    border-width: 1px;
    border-color: rgba(140, 100, 50, 0.2);
    margin-right: var(--spacing-xs);
    flex-shrink: 0;
    -unity-background-scale-mode: scale-to-fit;
}

.recipe-card-ing-info {
    flex-grow: 1;
    flex-direction: column;
}

.recipe-card-ing-name {
    font-size: var(--font-sm);
    color: rgb(220, 200, 165);
}

.recipe-card-ing-category {
    font-size: var(--font-xs);
    color: rgb(140, 120, 90);
}

.recipe-card-ing-count {
    flex-direction: row;
    align-items: baseline;
    flex-shrink: 0;
    margin-left: var(--spacing-xs);
}

.recipe-card-ing-owned--ok {
    font-size: var(--font-md);
    color: rgb(140, 200, 80);
    -unity-font-style: bold;
}

.recipe-card-ing-owned--missing {
    font-size: var(--font-md);
    color: rgb(200, 120, 60);
    -unity-font-style: bold;
}

.recipe-card-ing-needed {
    font-size: var(--font-xs);
    color: rgb(140, 120, 90);
}

/* ── Result row ── */
.recipe-card-result-row {
    flex-direction: row;
    align-items: center;
    margin-top: var(--spacing-sm);
    padding-top: var(--spacing-xs);
    border-top-width: 1px;
    border-top-color: rgba(180, 130, 60, 0.15);
}

.recipe-card-result-label {
    font-size: var(--font-xs);
    color: rgb(150, 130, 100);
    margin-right: var(--spacing-xs);
}

.recipe-card-result-small-icon {
    width: 24px;
    height: 24px;
    -unity-background-scale-mode: scale-to-fit;
    margin-right: var(--spacing-xxs);
}

.recipe-card-result-name {
    font-size: var(--font-sm);
    color: rgb(180, 220, 120);
    -unity-font-style: bold;
}

/* ── Full-width craft button (expanded) ── */
.recipe-card-craft-full {
    min-height: 52px;
    margin-top: var(--spacing-sm);
    background-color: rgba(80, 130, 40, 0.5);
    border-width: 1px;
    border-color: rgba(120, 180, 60, 0.5);
    border-radius: 8px;
    color: rgb(200, 240, 150);
    font-size: var(--font-md);
    -unity-text-align: middle-center;
    -unity-font-style: bold;
}

.recipe-card-craft-full:hover {
    background-color: rgba(95, 150, 48, 0.65);
    border-color: rgba(140, 200, 70, 0.6);
}

.recipe-card-craft-full:disabled {
    background-color: rgba(40, 50, 22, 0.4);
    border-color: rgba(80, 100, 40, 0.2);
    color: rgb(100, 120, 75);
}
```

- [ ] **Step 5: Clean up old unused styles from Apotheke.uss**

Remove the old recipe card styles that are no longer used. **Keep `.recipe-category-header`** — it's still used by `RefreshSeeds()` in the inventory tab (line 223).

Remove:
- `.recipe-card-status`, `.recipe-card-status--ready`, `.recipe-card-status--missing` (replaced by craftable border + opacity)
- `.recipe-ingredients-title` (replaced by `.recipe-card-ing-title`)
- `.recipe-ingredient-row`, `.recipe-ingredient-name`, `.recipe-ingredient-count`, `.recipe-ingredient-count--satisfied`, `.recipe-ingredient-count--missing` (replaced by new `.recipe-card-ing-*` classes)
- `.recipe-result-row`, `.recipe-result-label`, `.recipe-result-name` (replaced by new `.recipe-card-result-*` classes)
- `.recipe-action`, `.recipe-action:hover`, `.recipe-action:disabled` (replaced by `.recipe-card-craft-full`)

Also remove the `.craft-item`, `.craft-name`, `.craft-cost`, `.craft-action` styles from `Build.uss` if they're only used by the old Apotheke craft tab (check `CraftItem.uxml` template usage first).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/ApothekeUI.cs Assets/UI/Styles/Apotheke.uss
git commit -m "feat: redesign craft tab with cozy workshop card layout"
```

---

### Task 6: Verify and polish in Unity

Open the project, run through the craft tab, and fix any visual or functional issues.

**Files:**
- Potentially: minor tweaks to `ApothekeUI.cs` or `Apotheke.uss`

- [ ] **Step 1: Check Unity console for compilation errors**

Use `read_console` MCP tool or open Unity — verify no compile errors after all changes.

- [ ] **Step 2: Open the Apotheke overlay and check the Craft tab**

Verify:
- Category dividers render with horizontal lines + centered label
- Craftable recipe cards show: result icon, name, ingredient chips with green counts, inline Craft button
- Uncraftable cards show: faded opacity, orange counts on missing ingredients, no Craft button
- Tapping a card expands it with flavor text, full ingredient rows, result row, and full-width Craft button
- Tapping again collapses it
- Only one card expanded at a time
- Crafting a recipe works (updates inventory, plays sound, card state updates)

- [ ] **Step 3: Fix any visual issues**

Common things to check:
- Ingredient chip icons loading correctly from SpriteService
- Result icon loading correctly
- Text truncation on long recipe/ingredient names
- Scroll behavior still works properly (no jumps from the pool pattern)
- Flavor text wrapping correctly (`white-space: normal` in USS)
- Spacing and padding feel right on mobile-scale viewport

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "fix: craft tab visual polish and fixes"
```
