# Satchel Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the grid + popup + Plant-button Satchel with a vertical list of horizontal seed cards that plant on tap, inside a bottom sheet that animates in and dismisses via swipe-down.

**Architecture:** Five files change — UXML template, USS stylesheet, and three C# scripts. No new files needed. No business logic changes; `PlantManager.Plant()` is called directly on tap instead of requiring a selection + button press.

**Tech Stack:** Unity 6 UI Toolkit (UXML, USS, `VisualElement` C# API), `PointerDownEvent / PointerMoveEvent / PointerUpEvent` for swipe gesture, `IVisualElementScheduler` for one-frame-delayed animation trigger.

---

### Task 1: Rewrite `SeedSlot.uxml` as a horizontal card

**Files:**
- Modify: `Assets/Resources/UI/Templates/SeedSlot.uxml`

The current template is a vertical square tile. Replace it entirely with a horizontal row:

**Step 1: Overwrite SeedSlot.uxml**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:Button class="seed-slot">
        <ui:VisualElement class="seed-icon" />
        <ui:VisualElement class="seed-info">
            <ui:VisualElement class="seed-info-top">
                <ui:Label class="seed-name" />
                <ui:Label class="seed-count" />
            </ui:VisualElement>
            <ui:Label class="seed-meta" />
            <ui:VisualElement class="seed-variants" />
        </ui:VisualElement>
    </ui:Button>
</ui:UXML>
```

`seed-slot` = the tappable row.
`seed-icon` = 64×64 sprite on the left.
`seed-info` = flex column (name + count row, meta row, variants row).
`seed-meta` = dim text: grow time + preferred weather condition.
`seed-variants` = flex-row chip container, populated at runtime.

**Step 2: No test (pure template — verified visually in Unity)**

---

### Task 2: Rewrite `Satchel.uss`

**Files:**
- Modify: `Assets/UI/Styles/Satchel.uss`

The old file sizes cards as 220×260px grid items. Replace entirely:

**Step 1: Overwrite Satchel.uss**

```css
/* Satchel — horizontal seed card list */

#satchel-panel .seed-slot {
    flex-direction: row;
    align-items: flex-start;
    width: 100%;
    padding: var(--spacing-sm) var(--spacing-md);
    margin-bottom: var(--spacing-sm);
    background-color: rgba(15, 30, 40, 0.60);
    border-radius: var(--radius-md);
    border-width: 1px;
    border-color: rgba(120, 200, 220, 0.15);
}

#satchel-panel .seed-icon {
    width: 64px;
    height: 64px;
    min-width: 64px;
    margin-right: var(--spacing-md);
    background-size: contain;
    background-position: center;
    background-repeat: no-repeat;
}

#satchel-panel .seed-info {
    flex: 1;
    flex-direction: column;
    justify-content: center;
}

#satchel-panel .seed-info-top {
    flex-direction: row;
    justify-content: space-between;
    margin-bottom: var(--spacing-xs);
}

#satchel-panel .seed-name {
    color: var(--color-text-bright);
    font-size: var(--font-md);
    -unity-font-style: bold;
}

#satchel-panel .seed-count {
    color: var(--color-text-dim);
    font-size: var(--font-sm);
}

#satchel-panel .seed-meta {
    color: var(--color-text-dim);
    font-size: var(--font-xs);
    margin-bottom: var(--spacing-xs);
}

#satchel-panel .seed-variants {
    flex-direction: row;
    flex-wrap: wrap;
}

.variant-chip {
    font-size: var(--font-xs);
    padding: 2px 8px;
    margin-right: 6px;
    margin-bottom: 4px;
    border-radius: var(--radius-sm);
    background-color: rgba(255, 255, 255, 0.07);
}

/* Suppresses transition during pointer-drag so the panel tracks the finger instantly */
.no-transition {
    transition-duration: 0ms;
}
```

**Step 2: No test (pure style — verified visually in Unity)**

---

### Task 3: Update `GardenRoot.uxml` — remove dead elements, rename list

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

The satchel section currently contains `probability-panel`, `plant-button`, and `selected-seed-name`. Remove them all. Also rename `seed-grid` → `seed-list`.

**Step 1: Find the satchel section** (lines ~124–136) and replace it

Old block:
```xml
<!-- Satchel Bottom Sheet -->
<ui:VisualElement name="satchel-scrim" class="scrim" style="display: none;" />
<ui:VisualElement name="satchel-panel" class="bottom-sheet" style="display: none;">
    <ui:VisualElement name="satchel-handle" class="bottom-sheet-handle" />
    <ui:Label text="Satchel" class="panel-header" />
    <ui:ScrollView name="seed-grid" class="scroll-view" />
    <ui:VisualElement name="probability-panel" style="display: none;">
        <ui:Label name="selected-seed-name" text="" />
        <ui:VisualElement class="separator" />
        <ui:ScrollView name="probability-grid" />
    </ui:VisualElement>
    <ui:Button name="plant-button" text="Plant" class="btn" enabled="false" />
</ui:VisualElement>
```

New block:
```xml
<!-- Satchel Bottom Sheet -->
<ui:VisualElement name="satchel-scrim" class="scrim" style="display: none;" />
<ui:VisualElement name="satchel-panel" class="bottom-sheet" style="display: none;">
    <ui:VisualElement name="satchel-handle" class="bottom-sheet-handle" />
    <ui:Label text="Satchel" class="panel-header" />
    <ui:ScrollView name="seed-list" class="scroll-view" />
</ui:VisualElement>
```

**Step 2: No test (UXML structure — verified by Unity compilation)**

---

### Task 4: Update `SeedSlotUI.cs` — populate meta and variant chips

**Files:**
- Modify: `Assets/Scripts/UI/SeedSlotUI.cs`

The static `Create` method currently fills name, count, and icon. Extend it to also fill `seed-meta` and `seed-variants`.

**Step 1: Overwrite SeedSlotUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class SeedSlotUI
    {
        public static VisualElement Create(VisualTreeAsset template, SeedData data, int count, System.Action<SeedData> callback)
        {
            var root = template.CloneTree();
            var slot = root.Q<Button>(className: "seed-slot");

            var nameLabel   = root.Q<Label>(className: "seed-name");
            var countLabel  = root.Q<Label>(className: "seed-count");
            var icon        = root.Q<VisualElement>(className: "seed-icon");
            var metaLabel   = root.Q<Label>(className: "seed-meta");
            var variantRow  = root.Q<VisualElement>(className: "seed-variants");

            if (nameLabel  != null) nameLabel.text  = data.seedName;
            if (countLabel != null) countLabel.text = count < 0 ? "∞" : $"×{count}";
            if (icon != null && data.icon != null)
                icon.style.backgroundImage = new StyleBackground(data.icon);

            if (metaLabel != null)
                metaLabel.text = $"{data.baseGrowthHours:0.#}h · {data.preferredWeather}";

            if (variantRow != null)
                PopulateVariantChips(variantRow, data);

            if (slot != null)
                slot.clicked += () => callback?.Invoke(data);

            return root;
        }

        private static void PopulateVariantChips(VisualElement container, SeedData data)
        {
            var discovered = SaveManager.Instance.Data.discoveredVariants;
            foreach (var variant in data.variants)
            {
                var chip = new Label();
                chip.AddToClassList("variant-chip");
                if (discovered.Contains(variant.variantName))
                {
                    chip.text = variant.variantName;
                    chip.AddToClassList($"rarity-{variant.rarity.ToString().ToLower()}");
                }
                else
                {
                    chip.text = "?????";
                }
                container.Add(chip);
            }
        }
    }
}
```

**Step 2: No Unity EditMode test exists for UI factory methods** — verify visually after Task 5.

---

### Task 5: Rewrite `SatchelUI.cs` — animated sheet + swipe dismiss + direct plant

**Files:**
- Modify: `Assets/Scripts/UI/SatchelUI.cs`

Remove all selection/probability/Plant-button logic. Add animated show/hide using the CSS `translate` transition. Add swipe-to-dismiss gesture restricted to the handle so it doesn't conflict with list scrolling.

**Step 1: Overwrite SatchelUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        private VisualTreeAsset seedSlotTemplate;

        private VisualElement panel;
        private VisualElement scrim;
        private ScrollView seedList;

        private int targetEnvIndex = -1;
        private int targetSlotIndex = -1;

        private bool _isDragging;
        private float _dragStartY;

        public void Initialize(VisualElement root)
        {
            seedSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedSlot");

            panel    = root.Q<VisualElement>("satchel-panel");
            scrim    = root.Q<VisualElement>("satchel-scrim");
            seedList = root.Q<ScrollView>("seed-list");

            scrim.RegisterCallback<ClickEvent>(_ => Hide());

            // Swipe-to-dismiss: PointerDown on handle only, so list scrolling works normally.
            var handle = root.Q<VisualElement>("satchel-handle");
            handle.RegisterCallback<PointerDownEvent>(OnHandlePointerDown);
            panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);
        }

        public void Show() => Show(-1, -1);

        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex = envIndex;
            targetSlotIndex = slotIndex;

            // Place panel off-screen before making it visible.
            panel.style.translate = new StyleTranslate(new Translate(0, Length.Percent(100)));
            scrim.style.display  = DisplayStyle.Flex;
            panel.style.display  = DisplayStyle.Flex;

            // One frame later: remove the translate → CSS transition slides it up.
            panel.schedule.Execute(() =>
                panel.style.translate = new StyleTranslate(new Translate(0, 0))
            );

            RefreshList();
        }

        public void Hide()
        {
            panel.RemoveFromClassList("no-transition");
            panel.style.translate = new StyleTranslate(new Translate(0, Length.Percent(100)));
            scrim.style.display   = DisplayStyle.None;

            // After the 250ms transition, fully hide and reset translate for next Show().
            panel.schedule.Execute(() =>
            {
                panel.style.display   = DisplayStyle.None;
                panel.style.translate = new StyleTranslate(new Translate(0, 0));
            }).StartingIn(260);
        }

        private void RefreshList()
        {
            seedList.Clear();
            var seeds = SeedRegistry.Instance.GetOwnedSeeds();
            foreach (var seed in seeds)
            {
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                var slot  = SeedSlotUI.Create(seedSlotTemplate, seed, count, OnSeedTapped);
                seedList.Add(slot);
            }
        }

        private void OnSeedTapped(SeedData seed)
        {
            if (targetEnvIndex >= 0 && targetSlotIndex >= 0)
                PlantManager.Instance.Plant(seed, targetEnvIndex, targetSlotIndex);
            else
                PlantManager.Instance.Plant(seed);
            Hide();
        }

        // ── Swipe gesture ─────────────────────────────────────────────────────────

        private void OnHandlePointerDown(PointerDownEvent evt)
        {
            _isDragging  = true;
            _dragStartY  = evt.position.y;
            panel.AddToClassList("no-transition");
            panel.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPanelPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;
            float delta = Mathf.Max(0f, evt.position.y - _dragStartY);
            panel.style.translate = new StyleTranslate(
                new Translate(0, new Length(delta, LengthUnit.Pixel)));
        }

        private void OnPanelPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            panel.ReleasePointer(evt.pointerId);
            panel.RemoveFromClassList("no-transition");

            float delta = evt.position.y - _dragStartY;
            if (delta > 80f)
                Hide();
            else
                panel.style.translate = new StyleTranslate(new Translate(0, 0));
        }
    }
}
```

**Step 2: Check Unity console for compile errors** after saving (Unity recompiles automatically). Expected: no errors.

---

### Task 6: Verify in Unity and commit

**Step 1: Open Unity → check Console for compile errors**

Expected: zero errors. If any, fix before proceeding.

**Step 2: Enter Play Mode → tap an empty plant slot**

- Satchel sheet should slide up from the bottom with a 250ms ease-out animation
- List shows one horizontal card per owned seed with icon, name, count, grow time, weather, and variant chips
- Discovered variants show colored names; undiscovered show "?????"

**Step 3: Swipe the handle downward**

- Panel should track the finger during drag
- Release past 80px → panel slides down and disappears
- Release short of 80px → panel snaps back up

**Step 4: Tap a seed card**

- PlantManager.Plant() fires immediately
- Sheet dismisses with slide-down animation
- Seed appears planted in the target slot

**Step 5: Commit**

```bash
git add Assets/Resources/UI/Templates/SeedSlot.uxml \
        Assets/UI/Styles/Satchel.uss \
        Assets/UI/Documents/GardenRoot.uxml \
        Assets/Scripts/UI/SeedSlotUI.cs \
        Assets/Scripts/UI/SatchelUI.cs \
        docs/plans/2026-02-25-satchel-redesign-design.md \
        docs/plans/2026-02-25-satchel-redesign.md
git commit -m "feat: redesign Satchel as animated card sheet with swipe-to-dismiss"
```
