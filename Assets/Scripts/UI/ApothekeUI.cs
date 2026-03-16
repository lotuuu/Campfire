using System.Collections.Generic;
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
        private int expandedIndex = -1;
        private int expandedRecipeIndex = -1;

        // Card element pools — reused across refreshes to avoid DOM churn
        // that causes ScrollView to clamp scrollOffset and visibly jump.
        private readonly List<VisualElement> seedPool = new();
        private readonly List<VisualElement> recipePool = new();

        public void Initialize(VisualElement root)
        {
            tabInventory = root.Q<Button>("tab-inventory");
            tabCraft = root.Q<Button>("tab-craft");
            inventoryView = root.Q("apotheke-inventory");
            craftView = root.Q("apotheke-craft");
            inventoryEmpty = root.Q<Label>("inventory-empty");
            seedList = root.Q("seed-list");
            recipeList = root.Q("recipe-list");

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

        private enum InventoryCategory { Seeds, Yields, Pigments, Consumables }

        private static InventoryCategory CategorizeItem(string itemKey)
        {
            var config = ConfigService.Instance?.GetItem(itemKey);
            if (config == null) return InventoryCategory.Yields;
            return config.category switch
            {
                "seed" => InventoryCategory.Seeds,
                "pigment" => InventoryCategory.Pigments,
                "potion" or "consumable" or "material" => InventoryCategory.Consumables,
                _ => InventoryCategory.Yields
            };
        }

        private static string CategoryLabel(InventoryCategory cat) => cat switch
        {
            InventoryCategory.Seeds => "Seeds",
            InventoryCategory.Yields => "Harvests",
            InventoryCategory.Pigments => "Pigments",
            InventoryCategory.Consumables => "Consumables",
            _ => cat.ToString()
        };

        // Display order for inventory categories
        private static readonly InventoryCategory[] CategoryOrder =
        {
            InventoryCategory.Seeds,
            InventoryCategory.Yields,
            InventoryCategory.Pigments,
            InventoryCategory.Consumables
        };

        /// <summary>
        /// Ensures a container has exactly <paramref name="needed"/> children in the pool,
        /// adding new placeholder elements or removing excess from the end.
        /// Never calls Clear() — DOM mutations are minimal to prevent scroll jumps.
        /// </summary>
        private static void SyncPoolCount(VisualElement container, List<VisualElement> pool, int needed)
        {
            while (pool.Count < needed)
            {
                var el = new VisualElement();
                pool.Add(el);
                container.Add(el);
            }
            while (pool.Count > needed)
            {
                var last = pool[pool.Count - 1];
                last.RemoveFromHierarchy();
                pool.RemoveAt(pool.Count - 1);
            }
        }

        /// <summary>
        /// Replaces the element at the given index in both the pool and the container's
        /// child list, preserving the position in the DOM hierarchy.
        /// </summary>
        private static void ReplacePoolElement(VisualElement container, List<VisualElement> pool, int index, VisualElement newElement)
        {
            var old = pool[index];
            if (old.parent == container)
            {
                int childIndex = container.IndexOf(old);
                container.Remove(old);
                container.Insert(childIndex, newElement);
            }
            else
            {
                container.Add(newElement);
            }
            pool[index] = newElement;
        }

        private void RefreshSeeds()
        {
            if (seedList == null) return;

            var allItems = ApothekeManager.Instance?.Items;
            if (allItems == null || allItems.Count == 0)
            {
                SyncPoolCount(seedList, seedPool, 0);
                if (inventoryEmpty != null) inventoryEmpty.style.display = DisplayStyle.Flex;
                return;
            }

            var groups = new Dictionary<InventoryCategory, List<InventoryItem>>();
            foreach (var item in allItems)
            {
                if (item.count <= 0) continue;
                var cat = CategorizeItem(item.itemKey);
                if (!groups.ContainsKey(cat))
                    groups[cat] = new List<InventoryItem>();
                groups[cat].Add(item);
            }

            if (groups.Count == 0)
            {
                SyncPoolCount(seedList, seedPool, 0);
                if (inventoryEmpty != null) inventoryEmpty.style.display = DisplayStyle.Flex;
                return;
            }
            if (inventoryEmpty != null) inventoryEmpty.style.display = DisplayStyle.None;

            // Sort seeds by tier then growth duration (fastest first)
            if (groups.TryGetValue(InventoryCategory.Seeds, out var seedGroup))
            {
                seedGroup.Sort((a, b) =>
                {
                    var sa = ConfigService.Instance?.GetSeed(SpriteService.SeedToSpriteKey(a.itemKey));
                    var sb = ConfigService.Instance?.GetSeed(SpriteService.SeedToSpriteKey(b.itemKey));
                    int tierCmp = (sa?.tier ?? 99).CompareTo(sb?.tier ?? 99);
                    if (tierCmp != 0) return tierCmp;
                    return (sa?.growthDurationHours ?? 99f).CompareTo(sb?.growthDurationHours ?? 99f);
                });
            }

            // Build the new elements into a flat list (headers + grids interleaved)
            var newElements = new List<VisualElement>();
            int seedIndex = 0;
            foreach (var cat in CategoryOrder)
            {
                if (!groups.TryGetValue(cat, out var items)) continue;

                var header = new Label(CategoryLabel(cat));
                header.AddToClassList("recipe-category-header");
                newElements.Add(header);

                var grid = new VisualElement();
                grid.AddToClassList("inventory-grid");

                foreach (var entry in items)
                {
                    if (cat == InventoryCategory.Seeds)
                    {
                        var plantName = SpriteService.SeedToSpriteKey(entry.itemKey);
                        var seedConfig = ConfigService.Instance?.GetSeed(plantName);
                        var card = BuildSeedCard(entry, seedConfig, seedIndex++);
                        grid.Add(card);
                    }
                    else
                    {
                        grid.Add(BuildItemCard(entry));
                    }
                }

                newElements.Add(grid);
            }

            // Sync pool count then replace each element in place
            SyncPoolCount(seedList, seedPool, newElements.Count);
            for (int i = 0; i < newElements.Count; i++)
            {
                ReplacePoolElement(seedList, seedPool, i, newElements[i]);
            }
        }

        private VisualElement BuildItemCard(InventoryItem entry)
        {
            var card = new VisualElement();
            card.AddToClassList("seed-card");

            var header = new VisualElement();
            header.AddToClassList("seed-card-header");

            var icon = new VisualElement();
            icon.AddToClassList("seed-icon");
            var sprite = SpriteService.Instance?.GetSprite(SpriteService.ItemToSpriteKey(entry.itemKey));
            if (sprite != null)
                icon.style.backgroundImage = new StyleBackground(sprite);
            header.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("seed-info");
            var nameLabel = new Label(ConfigService.Instance.GetItemDisplayName(entry.itemKey));
            nameLabel.AddToClassList("seed-name");
            info.Add(nameLabel);
            var countLabel = new Label($"x{entry.count}");
            countLabel.AddToClassList("seed-count");
            info.Add(countLabel);
            header.Add(info);

            card.Add(header);
            return card;
        }

        private VisualElement BuildSeedCard(InventoryItem entry, ServerSeedConfig seedData, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("seed-card");
            if (index == expandedIndex) card.AddToClassList("seed-card--expanded");

            var header = new VisualElement();
            header.AddToClassList("seed-card-header");

            var icon = new VisualElement();
            icon.AddToClassList("seed-icon");
            if (seedData != null)
            {
                var sprite = SpriteService.Instance?.GetSprite(SpriteService.ItemToSpriteKey(entry.itemKey));
                if (sprite != null)
                    icon.style.backgroundImage = new StyleBackground(sprite);
            }
            header.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("seed-info");
            var nameLabel = new Label(ConfigService.Instance.GetItemDisplayName(entry.itemKey));
            nameLabel.AddToClassList("seed-name");
            info.Add(nameLabel);
            var countLabel = new Label($"x{entry.count}");
            countLabel.AddToClassList("seed-count");
            info.Add(countLabel);
            header.Add(info);

            card.Add(header);

            var details = new VisualElement();
            details.AddToClassList("seed-card-details");

            if (seedData != null)
            {
                if (seedData.recipe != null)
                {
                    var title = new Label("Growth Recipe");
                    title.AddToClassList("seed-recipe-title");
                    details.Add(title);

                    AddRecipeDimensions(details, seedData.recipe);
                }

                var dropsRow = new VisualElement();
                dropsRow.AddToClassList("seed-recipe-row");
                var dropsLabel = new Label("Drops");
                dropsLabel.AddToClassList("seed-recipe-label");
                dropsRow.Add(dropsLabel);
                var dropsValue = new Label($"{seedData.minDrops}-{seedData.maxDrops}");
                dropsValue.AddToClassList("seed-recipe-value");
                dropsRow.Add(dropsValue);
                details.Add(dropsRow);

                var durationRow = new VisualElement();
                durationRow.AddToClassList("seed-recipe-row");
                var durLabel = new Label("Growth time");
                durLabel.AddToClassList("seed-recipe-label");
                durationRow.Add(durLabel);
                var durValue = new Label(TimeUtils.FormatDurationHours(seedData.growthDurationHours));
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

            int idx = index;
            header.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                expandedIndex = expandedIndex == idx ? -1 : idx;
                RefreshSeeds();
            });

            return card;
        }

        internal static void AddRecipeDimensions(VisualElement container, GrowthRecipe recipe)
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
            {
                string waterStr = recipe.idealWateringsMin == recipe.idealWateringsMax
                    ? $"{recipe.idealWateringsMin}"
                    : $"{recipe.idealWateringsMin}-{recipe.idealWateringsMax}";
                AddDimensionRow(container, "Waterings", waterStr, recipe.wateringsWeight);
            }
        }

        internal static void AddDimensionRow(VisualElement container, string label, string value, float weight)
        {
            var row = new VisualElement();
            row.AddToClassList("seed-recipe-row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("seed-recipe-label");
            row.Add(labelEl);

            var valueEl = new Label(value);
            valueEl.AddToClassList("seed-recipe-value");
            row.Add(valueEl);


            container.Add(row);
        }

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

            // Group by category, craftable first within each group
            var grouped = new System.Collections.Generic.SortedDictionary<RecipeCategory,
                List<RecipeData>>();
            foreach (var r in recipes)
            {
                if (!grouped.ContainsKey(r.category))
                    grouped[r.category] = new List<RecipeData>();
                grouped[r.category].Add(r);
            }

            // Build the new elements into a flat list (headers + cards interleaved)
            var newElements = new List<VisualElement>();

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
                newElements.Add(header);

                foreach (var recipe in kvp.Value)
                {
                    var card = BuildRecipeCard(recipe, items);
                    newElements.Add(card);
                }
            }

            // Sync pool count then replace each element in place
            SyncPoolCount(recipeList, recipePool, newElements.Count);
            for (int i = 0; i < newElements.Count; i++)
            {
                ReplacePoolElement(recipeList, recipePool, i, newElements[i]);
            }
        }

        private static string CategoryLabel(RecipeCategory cat)
        {
            return cat switch
            {
                RecipeCategory.Pigment => "Pigments",
                RecipeCategory.Consumable => "Consumables",
                RecipeCategory.Material => "Materials",
                _ => cat.ToString()
            };
        }

        private VisualElement BuildRecipeCard(RecipeData recipe, List<InventoryItem> items)
        {
            bool canMix = ApothekeManager.Instance.CanMix(recipe);
            int recipeIndex = System.Array.IndexOf(ApothekeManager.Instance.AllRecipes, recipe);
            bool isExpanded = recipeIndex == expandedRecipeIndex;

            var card = new VisualElement();
            card.AddToClassList("recipe-card");
            if (isExpanded) card.AddToClassList("recipe-card--expanded");
            if (canMix) card.AddToClassList("recipe-card--craftable");

            // Header row (always visible)
            var headerRow = new VisualElement();
            headerRow.AddToClassList("recipe-card-header");

            var nameLabel = new Label(recipe.recipeName);
            nameLabel.AddToClassList("recipe-card-name");
            headerRow.Add(nameLabel);

            var status = new VisualElement();
            status.AddToClassList("recipe-card-status");
            status.AddToClassList(canMix ? "recipe-card-status--ready" : "recipe-card-status--missing");
            headerRow.Add(status);

            card.Add(headerRow);

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

                var ingName = new Label(ConfigService.Instance.GetItemDisplayName(ing.itemKey));
                ingName.AddToClassList("recipe-ingredient-name");
                row.Add(ingName);

                int owned = 0;
                if (items != null)
                {
                    var item = items.Find(i => i.itemKey == ing.itemKey);
                    if (item != null) owned = item.count;
                }
                bool satisfied = owned >= ing.quantity;

                string prefix = satisfied ? "\u2713 " : "\u2717 ";
                var countLabel = new Label($"{prefix}{owned}/{ing.quantity}");
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
            var resultName = new Label($"{recipe.resultQuantity}x {ConfigService.Instance.GetItemDisplayName(recipe.result)}");
            resultName.AddToClassList("recipe-result-name");
            resultRow.Add(resultName);
            details.Add(resultRow);

            // Mix button — use clickable assignment to avoid handler accumulation on reuse
            var mixBtn = new Button();
            mixBtn.clickable = new Clickable(() =>
            {
                ApothekeManager.Instance.Mix(recipe);
                Refresh();
            });
            mixBtn.text = "Craft";
            mixBtn.AddToClassList("recipe-action");
            mixBtn.SetEnabled(canMix);
            details.Add(mixBtn);

            card.Add(details);

            // Click to expand/collapse
            int idx = recipeIndex;
            headerRow.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                expandedRecipeIndex = expandedRecipeIndex == idx ? -1 : idx;
                RefreshRecipes();
            });

            return card;
        }

    }
}
