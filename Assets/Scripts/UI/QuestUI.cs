using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestUI : MonoBehaviour
    {
        private VisualElement root;
        private Label mallumStatusLabel;
        private VisualElement activeSection;
        private VisualElement availableSection;
        private VisualElement lockedSection;
        private VisualTreeAsset questCardTemplate;

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            mallumStatusLabel = root.Q<Label>("quest-mallum-status");
            activeSection = root.Q("quest-active-section");
            availableSection = root.Q("quest-available-section");
            lockedSection = root.Q("quest-locked-section");
            questCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/QuestCard");
        }

        public void Refresh()
        {
            if (MallumManager.Instance == null) return;

            UpdateMallumStatus();
            BuildActiveSection();
            BuildAvailableSection();
            BuildLockedSection();
        }

        private void UpdateMallumStatus()
        {
            int available = MallumManager.Instance.GetAvailableMallumCount();
            int total = MallumManager.Instance.GetTotalMallumCount();
            mallumStatusLabel.text = $"Mallums: {available} / {total} available";
        }

        private void BuildActiveSection()
        {
            activeSection.Clear();
            var data = SaveManager.Instance.Data;
            bool hasActive = false;

            for (int i = 0; i < data.mallums.Count; i++)
            {
                var mallum = data.mallums[i];
                if (mallum.state == MallumState.Idle) continue;

                hasActive = true;
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var progressFill = card.Q("quest-progress-fill");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                lockedLabel.style.display = DisplayStyle.None;
                rewardsContainer.style.display = DisplayStyle.None;
                descLabel.style.display = DisplayStyle.None;

                int mallumIndex = i;

                switch (mallum.state)
                {
                    case MallumState.FetchingWater:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = "Fetching Water";
                        float waterRemaining = VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex);
                        durationLabel.text = FormatTime(waterRemaining);
                        float waterProgress = VaseManager.Instance.GetFillProgress(mallum.assignedVaseIndex);
                        progressFill.style.width = new StyleLength(new Length(waterProgress * 100f, LengthUnit.Percent));
                        timerLabel.style.display = DisplayStyle.None;
                        actionBtn.style.display = DisplayStyle.None;
                        break;

                    case MallumState.OnQuest:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = mallum.assignedQuestName;
                        float remaining = MallumManager.Instance.GetQuestRemainingSeconds(mallum);
                        float progress = MallumManager.Instance.GetQuestProgress(mallum);
                        durationLabel.text = FormatTime(remaining);
                        progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                        timerLabel.style.display = DisplayStyle.None;
                        actionBtn.style.display = DisplayStyle.None;
                        break;

                    case MallumState.QuestComplete:
                        cardRoot.AddToClassList("quest-card--complete");
                        nameLabel.text = mallum.assignedQuestName;
                        durationLabel.text = "Complete!";
                        progressContainer.style.display = DisplayStyle.None;
                        timerLabel.style.display = DisplayStyle.None;

                        rewardsContainer.style.display = DisplayStyle.Flex;
                        foreach (var reward in mallum.pendingRewards)
                        {
                            var chip = new VisualElement();
                            chip.AddToClassList("quest-reward-chip");
                            var chipLabel = new Label($"{reward.seedName} x{reward.count}");
                            chipLabel.AddToClassList("quest-reward-name");
                            chip.Add(chipLabel);
                            rewardsContainer.Add(chip);
                        }

                        actionBtn.text = "Collect";
                        actionBtn.AddToClassList("quest-collect-btn");
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.CollectQuestRewards(mallumIndex);
                            Refresh();
                        };
                        break;
                }

                activeSection.Add(card);
            }

            if (!hasActive)
            {
                var empty = new Label("No active Mallums");
                empty.AddToClassList("quest-empty-text");
                activeSection.Add(empty);
            }
            else
            {
                var title = new Label("Active");
                title.AddToClassList("quest-section-title");
                title.AddToClassList("quest-section-title-first");
                activeSection.Insert(0, title);
            }
        }

        private void BuildAvailableSection()
        {
            availableSection.Clear();
            var quests = MallumManager.Instance.GetAvailableQuests();
            if (quests.Count == 0) return;

            var title = new Label("Available Quests");
            title.AddToClassList("quest-section-title");
            availableSection.Add(title);

            int available = MallumManager.Instance.GetAvailableMallumCount();

            foreach (var quest in quests)
            {
                var card = questCardTemplate.CloneTree();
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                nameLabel.text = quest.questName;
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                lockedLabel.style.display = DisplayStyle.None;

                foreach (var reward in quest.rewardPool)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("quest-reward-chip");
                    var chipLabel = new Label(reward.seed != null ? reward.seed.seedName : "?");
                    chipLabel.AddToClassList("quest-reward-name");
                    chip.Add(chipLabel);
                    rewardsContainer.Add(chip);
                }

                var capturedQuest = quest;
                actionBtn.text = $"Send Mallum ({available} available)";
                actionBtn.SetEnabled(available > 0);
                actionBtn.clicked += () =>
                {
                    MallumManager.Instance.SendOnQuest(capturedQuest);
                    Refresh();
                };

                availableSection.Add(card);
            }
        }

        private void BuildLockedSection()
        {
            lockedSection.Clear();
            var locked = MallumManager.Instance.GetLockedQuests();
            if (locked.Count == 0) return;

            var title = new Label("Locked");
            title.AddToClassList("quest-section-title");
            lockedSection.Add(title);

            foreach (var quest in locked)
            {
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                cardRoot.AddToClassList("quest-card--locked");
                nameLabel.text = quest.questName;
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                actionBtn.style.display = DisplayStyle.None;
                lockedLabel.text = $"Requires Flame Level {quest.requiredFlameLevel}";

                lockedSection.Add(card);
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "Done";
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            if (h > 0) return $"{h}h {m}m";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        private static string FormatDuration(int minutes)
        {
            if (minutes >= 60)
            {
                int h = minutes / 60;
                int m = minutes % 60;
                return m > 0 ? $"{h}h {m}m" : $"{h}h";
            }
            return $"{minutes}m";
        }
    }
}
