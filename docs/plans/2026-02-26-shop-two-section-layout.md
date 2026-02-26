# Shop Two-Section Layout Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split the Shop tab's single scroll list into two visually distinct sections — Seeds (AuraDust) and Consumables (SunShards) — using category banners and colored left-border accents on cards, within one continuous scroll view.

**Architecture:** Pure UI change. Add two currency color variables to `Variables.uss`, add section banner + card accent styles to `SeedShop.uss`, and update `SeedShopUI.cs` to emit banners and apply modifier classes. No new templates, no data model changes, no tests required (purely visual).

**Tech Stack:** Unity 6 UI Toolkit (USS + C#), `VisualElement` built in code

---

### Task 1: Add currency color variables

**Files:**
- Modify: `Assets/UI/Styles/Variables.uss`

**Step 1: Add two custom properties inside `:root`**

Open `Assets/UI/Styles/Variables.uss`. After the `--color-highlight` line (line 26), insert:

```css
    --color-aura-dust: rgb(80, 200, 160);
    --color-sun-shards: rgb(255, 185, 80);
```

The block should read:
```css
    --color-highlight: rgb(255, 220, 80);
    --color-aura-dust: rgb(80, 200, 160);
    --color-sun-shards: rgb(255, 185, 80);
    --color-dim: rgb(100, 130, 150);
```

**Step 2: Verify**

No test runner needed. Confirm the file has no syntax errors (missing semicolons, unclosed braces). USS custom properties follow standard CSS syntax.

**Step 3: Commit**

```bash
git add Assets/UI/Styles/Variables.uss
git commit -m "style: add aura-dust and sun-shards color variables"
```

---

### Task 2: Add section banner and card accent styles

**Files:**
- Modify: `Assets/UI/Styles/SeedShop.uss`

**Step 1: Replace the existing `.shop-section-header` block and add new rules**

The current `SeedShop.uss` ends with a `.shop-section-header` rule (lines 44–51). Replace that entire rule and append all new rules so the file ends with:

```css
.shop-section-banner {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    background-color: rgba(30, 50, 65, 0.60);
    padding: var(--spacing-sm) var(--spacing-md);
    margin-bottom: var(--spacing-xs);
    border-radius: var(--radius-sm);
}

.shop-section-banner-title {
    font-size: var(--font-md);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

.shop-currency-badge {
    font-size: var(--font-xs);
    background-color: rgba(20, 35, 50, 0.60);
    border-radius: var(--radius-sm);
    padding-left: var(--spacing-sm);
    padding-right: var(--spacing-sm);
    padding-top: 6px;
    padding-bottom: 6px;
}

.shop-currency-badge--aura-dust {
    color: var(--color-aura-dust);
}

.shop-currency-badge--sun-shards {
    color: var(--color-sun-shards);
}

.shop-card--seeds .shop-card {
    border-left-width: 4px;
    border-left-color: var(--color-aura-dust);
}

.shop-card--seeds .shop-price {
    color: var(--color-aura-dust);
}

.shop-card--consumables .shop-card {
    border-left-width: 4px;
    border-left-color: var(--color-sun-shards);
}

.shop-card--consumables .shop-price {
    color: var(--color-sun-shards);
}
```

Note: `.shop-card--seeds` and `.shop-card--consumables` are applied to the `TemplateContainer` (the root returned by `CloneTree()`). Descendant selectors reach inside the template to the `.shop-card` and `.shop-price` elements.

**Step 2: Verify**

Check the file has no unclosed braces and all `var()` references match names added in Task 1.

**Step 3: Commit**

```bash
git add Assets/UI/Styles/SeedShop.uss
git commit -m "style: shop section banners and card accent styles"
```

---

### Task 3: Update SeedShopUI.cs

**Files:**
- Modify: `Assets/Scripts/UI/SeedShopUI.cs`

**Background:** The current file has `AddSeedSection()` (no header) and `AddConsumableSection()` (emits a plain `Label("Consumables")`). We will:
1. Add a private `MakeSectionBanner()` helper
2. Emit a Seeds banner at the top of `AddSeedSection()`, add `shop-card--seeds` class to each seed card
3. Replace the plain label in `AddConsumableSection()` with a Consumables banner, add `shop-card--consumables` class to each consumable card

**Step 1: Add the `MakeSectionBanner` helper method**

Add this private method inside the `SeedShopUI` class, before `AddSeedSection`:

```csharp
private VisualElement MakeSectionBanner(string title, string currencyLabel, string badgeClass)
{
    var banner = new VisualElement();
    banner.AddToClassList("shop-section-banner");

    var titleLabel = new Label(title);
    titleLabel.AddToClassList("shop-section-banner-title");

    var badge = new Label(currencyLabel);
    badge.AddToClassList("shop-currency-badge");
    badge.AddToClassList(badgeClass);

    banner.Add(titleLabel);
    banner.Add(badge);
    return banner;
}
```

**Step 2: Update `AddSeedSection()`**

Replace the current `AddSeedSection()` body. Add the banner as the first element, then apply the `shop-card--seeds` class to each cloned card:

```csharp
private void AddSeedSection()
{
    shopGrid.Add(MakeSectionBanner("Seeds", "AuraDust", "shop-currency-badge--aura-dust"));

    var seeds = SeedShopManager.Instance.GetShopSeeds();
    seeds.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

    foreach (var seed in seeds)
    {
        var card = shopCardTemplate.CloneTree();
        card.AddToClassList("shop-card--seeds");
        card.style.flexGrow = 1;
        card.style.flexShrink = 0;

        var nameLabel  = card.Q<Label>(className: "shop-seed-name");
        var priceLabel = card.Q<Label>(className: "shop-price");
        var condLabel  = card.Q<Label>(className: "shop-condition");
        var icon       = card.Q<VisualElement>(className: "shop-icon");
        var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

        int owned = SeedRegistry.Instance.GetSeedCount(seed.seedName);
        if (nameLabel  != null) nameLabel.text  = $"{seed.seedName} (x{owned})";
        if (priceLabel != null) priceLabel.text = $"{seed.buyPrice} Dust";
        if (condLabel  != null) condLabel.text  = seed.description ?? "";
        if (icon != null && seed.icon != null)
            icon.style.backgroundImage = new StyleBackground(seed.icon);

        if (buyBtn != null)
        {
            buyBtn.SetEnabled(SeedShopManager.Instance.CanBuy(seed.seedName));
            buyBtn.text = $"Buy ({seed.buyPrice} Dust)";
            var seedName = seed.seedName;
            buyBtn.clicked += () => { if (SeedShopManager.Instance.BuySeed(seedName)) RefreshDisplay(); };
        }
        shopGrid.Add(card);
    }
}
```

**Step 3: Update `AddConsumableSection()`**

Replace the current `AddConsumableSection()` body. Replace the plain `Label("Consumables")` with the banner (with top margin), and apply `shop-card--consumables` to each card:

```csharp
private void AddConsumableSection()
{
    if (ConsumableManager.Instance == null) return;

    var banner = MakeSectionBanner("Consumables", "SunShards", "shop-currency-badge--sun-shards");
    banner.style.marginTop = 32;
    shopGrid.Add(banner);

    var consumables = new System.Collections.Generic.List<ConsumableData>(
        ConsumableManager.Instance.AllConsumables);
    consumables.Sort((a, b) => a.buyPrice.CompareTo(b.buyPrice));

    foreach (var c in consumables)
    {
        var card = shopCardTemplate.CloneTree();
        card.AddToClassList("shop-card--consumables");
        card.style.flexGrow = 1;
        card.style.flexShrink = 0;

        var nameLabel  = card.Q<Label>(className: "shop-seed-name");
        var priceLabel = card.Q<Label>(className: "shop-price");
        var condLabel  = card.Q<Label>(className: "shop-condition");
        var icon       = card.Q<VisualElement>(className: "shop-icon");
        var buyBtn     = card.Q<Button>(className: "shop-buy-btn");

        int owned = ConsumableManager.Instance.GetCount(c.type);
        if (nameLabel  != null) nameLabel.text  = $"{c.displayName} (x{owned})";
        if (priceLabel != null) priceLabel.text = $"{c.buyPrice} {c.currency}";
        if (condLabel  != null) condLabel.text  = c.description ?? "";
        if (icon != null && c.icon != null)
            icon.style.backgroundImage = new StyleBackground(c.icon);

        if (buyBtn != null)
        {
            buyBtn.SetEnabled(ConsumableManager.Instance.CanBuy(c));
            buyBtn.text = $"Buy ({c.buyPrice} {c.currency})";
            var consumable = c;
            buyBtn.clicked += () => { if (ConsumableManager.Instance.Buy(consumable)) RefreshDisplay(); };
        }
        shopGrid.Add(card);
    }
}
```

**Step 4: Verify compilation**

In Unity Editor, check the Console window for compiler errors after saving. The only new APIs used are `VisualElement`, `Label`, `AddToClassList` — all standard UI Toolkit. No new `using` statements needed.

**Step 5: Commit**

```bash
git add Assets/Scripts/UI/SeedShopUI.cs
git commit -m "feat: shop two-section layout with category banners and card accents"
```

---

### Task 4: Visual verification in Unity Editor

**Step 1: Open the Shop tab in Play Mode**

Enter Play Mode in Unity. Navigate to the Shop tab (page index 1). Confirm:
- A teal/green "Seeds — AuraDust" banner appears at the top
- Seed cards have a teal left border and teal price label
- An amber "Consumables — SunShards" banner appears below the seed cards, with extra top spacing
- Consumable cards have an amber left border and amber price label
- The single scroll flows through both sections without pagination

**Step 2: Confirm buy buttons still work**

Tap a Buy button for a seed (if you have AuraDust). Confirm the card refreshes (owned count increments). Tap a consumable Buy button. Confirm it decrements SunShards and owned count updates.

**Step 3: Final commit (if any tweaks were made)**

```bash
git add -p
git commit -m "fix: shop section layout tweaks"
```
