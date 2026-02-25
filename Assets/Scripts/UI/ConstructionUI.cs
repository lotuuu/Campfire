using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class ConstructionUI : MonoBehaviour
    {
        private VisualTreeAsset cardTemplate;
        private VisualTreeAsset upgradeButtonTemplate;

        private ScrollView scrollView;

        public void Initialize(VisualElement root)
        {
            cardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ConstructionLocationCard");
            upgradeButtonTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/ConstructionUpgradeButton");

            scrollView = root.Q<ScrollView>("construction-scroll");

            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        }

        public void Show()
        {
            RefreshDisplay();
        }

        private void OnCurrencyChanged(CurrencyType type, int oldVal, int newVal)
        {
            if (type == CurrencyType.Gold)
                RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (scrollView == null) return;
            scrollView.Clear();

            var em = EnvironmentManager.Instance;
            for (int i = 0; i < em.Environments.Count; i++)
            {
                if (!IsRevealedForConstruction(em, i)) continue;

                var env = em.Environments[i];
                bool unlocked = em.IsUnlocked(i);

                var card = cardTemplate.CloneTree();
                var nameLabel = card.Q<Label>(className: "construction-card-name");
                var badge = card.Q<Label>(className: "construction-card-badge");
                var lockedSection = card.Q<VisualElement>("locked-section");
                var upgradesSection = card.Q<VisualElement>("upgrades-section");

                if (nameLabel != null) nameLabel.text = env.environmentName;

                if (unlocked)
                {
                    lockedSection?.Hide();
                    if (badge != null) badge.text = "✓";

                    int current = em.GetActiveSlotCount(i);
                    int max = env.maxSlotCount;

                    var upgradeRow = upgradeButtonTemplate.CloneTree();
                    var upgradeLabel = upgradeRow.Q<Label>("upgrade-label");
                    var upgradeProgress = upgradeRow.Q<Label>("upgrade-progress");
                    var upgradeBtn = upgradeRow.Q<Button>("upgrade-btn");

                    if (upgradeLabel != null) upgradeLabel.text = "Slots";
                    if (upgradeProgress != null) upgradeProgress.text = $"{current} / {max}";

                    if (upgradeBtn != null)
                    {
                        bool canAdd = current < max;
                        bool canAfford = CurrencyManager.Instance.CanAfford(
                            CurrencyType.Gold, env.slotUnlockCostGold);
                        upgradeBtn.text = canAdd ? $"+ ({env.slotUnlockCostGold} Gold)" : "Max";
                        upgradeBtn.SetEnabled(canAdd && canAfford);

                        int capturedIndex = i;
                        upgradeBtn.clicked += () =>
                        {
                            EnvironmentManager.Instance.UnlockSlot(capturedIndex);
                            RefreshDisplay();
                        };
                    }

                    upgradesSection?.Add(upgradeRow);
                }
                else
                {
                    upgradesSection?.Hide();
                    if (badge != null) badge.text = "";

                    card.Q<VisualElement>(className: "construction-card")?.AddToClassList("is-locked");

                    var costLabel = card.Q<Label>("unlock-cost-label");
                    var unlockBtn = card.Q<Button>("unlock-btn");

                    if (costLabel != null) costLabel.text = $"{env.unlockCostGold} Gold";

                    if (unlockBtn != null)
                    {
                        bool canAfford = CurrencyManager.Instance.CanAfford(
                            CurrencyType.Gold, env.unlockCostGold);
                        unlockBtn.text = $"Purchase {env.environmentName}";
                        unlockBtn.SetEnabled(canAfford);

                        int capturedIndex = i;
                        unlockBtn.clicked += () =>
                        {
                            EnvironmentManager.Instance.Unlock(capturedIndex);
                            RefreshDisplay();
                        };
                    }
                }

                scrollView.Add(card);
            }

            AppendGreenhouseCard();
        }

        private void AppendGreenhouseCard()
        {
            var gm = GreenhouseManager.Instance;
            var config = CurrencyManager.Instance.Config;

            var card = cardTemplate.CloneTree();
            var nameLabel = card.Q<Label>(className: "construction-card-name");
            var badge = card.Q<Label>(className: "construction-card-badge");
            var lockedSection = card.Q<VisualElement>("locked-section");
            var upgradesSection = card.Q<VisualElement>("upgrades-section");

            if (nameLabel != null) nameLabel.text = "Greenhouse";
            if (badge != null) badge.text = "✓";
            lockedSection?.Hide();

            var upgradeRow = upgradeButtonTemplate.CloneTree();
            var upgradeLabel = upgradeRow.Q<Label>("upgrade-label");
            var upgradeProgress = upgradeRow.Q<Label>("upgrade-progress");
            var upgradeBtn = upgradeRow.Q<Button>("upgrade-btn");

            if (upgradeLabel != null) upgradeLabel.text = "Capacity";
            if (upgradeProgress != null) upgradeProgress.text = $"{gm.Plants.Count} / {gm.MaxSlots}";

            if (upgradeBtn != null)
            {
                bool canAfford = CurrencyManager.Instance.CanAfford(
                    CurrencyType.Gold, config.greenhouseExpandCostGold);
                upgradeBtn.text = $"+ ({config.greenhouseExpandCostGold} Gold)";
                upgradeBtn.SetEnabled(canAfford);
                upgradeBtn.clicked += () =>
                {
                    GreenhouseManager.Instance.ExpandSlots();
                    RefreshDisplay();
                };
            }

            upgradesSection?.Add(upgradeRow);
            scrollView.Add(card);
        }

        private static bool IsRevealedForConstruction(EnvironmentManager em, int envIndex)
        {
            if (envIndex == 0) return true;
            int prevIdx = envIndex - 1;
            return em.GetActiveSlotCount(prevIdx) >= em.Environments[prevIdx].maxSlotCount;
        }

        private void OnDestroy()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
        }
    }

    internal static class VisualElementExtensions
    {
        public static void Hide(this VisualElement el)
        {
            if (el != null) el.style.display = DisplayStyle.None;
        }
    }
}
