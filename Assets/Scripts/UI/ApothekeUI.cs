using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ApothekeUI : MonoBehaviour
    {
        /// <summary>
        /// Registers a tap recognizer on an element that tolerates more finger movement
        /// than the default ClickEvent (which gets swallowed by ScrollView on slight drags).
        /// Uses PointerDown/PointerUp with a generous threshold so taps inside scroll views
        /// fire reliably on mobile.
        /// </summary>
        private static void RegisterTapInScrollView(VisualElement target, System.Action onTap, float threshold = 30f)
        {
            Vector3 downPos = Vector3.zero;
            target.RegisterCallback<PointerDownEvent>(e => downPos = e.position, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(e =>
            {
                if (Vector3.Distance(downPos, e.position) < threshold)
                {
                    e.StopPropagation();
                    onTap();
                }
            }, TrickleDown.TrickleDown);
        }

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

            RefreshTabLabels();
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += RefreshTabLabels;

            ShowTab(0);
        }

        private void RefreshTabLabels()
        {
            if (tabInventory != null) tabInventory.text = Loc.Get("ui.apotheke.inventory", "Inventory");
            if (tabCraft != null) tabCraft.text = Loc.Get("ui.button.craft", "Craft");
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= RefreshTabLabels;
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
            InventoryCategory.Seeds => Loc.Get("ui.apotheke.seeds", "Seeds"),
            InventoryCategory.Yields => Loc.Get("ui.apotheke.harvests", "Harvests"),
            InventoryCategory.Pigments => Loc.Get("ui.apotheke.pigments", "Pigments"),
            InventoryCategory.Consumables => Loc.Get("ui.apotheke.consumables", "Consumables"),
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
                    var title = new Label(Loc.Get("ui.apotheke.growth_recipe", "Growth Recipe"));
                    title.AddToClassList("seed-recipe-title");
                    details.Add(title);

                    AddRecipeDimensions(details, seedData.recipe);
                }

                var dropsRow = new VisualElement();
                dropsRow.AddToClassList("seed-recipe-row");
                var dropsLabel = new Label(Loc.Get("ui.apotheke.drops", "Drops"));
                dropsLabel.AddToClassList("seed-recipe-label");
                dropsRow.Add(dropsLabel);
                var dropsValue = new Label($"{seedData.minDrops}-{seedData.maxDrops}");
                dropsValue.AddToClassList("seed-recipe-value");
                dropsRow.Add(dropsValue);
                details.Add(dropsRow);

                var durationRow = new VisualElement();
                durationRow.AddToClassList("seed-recipe-row");
                var durLabel = new Label(Loc.Get("ui.apotheke.growth_time", "Growth time"));
                durLabel.AddToClassList("seed-recipe-label");
                durationRow.Add(durLabel);
                var durValue = new Label(TimeUtils.FormatDurationHours(seedData.growthDurationHours));
                durValue.AddToClassList("seed-recipe-value");
                durationRow.Add(durValue);
                details.Add(durationRow);
            }
            else
            {
                var noRecipe = new Label(Loc.Get("ui.apotheke.no_recipe", "No recipe data"));
                noRecipe.AddToClassList("seed-recipe-label");
                details.Add(noRecipe);
            }

            card.Add(details);

            int idx = index;
            RegisterTapInScrollView(header, () =>
            {
                expandedIndex = expandedIndex == idx ? -1 : idx;
                RefreshSeeds();
            });

            return card;
        }

        internal static void AddRecipeDimensions(VisualElement container, GrowthRecipe recipe)
        {
            if (recipe.useHeat)
                AddDimensionRow(container, Loc.Get("ui.recipe.heat", "Heat"), $"{recipe.idealTempMin}-{recipe.idealTempMax}\u00b0C", recipe.heatWeight);
            if (recipe.useWind)
                AddDimensionRow(container, Loc.Get("ui.recipe.wind", "Wind"), $"{recipe.idealWindMin}-{recipe.idealWindMax} m/s", recipe.windWeight);
            if (recipe.useHumidity)
                AddDimensionRow(container, Loc.Get("ui.recipe.humidity", "Humidity"), $"{recipe.idealHumidityMin}-{recipe.idealHumidityMax}%", recipe.humidityWeight);
            if (recipe.useSunlight)
                AddDimensionRow(container, Loc.Get("ui.recipe.sunlight", "Sunlight"), $"{recipe.idealSunlightMin}-{recipe.idealSunlightMax}%", recipe.sunlightWeight);
            if (recipe.useRain)
            {
                int minPct = Mathf.RoundToInt(recipe.idealRainMin * 100f);
                int maxPct = Mathf.RoundToInt(recipe.idealRainMax * 100f);
                AddDimensionRow(container, Loc.Get("ui.recipe.rain", "Rain"), $"{minPct}-{maxPct}%", recipe.rainWeight);
            }
            if (recipe.useMoon)
                AddDimensionRow(container, Loc.Get("ui.recipe.moon", "Moon"), recipe.requiredMoonPhase.ToString(), recipe.moonWeight);
            if (recipe.useWaterings)
            {
                string waterStr = recipe.idealWateringsMin == recipe.idealWateringsMax
                    ? $"{recipe.idealWateringsMin}"
                    : $"{recipe.idealWateringsMin}-{recipe.idealWateringsMax}";
                AddDimensionRow(container, Loc.Get("ui.recipe.waterings", "Waterings"), waterStr, recipe.wateringsWeight);
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

        // Sort order for recipe categories (string-based, replacing the old enum)
        private static readonly Dictionary<string, int> CategorySortOrder = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Pigment", 0 },
            { "Consumable", 1 },
            { "Material", 2 }
        };

        private static string RecipeCategoryLabel(string category)
        {
            if (category == null) return "Other";
            var lower = category.ToLowerInvariant();
            return lower switch
            {
                "pigment" => Loc.Get("ui.apotheke.pigments", "Pigments"),
                "consumable" => Loc.Get("ui.apotheke.consumables", "Consumables"),
                "material" => Loc.Get("ui.apotheke.materials", "Materials"),
                _ => category
            };
        }

        private static int GetCategorySortOrder(string category)
        {
            if (category != null && CategorySortOrder.TryGetValue(category, out var order))
                return order;
            return 99;
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

            // Group by category
            var grouped = new SortedDictionary<int, (string category, List<ServerRecipeConfig> recipes)>();
            foreach (var r in recipes)
            {
                int sortKey = GetCategorySortOrder(r.category);
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

                // Decorative category divider
                var divider = new VisualElement();
                divider.AddToClassList("recipe-category-divider");

                var lineLeft = new VisualElement();
                lineLeft.AddToClassList("recipe-category-line");
                divider.Add(lineLeft);

                var label = new Label(RecipeCategoryLabel(category).ToUpper());
                label.AddToClassList("recipe-category-label");
                divider.Add(label);

                var lineRight = new VisualElement();
                lineRight.AddToClassList("recipe-category-line");
                divider.Add(lineRight);

                newElements.Add(divider);

                foreach (var recipe in list)
                    newElements.Add(BuildRecipeCard(recipe, items));
            }

            SyncPoolCount(recipeList, recipePool, newElements.Count);
            for (int i = 0; i < newElements.Count; i++)
                ReplacePoolElement(recipeList, recipePool, i, newElements[i]);
        }

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

            // Inline craft button (collapsed state, always visible)
            if (!isExpanded)
            {
                var inlineCraft = new Button();
                inlineCraft.clickable = new Clickable(() =>
                {
                    ApothekeManager.Instance.Mix(recipe);
                    Refresh();
                });
                inlineCraft.text = Loc.Get("ui.button.craft", "Craft");
                inlineCraft.AddToClassList("recipe-card-craft-inline");
                inlineCraft.SetEnabled(canMix);
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

    }
}
