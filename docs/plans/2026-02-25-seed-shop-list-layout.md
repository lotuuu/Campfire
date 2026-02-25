# Seed Shop List Layout + Price Sort Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Sort seeds by buyPrice ascending and display them in a single-column vertical list instead of the current 2-column grid.

**Architecture:** Two targeted edits to `SeedShopUI.RefreshDisplay()` — sort the seed list before iteration, remove the `flexBasis` percent assignment, and set column layout on the scroll view container.

**Tech Stack:** Unity 6, UI Toolkit (UIElements), C#

---

### Task 1: Sort seeds and switch to list layout

**Files:**
- Modify: `Assets/Scripts/UI/SeedShopUI.cs:24-65`

**Step 1: Sort seeds by buyPrice ascending**

In `RefreshDisplay()`, after line 28 (`var seeds = ...`), add:

```csharp
seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));
```

**Step 2: Set scroll view content to column layout**

After the sort line, add:

```csharp
shopGrid.contentContainer.style.flexDirection = FlexDirection.Column;
```

**Step 3: Remove the flexBasis percent assignment**

Delete line 33:
```csharp
card.style.flexBasis = new StyleLength(new Length(45, LengthUnit.Percent));
```

The final `RefreshDisplay()` should look like:

```csharp
private void RefreshDisplay()
{
    shopGrid.Clear();

    var seeds = SeedShopManager.Instance.GetShopSeeds();
    seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));
    shopGrid.contentContainer.style.flexDirection = FlexDirection.Column;

    foreach (var seed in seeds)
    {
        var card = shopCardTemplate.CloneTree();
        card.style.flexGrow = 1;
        card.style.flexShrink = 0;

        var nameLabel = card.Q<Label>(className: "shop-seed-name");
        var priceLabel = card.Q<Label>(className: "shop-price");
        var conditionLabel = card.Q<Label>(className: "shop-condition");
        var icon = card.Q<VisualElement>(className: "shop-icon");
        var buyBtn = card.Q<Button>(className: "shop-buy-btn");

        int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
        if (nameLabel != null) nameLabel.text = $"{seed.seedName} (x{owned})";
        if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
        if (conditionLabel != null) conditionLabel.text = seed.description ?? "";
        if (icon != null && seed.icon != null)
            icon.style.backgroundImage = new StyleBackground(seed.icon);

        if (buyBtn != null)
        {
            bool canBuy = SeedShopManager.Instance.CanBuy(seed.seedName);
            buyBtn.SetEnabled(canBuy);
            buyBtn.text = $"Buy ({seed.buyPrice} Dust)";

            var seedName = seed.seedName;
            buyBtn.clicked += () =>
            {
                if (SeedShopManager.Instance.BuySeed(seedName))
                    RefreshDisplay();
            };
        }

        shopGrid.Add(card);
    }
}
```

**Step 4: Verify in Unity**

Open Unity, navigate to the Shop page. Confirm:
- Seeds appear in a single column (no wrapping into two columns)
- Order is: Quicksprout (0) → Dashbloom (50) → Astra (150) → Cinder-Fern (500) → Mist-Vine (1200) → Luna-Petal (3000) → Storm-Root (8000)

**Step 5: Commit**

```bash
git add Assets/Scripts/UI/SeedShopUI.cs
git commit -m "feat: sort seeds by price asc and switch shop to list layout"
```
