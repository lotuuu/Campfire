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

        // Card element pools — reused across refreshes to avoid DOM churn
        // that causes ScrollView to clamp scrollOffset and visibly jump.
        private readonly List<VisualElement> activeCardPool = new();
        private readonly List<VisualElement> availableCardPool = new();
        private readonly List<VisualElement> lockedCardPool = new();
        private VisualElement activeSectionHeader;
        private VisualElement availableSectionHeader;
        private VisualElement lockedSectionHeader;

        // Mallum status element pool
        private Label mallumStatusLabel;
        private readonly List<VisualElement> mallumDots = new();

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

            UpdateMallumStatus();
            BuildActiveSection();
            BuildAvailableSection();
            BuildLockedSection();
            SnapshotStates();
        }

        // ── Card pool helpers ──────────────────────────────────────────────

        /// <summary>
        /// Ensures a section has exactly <paramref name="needed"/> card elements,
        /// adding new ones from the template or removing excess from the end.
        /// Never calls Clear() on the section — DOM mutations are minimal.
        /// </summary>
        private void SyncCardCount(VisualElement section, List<VisualElement> pool, int needed)
        {
            while (pool.Count < needed)
            {
                var card = questCardTemplate.CloneTree();
                pool.Add(card);
                section.Add(card);
            }
            while (pool.Count > needed)
            {
                pool[pool.Count - 1].RemoveFromHierarchy();
                pool.RemoveAt(pool.Count - 1);
            }
        }

        /// <summary>
        /// Creates the section header on first call, then toggles its display.
        /// </summary>
        private void SyncHeader(VisualElement section, ref VisualElement header,
                                string text, bool first, bool visible)
        {
            if (header == null)
            {
                header = MakeSectionHeader(text, first);
                section.Insert(0, header);
            }
            header.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Strips all state-specific classes, inline styles, and dynamic children
        /// from a reused card so it can be repopulated cleanly.
        /// </summary>
        private static void ResetCard(VisualElement card)
        {
            var cardRoot = card.Q(className: "quest-card");
            cardRoot.RemoveFromClassList("quest-card--active");
            cardRoot.RemoveFromClassList("quest-card--complete");
            cardRoot.RemoveFromClassList("quest-card--locked");

            var strip = card.Q("quest-accent-strip");
            for (int t = 1; t <= 8; t++)
                strip.RemoveFromClassList($"quest-tier-{t}");

            // Reset visibility on all toggled elements
            string[] names =
            {
                "quest-description", "quest-rewards", "quest-progress-container",
                "quest-timer", "quest-action", "quest-locked", "quest-level-badge"
            };
            foreach (var n in names)
            {
                var el = card.Q(n);
                if (el != null) el.style.display = StyleKeyword.Null;
            }

            // Reset button state
            var btn = card.Q<Button>("quest-action");
            btn.RemoveFromClassList("quest-speedup-btn");
            btn.RemoveFromClassList("quest-collect-btn");
            btn.RemoveFromClassList("quest-send-btn");
            btn.SetEnabled(true);

            // Clear dynamic reward chips
            card.Q("quest-reward-list")?.Clear();

            // Reset duration label color
            var dur = card.Q<Label>("quest-duration");
            if (dur != null) dur.style.color = StyleKeyword.Null;
        }

        // ── Section builders ───────────────────────────────────────────────

        private void UpdateMallumStatus()
        {
            int available = MallumManager.Instance.GetAvailableMallumCount();
            int total = MallumManager.Instance.GetTotalMallumCount();

            // Reuse or create the label
            if (mallumStatusLabel == null)
            {
                mallumStatusLabel = new Label();
                mallumStatusLabel.AddToClassList("quest-mallum-status-label");
                mallumStatusContainer.Add(mallumStatusLabel);
            }
            int busy = total - available;
            mallumStatusLabel.text = string.Format(Loc.Get("ui.quest.mallum_status", "{0} Idle / {1} Total"), available, total);
            if (busy > 0) mallumStatusLabel.text += "  " + string.Format(Loc.Get("ui.quest.mallum_on_task", "({0} on task)"), busy);

            // Sync dot count
            var data = SaveManager.Instance.Data;
            while (mallumDots.Count < data.mallums.Count)
            {
                var dot = new VisualElement();
                dot.AddToClassList("quest-mallum-dot");
                mallumDots.Add(dot);
                mallumStatusContainer.Add(dot);
            }
            while (mallumDots.Count > data.mallums.Count)
            {
                mallumDots[mallumDots.Count - 1].RemoveFromHierarchy();
                mallumDots.RemoveAt(mallumDots.Count - 1);
            }

            // Update dot classes
            for (int i = 0; i < data.mallums.Count; i++)
            {
                var dot = mallumDots[i];
                bool idle = data.mallums[i].state == MallumState.Idle;
                dot.EnableInClassList("quest-mallum-dot--idle", idle);
                dot.EnableInClassList("quest-mallum-dot--busy", !idle);
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
            cachedActiveCards.Clear();
            var data = SaveManager.Instance.Data;

            // Collect active mallums
            var activeMallums = new List<(int idx, MallumSave save)>();
            for (int i = 0; i < data.mallums.Count; i++)
            {
                if (data.mallums[i].state != MallumState.Idle)
                    activeMallums.Add((i, data.mallums[i]));
            }

            SyncHeader(activeSection, ref activeSectionHeader, Loc.Get("ui.quest.active", "Active"), true, activeMallums.Count > 0);
            SyncCardCount(activeSection, activeCardPool, activeMallums.Count);

            for (int c = 0; c < activeMallums.Count; c++)
            {
                var (mallumIndex, mallum) = activeMallums[c];
                var card = activeCardPool[c];
                ResetCard(card);

                var cardRoot = card.Q(className: "quest-card");
                var accentStrip = card.Q("quest-accent-strip");
                var nameLabel = card.Q<Label>("quest-name");
                var levelBadge = card.Q<Label>("quest-level-badge");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
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

                int capturedIndex = mallumIndex;

                switch (mallum.state)
                {
                    case MallumState.FetchingWater:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = Loc.Get("ui.quest.fetching_water", "Fetching Water");
                        float waterRemaining = VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex);
                        float waterProgress = VaseManager.Instance.GetFillProgress(mallum.assignedVaseIndex);
                        durationLabel.text = FormatTime(waterRemaining);
                        progressFill.style.width = new StyleLength(new Length(waterProgress * 100f, LengthUnit.Percent));
                        progressText.text = $"{Mathf.RoundToInt(waterProgress * 100)}%";
                        timerLabel.style.display = DisplayStyle.None;
                        int waterDrinkCount = MallumManager.Instance.GetVaseSpeedItemCount();
                        actionBtn.text = waterDrinkCount > 0 ? string.Format(Loc.Get("ui.button.speed_up", "Speed Up ({0})"), waterDrinkCount) : Loc.Get("ui.button.speed_up_plain", "Speed Up");
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(waterDrinkCount > 0 || CurrencyManager.FreeMode);
                        actionBtn.clickable = new Clickable(() =>
                        {
                            MallumManager.Instance.SpeedUpWaterFetch(capturedIndex);
                            Refresh();
                        });
                        cachedActiveCards.Add(new ActiveCardCache
                        {
                            mallumIndex = capturedIndex,
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
                        var questData = FindQuestByName(mallum.assignedQuestName);
                        nameLabel.text = FormatQuestName(questData?.questName ?? mallum.assignedQuestName);
                        if (questData != null)
                            accentStrip.AddToClassList(TierClass(questData.requiredFlameLevel));

                        float remaining = MallumManager.Instance.GetQuestRemainingSeconds(mallum);
                        float progress = MallumManager.Instance.GetQuestProgress(mallum);
                        durationLabel.text = FormatTime(remaining);
                        progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                        progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
                        timerLabel.style.display = DisplayStyle.None;

                        int drinkCount = MallumManager.Instance.GetQuestSpeedItemCount();
                        actionBtn.text = drinkCount > 0 ? string.Format(Loc.Get("ui.button.speed_up", "Speed Up ({0})"), drinkCount) : Loc.Get("ui.button.speed_up_plain", "Speed Up");
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(drinkCount > 0 || CurrencyManager.FreeMode);
                        var capturedQuestName = FormatQuestName(questData?.questName ?? mallum.assignedQuestName);
                        actionBtn.clickable = new Clickable(() =>
                        {
                            var rewards = MallumManager.Instance.SpeedUpAndCollectQuest(capturedIndex);
                            if (rewards == null || rewards.Count == 0) return;
                            FindFirstObjectByType<CampFireUI>()?.CloseOverlay(silent: true);
                            RewardRevealUI.Instance?.Show(Loc.Get("ui.quest.quest_complete", "Quest Complete!"), capturedQuestName, rewards, () =>
                            {
                                Refresh();
                            });
                        });
                        cachedActiveCards.Add(new ActiveCardCache
                        {
                            mallumIndex = capturedIndex,
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
                        var completeQuest = FindQuestByName(mallum.assignedQuestName);
                        nameLabel.text = FormatQuestName(completeQuest?.questName ?? mallum.assignedQuestName);
                        durationLabel.text = Loc.Get("ui.quest.complete", "Complete!");
                        durationLabel.style.color = new StyleColor(new Color32(80, 190, 100, 255));
                        if (completeQuest != null)
                            accentStrip.AddToClassList(TierClass(completeQuest.requiredFlameLevel));

                        progressContainer.style.display = DisplayStyle.None;
                        timerLabel.style.display = DisplayStyle.None;

                        actionBtn.text = Loc.Get("ui.quest.collect_rewards", "Collect Rewards");
                        actionBtn.AddToClassList("quest-collect-btn");
                        var capturedQuestName2 = FormatQuestName(completeQuest?.questName ?? mallum.assignedQuestName);
                        actionBtn.clickable = new Clickable(() =>
                        {
                            var rewards = new List<RewardEntry>(mallum.pendingRewards);
                            FindFirstObjectByType<CampFireUI>()?.CloseOverlay(silent: true);
                            RewardRevealUI.Instance?.Show(Loc.Get("ui.quest.quest_complete", "Quest Complete!"), capturedQuestName2, rewards, () =>
                            {
                                MallumManager.Instance.CollectQuestRewards(capturedIndex);
                                Refresh();
                            });
                        });
                        break;
                }
            }
        }

        private void BuildAvailableSection()
        {
            var quests = MallumManager.Instance.GetAvailableQuests();

            SyncHeader(availableSection, ref availableSectionHeader, Loc.Get("ui.quest.available", "Available Quests"), false, quests.Count > 0);
            SyncCardCount(availableSection, availableCardPool, quests.Count);

            int available = MallumManager.Instance.GetAvailableMallumCount();

            for (int c = 0; c < quests.Count; c++)
            {
                var quest = quests[c];
                var card = availableCardPool[c];
                ResetCard(card);

                var accentStrip = card.Q("quest-accent-strip");
                var nameLabel = card.Q<Label>("quest-name");
                var levelBadge = card.Q<Label>("quest-level-badge");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardList = card.Q("quest-reward-list");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                accentStrip.AddToClassList(TierClass(quest.requiredFlameLevel));
                nameLabel.text = FormatQuestName(quest.questName);
                levelBadge.text = string.Format(Loc.Get("ui.label.lv", "Lv {0}"), quest.requiredFlameLevel);
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                lockedLabel.style.display = DisplayStyle.None;

                foreach (var reward in quest.rewardPool)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("quest-reward-chip");
                    bool discovered = !string.IsNullOrEmpty(reward.itemKey) && ApothekeManager.IsSeedDiscovered(reward.itemKey);
                    var chipLabel = new Label(discovered ? ConfigService.Instance.GetItemDisplayName(reward.itemKey) : "???");
                    chipLabel.AddToClassList("quest-reward-name");
                    if (!discovered) chip.AddToClassList("quest-reward-chip--unknown");
                    chip.Add(chipLabel);
                    rewardList.Add(chip);
                }

                var capturedQuest = quest;
                actionBtn.text = available > 0 ? Loc.Get("ui.quest.send_mallum", "Send Mallum") : Loc.Get("ui.quest.no_mallums", "No Mallums Idle");
                actionBtn.AddToClassList("quest-send-btn");
                actionBtn.SetEnabled(available > 0);
                actionBtn.clickable = new Clickable(() =>
                {
                    MallumManager.Instance.SendOnQuest(capturedQuest);
                    Refresh();
                });
            }
        }

        private void BuildLockedSection()
        {
            var locked = MallumManager.Instance.GetLockedQuests();

            SyncHeader(lockedSection, ref lockedSectionHeader, Loc.Get("ui.quest.locked", "Locked"), false, locked.Count > 0);
            SyncCardCount(lockedSection, lockedCardPool, locked.Count);

            for (int c = 0; c < locked.Count; c++)
            {
                var quest = locked[c];
                var card = lockedCardPool[c];
                ResetCard(card);

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
                nameLabel.text = FormatQuestName(quest.questName);
                levelBadge.text = string.Format(Loc.Get("ui.label.lv", "Lv {0}"), quest.requiredFlameLevel);
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                actionBtn.style.display = DisplayStyle.None;
                rewardsContainer.style.display = DisplayStyle.None;
                lockedLabel.text = string.Format(Loc.Get("ui.quest.requires_level", "Requires Flame Level {0}"), quest.requiredFlameLevel);
            }
        }

        private ServerQuestConfig FindQuestByName(string questName)
        {
            return ConfigService.Instance?.GetQuest(questName);
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0) return Loc.Get("ui.quest.done", "Done");
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            if (h > 0) return $"{h}h {m}m";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        private static string FormatQuestName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length + 4);
            sb.Append(name[0]);
            for (int i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
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
