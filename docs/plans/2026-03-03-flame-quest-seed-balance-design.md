# Flame-Quest-Seed Progression Balance Design

## Overview

Rebalance the flame upgrade system from mana-based to harvest-based. Flame upgrades consume harvested yields (not mana, not seeds). This creates an interlocking resource loop:

```
Plant Seeds → Grow & Harvest → Burn Harvests to Level Flame → Unlock New Quests
                                                                     ↓
         ← New Seeds from Quests ← Send Mallum on Quest ←──────────┘
```

Mana remains as the infrastructure currency (crafting plots, vases, buying seeds from shop).

## Target Progression Timeline

| Level | Cumulative days | Delta |
|-------|----------------|-------|
| 1→2 | 0 | ~hours |
| 2→3 | 0 | ~hours |
| 3 | 0 | — |
| 4 | 2 | ~2 days |
| 5 | 3 | ~1 day |
| 6 | 6 | ~3 days |
| 7 | 11 | ~5 days |
| 8 | 20 | ~9 days |
| 9 | 30 | ~10 days |
| 10 | 45 | ~15 days |

## Seed Tiers

Seeds are grouped by the quest that provides them:

| Tier | Seeds | Quest that provides them |
|------|-------|------------------------|
| T1 | Basil (1h, 1 drop), Chamomile (1.5h, 2 drops) | Swamp Forage (Lvl 1) |
| T2 | Marigold (2h, 2 drops), Snowdrop (2.5h, 2 drops) | Meadow Expedition (Lvl 2) |
| T3 | Mint (3h, 3 drops), Pansy (6h, 3 drops) | Forest Trail (Lvl 3) |
| T4 | Lavender (5h, 3 drops) | Highland Pass (Lvl 4) |
| T5 | Poppy (8h, 4 drops) | Deep Marsh (Lvl 5) |
| T6 | Jasmine (12h, 4 drops) | Mountain Ascent (Lvl 6) |
| T7 | Rosemary (18h, 5 drops) | Moonlit Path (Lvl 7) |
| T8 | Dahlia (30h, 6 drops), Moonflower (48h, 8 drops, moon phase) | Ancient Grove (Lvl 8) |

## Effort Per Harvest Item

| Seed | Growth time | Drops | Hours per item | Relative effort |
|------|------------|-------|---------------|-----------------|
| Basil | 1h | 1 | 1.0h | 1x |
| Chamomile | 1.5h | 2 | 0.75h | 0.75x |
| Marigold | 2h | 2 | 1.0h | 1x |
| Snowdrop | 2.5h | 2 | 1.25h | 1.25x |
| Mint | 3h | 3 | 1.0h | 1x |
| Pansy | 6h | 3 | 2.0h | 2x |
| Lavender | 5h | 3 | 1.67h | 1.7x |
| Poppy | 8h | 4 | 2.0h | 2x |
| Jasmine | 12h | 4 | 3.0h | 3x |
| Rosemary | 18h | 5 | 3.6h | 3.6x |
| Dahlia | 30h | 6 | 5.0h | 5x |
| Moonflower | 48h | 8 | 6.0h + moon | 8x |

## Effort Budget Derivation

Target effort = delta_days x usable_plot_hours/day x ~30% utilization (accounting for sleep, quest time to acquire seeds, growing for other purposes).

| Upgrade | Delta days | Plot-hrs/day | ~30% util | Target effort |
|---------|-----------|-------------|-----------|---------------|
| 1→2 | 0 | — | — | ~3h |
| 2→3 | 0 | — | — | ~4h |
| 3→4 | 2 | 48 | 14/day | ~30h |
| 4→5 | 1 | 72 | 22/day | ~22h |
| 5→6 | 3 | 96 | 29/day | ~85h |
| 6→7 | 5 | 120 | 36/day | ~180h |
| 7→8 | 9 | 144 | 43/day | ~390h |
| 8→9 | 10 | 168 | 50/day | ~500h |
| 9→10 | 15 | 192 | 58/day | ~860h |

## Flame Upgrade Recipes

Each recipe quantity derived from: effort_allocation / hours_per_item.

| Upgrade | Budget | Recipe | Effort | Tiers |
|---------|--------|--------|--------|-------|
| 1→2 | 3h | 3 Basil | 3h | T1 |
| 2→3 | 4h | 5 Chamomile | 3.75h | T1 |
| 3→4 | 30h | 12 Marigold + 8 Snowdrop + 8 Basil | 30h | T2, T2, T1 |
| 4→5 | 22h | 8 Mint + 4 Pansy + 8 Chamomile | 22h | T3, T3, T1 |
| 5→6 | 85h | 22 Lavender + 24 Snowdrop + 18 Basil | 84.7h | T4, T2, T1 |
| 6→7 | 180h | 35 Poppy + 30 Pansy + 50 Marigold | 180h | T5, T3, T2 |
| 7→8 | 390h | 60 Jasmine + 50 Lavender + 60 Poppy | 384h | T6, T4, T5 |
| 8→9 | 500h | 50 Rosemary + 60 Jasmine + 55 Lavender + 40 Snowdrop | 502h | T7, T6, T4, T2 |
| 9→10 | 860h | 50 Dahlia + 30 Moonflower + 60 Rosemary + 80 Poppy + 50 Basil | 856h | T8, T8, T7, T5, T1 |

### Reach-back pattern

- 3→4: reaches to Basil (T1)
- 4→5: reaches to Chamomile (different T1 seed)
- 5→6: reaches to Snowdrop (T2) + Basil (T1)
- 6→7: reaches to Pansy (T3) + Marigold (T2)
- 7→8: reaches to Lavender (T4) + Poppy (T5)
- 8→9: reaches to Jasmine (T6) + Lavender (T4) + Snowdrop (T2)
- 9→10: reaches to Rosemary (T7) + Poppy (T5) + Basil (T1, bookend)

No two consecutive recipes share the same reach-back ingredients.

### Seeds never used as flame fuel

Mint (only in 4→5) and Chamomile (only in 4→5) exit the flame progression early. They remain valuable for Apotheke recipes/crafting.

## Quest Table (expanded to 8 quests)

| Quest | Flame Req | Duration | Rolls | Reward Pool |
|-------|-----------|----------|-------|-------------|
| Swamp Forage | 1 | 30m | 2 | Basil (w3, 1-2), Chamomile (w2, 1-2) |
| Meadow Expedition | 2 | 2h | 3 | Marigold (w3, 1-2), Snowdrop (w2, 1-2) |
| Forest Trail | 3 | 4h | 3 | Mint (w3, 1-2), Pansy (w2, 1-1) |
| Highland Pass | 4 | 6h | 3 | Lavender (w3, 1-2), Marigold (w1, 1-1) |
| Deep Marsh | 5 | 8h | 4 | Poppy (w3, 1-2), Mint (w1, 1-1) |
| Mountain Ascent | 6 | 12h | 4 | Jasmine (w3, 1-2), Lavender (w1, 1-1) |
| Moonlit Path | 7 | 16h | 4 | Rosemary (w3, 1-2), Pansy (w1, 1-1) |
| Ancient Grove | 8 | 20h | 5 | Dahlia (w3, 1-2), Moonflower (w1, 1-1), Rosemary (w1, 1-1) |

Seeds become available one level before they're needed in recipes.

## Entity Scaling (10 levels)

Flame level controls entity cap and grid size only. Mallum cap is separate (TBD).

| Level | Entities | Grid |
|-------|----------|------|
| 1 | 3 | 2 |
| 2 | 5 | 2 |
| 3 | 8 | 3 |
| 4 | 12 | 3 |
| 5 | 15 | 3 |
| 6 | 18 | 4 |
| 7 | 22 | 4 |
| 8 | 26 | 4 |
| 9 | 30 | 5 |
| 10 | 35 | 5 |

Mana generation: `baseManaPerSecond + (level - 1) * manaPerLevel` (unchanged formula, values TBD for 10 levels).

## Data Model Changes

### FlameConfig (ScriptableObject)

- **Remove:** `upgradeCosts` (float array — mana costs)
- **Add:** `upgradeRecipes` — serialized list of `FlameUpgradeRecipe`, each containing `List<FlameIngredient>` where `FlameIngredient` has `string itemName` and `int count`
- **Expand:** `maxEntitiesPerLevel` and `gridSizePerLevel` to 10 entries
- **Keep:** `baseManaPerSecond`, `manaPerLevel` (mana still used for crafting)

### FlameManager

- `CanUpgrade()` → checks `SaveData.items` for required harvest items instead of mana
- `UpgradeFlame()` → consumes harvest items from inventory instead of spending mana
- New: `GetUpgradeRecipe(int level)` returns ingredient list for display in UI

### New/Modified Assets

- **5 new quest assets:** Highland Pass, Deep Marsh, Mountain Ascent, Moonlit Path, Ancient Grove
- **3 updated quest assets:** SwampForage, MeadowExpedition, DeepWoodsTrek (update reward pools to match new seed names)
- **FlameConfig.asset:** Rewrite with 10-level arrays and upgrade recipes
- **MallumConfig:** No changes (Mallum scaling is separate)
