# Visitor System Redesign

**Date:** 2026-03-04
**Status:** Approved

## Overview

Replace the current silent `VisitorSystem` and separate `MerchantManager` with a unified, server-driven `VisitorManager`. The server decides which visitor appears each night, enabling personalized milestones, weather-triggered events, and admin-scheduled special visitors.

The game already requires internet connectivity (weather gate), so this is designed as always-online with no offline fallback.

## Visitor Types

| Type | Behavior | Player Interaction |
|------|----------|--------------------|
| **Merchant** | Shows trade offers (items for seeds) | Tap → dialogue → trade panel |
| **Gifter** | Gives a free gift (seeds, water, or items) | Tap → dialogue → gift applied + confirmation |
| **Quester** | Requests an item, returns days later with reward | Tap → dialogue → accept quest. On return: tap → turn-in items → receive reward |

### Quester Details

- Quester stays on grid after initial interaction (player can tap again to review quest)
- On return date, server sends the same quester back as that night's visitor
- Player taps to turn in items actively (not auto-collected)
- If player doesn't have items, visitor stays on grid until night ends — dialogue reminds what's needed
- If return deadline passes without turn-in, quest expires and quester leaves. No fail dialogue.

## Server Architecture

### Rule-Based Visitor Selection

Server evaluates rules top-down by priority:

1. **Scheduled events** (highest) — admin-inserted: `{date, visitor_id}`. Everyone gets this visitor.
2. **Account milestones** — player's `visit_count`: e.g. visit #1 → "Wanderer_Arlo", visit #7 → "Wanderer_Arlo" (different dialogue/gifts).
3. **Weather triggers** — full moon, storm, etc. mapped to specific visitors.
4. **Active quest return** — if player has a quest whose return date is today, that quester comes back.
5. **Random pool** (fallback) — weighted random from visitors appropriate for player's flame level.

### Database Schema

```sql
visitor_templates:
  id, visitor_id (unique string), name, portrait_id,
  type (merchant/gifter/quester), flame_level_min,
  dialogue_pool (JSON), offer_pool (JSON), gift_pool (JSON), quest_pool (JSON),
  weight (for random selection)

visitor_schedule:
  id, date (nullable), visit_number (nullable), weather_condition (nullable),
  visitor_id (FK), priority (higher = checked first)

visitor_quests:
  id, player_id, visitor_id, request_item, request_count,
  return_date_utc, reward (JSON), return_dialogue (JSON),
  created_at

player_visit_counts:
  player_id (unique), count
```

### API Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/visitors/tonight` | Returns tonight's visitor for authenticated player. Evaluates rules, increments visit count (deduplicated by date), returns full payload. |
| `POST` | `/visitors/quest/accept` | Client confirms quest accepted. Server creates `visitor_quests` row. |
| `POST` | `/visitors/quest/complete` | Client confirms turn-in. Server validates active quest + return date, deletes quest row, returns reward confirmation. |

### Visitor Payload (from `GET /visitors/tonight`)

```json
{
  "visitor_type": "quester",
  "visitor_id": "wanderer_arlo",
  "name": "Arlo the Wanderer",
  "portrait_id": "arlo",
  "dialogue": ["I've traveled far...", "If you could find me 3 Lavender..."],
  "quest": {
    "request_item": "Lavender",
    "request_count": 3,
    "return_days": 7,
    "reward": { "type": "seed", "name": "Moonflower", "count": 2 },
    "return_dialogue": ["You found them!", "Here, take these rare seeds."]
  }
}
```

Merchant and gifter payloads follow the same structure with `offers[]` or `gift` fields instead of `quest`.

## Client Architecture

### Unified VisitorManager

Replaces both `VisitorSystem` and `MerchantManager`. Single manager handles all visitor types.

**Flow:**
1. Night begins (10 PM local) → calls `GET /visitors/tonight`
2. Spawns visitor on a free hex tile
3. Stores state in `SaveData.currentVisitor` (a `VisitorSave`)
4. Player taps → DialogueUI → type-specific interaction
5. Night ends → visitor departs (dismissed from grid)

### VisitorSave (replaces MerchantSave + lastVisitorDateUtc)

```
VisitorSave:
  gridX, gridY
  visitorId, name, portraitId
  type (merchant/gifter/quester)
  dialogueLines[], dialogueSeen
  appearedAtUtc
  # merchant-specific:
  offers[] (list of {costs[], rewardSeedName, rewardCount})
  # gifter-specific:
  giftType, giftName, giftAmount, giftClaimed
  # quester-specific:
  requestItem, requestCount, returnDateUtc
  reward (type, name, count), returnDialogue[]
  isReturnVisit, questFulfilled
```

### ActiveVisitorQuest (persistent across nights)

```
ActiveVisitorQuest:
  visitorId, requestItem, requestCount
  returnDateUtc, reward, returnDialogue[]
```

Stored in `SaveData.activeQuests`. When a quest is accepted, added here. Server uses this to send the quester back on return date. Removed on turn-in or expiry.

### UI Flow Per Type

- **All types**: tap grid cell → DialogueUI with portrait → then:
- **Merchant**: overlay opens with trade offer list (reuse existing MerchantUI pattern)
- **Gifter**: gift applied to inventory, confirmation message shown
- **Quester (initial visit)**: quest details shown, player accepts → quest saved to `activeQuests`, server notified via `POST /visitors/quest/accept`
- **Quester (return visit)**: if player has items → turn-in button, items consumed, reward given, server notified via `POST /visitors/quest/complete`. If not → dialogue reminds what's needed, visitor stays.

### CampsiteViewUI Changes

- Replace `CampBuildingType.NightMerchant` with `CampBuildingType.Visitor`
- Render visitor cell with name + type indicator
- Tap handler routes to `VisitorManager` instead of `MerchantManager`

## Migration & Cleanup

### Removed
- `VisitorSystem.cs`
- `MerchantManager.cs`
- `MerchantData.cs` (ScriptableObject — definitions move to server)
- `MerchantSave.cs`
- `SaveData.lastVisitorDateUtc`, `SaveData.merchants`, `SaveData.lastMerchantDateUtc`, `SaveData.seenMerchantDialogues`
- Merchant ScriptableObject assets in `Resources/Merchants/`

### Reused
- `MerchantUI.cs` — refactored for merchant-type visitors within unified system
- `DialogueUI.cs` — unchanged
- `CampsiteViewUI` grid rendering — swap building type
- `TradeCost` struct

### Tests
Replace `TestVisitorSystem.cs` and `TestMerchantManager.cs` with `TestVisitorManager.cs`:
- Visitor spawning from server payload
- Gift application (gifter)
- Trade execution (merchant)
- Quest accept / return / expiry flow
- Departure on night end

## Future: Server-Authoritative Migration

This visitor system is the first step toward broader server authority. The pattern established here (server provides game data, client renders and confirms actions, server validates) can extend to:
- Save validation (server checks economy transactions)
- Server-side quest/progression tracking
- Anti-cheat for trades and rewards
