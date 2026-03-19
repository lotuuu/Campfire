# Craft Menu Redesign

## Summary

Completely redesign the Apotheke craft tab from a text-only expandable list into a visually rich "cozy workshop" UI. Migrate recipe data from client-side ScriptableObjects to server-authoritative config (already partially in place). Add localized descriptions/flavor text to recipes.

## Current State

**Craft tab problems:**
- Collapsed recipe cards show only a name + tiny 16px status dot — no icons, no visual identity
- Must expand to see any useful information (ingredients, result, costs)
- Ingredient list is text-only — no item icons
- Every card looks identical; feels like a debug screen

**Data source:** `ApothekeManager` loads `RecipeData` ScriptableObjects from `Resources/Recipes/`. However, the server already sends recipe data under the `"recipes"` key in the `/game/configs` response (sourced from `ConfigCache.get("recipe_configs")` internally). The server also validates crafts server-side in `Apotheke.craft/3`. The client just doesn't parse the server recipe data yet.

## Design

### Recipe Card — Collapsed State

Each recipe card shows:
- **Result icon** (64px) on the left — loaded from `SpriteService` via item sprite key, gives each recipe a unique visual identity
- **Recipe name** (bold, gold text) — derived from `ConfigService.Instance.GetItemDisplayName(resultItem)` (the result item's display name)
- **Ingredient chips** — row of small (28px) icon chips with count labels. Green count = have enough, orange = missing
- **Craft button** (inline, right-aligned) — only shown when all ingredients are satisfied
- Uncraftable recipes: faded opacity, no Craft button

### Recipe Card — Expanded State (tap to toggle)

Tapping a card expands it in-place (only one expanded at a time):
- **Flavor text** — italic description under the recipe name, loaded via `Loc.Get(config.descriptionKey, "")` (empty string fallback — no description shown if translation missing)
- **Full ingredient list** — each ingredient as a full row:
  - 40px icon (from SpriteService — if sprite missing, show empty dark placeholder matching existing inventory tab behavior)
  - Item name (from `ConfigService.Instance.GetItemDisplayName(itemKey)`)
  - Category label (from `ConfigService.Instance.GetItem(itemKey)?.category`)
  - Owned / Needed count (green if satisfied, orange if missing)
- **Result row** — "Makes: [icon] 1x Red Pigment"
- **Full-width Craft button** — larger, more prominent than collapsed state

### Category Headers

Decorative dividers between recipe categories (Pigments, Consumables, Materials):
- Centered uppercase label with horizontal lines on each side
- Replaces the current plain bold text headers

### Sorting

Grouped by category with explicit sort order: Pigment=0, Consumable=1, Material=2 (matching current `RecipeCategory` enum order). Within each category: craftable first, then alphabetical by recipe name.

## Data Migration: Recipes to Server Config

### Server Changes

The server already sends recipes under the `"recipes"` key in the config response (line 172 of `game_controller.ex`). Add a `description_key` field to each recipe in `seeds.exs`:

```elixir
"basil_pigment" => %{
  "ingredients" => [%{"itemKey" => "basil", "count" => 3}],
  "result_item" => "basil_pigment",
  "result_quantity" => 1,
  "category" => "Pigment",
  "description_key" => "recipe.basil_pigment.desc"
}
```

Add corresponding translation entries to the server's translations seed data (same place other `Loc` keys are seeded). All ~14 existing recipes need description keys. English flavor text should be written for each recipe describing what the item is and what it's used for.

### Client Changes

**New DTO in ConfigService:**

```csharp
public class ServerRecipeConfig
{
    public string name;           // recipe key (e.g. "basil_pigment")
    public string category;       // "Pigment", "Consumable", "Material"
    public string resultItem;
    public int resultQuantity;
    public string descriptionKey; // localization key for flavor text
    public List<ServerRecipeIngredient> ingredients;
}

public class ServerRecipeIngredient
{
    public string itemKey;
    public int count;
}
```

**ConfigService parsing:** Parse the `"recipes"` key from the server response in `ParseResponse()` using MiniJson manual parsing (matching the existing pattern for quests, seeds, etc. — not JsonUtility). Store as `Dictionary<string, ServerRecipeConfig>`. Expose via `GetRecipe(name)` and `GetAllRecipes()`.

**ApothekeManager:** Replace `Resources.LoadAll<RecipeData>("Recipes")` with `ConfigService.Instance.GetAllRecipes()`. Update `CanMix()` and `Mix()` to work with `ServerRecipeConfig` instead of `RecipeData`. Note: ingredient field changes from `quantity` to `count` at all call sites. The UI Craft button should use `CraftOnServer()` for server-authoritative crafting with `Mix()` as the optimistic local fallback (existing pattern).

**Relocate `RecipeData.FormatItemName()`:** This static utility method lives on `RecipeData` but may be used elsewhere. Move it to a utility class or remove it if all callers already use `ConfigService.Instance.GetItemDisplayName()` instead.

**Delete:** `RecipeData.cs`, `IngredientEntry.cs` (if separate), `RecipeCategory` enum (replace with string-based category from server), and all `.asset` files in `Resources/Recipes/` once migration is complete.

## UI Implementation

### Files Changed

- `ApothekeUI.cs` — rewrite `RefreshRecipes()` and `BuildRecipeCard()` to use new card layout with `ServerRecipeConfig`
- `Apotheke.uss` — replace recipe card styles with new cozy workshop styling
- `CampFireRoot.uxml` — no structural changes needed (recipe-list ScrollView stays)

### No New Templates

Recipe cards are built programmatically in `BuildRecipeCard()` (current pattern). No new UXML template needed — keeps consistency with existing approach.

### Ingredient Icons

Use existing `SpriteService` pipeline:
- `SpriteService.ItemToSpriteKey(itemKey)` for harvest/item icons
- `SpriteService.GetSprite(key)` for the Sprite
- **Fallback when sprite missing:** show empty dark placeholder (same behavior as inventory tab's `BuildItemCard()`)

Same pattern already used by `BuildCardHelper.LoadHarvestIcon()` and the inventory tab's `BuildItemCard()`.

## Out of Scope

- Inventory tab redesign (works fine for now)
- Craft animations/feedback (can add later)
- New recipe content (just redesigning existing recipes' presentation)
