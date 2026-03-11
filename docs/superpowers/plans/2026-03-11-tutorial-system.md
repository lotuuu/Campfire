# Tutorial System Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a character-guided tutorial that teaches core mechanics through play, ending when the player upgrades the flame to level 2.

**Architecture:** Event-driven TutorialManager singleton listens to existing manager events (PlotManager.OnHarvested, FlameManager.OnFlameUpgraded, etc.) and triggers DialogueUI + TutorialUI (hint bar + element highlights). Single `int tutorialStep` in SaveData tracks progress (0–11 active, 12 = complete). Also introduces Energy Drink item (speeds up mallums) and repurposes Speed Potion (crops only).

**Tech Stack:** Unity 6 UI Toolkit, C# MonoBehaviours, Elixir/Phoenix seeds

---

## File Structure

**New files:**
- `Assets/Scripts/Managers/TutorialManager.cs` — Singleton state machine. Listens to game events, triggers dialogue and UI updates.
- `Assets/Scripts/UI/TutorialUI.cs` — Manages hint bar text and element highlighting (adds/removes USS classes).
- `Assets/UI/Styles/Tutorial.uss` — Hint bar styling and `.tutorial-highlight` pulsing glow animation.

**Modified files:**
- `Assets/Scripts/Data/SaveData.cs` — Add `int tutorialStep` field
- `server/priv/repo/seeds.exs` — Sprouts: 10s growth + waterings-only recipe; second plot cost uses Cress_harvest
- `Assets/Scripts/Managers/MallumManager.cs` — Replace Speed_Potion with Energy_Drink for quest/water speedup; add `SpeedUpWaterFetch()` method
- `Assets/Scripts/Managers/PlotManager.cs` — Add `SpeedUpGrowth()` method consuming Speed_Potion
- `Assets/Scripts/UI/QuestUI.cs` — Update speed-up button to use Energy Drink count/label
- `Assets/Scripts/Managers/GameManager.cs` — Adjust starting items (2 Energy Drinks, 2 Speed Potions, vase with 1 water)
- `Assets/UI/Documents/CampFireRoot.uxml` — Add hint bar element + Tutorial.uss stylesheet link
- `Assets/Scripts/UI/CampsiteViewUI.cs` — Expose `GetCellElement(int q, int r)` for tutorial highlights
- `Assets/Scripts/UI/CampFireUI.cs` — Initialize TutorialUI, wire TutorialManager startup after loading gate

---

## Chunk 1: Data Model & Server Changes

### Task 1: Add tutorialStep to SaveData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs:9-31`

- [ ] **Step 1: Add the field**

In `SaveData` class, after line 31 (`public float sfxVolume = 1f;`), add:

```csharp
public int tutorialStep;
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs
git commit -m "feat(tutorial): add tutorialStep field to SaveData"
```

### Task 2: Server seed changes

**Files:**
- Modify: `server/priv/repo/seeds.exs`

- [ ] **Step 1: Update Sprouts seed config**

Change lines 133-136 from:
```elixir
%{seed_name: "Sprouts", growth_duration_hours: 0.00833, min_drops: 1, max_drops: 3, tier: 0, recipe: %{
    "humidity" => RecipeHelper.axis(40, 80, 20, 1),
    "waterings" => RecipeHelper.axis(1, 1, 1, 0.5)
  }},
```

To:
```elixir
%{seed_name: "Sprouts", growth_duration_hours: 0.00278, min_drops: 1, max_drops: 3, tier: 0, recipe: %{
    "waterings" => RecipeHelper.axis(1, 1, 1, 1)
  }},
```

Note: `0.00278` hours = ~10 seconds. Weight changed to 1 (was 0.5) since it's the only axis.

- [ ] **Step 2: Update second plot cost**

Change line 297 from:
```elixir
%{"manaCost" => 150, "harvestCosts" => [%{"itemName" => "Sprouts_harvest", "count" => 1}]},
```

To:
```elixir
%{"manaCost" => 150, "harvestCosts" => [%{"itemName" => "Cress_harvest", "count" => 1}]},
```

- [ ] **Step 3: Commit**

```bash
git add server/priv/repo/seeds.exs
git commit -m "feat(tutorial): retune Sprouts to 10s/waterings-only, second plot costs Cress_harvest"
```

---

## Chunk 2: Item System Changes

### Task 3: Energy Drink item — MallumManager changes

**Files:**
- Modify: `Assets/Scripts/Managers/MallumManager.cs`

The Energy Drink replaces Speed Potion for mallum speedup (quests + water fetching). Speed Potion will be repurposed for crops in Task 4.

- [ ] **Step 1: Add Energy Drink constant and helpers alongside existing Speed Potion ones**

After line 321 (`private const string SpeedPotionItem = "Speed_Potion";`), add:

```csharp
private const string EnergyDrinkItem = "Energy_Drink";

public bool CanUseEnergyDrink()
{
    if (CurrencyManager.FreeMode) return true;
    var item = SaveManager.Instance.Data.items.Find(i => i.itemName == EnergyDrinkItem);
    return item != null && item.count > 0;
}

public int GetEnergyDrinkCount()
{
    var item = SaveManager.Instance.Data.items.Find(i => i.itemName == EnergyDrinkItem);
    return item?.count ?? 0;
}

private bool ConsumeEnergyDrink()
{
    if (CurrencyManager.FreeMode) return true;
    var data = SaveManager.Instance.Data;
    var drink = data.items.Find(i => i.itemName == EnergyDrinkItem);
    if (drink == null || drink.count <= 0) return false;
    drink.count--;
    if (drink.count <= 0) data.items.Remove(drink);
    SaveManager.Instance.Save();
    return true;
}
```

- [ ] **Step 2: Update SpeedUpQuest to use Energy Drink instead of Speed Potion**

Replace the `SpeedUpQuest` method (lines 348-377) with:

```csharp
public bool SpeedUpQuest(int mallumIndex)
{
    var data = SaveManager.Instance.Data;
    if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return false;
    var mallum = data.mallums[mallumIndex];
    if (mallum.state != MallumState.OnQuest) return false;

    if (!ConsumeEnergyDrink()) return false;

    int serverId = mallum.serverId;
    CompleteQuest(mallum);
    NotificationService.Instance?.CancelQuestNotification(mallumIndex);
    SaveManager.Instance.Save();
    OnMallumsChanged?.Invoke();

    if (GameService.Instance != null && GameService.Instance.IsOnline && serverId > 0)
    {
        _ = GameService.Instance.SpeedUpQuest(serverId);
    }

    return true;
}
```

- [ ] **Step 3: Add SpeedUpWaterFetch method**

After the `SpeedUpQuest` method, add:

```csharp
public bool SpeedUpWaterFetch(int mallumIndex)
{
    var data = SaveManager.Instance.Data;
    if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return false;
    var mallum = data.mallums[mallumIndex];
    if (mallum.state != MallumState.FetchingWater) return false;

    if (!ConsumeEnergyDrink()) return false;

    int vaseIndex = mallum.assignedVaseIndex;
    if (vaseIndex >= 0 && vaseIndex < data.vases.Count)
        VaseManager.Instance.InstantFinish(vaseIndex);

    FreeMallumFromWater(mallum);
    NotificationService.Instance?.CancelWaterFetchNotification(mallumIndex);
    SaveManager.Instance.Save();
    OnMallumsChanged?.Invoke();

    return true;
}
```

Note: Uses existing `VaseManager.InstantFinish()` (line 81) which sets vase to Full, clears fill timer, and notifies server. The vase must be in `Filling` state, which is guaranteed since we only call this when the mallum is `FetchingWater` (which means `SendToCollect` was already called, setting the vase to `Filling`).

- [ ] **Step 4: Remove dead Speed Potion methods**

Remove the now-unused methods from MallumManager: `CanSpeedUpQuest()`, `GetSpeedPotionCount()`, `ConsumeSpeedPotion()`, and the `SpeedPotionItem` constant. These are replaced by the Energy Drink equivalents. Find them by name (they are near `SpeedUpQuest`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Managers/MallumManager.cs
git commit -m "feat(items): add Energy Drink for mallum speedup, replace Speed Potion in quests"
```

### Task 4: Speed Potion for crops — PlotManager changes

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

- [ ] **Step 1: Add SpeedUpGrowth method**

After the `InstantFinish` method (line 392), add:

```csharp
public bool SpeedUpGrowth(int plotIndex)
{
    var data = SaveManager.Instance.Data;
    if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
    var plot = data.plots[plotIndex];
    if (plot.state != PlotState.Growing) return false;

    // Check potion availability first
    if (!CurrencyManager.FreeMode)
    {
        var potion = data.items.Find(i => i.itemName == "Speed_Potion");
        if (potion == null || potion.count <= 0) return false;
        potion.count--;
        if (potion.count <= 0) data.items.Remove(potion);
    }

    return InstantFinish(plotIndex);
}

public int GetSpeedPotionCount()
{
    var item = SaveManager.Instance.Data.items.Find(i => i.itemName == "Speed_Potion");
    return item?.count ?? 0;
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs
git commit -m "feat(items): add SpeedUpGrowth consuming Speed Potion for crop speedup"
```

### Task 5: QuestUI — Update speed-up button to use Energy Drink

**Files:**
- Modify: `Assets/Scripts/UI/QuestUI.cs:162-171`

- [ ] **Step 1: Update OnQuest speed-up button**

Replace lines 162-171 (inside the `MallumState.OnQuest` case):

```csharp
                        int potionCount = MallumManager.Instance.GetSpeedPotionCount();
                        actionBtn.text = potionCount > 0 ? $"Speed Up ({potionCount})" : "Speed Up";
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(potionCount > 0);
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpQuest(mallumIndex);
                            MallumManager.Instance.CollectQuestRewards(mallumIndex);
                            Refresh();
                        };
```

With:

```csharp
                        int drinkCount = MallumManager.Instance.GetEnergyDrinkCount();
                        actionBtn.text = drinkCount > 0 ? $"Speed Up ({drinkCount})" : "Speed Up";
                        actionBtn.AddToClassList("quest-speedup-btn");
                        actionBtn.SetEnabled(drinkCount > 0);
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpQuest(mallumIndex);
                            MallumManager.Instance.CollectQuestRewards(mallumIndex);
                            Refresh();
                        };
```

- [ ] **Step 2: Add Energy Drink speed-up button for FetchingWater state**

In the `MallumState.FetchingWater` case (lines 134-144), replace:

```csharp
                        timerLabel.style.display = DisplayStyle.None;
                        actionBtn.style.display = DisplayStyle.None;
```

With:

```csharp
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
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/QuestUI.cs
git commit -m "feat(ui): update QuestUI speed-up buttons to use Energy Drink"
```

### Task 6: GameManager — Adjust starting items

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:72-98`

- [ ] **Step 1: Update InitializeNewPlayer**

Replace lines 85-95:

```csharp
            VaseManager.InitializeNewPlayer(data, ConfigService.Instance.VaseConfig.default_capacity);
            data.vases[0].currentWater = data.vases[0].capacity;
            data.vases[0].state = VaseState.Full;
            data.vases[0].gridX = positions[0].q;
            data.vases[0].gridY = positions[0].r;
            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = positions[1].q, gridY = positions[1].r });
            data.apothekeGridX = positions[3].q;
            data.apothekeGridY = positions[3].r;
            ApothekeManager.Instance.AddSeed("Sprouts", 5);
            ApothekeManager.Instance.AddSeed("Cress", 3);
            data.items.Add(new InventoryItem { itemName = "Speed_Potion", count = 3 });
```

With:

```csharp
            VaseManager.InitializeNewPlayer(data, ConfigService.Instance.VaseConfig.default_capacity);
            data.vases[0].currentWater = 1;
            data.vases[0].state = VaseState.Full;
            data.vases[0].gridX = positions[0].q;
            data.vases[0].gridY = positions[0].r;
            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = positions[1].q, gridY = positions[1].r });
            data.apothekeGridX = positions[3].q;
            data.apothekeGridY = positions[3].r;
            ApothekeManager.Instance.AddSeed("Sprouts", 5);
            ApothekeManager.Instance.AddSeed("Cress", 3);
            data.items.Add(new InventoryItem { itemName = "Speed_Potion", count = 2 });
            data.items.Add(new InventoryItem { itemName = "Energy_Drink", count = 2 });
```

Key changes: vase starts with 1 water (not full capacity), 2 Speed Potions (was 3), 2 Energy Drinks (new).

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "feat(tutorial): adjust new player starting items for tutorial flow"
```

---

## Chunk 3: Tutorial UI

### Task 7: Tutorial stylesheet

**Files:**
- Create: `Assets/UI/Styles/Tutorial.uss`

- [ ] **Step 1: Create the stylesheet**

Unity UI Toolkit does not support `@keyframes`. The pulse effect is implemented via C# class toggling in TutorialUI (alternates between `.tutorial-highlight` and `.tutorial-highlight-dim` on a timer).

```css
/* Tutorial hint bar */
#tutorial-hint-bar {
    position: absolute;
    bottom: 80px;
    left: 0;
    right: 0;
    padding: 8px 16px;
    background-color: rgba(0, 0, 0, 0.75);
    align-items: center;
    justify-content: center;
    flex-direction: row;
}

#tutorial-hint-text {
    color: rgb(255, 255, 255);
    font-size: 14px;
    -unity-text-align: middle-center;
    white-space: normal;
}

/* Highlight for tutorial targets — golden glow border, pulsed via C# class toggle */
.tutorial-highlight {
    border-color: rgba(255, 200, 60, 0.9);
    border-width: 3px;
    transition-property: border-color, border-width;
    transition-duration: 0.4s;
}

.tutorial-highlight-dim {
    border-color: rgba(255, 200, 60, 0.3);
    border-width: 2px;
    transition-property: border-color, border-width;
    transition-duration: 0.4s;
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/UI/Styles/Tutorial.uss Assets/UI/Styles/Tutorial.uss.meta
git commit -m "feat(tutorial): add Tutorial.uss with hint bar and highlight styles"
```

### Task 8: UXML hint bar element

**Files:**
- Modify: `Assets/UI/Documents/CampFireRoot.uxml`

- [ ] **Step 1: Add Tutorial.uss stylesheet link**

After line 18 (`<Style src="project://database/Assets/UI/Styles/Visitor.uss" />`), add:

```xml
    <Style src="project://database/Assets/UI/Styles/Tutorial.uss" />
```

- [ ] **Step 2: Add hint bar element**

After the `bottom-nav` element (after line 97, before the overlay-container), add:

```xml
        <!-- Tutorial hint bar -->
        <ui:VisualElement name="tutorial-hint-bar" style="display: none;">
            <ui:Label name="tutorial-hint-text" text="" />
        </ui:VisualElement>
```

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(tutorial): add hint bar UXML element and Tutorial.uss link"
```

### Task 9: TutorialUI controller

**Files:**
- Create: `Assets/Scripts/UI/TutorialUI.cs`

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class TutorialUI : MonoBehaviour
    {
        private VisualElement hintBar;
        private Label hintText;
        private VisualElement root;

        // Highlight pulse state
        private VisualElement currentHighlight;
        private float pulseTimer;
        private bool pulseBright;
        private const float PulseInterval = 0.8f;

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            hintBar = root.Q("tutorial-hint-bar");
            hintText = root.Q<Label>("tutorial-hint-text");
        }

        private void Update()
        {
            if (currentHighlight == null) return;

            pulseTimer += Time.deltaTime;
            if (pulseTimer >= PulseInterval)
            {
                pulseTimer = 0f;
                pulseBright = !pulseBright;
                if (pulseBright)
                {
                    currentHighlight.RemoveFromClassList("tutorial-highlight-dim");
                    currentHighlight.AddToClassList("tutorial-highlight");
                }
                else
                {
                    currentHighlight.RemoveFromClassList("tutorial-highlight");
                    currentHighlight.AddToClassList("tutorial-highlight-dim");
                }
            }
        }

        public void ShowHint(string text)
        {
            if (hintBar == null) return;
            hintText.text = text;
            hintBar.style.display = DisplayStyle.Flex;
        }

        public void HideHint()
        {
            if (hintBar != null)
                hintBar.style.display = DisplayStyle.None;
        }

        public void HighlightElement(string elementName)
        {
            ClearHighlight();
            var element = root.Q(elementName);
            if (element == null) return;
            currentHighlight = element;
            pulseBright = true;
            pulseTimer = 0f;
            element.AddToClassList("tutorial-highlight");
        }

        public void HighlightElement(VisualElement element)
        {
            ClearHighlight();
            if (element == null) return;
            currentHighlight = element;
            pulseBright = true;
            pulseTimer = 0f;
            element.AddToClassList("tutorial-highlight");
        }

        public void ClearHighlight()
        {
            if (currentHighlight != null)
            {
                currentHighlight.RemoveFromClassList("tutorial-highlight");
                currentHighlight.RemoveFromClassList("tutorial-highlight-dim");
                currentHighlight = null;
            }
        }

        public void HideAll()
        {
            HideHint();
            ClearHighlight();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/UI/TutorialUI.cs
git commit -m "feat(tutorial): add TutorialUI controller for hint bar and highlights"
```

---

## Chunk 4: Tutorial Manager & Wiring

### Task 10a: Expose hex cell lookup on CampsiteViewUI

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs`

Hex cells are stored in a private `cellLookup` dictionary keyed by `(q, r)` coordinates (line 51). Cells have no `name` attribute, so TutorialUI can't find them via `root.Q()`. We need to expose a public accessor.

- [ ] **Step 1: Add public accessor**

After line 51 (`private readonly Dictionary<(int, int), VisualElement> cellLookup = new();`), add:

```csharp
public VisualElement GetCellElement(int q, int r)
{
    return cellLookup.TryGetValue((q, r), out var cell) ? cell : null;
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat(tutorial): expose GetCellElement on CampsiteViewUI for tutorial highlights"
```

### Task 10b: TutorialManager — Core state machine

**Files:**
- Create: `Assets/Scripts/Managers/TutorialManager.cs`

This is the largest file. The manager listens to existing events and drives the tutorial flow.

- [ ] **Step 1: Create TutorialManager.cs**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Managers/TutorialManager.cs
git commit -m "feat(tutorial): add TutorialManager state machine with event-driven progression"
```

### Task 11: Wire TutorialManager into CampFireUI

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

- [ ] **Step 1: Add TutorialUI and TutorialManager fields**

After line 27 (`private SettingsUI settingsUI;`), add:

```csharp
        private TutorialUI tutorialUI;
```

- [ ] **Step 2: Initialize TutorialUI in Start()**

After line 92 (`settingsUI?.Initialize(root);`), add:

```csharp
            tutorialUI = GetComponent<TutorialUI>();
            tutorialUI?.Initialize(root);
```

- [ ] **Step 3: Start tutorial after loading gate completes**

In the `UpdateLoadingGate()` method, after line 407 (`UpdateQuestBadge();`), before the `return;`, add:

```csharp
                // Start tutorial after all services are ready
                if (TutorialManager.Instance != null && dialogueUI != null && tutorialUI != null)
                    TutorialManager.Instance.Initialize(tutorialUI, dialogueUI, campsiteView);
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat(tutorial): wire TutorialUI and TutorialManager into CampFireUI"
```

### Task 12: Add TutorialManager and TutorialUI components in Unity

**Files:**
- Scene: `Assets/Scenes/Garden.unity` (via Unity MCP)

- [ ] **Step 1: Add TutorialManager and TutorialUI MonoBehaviours to the "--- UI ---" GameObject**

Use Unity MCP `manage_components` to add `Garden.TutorialManager` and `Garden.TutorialUI` components to the same GameObject that has `CampFireUI` on it (the `--- UI ---` GameObject).

- [ ] **Step 2: Verify compilation**

Use `read_console` to check for compilation errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat(tutorial): add TutorialManager and TutorialUI components to UI GameObject"
```

---

## Chunk 5: Testing & Polish

### Task 13: Manual testing checklist

- [ ] **Step 1: Clear save data** — Use debug panel "Clear Save Data" to start fresh
- [ ] **Step 2: Verify welcome dialogue appears** on first load
- [ ] **Step 3: Verify hint bar** shows "Tap your plot to plant a seed" with plot highlighted
- [ ] **Step 4: Plant Sprouts** — verify hint advances to "Water your plant"
- [ ] **Step 5: Water plant** — verify hint advances to "harvest"
- [ ] **Step 6: Harvest** — verify recipe explanation dialogue appears
- [ ] **Step 7: Plant again** — verify dual hint about planting + water fetch
- [ ] **Step 8: Test Energy Drink** speed up on water fetch mallum
- [ ] **Step 9: Harvest second plant** — verify branching dialogue (watered vs not)
- [ ] **Step 10: Build Mallum house** — verify dialogue + quest hint
- [ ] **Step 11: Send Mallum on quest** — verify dialogue + Cress hint
- [ ] **Step 12: Plant Cress, use Speed Potion** — verify speedup works
- [ ] **Step 13: Build second plot** — verify Cress_harvest cost works
- [ ] **Step 14: Upgrade flame to level 2** — verify completion dialogue
- [ ] **Step 15: Verify hint bar disappears** after tutorial completes
- [ ] **Step 16: Reload game** — verify tutorialStep persists and tutorial doesn't restart

### Task 14: Final commit

- [ ] **Step 1: Run tests**

Use Unity MCP `run_tests` with `mode: "EditMode"` to verify no regressions.

- [ ] **Step 2: Final commit if any polish changes were made**
