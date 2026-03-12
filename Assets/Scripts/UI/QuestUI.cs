using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestUI : MonoBehaviour
    {
        private VisualElement root;
        private VisualElement questsPanel;
        private VisualElement mallumStatusContainer;
        private VisualElement activeSection;
        private VisualElement availableSection;
        private VisualElement lockedSection;
        private VisualTreeAsset questCardTemplate;
        private ScrollView questScroll;

        private float nextTickTime;
        private const float TickInterval = 0.5f;

        // Cached active card elements for in-place timer updates
        private struct ActiveCardCache
        {
            public int mallumIndex;
            public MallumState state;
            public Label durationLabel;
            public VisualElement progressFill;
            public Label progressText;
            public Button actionBtn;
            public Label timerLabel;
        }

        private readonly List<ActiveCardCache> cachedActiveCards = new();

        // Snapshot of mallum states to detect when a full rebuild is needed
        private readonly List<MallumState> lastMallumStates = new();

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            questsPanel = root.Q("quests-panel");
            mallumStatusContainer = root.Q("quest-mallum-status");
            activeSection = root.Q("quest-active-section");
            availableSection = root.Q("quest-available-section");
            lockedSection = root.Q("quest-locked-section");
            questCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/QuestCard");
            questScroll = root.Q<ScrollView>("quest-scroll");
        }

        private void Update()
        {
            if (questsPanel == null || questsPanel.resolvedStyle.display == DisplayStyle.None) return;
            if (Time.time < nextTickTime) return;
            nextTickTime = Time.time + TickInterval;

            if (NeedsRebuild())
                Refresh();
            else
                UpdateTimers();
        }

        /// <summary>
        /// Check if mallum states have changed since last build (needs full rebuild).
        /// </summary>
        private bool NeedsRebuild()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return true;

            if (data.mallums.Count != lastMallumStates.Count) return true;
            for (int i = 0; i < data.mallums.Count; i++)
            {
                if (data.mallums[i].state != lastMallumStates[i]) return true;
            }
            return false;
        }

        private void SnapshotStates()
        {
            lastMallumStates.Clear();
            var data = SaveManager.Instance?.Data;
            if (data == null) return;
            for (int i = 0; i < data.mallums.Count; i++)
                lastMallumStates.Add(data.mallums[i].state);
        }

        /// <summary>
        /// Lightweight update — only refresh timer text and progress bars in-place.
        /// </summary>
        private void UpdateTimers()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            foreach (var cache in cachedActiveCards)
            {
                if (cache.mallumIndex >= data.mallums.Count) continue;
                var mallum = data.mallums[cache.mallumIndex];

                switch (cache.state)
                {
                    case MallumState.FetchingWater:
                        float waterRemaining = VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex);
                        float waterProgress = VaseManager.Instance.GetFillProgress(mallum.assignedVaseIndex);
                        cache.durationLabel.text = FormatTime(waterRemaining);
                        cache.progressFill.style.width = new StyleLength(new Length(waterProgress * 100f, LengthUnit.Percent));
                        cache.progressText.text = $"{Mathf.RoundToInt(waterProgress * 100)}%";
                        break;

                    case MallumState.OnQuest:
                        float remaining = MallumManager.Instance.GetQuestRemainingSeconds(mallum);
                        float progress = MallumManager.Instance.GetQuestProgress(mallum);
                        cache.durationLabel.text = FormatTime(remaining);
                        cache.progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                        cache.progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
                        break;
                }
            }
        }

        public void Refresh()
        {
            if (MallumManager.Instance == null) return;

            // Preserve scroll position across rebuild
            var savedOffset = questScroll?.scrollOffset ?? Vector2.zero;

            UpdateMallumStatus();
            BuildActiveSection();
            BuildAvailableSection();
            BuildLockedSection();
            SnapshotStates();

            // Must defer scroll restore — layout needs a frame after Clear()+rebuild
            if (questScroll != null)
                questScroll.schedule.Execute(() => questScroll.scrollOffset = savedOffset);
        }

        private void UpdateMallumStatus()
        {
            mallumStatusContainer.Clear();

            int available = MallumManager.Instance.GetAvailableMallumCount();
            int total = MallumManager.Instance.GetTotalMallumCount();

            var label = new Label($"Free Mallums: {available} / {total}");
            label.AddToClassList("quest-mallum-status-label");
            mallumStatusContainer.Add(label);

            // Visual dots for each Mallum
            var data = SaveManager.Instance.Data;
            for (int i = 0; i < data.mallums.Count; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("quest-mallum-dot");
                if (data.mallums[i].state == MallumState.Idle)
                    dot.AddToClassList("quest-mallum-dot--idle");
                else
                    dot.AddToClassList("quest-mallum-dot--busy");
                mallumStatusContainer.Add(dot);
            }
        }

        private VisualElement MakeSectionHeader(string text, bool first = false)
        {
            var header = new VisualElement();
            header.AddToClassList("quest-section-header");
            if (first) header.AddToClassList("quest-section-header-first");

            var title = new Label(text);
            title.AddToClassList("quest-section-title");
            header.Add(title);

            var rule = new VisualElement();
            rule.AddToClassList("quest-section-rule");
            header.Add(rule);

            return header;
        }

        private string TierClass(int flameLevel)
        {
            int tier = Mathf.Clamp(flameLevel, 1, 8);
            return $"quest-tier-{tier}";
        }

        private void BuildActiveSection()
        {
            activeSection.Clear();
            cachedActiveCards.Clear();
            var data = SaveManager.Instance.Data;
            bool hasActive = false;

            for (int i = 0; i < data.mallums.Count; i++)
            {
                var mallum = data.mallums[i];
                if (mallum.state == MallumState.Idle) continue;

                hasActive = true;
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var accentStrip = card.Q("quest-accent-strip");
                var nameLabel = card.Q<Label>("quest-name");
                var levelBadge = card.Q<Label>("quest-level-badge");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var rewardList = card.Q("quest-reward-list");
                var progressContainer = card.Q("quest-progress-container");
                var progressFill = card.Q("quest-progress-fill");
                var progressText = card.Q<Label>("quest-progress-text");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                lockedLabel.style.display = DisplayStyle.None;
                rewardsContainer.style.display = DisplayStyle.None;
                descLabel.style.display = DisplayStyle.None;
                levelBadge.style.display = DisplayStyle.None;

                int mallumIndex = i;

                switch (mallum.state)
                {
                    case MallumState.FetchingWater:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = "Fetching Water";
                        float waterRemaining = VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex);
                        float waterProgress = VaseManager.Instance.GetFillProgress(mallum.assignedVaseIndex);
                        durationLabel.text = FormatTime(waterRemaining);
                        progressFill.style.width = new StyleLength(new Length(waterProgress * 100f, LengthUnit.Percent));
                        progressText.text = $"{Mathf.RoundToInt(waterProgress * 100)}%";
                        timerLabel.style.display = DisplayStyle.None;
                        int waterDrinkCount = MallumManager.Instance.GetEnergyDrinkCount();
                        actionBtn.text = waterDrinkCount > 0 ? $"Speed Up ({waterDrinkCount})" : "Speed Up";
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(waterDrinkCount > 0);
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpWaterFetch(mallumIndex);
                            Refresh();
                        };
                        cachedActiveCards.Add(new ActiveCardCache
                        {
                            mallumIndex = mallumIndex,
                            state = MallumState.FetchingWater,
                            durationLabel = durationLabel,
                            progressFill = progressFill,
                            progressText = progressText,
                            actionBtn = actionBtn,
                            timerLabel = timerLabel
                        });
                        break;

                    case MallumState.OnQuest:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = mallum.assignedQuestName;

                        // Find quest data for tier color
                        var questData = FindQuestByName(mallum.assignedQuestName);
                        if (questData != null)
                            accentStrip.AddToClassList(TierClass(questData.requiredFlameLevel));

                        float remaining = MallumManager.Instance.GetQuestRemainingSeconds(mallum);
                        float progress = MallumManager.Instance.GetQuestProgress(mallum);
                        durationLabel.text = FormatTime(remaining);
                        progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                        progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
                        timerLabel.style.display = DisplayStyle.None;

                        int drinkCount = MallumManager.Instance.GetEnergyDrinkCount();
                        actionBtn.text = drinkCount > 0 ? $"Speed Up ({drinkCount})" : "Speed Up";
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(drinkCount > 0);
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpQuest(mallumIndex);
                            var mallumData = SaveManager.Instance.Data.mallums[mallumIndex];
                            var rewards = new List<RewardEntry>(mallumData.pendingRewards);
                            FindFirstObjectByType<CampFireUI>()?.CloseOverlay();
                            RewardRevealUI.Instance?.Show("Quest Complete!", rewards, () =>
                            {
                                MallumManager.Instance.CollectQuestRewards(mallumIndex);
                                Refresh();
                            });
                        };
                        cachedActiveCards.Add(new ActiveCardCache
                        {
                            mallumIndex = mallumIndex,
                            state = MallumState.OnQuest,
                            durationLabel = durationLabel,
                            progressFill = progressFill,
                            progressText = progressText,
                            actionBtn = actionBtn,
                            timerLabel = timerLabel
                        });
                        break;

                    case MallumState.QuestComplete:
                        cardRoot.AddToClassList("quest-card--complete");
                        nameLabel.text = mallum.assignedQuestName;
                        durationLabel.text = "Complete!";
                        durationLabel.style.color = new StyleColor(new Color32(80, 190, 100, 255));

                        var completeQuest = FindQuestByName(mallum.assignedQuestName);
                        if (completeQuest != null)
                            accentStrip.AddToClassList(TierClass(completeQuest.requiredFlameLevel));

                        progressContainer.style.display = DisplayStyle.None;
                        timerLabel.style.display = DisplayStyle.None;

                        actionBtn.text = "Collect Rewards";
                        actionBtn.AddToClassList("quest-collect-btn");
                        actionBtn.clicked += () =>
                        {
                            var rewards = new List<RewardEntry>(mallum.pendingRewards);
                            FindFirstObjectByType<CampFireUI>()?.CloseOverlay();
                            RewardRevealUI.Instance?.Show("Quest Complete!", rewards, () =>
                            {
                                MallumManager.Instance.CollectQuestRewards(mallumIndex);
                                Refresh();
                            });
                        };
                        break;
                }

                activeSection.Add(card);
            }

            if (!hasActive)
            {
                var empty = new Label("All Mallums idle - send them on a quest!");
                empty.AddToClassList("quest-empty-text");
                activeSection.Add(empty);
            }
            else
            {
                activeSection.Insert(0, MakeSectionHeader("Active", true));
            }
        }

        private void BuildAvailableSection()
        {
            availableSection.Clear();
            var quests = MallumManager.Instance.GetAvailableQuests();
            if (quests.Count == 0) return;

            availableSection.Add(MakeSectionHeader("Available Quests"));

            int available = MallumManager.Instance.GetAvailableMallumCount();

            foreach (var quest in quests)
            {
                var card = questCardTemplate.CloneTree();
                var accentStrip = card.Q("quest-accent-strip");
                var nameLabel = card.Q<Label>("quest-name");
                var levelBadge = card.Q<Label>("quest-level-badge");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var rewardList = card.Q("quest-reward-list");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                accentStrip.AddToClassList(TierClass(quest.requiredFlameLevel));
                nameLabel.text = quest.questName;
                levelBadge.text = $"Lv {quest.requiredFlameLevel}";
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                lockedLabel.style.display = DisplayStyle.None;

                foreach (var reward in quest.rewardPool)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("quest-reward-chip");
                    string rewardText;
                    if (!string.IsNullOrEmpty(reward.seedName))
                    {
                        string countStr = reward.minCount == reward.maxCount
                            ? $"{reward.minCount}"
                            : $"{reward.minCount}-{reward.maxCount}";
                        rewardText = $"{countStr} {reward.seedName}";
                    }
                    else
                    {
                        rewardText = "?";
                    }
                    var chipLabel = new Label(rewardText);
                    chipLabel.AddToClassList("quest-reward-name");
                    chip.Add(chipLabel);
                    rewardList.Add(chip);
                }

                var capturedQuest = quest;
                actionBtn.text = available > 0 ? "Send Mallum" : "No Mallums Idle";
                actionBtn.AddToClassList("quest-send-btn");
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

            lockedSection.Add(MakeSectionHeader("Locked"));

            foreach (var quest in locked)
            {
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var accentStrip = card.Q("quest-accent-strip");
                var nameLabel = card.Q<Label>("quest-name");
                var levelBadge = card.Q<Label>("quest-level-badge");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                cardRoot.AddToClassList("quest-card--locked");
                accentStrip.AddToClassList(TierClass(quest.requiredFlameLevel));
                nameLabel.text = quest.questName;
                levelBadge.text = $"Lv {quest.requiredFlameLevel}";
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                actionBtn.style.display = DisplayStyle.None;
                rewardsContainer.style.display = DisplayStyle.None;
                lockedLabel.text = $"Requires Flame Level {quest.requiredFlameLevel}";

                lockedSection.Add(card);
            }
        }

        private ServerQuestConfig FindQuestByName(string questName)
        {
            return ConfigService.Instance?.GetQuest(questName);
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
