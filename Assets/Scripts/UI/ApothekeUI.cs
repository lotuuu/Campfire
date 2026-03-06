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
        private SeedData[] allSeeds;
        private int expandedIndex = -1;
        private int expandedRecipeIndex = -1;

        public void Initialize(VisualElement root)
        {
            tabInventory = root.Q<Button>("tab-inventory");
            tabCraft = root.Q<Button>("tab-craft");
            inventoryView = root.Q("apotheke-inventory");
            craftView = root.Q("apotheke-craft");
            inventoryEmpty = root.Q<Label>("inventory-empty");
            seedList = root.Q("seed-list");
            recipeList = root.Q("recipe-list");

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

            var header = new VisualElement();
            header.AddToClassList("seed-card-header");

            var icon = new VisualElement();
            icon.AddToClassList("seed-icon");
            if (seedData != null && seedData.icon != null)
                icon.style.backgroundImage = new StyleBackground(seedData.icon);
            header.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("seed-info");
            var nameLabel = new Label(seedData != null ? seedData.seedName : entry.seedName);
            nameLabel.AddToClassList("seed-name");
            info.Add(nameLabel);
            var countLabel = new Label($"x{entry.count}");
            countLabel.AddToClassList("seed-count");
            info.Add(countLabel);
            header.Add(info);

            var outcome = new VisualElement();
            outcome.AddToClassList("seed-outcome");
            var outcomeName = new Label(entry.seedName);
            outcomeName.AddToClassList("seed-outcome-name");
            outcome.Add(outcomeName);
            header.Add(outcome);

            card.Add(header);

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

        private SeedData FindSeedData(string seedName)
        {
            if (allSeeds == null || string.IsNullOrEmpty(seedName)) return null;
            foreach (var s in allSeeds)
                if (s.name == seedName) return s;
            return null;
        }
    }
}
