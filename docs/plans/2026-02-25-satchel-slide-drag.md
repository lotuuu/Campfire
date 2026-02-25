# Satchel Slide Animation & Drag-to-Close Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix the satchel bottom sheet so it slides in from below (instead of snapping) and provides a large drag zone at the top for drag-to-close.

**Architecture:** The snap bug is caused by `translate: 0 100%` being set while the panel is `display:none` — the height is unknown so the percentage resolves to 0px. Fix by making the panel always `display:flex` and using a UXML inline `translate: 0 100%` as the initial off-screen state; `Show()` then just sets translate to 0 and the CSS transition fires reliably. For drag-to-close, replace the 4px `satchel-handle` element with a 44px-tall `satchel-drag-zone` container holding a visual pip child, and register the pointer-down on the zone instead of the pip.

**Tech Stack:** Unity UIToolkit (UXML, USS, C# UIElements API)

---

### Task 1: UXML — swap satchel-panel initial state and replace handle with drag zone

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml:125-130`

**Step 1: Replace the satchel block**

Find (lines 125–130):
```xml
    <!-- Satchel Bottom Sheet -->
    <ui:VisualElement name="satchel-scrim" class="scrim" style="display: none;" />
    <ui:VisualElement name="satchel-panel" class="bottom-sheet" style="display: none;">
        <ui:VisualElement name="satchel-handle" class="bottom-sheet-handle" />
        <ui:Label text="Satchel" class="panel-header" />
        <ui:ScrollView name="seed-list" class="scroll-view" />
    </ui:VisualElement>
```

Replace with:
```xml
    <!-- Satchel Bottom Sheet -->
    <ui:VisualElement name="satchel-scrim" class="scrim" style="display: none;" />
    <ui:VisualElement name="satchel-panel" class="bottom-sheet" style="translate: 0 100%;">
        <ui:VisualElement name="satchel-drag-zone" class="bottom-sheet-drag-zone">
            <ui:VisualElement class="bottom-sheet-handle-pip" />
        </ui:VisualElement>
        <ui:Label text="Satchel" class="panel-header" />
        <ui:ScrollView name="seed-list" class="scroll-view" />
    </ui:VisualElement>
```

Key changes:
- `style="display: none;"` → `style="translate: 0 100%;"` — panel is always `display:flex` but starts off-screen
- `satchel-handle` (4px pill element) → `satchel-drag-zone` container (44px tall, full-width) with `bottom-sheet-handle-pip` visual child inside

**Step 2: Verify the UXML is valid**

Open Unity — no console errors about malformed UXML. The satchel panel will now be visible at the bottom of the screen (since display:none is gone), which is expected — we fix the C# in Task 3.

**Step 3: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml
git commit -m "refactor: replace satchel-handle with drag-zone container, remove display:none"
```

---

### Task 2: CSS — add drag-zone styles, replace handle styles

**Files:**
- Modify: `Assets/UI/Styles/Common.uss:196-203`

**Step 1: Replace the `.bottom-sheet-handle` block**

Find (lines 196–203):
```css
.bottom-sheet-handle {
    width: 40px;
    height: 4px;
    border-radius: 2px;
    background-color: rgba(120, 150, 170, 0.4);
    align-self: center;
    margin-bottom: var(--spacing-sm);
}
```

Replace with:
```css
/* Full-width 44px touch target — pointer events registered on this */
.bottom-sheet-drag-zone {
    width: 100%;
    height: 44px;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}

/* Visual pill inside the drag zone */
.bottom-sheet-handle-pip {
    width: 40px;
    height: 4px;
    border-radius: 2px;
    background-color: rgba(120, 150, 170, 0.4);
}
```

**Step 2: Verify in Unity**

Unity should recompile styles with no errors. The satchel panel (still visible off-screen from Task 1) now has a 44px drag zone at the top with a centered pip indicator.

**Step 3: Commit**

```bash
git add Assets/UI/Styles/Common.uss
git commit -m "style: replace bottom-sheet-handle with drag-zone and pip styles"
```

---

### Task 3: C# — fix Show/Hide and update handle registration

**Files:**
- Modify: `Assets/Scripts/UI/SatchelUI.cs`

**Step 1: Update Initialize() — query drag zone instead of handle**

Find in `Initialize()`:
```csharp
var handle = root.Q<VisualElement>("satchel-handle");
handle.RegisterCallback<PointerDownEvent>(OnHandlePointerDown);
```

Replace with:
```csharp
var dragZone = root.Q<VisualElement>("satchel-drag-zone");
dragZone.RegisterCallback<PointerDownEvent>(OnHandlePointerDown);
```

**Step 2: Rewrite Show() — remove display/schedule hack, just set translate**

Find the entire `Show(int envIndex, int slotIndex)` body:
```csharp
        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex  = envIndex;
            targetSlotIndex = slotIndex;

            panel.style.translate = new StyleTranslate(new Translate(0, Length.Percent(100)));
            scrim.style.display   = DisplayStyle.Flex;
            panel.style.display   = DisplayStyle.Flex;

            panel.schedule.Execute(() =>
                panel.style.translate = new StyleTranslate(new Translate(0, 0))
            );

            RefreshList();
        }
```

Replace with:
```csharp
        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex  = envIndex;
            targetSlotIndex = slotIndex;

            scrim.style.display = DisplayStyle.Flex;
            panel.style.translate = new StyleTranslate(new Translate(0, 0));

            RefreshList();
        }
```

The panel is always `display:flex`. It starts at `translate: 0 100%` (set in UXML inline style). Setting translate to 0 triggers the CSS transition on `.bottom-sheet` and slides it up.

**Step 3: Simplify OnHideTransitionEnd() — remove display:none**

Find:
```csharp
        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            panel.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            panel.style.display   = DisplayStyle.None;
            panel.style.translate = new StyleTranslate(new Translate(0, 0));
        }
```

Replace with:
```csharp
        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            panel.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
        }
```

The panel no longer needs `display:none` — it stays off-screen via `translate: 0 100%` (set by `Hide()` which calls `panel.style.translate = new StyleTranslate(new Translate(0, Length.Percent(100)))`). No need to reset translate to 0 either since the next `Show()` sets it explicitly.

**Step 4: Verify in Unity Editor (Play Mode)**

- Open the Terrarium tab
- Tap an empty slot → Satchel should slide up smoothly from below (250ms ease-out)
- Tap the scrim → Satchel should slide down smoothly
- Drag the top 44px zone downward → Satchel should follow the finger
- Release after >80px drag → Satchel should slide fully closed
- Release after <80px drag → Satchel should snap back up

**Step 5: Commit**

```bash
git add Assets/Scripts/UI/SatchelUI.cs
git commit -m "fix: satchel slides in from below and uses full-width drag zone"
```
