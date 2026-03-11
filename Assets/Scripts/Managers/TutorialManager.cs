using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private TutorialUI tutorialUI;
        private DialogueUI dialogueUI;
        private CampsiteViewUI campsiteView;
        private bool initialized;
        private int highlightQ = int.MinValue;
        private int highlightR = int.MinValue;

        // Flow: Welcome → Plant → Water → Harvest → Build House → Plant Again → Fetch Water → Quest → Speed Up Quest → Cress → Second Plot → Upgrade → Complete
        private const int StepWelcome = 0;
        private const int StepPlantFirst = 1;
        private const int StepWaterFirst = 2;
        private const int StepHarvestFirst = 3;
        private const int StepBuildHouse = 4;
        private const int StepPlantAgain = 5;
        private const int StepFetchWater = 6;
        private const int StepSendOnQuest = 7;
        private const int StepSpeedUpQuest = 8;
        private const int StepPlantCressSpeedPotion = 9;
        private const int StepBuildSecondPlot = 10;
        private const int StepUpgradeFlame = 11;
        private const int StepComplete = 12;

        public bool IsComplete => CurrentStep >= StepComplete;
        public int CurrentStep => SaveManager.Instance.Data.tutorialStep;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Initialize(TutorialUI ui, DialogueUI dialogue, CampsiteViewUI campsite)
        {
            tutorialUI = ui;
            dialogueUI = dialogue;
            campsiteView = campsite;

            if (IsComplete)
            {
                tutorialUI?.HideAll();
                return;
            }

            SubscribeEvents();
            initialized = true;

            // If step 0, show welcome. Otherwise resume current step hint.
            if (CurrentStep == StepWelcome)
                ShowWelcome();
            else
                ShowHintForStep(CurrentStep);
        }

        private void SubscribeEvents()
        {
            if (PlotManager.Instance != null)
            {
                PlotManager.Instance.OnPlotChanged += OnPlotChanged;
                PlotManager.Instance.OnHarvested += OnHarvested;
            }
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged += OnVasesChanged;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged += OnMallumsChanged;
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += OnFlameUpgraded;
        }

        private void OnDestroy()
        {
            if (PlotManager.Instance != null)
            {
                PlotManager.Instance.OnPlotChanged -= OnPlotChanged;
                PlotManager.Instance.OnHarvested -= OnHarvested;
            }
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged -= OnVasesChanged;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged -= OnMallumsChanged;
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= OnFlameUpgraded;
        }

        private void AdvanceTo(int step)
        {
            SaveManager.Instance.Data.tutorialStep = step;
            SaveManager.Instance.Save();

            if (step >= StepComplete)
            {
                ClearAllHighlights();
                tutorialUI?.HideAll();
                return;
            }

            ShowHintForStep(step);
        }

        // --- Event handlers ---

        private void OnPlotChanged(int plotIndex)
        {
            if (!initialized || IsComplete) return;
            var data = SaveManager.Instance.Data;

            switch (CurrentStep)
            {
                case StepPlantFirst:
                    // Player planted a seed
                    if (plotIndex < data.plots.Count && data.plots[plotIndex].state == PlotState.Growing)
                    {
                        // Pause growth after 8s so the player has time to water
                        StartCoroutine(DelayedGrowthPause(8f));

                        ShowDialogue("Spark of Ara", new List<string> {
                            "Your seed is planted and growing!"
                        }, () => AdvanceTo(StepWaterFirst));
                    }
                    break;

                case StepWaterFirst:
                    // Player watered the plant
                    if (plotIndex < data.plots.Count && data.plots[plotIndex].waterCount > 0)
                    {
                        // Resume growth now that watering is done
                        if (PlotManager.Instance != null)
                            PlotManager.Instance.GrowthPaused = false;

                        ClearAllHighlights();
                        AdvanceTo(StepHarvestFirst);
                    }
                    break;

                case StepHarvestFirst:
                    // Wait for the plant to actually mature before showing harvest hint
                    if (plotIndex < data.plots.Count && data.plots[plotIndex].state == PlotState.Mature)
                    {
                        tutorialUI?.ShowHint("Your plant is ready! Tap to harvest");
                        HighlightHexCell(0);
                    }
                    break;

                case StepPlantAgain:
                    // Player planted a second seed
                    if (plotIndex < data.plots.Count && data.plots[plotIndex].state == PlotState.Growing)
                    {
                        AdvanceTo(StepFetchWater);
                    }
                    break;
            }
        }

        private void OnHarvested(int plotIndex, HarvestResult result)
        {
            if (!initialized || IsComplete) return;

            switch (CurrentStep)
            {
                case StepHarvestFirst:
                    ShowDialogue("Spark of Ara", new List<string> {
                        $"You harvested {result.drops} {result.seedName}!",
                        "Your harvest was better because you watered it.",
                        "Each seed has a recipe. Follow it for higher yields."
                    }, () => AdvanceTo(StepBuildHouse));
                    break;

                case StepFetchWater:
                    // Second harvest — check if they managed to water
                    if (result.waterCount > 0)
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Nice work getting the water in time!"
                        }, () => AdvanceTo(StepSendOnQuest));
                    }
                    else
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Without watering, you got less harvest.",
                            "Try to follow the recipe next time."
                        }, () => AdvanceTo(StepSendOnQuest));
                    }
                    break;

                case StepPlantCressSpeedPotion:
                    if (result.seedName == "Cress")
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Cress harvested! You can use this to build more plots."
                        }, () => AdvanceTo(StepBuildSecondPlot));
                    }
                    break;
            }
        }

        private void OnVasesChanged()
        {
            // Not directly used for step transitions, but could highlight vase state
        }

        private void OnMallumsChanged()
        {
            if (!initialized || IsComplete) return;
            var data = SaveManager.Instance.Data;

            switch (CurrentStep)
            {
                case StepBuildHouse:
                    // Check if a mallum house was built
                    if (data.mallumHouses.Count > 0)
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Your Mallum can fetch water and go on quests!"
                        }, () => AdvanceTo(StepPlantAgain));
                    }
                    break;

                case StepFetchWater:
                    // Mallum started fetching — clear vase highlight, show waiting hint
                    foreach (var m in data.mallums)
                    {
                        if (m.state == MallumState.FetchingWater)
                        {
                            ClearAllHighlights();
                            tutorialUI?.ShowHint("Your Mallum is fetching water. Use an Energy Drink to speed it up!");
                            return;
                        }
                    }
                    break;

                case StepSendOnQuest:
                    // Check if any mallum is on a quest
                    foreach (var m in data.mallums)
                    {
                        if (m.state == MallumState.OnQuest)
                        {
                            AdvanceTo(StepSpeedUpQuest);
                            return;
                        }
                    }
                    break;

                case StepSpeedUpQuest:
                    {
                        // Hide hint as soon as quest is sped up (QuestComplete),
                        // but wait for rewards to be collected (Idle) before showing next dialogue
                        bool anyOnQuest = false;
                        bool anyQuestComplete = false;
                        foreach (var m in data.mallums)
                        {
                            if (m.state == MallumState.OnQuest)
                                anyOnQuest = true;
                            if (m.state == MallumState.QuestComplete)
                                anyQuestComplete = true;
                        }
                        if (anyQuestComplete && !anyOnQuest)
                        {
                            // Quest sped up — hide hint while reward reveal is showing
                            tutorialUI?.HideHint();
                            ClearAllHighlights();
                        }
                        if (!anyOnQuest && !anyQuestComplete)
                        {
                            ShowDialogue("Spark of Ara", new List<string> {
                            "Quests reward you with rare seeds and items.",
                            "Use those seeds to expand your camp!"
                        }, () => AdvanceTo(StepPlantCressSpeedPotion));
                        }
                        break;
                    }
            }
        }

        private void OnFlameUpgraded()
        {
            if (!initialized || IsComplete) return;

            if (CurrentStep == StepUpgradeFlame)
            {
                ShowDialogue("Spark of Ara", new List<string> {
                    "Your flame grows stronger!",
                    "Your camp can hold more now.",
                    "You're on your own — good luck!"
                }, () => AdvanceTo(StepComplete));
            }
        }

        private IEnumerator DelayedGrowthPause(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (PlotManager.Instance != null && CurrentStep <= StepWaterFirst)
                PlotManager.Instance.GrowthPaused = true;
        }

        // --- Hint display per step ---

        private void ShowHintForStep(int step)
        {
            ClearAllHighlights();

            switch (step)
            {
                case StepPlantFirst:
                    tutorialUI?.ShowHint("Tap your plot to plant a seed");
                    HighlightHexCell(0);
                    break;
                case StepWaterFirst:
                    // On resume, restart the delayed pause coroutine
                    StartCoroutine(DelayedGrowthPause(8f));
                    tutorialUI?.ShowHint("Water your plant for a better harvest");
                    HighlightVaseHex(0);
                    break;
                case StepHarvestFirst:
                    {
                        // Check if plant is already mature (e.g. on resume)
                        var plots = SaveManager.Instance.Data.plots;
                        if (plots.Count > 0 && plots[0].state == PlotState.Mature)
                        {
                            tutorialUI?.ShowHint("Your plant is ready! Tap to harvest");
                            HighlightHexCell(0);
                        }
                        else
                        {
                            tutorialUI?.ShowHint("Your plant is growing...");
                        }
                        break;
                    }
                case StepBuildHouse:
                    tutorialUI?.ShowHint("Build a Mallum House to get a helper");
                    HighlightFlameHex();
                    break;
                case StepPlantAgain:
                    tutorialUI?.ShowHint("Plant another seed");
                    HighlightHexCell(0);
                    break;
                case StepFetchWater:
                    {
                        // Check if mallum is already fetching (resume case)
                        bool alreadyFetching = false;
                        foreach (var m in SaveManager.Instance.Data.mallums)
                        {
                            if (m.state == MallumState.FetchingWater)
                            { alreadyFetching = true; break; }
                        }
                        if (alreadyFetching)
                        {
                            tutorialUI?.ShowHint("Your Mallum is fetching water. Use an Energy Drink to speed it up!");
                        }
                        else
                        {
                            tutorialUI?.ShowHint("Send your Mallum to fetch water");
                            HighlightVaseHex(0);
                        }
                        break;
                    }
                case StepSendOnQuest:
                    ShowDialogue("Spark of Ara", new List<string> {
                        "Send a Mallum on a quest to earn rewards!"
                    }, () =>
                    {
                        tutorialUI?.HighlightElement("btn-quest");
                        tutorialUI?.DeferHighlightByClass("quest-send-btn");
                    });
                    break;
                case StepSpeedUpQuest:
                    {
                        // Check if quest already completed (resume case)
                        bool stillOnQuest = false;
                        foreach (var m in SaveManager.Instance.Data.mallums)
                        {
                            if (m.state == MallumState.OnQuest || m.state == MallumState.QuestComplete)
                            { stillOnQuest = true; break; }
                        }
                        if (stillOnQuest)
                        {
                            tutorialUI?.ShowHint("Use an Energy Drink to speed up the quest");
                            tutorialUI?.HighlightElementByClass("quest-speedup-btn");
                        }
                        else
                        {
                            // Already collected — auto-advance
                            AdvanceTo(StepPlantCressSpeedPotion);
                        }
                        break;
                    }
                case StepPlantCressSpeedPotion:
                    tutorialUI?.ShowHint("Plant Cress and use a Speed Potion to grow it faster");
                    HighlightHexCell(0);
                    break;
                case StepBuildSecondPlot:
                    tutorialUI?.ShowHint("Build another plot to grow more seeds!");
                    HighlightFlameHex();
                    break;
                case StepUpgradeFlame:
                    tutorialUI?.ShowHint("Collect harvests and upgrade your flame");
                    HighlightFlameHex();
                    break;
            }
        }

        // --- Dialogue helper ---

        private Texture2D _portraitCache;

        private void ShowDialogue(string speaker, List<string> lines, System.Action onComplete)
        {
            ClearAllHighlights();
            tutorialUI?.HideHint();
            if (_portraitCache == null)
                _portraitCache = SpriteService.Instance?.GetTexture("portraits/spark_of_ara");
            dialogueUI?.Show(speaker, lines, onComplete, _portraitCache);
        }

        private void ShowWelcome()
        {
            ShowDialogue("Spark of Ara", new List<string> {
                "Welcome to your camp!",
                "I'm the Spark of Ara. Let me show you around."
            }, () => AdvanceTo(StepPlantFirst));
        }

        // --- Highlight helpers ---

        private void ClearAllHighlights()
        {
            tutorialUI?.ClearHighlight();
            campsiteView?.ExitTutorialHighlight();
            highlightQ = int.MinValue;
            highlightR = int.MinValue;
        }

        private bool IsHighlightingCell(int q, int r)
        {
            return highlightQ == q && highlightR == r;
        }

        private void HighlightHexCell(int plotIndex)
        {
            if (campsiteView == null) return;
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return;
            var plot = data.plots[plotIndex];
            highlightQ = plot.gridX;
            highlightR = plot.gridY;
            campsiteView.EnterTutorialHighlight(plot.gridX, plot.gridY);
        }

        private void HighlightVaseHex(int vaseIndex)
        {
            if (campsiteView == null) return;
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return;
            var vase = data.vases[vaseIndex];
            highlightQ = vase.gridX;
            highlightR = vase.gridY;
            campsiteView.EnterTutorialHighlight(vase.gridX, vase.gridY);
        }

        private void HighlightFlameHex()
        {
            if (campsiteView == null) return;
            highlightQ = 0;
            highlightR = 0;
            campsiteView.EnterTutorialHighlight(0, 0);
        }

        // --- Auto-advance checks for poll-based steps ---
        private void Update()
        {
            if (!initialized || IsComplete) return;

            var data = SaveManager.Instance.Data;
            switch (CurrentStep)
            {
                case StepFetchWater:
                    // If the plot matured without watering, switch highlight to the plot
                    if (data.plots.Count > 0 && data.plots[0].state == PlotState.Mature)
                    {
                        var plot = data.plots[0];
                        if (!IsHighlightingCell(plot.gridX, plot.gridY))
                        {
                            string hint = plot.waterCount > 0
                                ? "Your plant is ready! Tap to harvest"
                                : "Your crop grew before you could water it. Harvest it now!";
                            tutorialUI?.ShowHint(hint);
                            ClearAllHighlights();
                            HighlightHexCell(0);
                        }
                    }
                    break;
                case StepBuildHouse:
                    if (data.mallumHouses.Count > 0)
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Your Mallum can fetch water and go on quests!"
                        }, () => AdvanceTo(StepPlantAgain));
                    }
                    break;
                case StepBuildSecondPlot:
                    if (data.plots.Count >= 2)
                        AdvanceTo(StepUpgradeFlame);
                    break;
                case StepUpgradeFlame:
                    if (data.flameLevel >= 2)
                        AdvanceTo(StepComplete);
                    break;
            }
        }
    }
}
