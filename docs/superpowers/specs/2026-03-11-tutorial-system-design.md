# Tutorial System Design

## Overview

Character-guided, event-driven tutorial that teaches core mechanics through play. The narrator (Spark of Ara) appears at milestones via DialogueUI. A persistent hint bar shows the current objective. Relevant UI elements/hexes get a pulsing highlight. The player is never locked — they can interact freely, and the tutorial reacts to what they do.

Tutorial ends when the player upgrades the flame to level 2.

## Tutorial Flow

### Phase 1 — First Plant Cycle

**Step 0 — Welcome**
- Trigger: First load (tutorialStep == 0)
- Dialogue: "Welcome to your camp! I'm the Spark of Ara."

**Step 1 — Plant First Sprouts**
- Hint: "Tap your plot to plant a seed"
- Highlight: Empty plot hex
- Trigger: Player plants a seed

**Step 2 — Water It**
- Hint: "Water your plant for a better harvest"
- Highlight: Growing plot hex
- Trigger: Player waters the plot (using 1 starting water in vase)

**Step 3 — Harvest**
- Hint: "Your plant is ready! Tap to harvest"
- Highlight: Mature plot hex
- Trigger: Player harvests

### Phase 2 — GrowthRecipes & Water Management

**Step 4 — Explain GrowthRecipes**
- Trigger: Immediately after step 3 harvest
- Dialogue: "Your harvest was better because you watered it. Each seed has a recipe — follow it for higher yields."

**Step 5 — Plant Again + Fetch Water Race**
- Hint: "Plant another seed. Send your Mallum to fetch water."
- Highlight: Empty plot hex + vase hex
- Two parallel objectives:
  - Plant Sprouts (10s growth timer starts)
  - Send Mallum to fetch water → use Energy Drink to speed up
- Player likely won't get water back in time
- Trigger: Player harvests the second plant

**Step 6 — Watering Outcome**
- Trigger: Immediately after step 5 harvest
- If missed watering: "See? Without watering, you got less. Try to follow the recipe next time."
- If watered in time: "Nice work getting the water in time!"

### Phase 3 — Mallums & Quests

**Step 7 — Build Mallum House**
- Hint: "Build a Mallum House to get more helpers"
- Highlight: Build nav button
- Trigger: Player builds a Mallum house

**Step 8 — Send Mallum on Quest**
- Hint: "Send a Mallum on a quest to earn rewards"
- Highlight: Quest button
- Trigger: Player sends Mallum on quest

### Phase 4 — Expanding & Upgrading

**Step 9 — Plant Cress + Speed Potion**
- Hint: "Plant Cress and use a Speed Potion to grow it faster"
- Highlight: Empty plot hex
- Trigger: Player harvests Cress

**Step 10 — Build Second Plot**
- Hint: "Build another plot to grow more"
- Highlight: Build nav button
- Trigger: Player builds second plot (costs 1 Cress_harvest)

**Step 11 — Upgrade Flame to Level 2**
- Hint: "Collect harvests and upgrade your flame"
- Highlight: Build nav button (when affordable)
- Costs: 5 Sprouts_harvest + 2 Cress_harvest
- Trigger: Player upgrades flame
- Dialogue: "Your flame grows stronger! Your camp can hold more now. You're on your own — good luck!"
- Tutorial complete

## Game Mechanic Changes

### New Item: Energy Drink
- Speeds up Mallums (both quests and water fetching)
- New players start with 2 (1 consumed during tutorial)

### Speed Potion Repurposed
- Now only speeds up crop growth (no longer affects Mallums)
- New players start with 2 (was 3)

### Sprouts Seed Retuned
- Growth duration: 10 seconds (was longer)
- GrowthRecipe: only `useWaterings = true`, ideal = 1 watering

### Vase Starting State
- Starts with 1 water unit (not full)

### Second Plot Building Cost
- Costs 1 Cress_harvest (server building cost change)

## Technical Architecture

### New Files

- `Assets/Scripts/Managers/TutorialManager.cs` — Singleton MonoBehaviour. State machine driven by `tutorialStep` int. Listens for game events (plant, water, harvest, build, upgrade) from existing managers. Triggers DialogueUI and TutorialUI highlights/hints at milestones. On session resume, shows brief reminder dialogue for current step.
- `Assets/Scripts/UI/TutorialUI.cs` — MonoBehaviour on UI GameObject. Manages hint bar display (text updates per step) and element highlighting (add/remove `.tutorial-highlight` USS class on target VisualElements). Initialized via `Initialize(VisualElement root)` pattern like other sub-controllers.
- `Assets/UI/Styles/Tutorial.uss` — Hint bar positioning/styling. `.tutorial-highlight` class with pulsing glow animation for highlighted elements.

### Modified Files

- `Assets/Scripts/Data/SaveData.cs` — Add `int tutorialStep` field (0 = not started, 12 = complete)
- `Assets/Scripts/Managers/GameManager.cs` — Adjust starting items: 2 Energy Drinks, 2 Speed Potions, vase with 1 water (not full)
- `Assets/Scripts/Managers/MallumManager.cs` — Add Energy Drink support for quest + water fetch speedup. Remove Speed Potion support for quests.
- `Assets/Scripts/Managers/PlotManager.cs` — Add Speed Potion support for crop growth speedup
- `Assets/UI/Documents/CampFireRoot.uxml` — Add hint bar element
- `Assets/Scripts/UI/CampFireUI.cs` — Initialize TutorialUI sub-controller
- `server/priv/repo/seeds.exs` — Sprouts: 10s growth, recipe = 1 watering. Second plot cost: 1 Cress_harvest.

### State

Single `int tutorialStep` in SaveData. Values 0–11 are active tutorial steps, 12 = complete. Persisted via existing SaveManager deferred flush. On load, TutorialManager checks step and resumes.

### Highlights

USS class `.tutorial-highlight` with pulsing glow animation (CSS keyframes). TutorialUI applies/removes the class on target VisualElements based on current step. Removed when step completes.

### Hint Bar

Fixed VisualElement below the resource bar, shows current objective text. Updated by TutorialUI when step changes. Hidden when tutorial is complete (tutorialStep == 12).
