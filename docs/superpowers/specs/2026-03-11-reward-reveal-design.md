# Reward Reveal Screen — Design Spec

## Overview

A generic full-screen reward reveal overlay that displays earned items with fanfare. Used by quests, visitor gifts, bird drops, and visitor quests — any system that grants items to the player.

## Visual Design

### Layout
- Full-screen dark overlay (`rgba(0,0,0,0.85)`), same as dialogue overlay
- Title label at top center (e.g. "Quest Complete!", "A bird dropped something!")
- Reward cards arranged horizontally in the center
- "Collect" button at bottom center

### Reward Cards
- Cards ~150x200px, rounded corners, dark inner background (`rgba(20, 14, 8, 0.95)`)
- Seed sprite centered in card, loaded via `SpriteService.GetTexture("items/{seed}/seed")`
- Seed display name below sprite
- Count badge in top-right corner if count > 1
- Tier-based border glow:
  - Tier 0: warm white (`rgba(255, 230, 170, 0.8)`)
  - Tier 1: green (`rgba(80, 190, 100, 0.8)`)
  - Tier 2: blue (`rgba(80, 150, 220, 0.8)`)
  - Tier 3: purple (`rgba(170, 100, 220, 0.8)`)
  - Tier 4+: gold (`rgba(255, 200, 60, 0.9)`)

### Animation
- Overlay fades in via opacity transition (~200ms)
- Cards scale up from 0 → 1 with stagger (~100ms delay per card)
- Brief bright glow/flash behind each card on appear (scale 1.2 → 1.0 on the glow element)

## API

```csharp
public class RewardRevealUI : MonoBehaviour
{
    public static RewardRevealUI Instance { get; private set; }

    public void Show(string title, List<RewardEntry> rewards, Action onCollect);
    public void Hide();
}
```

- `title`: display text at top ("Quest Complete!", "Gift Received!", etc.)
- `rewards`: list of `RewardEntry` (seedName + count)
- `onCollect`: callback invoked when player taps "Collect" — caller handles actual inventory changes

## Integration Points

### Quest Completion (QuestUI)
- **Speed-up flow**: `SpeedUpQuest()` → show reveal with `mallum.pendingRewards` → onCollect calls `CollectQuestRewards()`
- **Natural completion**: replace inline reward chips + "Collect Rewards" button → show reveal instead → onCollect calls `CollectQuestRewards()`

### Bird Drops (BirdManager)
- After bird drop event → show reveal with dropped seeds → onCollect callback (seeds already added)

### Visitor Gifts (VisitorUI)
- After accepting gift → show reveal with gift items → onCollect callback

### Visitor Quests
- After turning in visitor quest → show reveal with quest rewards → onCollect callback

## File Structure

| File | Purpose |
|------|---------|
| `Assets/Scripts/UI/RewardRevealUI.cs` | Controller MonoBehaviour |
| `Assets/UI/Styles/RewardReveal.uss` | All styling |
| `Assets/UI/Documents/CampFireRoot.uxml` | UXML elements (after tutorial hint bar, before dialogue overlay) |

Cards are built dynamically in C# — no separate UXML template needed.

## Initialization

- `RewardRevealUI` is a MonoBehaviour on the `--- UI ---` GameObject (same as all other UI controllers)
- Initialized via `Initialize(VisualElement root)` in `CampFireUI`
- Singleton with duplicate-destroy guard in `Awake()`

## Tier Resolution

Seed tier is looked up via `ConfigService.Instance.GetSeed(seedName)?.tier ?? 0` to determine card border color.

## Sound

- Play `ui_panel_open` SFX on show
- Play a reward-specific SFX on card reveal (if available, else skip)
- Play `ui_panel_close` SFX on collect/dismiss
