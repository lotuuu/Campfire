using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ApothekeUI : MonoBehaviour
    {
        private VisualElement seedList;
        private VisualElement recipeList;
        private VisualTreeAsset seedTemplate;
        private VisualTreeAsset recipeTemplate;

        public void Initialize(VisualElement root)
        {
            seedList = root.Q("seed-list");
            recipeList = root.Q("recipe-list");
            seedTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedCard");
            recipeTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/RecipeCard");
            Refresh();
        }

        public void Refresh()
        {
            RefreshSeeds();
            RefreshRecipes();
        }

        private void RefreshSeeds()
        {
            if (seedList == null || ApothekeManager.Instance == null) return;
            seedList.Clear();
            foreach (var entry in ApothekeManager.Instance.Seeds)
            {
                var el = seedTemplate.CloneTree();
                var nameLabel = el.Q<Label>(className: "seed-name");
                var countLabel = el.Q<Label>(className: "seed-count");
                if (nameLabel != null) nameLabel.text = entry.seedName;
                if (countLabel != null) countLabel.text = $"x{entry.count}";
                seedList.Add(el);
            }
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
    }
}
