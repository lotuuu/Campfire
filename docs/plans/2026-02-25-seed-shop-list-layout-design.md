# Seed Shop: List Layout + Price Sort

## Summary

Replace the 2-column grid layout in the seed shop with a single-column vertical list, and sort seeds by `buyPrice` ascending (cheapest first).

## Changes

**`Assets/Scripts/UI/SeedShopUI.cs`** — two edits in `RefreshDisplay()`:

1. Sort the seed list after fetching: `seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice))`
2. Set scroll view content to column layout: `scrollView.contentContainer.style.flexDirection = FlexDirection.Column`
3. Remove the `flexBasis = 45%` assignment on each card element

## Result

Seeds displayed cheapest-first in a single scrollable column:
- Quicksprout (0 Dust)
- Dashbloom (50 Dust)
- Astra (150 Dust)
- Cinder-Fern (500 Dust)
- Mist-Vine (1200 Dust)
- Luna-Petal (3000 Dust)
- Storm-Root (8000 Dust)
