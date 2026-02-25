using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        private VisualTreeAsset seedSlotTemplate;
        private VisualTreeAsset probabilityEntryTemplate;

        private VisualElement panel;
        private VisualElement scrim;
        private ScrollView seedGrid;
        private VisualElement probabilityPanel;
        private ScrollView probabilityGrid;
        private Button plantButton;
        private Label selectedSeedName;

        private SeedData selectedSeed;

        private int targetEnvIndex = -1;
        private int targetSlotIndex = -1;

        public void Initialize(VisualElement root)
        {
            seedSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedSlot");
            probabilityEntryTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ProbabilityEntry");

            panel = root.Q<VisualElement>("satchel-panel");
            scrim = root.Q<VisualElement>("satchel-scrim");
            seedGrid = root.Q<ScrollView>("seed-grid");
            probabilityPanel = root.Q<VisualElement>("probability-panel");
            probabilityGrid = root.Q<ScrollView>("probability-grid");
            plantButton = root.Q<Button>("plant-button");
            selectedSeedName = root.Q<Label>("selected-seed-name");

            plantButton.clicked += OnPlant;
            scrim.RegisterCallback<ClickEvent>(evt => Hide());
        }

        public void Show()
        {
            Show(-1, -1);
        }

        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex = envIndex;
            targetSlotIndex = slotIndex;
            scrim.style.display = DisplayStyle.Flex;
            panel.style.display = DisplayStyle.Flex;
            RefreshGrid();
            probabilityPanel.style.display = DisplayStyle.None;
            plantButton.SetEnabled(false);
            selectedSeed = null;
        }

        public void Hide()
        {
            scrim.style.display = DisplayStyle.None;
            panel.style.display = DisplayStyle.None;
        }

        private void RefreshGrid()
        {
            seedGrid.Clear();

            var seeds = SeedRegistry.Instance.GetOwnedSeeds();
            foreach (var seed in seeds)
            {
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                var slot = SeedSlotUI.Create(seedSlotTemplate, seed, count, OnSeedSelected);
                seedGrid.Add(slot);
            }
        }

        private void OnSeedSelected(SeedData seed)
        {
            selectedSeed = seed;
            selectedSeedName.text = seed.seedName;
            plantButton.SetEnabled(true);
            ShowProbabilities(seed);
        }

        private void ShowProbabilities(SeedData seed)
        {
            probabilityPanel.style.display = DisplayStyle.Flex;
            probabilityGrid.Clear();

            var variants = seed.variants;

            foreach (var variant in variants)
            {
                var entry = probabilityEntryTemplate.CloneTree();
                var nameLabel = entry.Q<Label>(className: "probability-name");
                if (nameLabel != null)
                {
                    bool discovered = SaveManager.Instance.Data.discoveredVariants.Contains(variant.variantName);
                    nameLabel.text = discovered ? variant.variantName : "?????";
                }
                probabilityGrid.Add(entry);
            }
        }

        private void OnPlant()
        {
            if (selectedSeed == null) return;

            if (targetEnvIndex >= 0 && targetSlotIndex >= 0)
            {
                PlantManager.Instance.Plant(selectedSeed, targetEnvIndex, targetSlotIndex);
            }
            else
            {
                PlantManager.Instance.Plant(selectedSeed);
            }
            Hide();
        }
    }
}
