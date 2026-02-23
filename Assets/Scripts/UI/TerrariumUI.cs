using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class TerrariumUI : MonoBehaviour
    {
        private VisualTreeAsset environmentTemplate;
        private VisualTreeAsset slotTemplate;

        private VisualElement panel;
        private ScrollView terrariumScroll;
        private Label dustRateText;
        private Label slotsText;
        private Button closeButton;

        public System.Action<int, int> OnEmptySlotTapped;
        public System.Action<int, int> OnMatureSlotTapped;

        public void Initialize(VisualElement root)
        {
            environmentTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/EnvironmentSection");
            slotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/TerrariumSlot");

            panel = root.Q<VisualElement>("terrarium-panel");
            terrariumScroll = root.Q<ScrollView>("terrarium-scroll");
            dustRateText = root.Q<Label>("dust-rate");
            slotsText = root.Q<Label>("terrarium-slots-text");
            closeButton = root.Q<Button>("terrarium-close");

            closeButton.clicked += Hide;
        }

        public void Show()
        {
            panel.style.display = DisplayStyle.Flex;
            RefreshDisplay();
        }

        public void Hide()
        {
            panel.style.display = DisplayStyle.None;
        }

        public void RefreshDisplay()
        {
            terrariumScroll.Clear();

            var em = EnvironmentManager.Instance;
            var pm = PlantManager.Instance;
            var gm = GreenhouseManager.Instance;

            if (em == null || pm == null) return;

            dustRateText.text = gm != null
                ? $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr"
                : "+0 Aura Dust/hr";

            int totalSlots = em.GetTotalUnlockedSlots();
            int usedSlots = pm.GetGrowingCount() + pm.GetMatureCount();
            slotsText.text = $"{usedSlots} / {totalSlots}";

            for (int e = 0; e < em.Environments.Count; e++)
            {
                var env = em.Environments[e];
                var section = environmentTemplate.CloneTree();

                var nameLabel = section.Q<Label>(className: "environment-name");
                var envSlotsText = section.Q<Label>(className: "environment-slots-text");
                var slotGrid = section.Q<VisualElement>(className: "environment-slot-grid");
                var lockedSection = section.Q<VisualElement>(className: "environment-locked");
                var costLabel = section.Q<Label>(className: "environment-cost");
                var unlockBtn = section.Q<Button>(className: "environment-unlock-btn");

                if (nameLabel != null) nameLabel.text = env.environmentName;

                bool unlocked = em.IsUnlocked(e);

                if (unlocked)
                {
                    if (lockedSection != null) lockedSection.style.display = DisplayStyle.None;

                    var envSlots = pm.GetSlotsForEnvironment(e);
                    int active = 0;
                    foreach (var s in envSlots)
                        if (s.state != PlantState.Empty) active++;
                    if (envSlotsText != null) envSlotsText.text = $"{active} / {env.slotCount}";

                    foreach (var slot in envSlots)
                    {
                        var slotEl = slotTemplate.CloneTree();
                        var swatch = slotEl.Q<VisualElement>(className: "terrarium-swatch");
                        var label = slotEl.Q<Label>(className: "terrarium-slot-label");
                        var progressFill = slotEl.Q<VisualElement>(className: "terrarium-progress-fill");
                        var btn = slotEl.Q<Button>(className: "terrarium-slot");

                        int envIdx = slot.environmentIndex;
                        int slotIdx = slot.slotIndex;

                        switch (slot.state)
                        {
                            case PlantState.Empty:
                                if (swatch != null) swatch.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                                if (label != null) label.text = "Empty";
                                if (progressFill != null) progressFill.style.width = new Length(0, LengthUnit.Percent);
                                if (btn != null) btn.clicked += () => OnEmptySlotTapped?.Invoke(envIdx, slotIdx);
                                break;

                            case PlantState.Growing:
                                if (swatch != null && slot.variant != null)
                                    swatch.style.backgroundColor = slot.variant.primaryColor;
                                float hours = pm.GetRemainingHours(envIdx, slotIdx);
                                if (label != null)
                                    label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                                if (progressFill != null)
                                    progressFill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                                break;

                            case PlantState.Mature:
                                if (swatch != null && slot.variant != null)
                                    swatch.style.backgroundColor = slot.variant.primaryColor;
                                if (label != null) label.text = "Harvest!";
                                if (progressFill != null) progressFill.style.width = new Length(100, LengthUnit.Percent);
                                if (btn != null) btn.clicked += () => OnMatureSlotTapped?.Invoke(envIdx, slotIdx);
                                break;
                        }

                        slotGrid.Add(slotEl);
                    }
                }
                else
                {
                    if (slotGrid != null) slotGrid.style.display = DisplayStyle.None;
                    if (envSlotsText != null) envSlotsText.text = "Locked";
                    if (costLabel != null) costLabel.text = $"{env.unlockCostDewdrops} Dewdrops to unlock";

                    int envIndex = e;
                    if (unlockBtn != null)
                    {
                        unlockBtn.SetEnabled(CurrencyManager.Instance.CanAfford(
                            CurrencyType.Dewdrops, env.unlockCostDewdrops));
                        unlockBtn.clicked += () =>
                        {
                            if (em.Unlock(envIndex))
                                RefreshDisplay();
                        };
                    }
                }

                terrariumScroll.Add(section);
            }
        }
    }
}
