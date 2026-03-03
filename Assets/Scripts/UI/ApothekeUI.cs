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
