# Shop Tab — Two-Section Layout with Category Banners

**Date:** 2026-02-26
**Status:** Approved

## Problem

The Shop tab renders seeds and consumables in a single undifferentiated scroll list using the same card template. The two item types are fundamentally different — seeds cost AuraDust, consumables cost SunShards — and the only visual separator is a small plain "Consumables" label. Players cannot tell at a glance which currency they are spending or where one category ends and the other begins.

## Chosen Approach: Category Banners + Accent Colors

One scroll view, two clearly delineated sections. No new templates required.

## Design

### Section Headers

Each section opens with a full-width banner row:

- **Left**: small currency icon (existing AuraDust / SunShards sprites)
- **Center-left**: section name ("Seeds" / "Consumables") in bold
- **Right**: currency name as a pill badge ("AuraDust" / "SunShards")

The banner has a subtly lighter background than the panel so it reads as a shelf divider. Always expanded — no collapse toggle.

### Card Accents

No change to card layout or template. Each card receives a 3–4px colored left border via a modifier USS class:

- `.shop-card--seeds`: left border in `--color-aura-dust` (muted teal/green)
- `.shop-card--consumables`: left border in `--color-sun-shards` (amber/gold)

The price label on each card also renders in the section's accent color — "120 Dust" in teal, "80 Shards" in amber — providing a redundant currency signal mid-scroll.

### Scroll Structure

Single `ScrollView` (`shop-grid`), populated in order:

1. Seeds banner
2. Seed cards (sorted by price ascending, infinite seeds excluded)
3. Consumables banner
4. Consumable cards (sorted by price ascending)

No structural change to the scroll container or card template.

## Files Affected

| File | Change |
|------|--------|
| `Assets/Scripts/UI/SeedShopUI.cs` | Replace `AddSeedSection` / `AddConsumableSection` to emit banners; add modifier classes to cards |
| `Assets/UI/Styles/SeedShop.uss` | Add `.shop-section-banner`, `.shop-currency-badge`, `.shop-card--seeds`, `.shop-card--consumables` |
| `Assets/UI/Styles/Variables.uss` | Add `--color-aura-dust` and `--color-sun-shards` custom properties if not already present |

## Non-Goals

- No collapse/expand behavior
- No horizontal scrolling rows
- No new UXML templates
- No changes to card layout or data model
