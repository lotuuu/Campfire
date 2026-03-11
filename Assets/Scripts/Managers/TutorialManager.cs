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

        private const int StepWelcome = 0;
        private const int StepPlantFirst = 1;
        private const int StepWaterFirst = 2;
        private const int StepHarvestFirst = 3;
        private const int StepExplainRecipes = 4;
        private const int StepPlantAgainAndFetchWater = 5;
        private const int StepWateringOutcome = 6;
        private const int StepBuildHouse = 7;
        private const int StepSendOnQuest = 8;
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
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Your seed is planted and growing!"
                        }, () => AdvanceTo(StepWaterFirst));
                    }
                    break;

                case StepWaterFirst:
                    // Player watered the plant
                    if (plotIndex < data.plots.Count && data.plots[plotIndex].waterCount > 0)
                    {
                        tutorialUI?.ClearHighlight();
                        AdvanceTo(StepHarvestFirst);
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
                    }, () => AdvanceTo(StepPlantAgainAndFetchWater));
                    break;

                case StepPlantAgainAndFetchWater:
                    // Second harvest — check if they managed to water
                    if (result.waterCount > 0)
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Nice work getting the water in time!"
                        }, () => AdvanceTo(StepBuildHouse));
                    }
                    else
                    {
                        ShowDialogue("Spark of Ara", new List<string> {
                            "Without watering, you got less harvest.",
                            "Try to follow the recipe next time."
                        }, () => AdvanceTo(StepBuildHouse));
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
                            "More Mallums means more water and more quests!"
                        }, () => AdvanceTo(StepSendOnQuest));
                    }
                    break;

                case StepSendOnQuest:
                    // Check if any mallum is on a quest
                    foreach (var m in data.mallums)
                    {
                        if (m.state == MallumState.OnQuest)
                        {
                            ShowDialogue("Spark of Ara", new List<string> {
                                "Quests reward you with rare seeds and items."
                            }, () => AdvanceTo(StepPlantCressSpeedPotion));
                            return;
                        }
                    }
                    break;
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

        // --- Hint display per step ---

        private void ShowHintForStep(int step)
        {
            tutorialUI?.ClearHighlight();

            switch (step)
            {
                case StepPlantFirst:
                    tutorialUI?.ShowHint("Tap your plot to plant a seed");
                    HighlightHexCell(0);
                    break;
                case StepWaterFirst:
                    tutorialUI?.ShowHint("Water your plant for a better harvest");
                    HighlightHexCell(0);
                    break;
                case StepHarvestFirst:
                    tutorialUI?.ShowHint("Your plant is ready! Tap to harvest");
                    HighlightHexCell(0);
                    break;
                case StepExplainRecipes:
                    // Dialogue-only step — auto-skip on resume
                    AdvanceTo(StepPlantAgainAndFetchWater);
                    break;
                case StepPlantAgainAndFetchWater:
                    tutorialUI?.ShowHint("Plant another seed. Send your Mallum to fetch water.");
                    HighlightHexCell(0); // plot
                    break;
                case StepWateringOutcome:
                    // Dialogue-only step — auto-skip on resume
                    AdvanceTo(StepBuildHouse);
                    break;
                case StepBuildHouse:
                    tutorialUI?.ShowHint("Build a Mallum House to get more helpers");
                    HighlightFlameHex();
                    break;
                case StepSendOnQuest:
                    tutorialUI?.ShowHint("Send a Mallum on a quest to earn rewards");
                    tutorialUI?.HighlightElement("btn-quest");
                    break;
                case StepPlantCressSpeedPotion:
                    tutorialUI?.ShowHint("Plant Cress and use a Speed Potion to grow it faster");
                    HighlightHexCell(0);
                    break;
                case StepBuildSecondPlot:
                    tutorialUI?.ShowHint("Build another plot to grow more");
                    HighlightFlameHex();
                    break;
                case StepUpgradeFlame:
                    tutorialUI?.ShowHint("Collect harvests and upgrade your flame");
                    HighlightFlameHex();
                    break;
            }
        }

        // --- Dialogue helper ---

        private void ShowDialogue(string speaker, List<string> lines, System.Action onComplete)
        {
            tutorialUI?.ClearHighlight();
            tutorialUI?.HideHint();
            dialogueUI?.Show(speaker, lines, onComplete);
        }

        private void ShowWelcome()
        {
            ShowDialogue("Spark of Ara", new List<string> {
                "Welcome to your camp!",
                "I'm the Spark of Ara. Let me show you around."
            }, () => AdvanceTo(StepPlantFirst));
        }

        // --- Highlight helpers ---
        // Hex cells have no name attribute; use CampsiteViewUI.GetCellElement(q, r).

        private void HighlightHexCell(int plotIndex)
        {
            if (campsiteView == null) return;
            var data = SaveManager.Instance.Data;
            if (plotIndex < 0 || plotIndex >= data.plots.Count) return;
            var plot = data.plots[plotIndex];
            var cell = campsiteView.GetCellElement(plot.gridX, plot.gridY);
            tutorialUI?.HighlightElement(cell);
        }

        private void HighlightFlameHex()
        {
            // Flame is always at center hex (0, 0)
            if (campsiteView == null) return;
            var cell = campsiteView.GetCellElement(0, 0);
            tutorialUI?.HighlightElement(cell);
        }

        // --- Check for skipped steps on PlotChanged ---
        // If player is on StepBuildSecondPlot and already has 2+ plots, auto-advance
        private void Update()
        {
            if (!initialized || IsComplete) return;

            var data = SaveManager.Instance.Data;
            switch (CurrentStep)
            {
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
