# Codex Discovery Rewards

## Summary

One-time gold reward for each variant discovered. Players must visit the Codex and tap the variant entry to claim the reward via a "Claim Reward" button in the detail panel. Unclaimed entries pulse with a rarity-colored glow border.

## Data Layer

- `SaveData` gets `public List<string> claimedDiscoveryRewards = new();`
- `CurrencyConfig` gets per-rarity discovery reward fields at 2x harvest values:
  - Common: 20, Uncommon: 50, Rare: 100, Epic: 200, Legendary: 500
- A variant is "unclaimed" when it's in `discoveredVariants` but NOT in `claimedDiscoveryRewards`

## Grid Visual State

- New CSS class `variant-entry-unclaimed` with pulsing border glow animation (rarity-colored, ~2s ease-in-out infinite)
- Applied in `CodexUI.RefreshCodex()` based on claim state
- Removed on claim without full refresh (toggle class on the entry element)

## Detail Panel Changes

- Add "Claim Reward" button to detail panel UXML (below description, above seed name)
- Button shows gold amount text (e.g. "Claim 100 Gold")
- Visible only for unclaimed discovered variants; hidden otherwise
- On click: grant gold via `CurrencyManager.Add()`, add variant to `claimedDiscoveryRewards`, save, update UI

## Claim Flow

1. Player discovers variant via harvest (existing popup)
2. Player opens Codex — unclaimed entry pulses with glow border
3. Player taps entry — detail panel shows "Claim Reward" button with gold amount
4. Player taps "Claim Reward" — gold granted, button disappears, pulse removed from grid entry
