# Mallum Quest System Design

## Overview

Add a quest system where players send Mallums on timed expeditions that yield seeds from a reward pool. Mallums become a unified limited resource — the same pool is shared between water-fetching and quests, creating meaningful trade-offs.

## Data Model

### MallumConfig (ScriptableObject, `Resources/Config/`)

- `maxMallumsPerFlameLevel` — `int[]` mapping flame level index to Mallum cap (e.g., `[1, 1, 2, 2, 3]`)

### QuestData (ScriptableObject, one per quest in `Resources/Quests/`)

- `questName` — display name
- `description` — flavor text
- `durationMinutes` — how long the quest takes
- `requiredFlameLevel` — minimum flame level to unlock
- `rewardRolls` — number of picks from the pool (e.g., 2-4)
- `rewardPool` — `List<QuestReward>` where each entry is `{ SeedData seed, float weight, int minCount, int maxCount }`

### MallumSave (Serializable, stored in `SaveData.mallums`)

- `state` — enum `MallumState`: `Idle`, `FetchingWater`, `OnQuest`, `QuestComplete`
- `assignedVaseIndex` — which vase if fetching water, -1 otherwise
- `assignedQuestName` — quest name string if on quest, null otherwise
- `startTimeUtc` — ISO timestamp for current activity
- `pendingRewards` — `List<RewardEntry>` (seed name + count), populated on quest completion, cleared on collect

### SaveData Changes

Add field: `List<MallumSave> mallums`

## Core Manager: MallumManager

Singleton MonoBehaviour owning all Mallum state.

### API

- `GetTotalMallumCount()` — based on current flame level + MallumConfig
- `GetAvailableMallumCount()` — count of Idle mallums
- `SendToFetchWater(vaseIndex)` — claims idle Mallum, sets FetchingWater state
- `SendOnQuest(QuestData)` — claims idle Mallum, sets OnQuest state
- `CollectQuestRewards(mallumIndex)` — adds pending seeds to Apotheke, returns Mallum to Idle
- `GetAvailableQuests()` — filters all QuestData by current flame level
- `OnMallumsChanged` — event for UI updates

### Update Tick

- Checks FetchingWater mallums: when elapsed time >= VaseConfig fill duration, triggers VaseManager fill completion, returns Mallum to Idle
- Checks OnQuest mallums: when elapsed time >= quest duration, rolls rewards from pool, transitions to QuestComplete
- Handles Mallum count changes on flame level up (adds new Idle mallums if cap increased)

## Reward Mechanics

When a quest timer completes:
1. Roll `rewardRolls` picks from the quest's `rewardPool`
2. Each pick: weighted random selection of a seed, then random count between minCount and maxCount
3. Store results in `pendingRewards` on the MallumSave
4. Transition state to QuestComplete
5. Player must tap "Collect" to receive seeds (added to Apotheke inventory)

## Offline Progress

Same pattern as vases — UTC timestamps. If app was closed and reopened after duration, quest immediately marked QuestComplete on next Update tick. Multiple quests can complete while offline.

## Starter Quest Content

| Quest | Duration | Flame Level | Reward Pool |
|-------|----------|-------------|-------------|
| Swamp Forage | 30 min | 1 | Fern (high weight), Moonvine (low weight) |
| Meadow Expedition | 2 hours | 2 | Sunflower (high), Fern (medium) |
| Deep Woods Trek | 6 hours | 3 | Moonvine (high), Sunflower (medium), Fern (low) |

Longer quests = rarer seeds, creating a trade-off between quick common seeds and tying up a Mallum for hours for rarer ones.

## VaseManager Integration

VaseManager's `SendToCollect()` routes through `MallumManager.SendToFetchWater()`. VaseManager keeps its fill timer logic. MallumManager gates the action on Mallum availability. When water fetch completes, MallumManager calls VaseManager completion and frees the Mallum.

## UI

### Floating Quest Button (QuestButtonUI)

- Positioned bottom-left of campsite, above safe area
- Circular button with quest/compass icon
- Badge overlay shows count of completed quests ready to collect (hidden when 0)
- Tap opens Quest overlay panel

### Quest Overlay Panel (QuestUI)

Slide-up overlay (same pattern as Apotheke/Craft). Three sections in ScrollView:

1. **Active** — cards showing Mallum activity (quest name or "Fetching Water"), progress bar, time remaining
2. **Completed** — cards with quest name, reward preview, "Collect" button. Collect adds seeds to Apotheke, returns Mallum to Idle
3. **Available Quests** — cards with quest name, description, duration, reward pool preview (seed icons), "Send Mallum" button. Disabled if no idle Mallums. Higher flame level quests shown locked/grayed

### Vase Interaction Changes

- "Send Mallum" button shows "(X available)" count from MallumManager
- Disabled with "No Mallums available" when none idle
- Active water-fetch Mallums also appear in Quest panel Active section

## Integration Points

| Existing System | Change |
|----------------|--------|
| `SaveData` | Add `List<MallumSave> mallums` |
| `VaseManager` | `SendToCollect()` routes through MallumManager |
| `CampsiteViewUI` | Vase "Send Mallum" checks MallumManager availability |
| `CampFireUI` | Adds floating quest button + quest overlay panel |
| `GameManager` | Init mallums for new players based on flame level |

## New Files

| File | Type |
|------|------|
| `Scripts/Data/QuestData.cs` | ScriptableObject |
| `Scripts/Data/MallumConfig.cs` | ScriptableObject |
| `Scripts/Data/MallumSave.cs` | Serializable data classes |
| `Scripts/Managers/MallumManager.cs` | Singleton manager |
| `Scripts/UI/QuestUI.cs` | Overlay panel controller |
| `Scripts/UI/QuestButtonUI.cs` | Floating button controller |
| `Resources/Quests/{SwampForage,MeadowExpedition,DeepWoodsTrek}.asset` | Quest assets |
| `Resources/Config/MallumConfig.asset` | Config asset |
| `Resources/UI/Templates/QuestCard.uxml` | Quest card template |
| `UI/Styles/Quest.uss` | Quest panel styles |
| `UI/Documents/CampFireRoot.uxml` | Updated with quest button + panel |
