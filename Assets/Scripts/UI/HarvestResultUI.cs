using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HarvestResultUI : MonoBehaviour
    {
        private VisualTreeAsset popupTemplate;

        private VisualElement popupContainer;

        public event Action OnDismissed;

        private HarvestResult currentResult;

        public void Initialize(VisualElement root)
        {
            popupTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/HarvestResultPopup");
            popupContainer = root.Q<VisualElement>("harvest-popup");
        }

        public void Show(HarvestResult result)
        {
            currentResult = result;
            popupContainer.Clear();

            var popup = popupTemplate.CloneTree();
            popup.style.flexGrow = 1; // TemplateContainer must fill harvest-popup's height so
                                      // the absolute harvest-overlay inside it spans the screen.

            var swatch = popup.Q<VisualElement>(className: "harvest-swatch");
            var variantName = popup.Q<Label>(className: "harvest-variant-name");
            var tierLabel = popup.Q<Label>(className: "harvest-tier-label");
            var syncBadge = popup.Q<Label>(className: "harvest-sync-badge");
            var dewdropsLabel = popup.Q<Label>(className: "harvest-dewdrops");
            var sellBtn = popup.Q<Button>(className: "harvest-sell-btn");
            var keepBtn = popup.Q<Button>(className: "harvest-keep-btn");

            if (swatch != null && result.variant != null)
                swatch.style.backgroundColor = result.variant.primaryColor;

            if (variantName != null)
                variantName.text = result.variant?.variantName ?? "Unknown";

            if (tierLabel != null)
            {
                tierLabel.text = $"{result.tier} - {CurrencyConfig.GetQualityLabel(result.tier)}";
                tierLabel.RemoveFromClassList("tier-d");
                tierLabel.RemoveFromClassList("tier-c");
                tierLabel.RemoveFromClassList("tier-b");
                tierLabel.RemoveFromClassList("tier-a");
                tierLabel.RemoveFromClassList("tier-s");
                tierLabel.AddToClassList($"tier-{result.tier.ToString().ToLower()}");
            }

            if (syncBadge != null)
            {
                syncBadge.text = result.syncShieldActive ? "Weather Sync!" : "";
                syncBadge.style.display = result.syncShieldActive
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (dewdropsLabel != null)
                dewdropsLabel.text = $"+{result.dewdropValue} Dewdrops";

            if (sellBtn != null)
            {
                sellBtn.clicked += () =>
                {
                    PlantManager.Instance.SellHarvest(currentResult);
                    Dismiss();
                };
            }

            if (keepBtn != null)
            {
                bool canKeep = GreenhouseManager.Instance.Plants.Count
                    < GreenhouseManager.Instance.MaxSlots;
                keepBtn.SetEnabled(canKeep);
                if (!canKeep) keepBtn.text = "Full";

                keepBtn.clicked += () =>
                {
                    PlantManager.Instance.KeepHarvest(currentResult);
                    Dismiss();
                };
            }

            popupContainer.Add(popup);
            popupContainer.style.display = DisplayStyle.Flex;
        }

        private void Dismiss()
        {
            popupContainer.Clear();
            popupContainer.style.display = DisplayStyle.None;
            OnDismissed?.Invoke();
        }
    }
}
