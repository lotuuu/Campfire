# Bottom Nav with Swipeable Pages — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace overlay panel navigation with a bottom tab bar and horizontally swipeable pages.

**Architecture:** A custom `SwipeablePageView` VisualElement holds 5 pages side-by-side. A `BottomNavUI` controller manages 5 tab buttons. `HortusUI` orchestrates both, replacing the old toggle/close pattern. Satchel becomes a bottom sheet overlay. Debug stays as an overlay accessed separately.

**Tech Stack:** Unity 6 UI Toolkit (VisualElement, USS, UXML), C# pointer events for swipe gestures.

**Tab order (left to right):** Codex (0), Shop (1), Terrarium (2, default), Greenhouse (3), Locked (4).

---

### Task 1: Create SwipeablePageView

**Files:**
- Create: `Assets/Scripts/UI/SwipeablePageView.cs`

**Step 1: Create SwipeablePageView.cs**

This is a pure `VisualElement` subclass (not MonoBehaviour). It manages a horizontal strip of pages inside a clipping viewport.

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SwipeablePageView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SwipeablePageView> { }

        private readonly VisualElement pageContainer;

        private int currentPageIndex;
        private int pageCount;
        private bool isDragging;
        private float dragStartX;
        private float dragStartY;
        private float dragCurrentX;
        private bool dragDirectionLocked;
        private bool dragIsHorizontal;
        private float pageWidth;

        private const float SwipeThreshold = 50f;
        private const float DirectionLockThreshold = 10f;
        private const int AnimationDurationMs = 300;

        public int CurrentPageIndex => currentPageIndex;
        public int PageCount => pageCount;
        public event Action<int> OnPageChanged;

        public SwipeablePageView()
        {
            style.overflow = Overflow.Hidden;
            style.flexGrow = 1;

            pageContainer = new VisualElement();
            pageContainer.style.flexDirection = FlexDirection.Row;
            pageContainer.style.height = new Length(100, LengthUnit.Percent);
            pageContainer.style.position = Position.Absolute;
            pageContainer.style.left = 0;
            pageContainer.style.top = 0;
            pageContainer.style.bottom = 0;
            hierarchy.Add(pageContainer);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void AddPage(VisualElement page)
        {
            page.style.width = new Length(100, LengthUnit.Percent);
            page.style.height = new Length(100, LengthUnit.Percent);
            page.style.flexShrink = 0;
            pageContainer.Add(page);
            pageCount = pageContainer.childCount;
            UpdateContainerWidth();
        }

        public void GoToPage(int index, bool animated = true)
        {
            index = Mathf.Clamp(index, 0, pageCount - 1);
            if (index == currentPageIndex && !isDragging) return;
            currentPageIndex = index;
            AnimateToCurrentPage(animated);
            OnPageChanged?.Invoke(currentPageIndex);
        }

        private void AnimateToCurrentPage(bool animated)
        {
            float target = -currentPageIndex * pageWidth;
            if (animated)
            {
                pageContainer.style.transitionProperty = new List<StylePropertyName> { new("translate") };
                pageContainer.style.transitionDuration = new List<TimeValue> { new(AnimationDurationMs, TimeUnit.Millisecond) };
                pageContainer.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut) };
            }
            else
            {
                pageContainer.style.transitionDuration = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            }
            pageContainer.style.translate = new Translate(target, 0);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            isDragging = true;
            dragStartX = evt.position.x;
            dragStartY = evt.position.y;
            dragCurrentX = evt.position.x;
            dragDirectionLocked = false;
            dragIsHorizontal = false;

            // Remove transition during drag for direct tracking
            pageContainer.style.transitionDuration = new List<TimeValue> { new(0, TimeUnit.Millisecond) };

            this.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging) return;

            float dx = evt.position.x - dragStartX;
            float dy = evt.position.y - dragStartY;

            // Lock direction after threshold
            if (!dragDirectionLocked)
            {
                if (Mathf.Abs(dx) > DirectionLockThreshold || Mathf.Abs(dy) > DirectionLockThreshold)
                {
                    dragDirectionLocked = true;
                    dragIsHorizontal = Mathf.Abs(dx) > Mathf.Abs(dy);
                }
            }

            if (!dragDirectionLocked || !dragIsHorizontal) return;

            dragCurrentX = evt.position.x;
            float offset = dragCurrentX - dragStartX;
            float baseX = -currentPageIndex * pageWidth;

            // Add resistance at edges
            if ((currentPageIndex == 0 && offset > 0) || (currentPageIndex == pageCount - 1 && offset < 0))
                offset *= 0.3f;

            pageContainer.style.translate = new Translate(baseX + offset, 0);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging) return;
            FinishDrag();
            this.ReleasePointer(evt.pointerId);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!isDragging) return;
            FinishDrag();
            this.ReleasePointer(evt.pointerId);
        }

        private void FinishDrag()
        {
            isDragging = false;
            if (!dragIsHorizontal)
            {
                AnimateToCurrentPage(false);
                return;
            }

            float dx = dragCurrentX - dragStartX;
            int targetPage = currentPageIndex;

            if (Mathf.Abs(dx) > SwipeThreshold)
            {
                if (dx < 0 && currentPageIndex < pageCount - 1)
                    targetPage = currentPageIndex + 1;
                else if (dx > 0 && currentPageIndex > 0)
                    targetPage = currentPageIndex - 1;
            }

            currentPageIndex = targetPage;
            AnimateToCurrentPage(true);
            OnPageChanged?.Invoke(currentPageIndex);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            pageWidth = resolvedStyle.width;
            UpdateContainerWidth();
            AnimateToCurrentPage(false);
        }

        private void UpdateContainerWidth()
        {
            pageContainer.style.width = new Length(pageCount * 100, LengthUnit.Percent);
        }
    }
}
```

**Step 2: Verify compilation**

Run: `read_console` to check for errors after Unity compiles.
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/SwipeablePageView.cs
git commit -m "feat: add SwipeablePageView VisualElement for swipe navigation"
```

---

### Task 2: Create BottomNav USS and update UXML

**Files:**
- Create: `Assets/UI/Styles/BottomNav.uss`
- Modify: `Assets/UI/Documents/GardenRoot.uxml` (full rewrite of layout)
- Modify: `Assets/UI/Styles/HUD.uss` (remove old nav-bar styles)
- Modify: `Assets/UI/Styles/Common.uss` (change `.panel` from absolute overlay to page content)

**Step 1: Create BottomNav.uss**

```css
/* Bottom navigation tab bar */

#bottom-nav {
    flex-direction: row;
    align-self: stretch;
    padding: var(--spacing-xs) var(--spacing-sm);
    padding-bottom: var(--spacing-md);
    background-color: rgba(10, 20, 30, 0.65);
    border-top-width: 1px;
    border-top-color: rgba(120, 200, 220, 0.15);
}

.nav-tab {
    flex-grow: 1;
    background-color: transparent;
    color: var(--color-text-dim);
    border-width: 0;
    border-bottom-width: 2px;
    border-bottom-color: transparent;
    border-radius: 0;
    padding: var(--spacing-sm) var(--spacing-xs);
    margin: 0 var(--spacing-xs);
    min-height: 72px;
    font-size: var(--font-sm);
    -unity-text-align: middle-center;
    transition-property: color, border-bottom-color;
    transition-duration: 0.2s;
}

.nav-tab--active {
    color: var(--color-text-accent);
    border-bottom-color: var(--color-text-accent);
}

.nav-tab--disabled {
    color: rgba(120, 150, 170, 0.35);
}

.nav-tab:hover {
    color: var(--color-text);
}

.nav-tab--disabled:hover {
    color: rgba(120, 150, 170, 0.35);
}
```

**Step 2: Rewrite GardenRoot.uxml**

Replace the entire file with the new page-based layout. Each former panel becomes a page child. The hearth-view moves into the terrarium-page. The greenhouse gets a new page placeholder. Close buttons are removed from all panels.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:uie="UnityEditor.UIElements">

    <Style src="../Styles/Variables.uss" />
    <Style src="../Styles/Common.uss" />
    <Style src="../Styles/HUD.uss" />
    <Style src="../Styles/BottomNav.uss" />
    <Style src="../Styles/Satchel.uss" />
    <Style src="../Styles/Codex.uss" />
    <Style src="../Styles/Greenhouse.uss" />
    <Style src="../Styles/Debug.uss" />
    <Style src="../Styles/SeedShop.uss" />
    <Style src="../Styles/HarvestResult.uss" />
    <Style src="../Styles/Terrarium.uss" />
    <Style src="../Styles/Hearth.uss" />

    <!-- App shell: vertical stack -->
    <ui:VisualElement name="app-shell" style="flex-grow: 1;">

        <!-- Top bar: weather + currency (always visible) -->
        <ui:VisualElement name="top-bar" picking-mode="Ignore">
            <ui:Label name="weather-text" text="--°C  ·  --  ·  --" />
            <ui:VisualElement name="currency-panel">
                <ui:Label name="dewdrops-text" class="currency-label" text="Dew: 0" />
                <ui:Label name="sun-shards-text" class="currency-label" text="Sun: 0" />
                <ui:Label name="aura-dust-text" class="currency-label" text="Dust: 0" />
            </ui:VisualElement>
        </ui:VisualElement>

        <!-- Page viewport (SwipeablePageView fills this) -->
        <ui:VisualElement name="page-viewport" style="flex-grow: 1; overflow: hidden;" />

        <!-- Bottom navigation -->
        <ui:VisualElement name="bottom-nav">
            <ui:Button name="tab-codex" text="Codex" class="nav-tab" />
            <ui:Button name="tab-shop" text="Shop" class="nav-tab" />
            <ui:Button name="tab-terrarium" text="Terrarium" class="nav-tab nav-tab--active" />
            <ui:Button name="tab-greenhouse" text="Greenhouse" class="nav-tab" />
            <ui:Button name="tab-locked" text="..." class="nav-tab nav-tab--disabled" />
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- ===== PAGE CONTENTS (added to SwipeablePageView by HortusUI) ===== -->

    <!-- Page 0: Codex -->
    <ui:VisualElement name="codex-page" class="page-content" style="display: none;">
        <ui:Label text="Codex" class="panel-header" />
        <ui:ScrollView name="variant-grid" class="scroll-view" />
        <ui:VisualElement name="detail-panel">
            <ui:Label name="detail-name" text="" />
            <ui:Label name="detail-description" text="" />
            <ui:Label name="detail-rarity" text="" />
            <ui:VisualElement name="detail-color-swatch" />
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- Page 1: Shop -->
    <ui:VisualElement name="shop-page" class="page-content" style="display: none;">
        <ui:Label text="Seed Shop" class="panel-header" />
        <ui:ScrollView name="shop-grid" class="scroll-view" />
    </ui:VisualElement>

    <!-- Page 2: Terrarium (Hearth isometric view) -->
    <ui:VisualElement name="terrarium-page" class="page-content" style="display: none;">
        <ui:VisualElement name="hearth-view" picking-mode="Ignore">
            <ui:Label name="hearth-title" text="The Hearth" />
            <ui:VisualElement name="hearth-plot">
                <ui:Button name="hearth-slot-0" class="hearth-slot">
                    <ui:VisualElement class="hearth-slot-inner">
                        <ui:VisualElement name="hearth-soil-0" class="hearth-soil" />
                        <ui:VisualElement name="hearth-swatch-0" class="hearth-plant-swatch" style="display: none;" />
                        <ui:Label name="hearth-label-0" text="Tap to Plant" class="hearth-slot-label" />
                        <ui:VisualElement class="hearth-progress-bar">
                            <ui:VisualElement name="hearth-progress-0" class="hearth-progress-fill" />
                        </ui:VisualElement>
                    </ui:VisualElement>
                </ui:Button>
                <ui:Button name="hearth-slot-1" class="hearth-slot">
                    <ui:VisualElement class="hearth-slot-inner">
                        <ui:VisualElement name="hearth-soil-1" class="hearth-soil" />
                        <ui:VisualElement name="hearth-swatch-1" class="hearth-plant-swatch" style="display: none;" />
                        <ui:Label name="hearth-label-1" text="Tap to Plant" class="hearth-slot-label" />
                        <ui:VisualElement class="hearth-progress-bar">
                            <ui:VisualElement name="hearth-progress-1" class="hearth-progress-fill" />
                        </ui:VisualElement>
                    </ui:VisualElement>
                </ui:Button>
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- Page 3: Greenhouse -->
    <ui:VisualElement name="greenhouse-page" class="page-content" style="display: none;">
        <ui:Label text="Greenhouse" class="panel-header" />
        <ui:VisualElement name="greenhouse-header">
            <ui:Label name="greenhouse-dust-rate" text="+0 Aura Dust/hr" />
            <ui:Label name="greenhouse-slots-text" text="0 / 0" />
        </ui:VisualElement>
        <ui:ScrollView name="greenhouse-grid" class="scroll-view" />
        <ui:Button name="greenhouse-expand-button" text="Expand Slots" class="btn" />
    </ui:VisualElement>

    <!-- Page 4: Locked -->
    <ui:VisualElement name="locked-page" class="page-content" style="display: none;">
        <ui:VisualElement style="flex-grow: 1; justify-content: center; align-items: center;">
            <ui:Label text="Coming Soon" class="label-dim" style="font-size: var(--font-lg);" />
        </ui:VisualElement>
    </ui:VisualElement>

    <!-- ===== OVERLAYS (stay on top of pages) ===== -->

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

    <!-- Harvest Result Popup -->
    <ui:VisualElement name="harvest-popup" style="display: none;" />

    <!-- Debug Panel (overlay, toggled separately) -->
    <ui:VisualElement name="debug-panel" class="panel overlay-panel" style="display: none;">
        <ui:Label text="Debug Weather" class="panel-header" />
        <ui:Button name="debug-close" text="X" class="btn btn-close" />

        <ui:ScrollView name="debug-scroll" class="scroll-view">
            <ui:Toggle name="debug-mode-toggle" label="Debug Mode" />

            <ui:VisualElement class="debug-row">
                <ui:Label text="Temp" class="debug-label" />
                <ui:Slider name="temp-slider" low-value="-20" high-value="50" value="22" />
                <ui:Label name="temp-value" text="22°C" class="debug-value" />
            </ui:VisualElement>

            <ui:VisualElement class="debug-row">
                <ui:Label text="Humidity" class="debug-label" />
                <ui:Slider name="humidity-slider" low-value="0" high-value="100" value="50" />
                <ui:Label name="humidity-value" text="50%" class="debug-value" />
            </ui:VisualElement>

            <ui:VisualElement class="debug-row">
                <ui:Label text="Wind" class="debug-label" />
                <ui:Slider name="wind-slider" low-value="0" high-value="50" value="3" />
                <ui:Label name="wind-value" text="3.0 m/s" class="debug-value" />
            </ui:VisualElement>

            <ui:DropdownField name="condition-dropdown" label="Condition"
                choices="Clear,Cloudy,Rain,Storm,Snow" />
            <ui:DropdownField name="moon-phase-dropdown" label="Moon Phase"
                choices="New Moon,Waxing Crescent,First Quarter,Waxing Gibbous,Full Moon,Waning Gibbous,Last Quarter,Waning Crescent" />
            <ui:DropdownField name="time-of-day-dropdown" label="Time of Day"
                choices="Day,Night,Golden Hour" />
            <ui:DropdownField name="calendar-event-dropdown" label="Calendar Event"
                choices="None,Spring Equinox,Fall Equinox,Lunar Eclipse" />

            <ui:VisualElement name="preset-buttons">
                <ui:Button name="blizzard-button" text="Blizzard" class="btn preset-btn" />
                <ui:Button name="thunderstorm-button" text="Thunderstorm" class="btn preset-btn" />
                <ui:Button name="clear-night-button" text="Clear Night" class="btn preset-btn" />
                <ui:Button name="golden-hour-button" text="Golden Hour" class="btn preset-btn" />
            </ui:VisualElement>

            <ui:Button name="apply-button" text="Apply" class="btn" />

            <ui:VisualElement class="separator" />
            <ui:VisualElement class="debug-row">
                <ui:Label text="Hours" class="debug-label" />
                <ui:IntegerField name="time-skip-field" value="1" />
                <ui:Button name="time-skip-button" text="Skip Time" class="btn preset-btn" />
            </ui:VisualElement>

            <ui:VisualElement class="separator" />
            <ui:VisualElement class="debug-row">
                <ui:Label text="Time" class="debug-label" />
                <ui:TextField name="time-override-field" value="" />
            </ui:VisualElement>
            <ui:VisualElement class="debug-row">
                <ui:Button name="set-time-button" text="Set Time" class="btn preset-btn" />
                <ui:Button name="reset-time-button" text="Reset" class="btn preset-btn" />
            </ui:VisualElement>
            <ui:Label name="current-time-label" class="label-dim" />

            <ui:VisualElement class="separator" />
            <ui:Button name="clear-save-button" text="Clear Save Data" class="btn" />
        </ui:ScrollView>
    </ui:VisualElement>

    <!-- Location Gate -->
    <ui:VisualElement name="location-gate" class="gate-panel" picking-mode="Position">
        <ui:Label name="gate-title" text="Garden" class="gate-title" />
        <ui:Label name="gate-status" text="Acquiring location..." class="gate-subtitle" />
        <ui:Button name="gate-retry" text="Retry" class="btn" style="display: none;" />
    </ui:VisualElement>

</ui:UXML>
```

**Step 3: Update Common.uss**

Add `.page-content` class for page children, `.bottom-sheet` and `.scrim` for Satchel overlay, and `.overlay-panel` for Debug. Keep `.panel` for backwards compat with Debug overlay.

Add to the end of `Assets/UI/Styles/Common.uss`:

```css
/* Page content (inside SwipeablePageView) */
.page-content {
    flex-grow: 1;
    padding: var(--spacing-lg);
    padding-top: var(--spacing-md);
}

/* Overlay panel (debug etc) - stays absolute */
.overlay-panel {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: var(--color-bg-panel);
}

/* Bottom sheet overlay */
.scrim {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.5);
}

.bottom-sheet {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    height: 70%;
    background-color: rgba(15, 25, 35, 0.95);
    border-top-left-radius: var(--radius-lg);
    border-top-right-radius: var(--radius-lg);
    border-top-width: 1px;
    border-top-color: rgba(140, 230, 240, 0.25);
    padding: var(--spacing-md) var(--spacing-lg);
    transition-property: translate;
    transition-duration: 250ms;
    transition-timing-function: ease-out;
}

.bottom-sheet-handle {
    width: 40px;
    height: 4px;
    border-radius: 2px;
    background-color: rgba(120, 150, 170, 0.4);
    align-self: center;
    margin-bottom: var(--spacing-sm);
}
```

**Step 4: Update HUD.uss**

Remove the old `#bottom-bar`, `#nav-bar`, `.nav-btn` styles (lines 48-118) since they are replaced by BottomNav.uss. Keep `#top-bar`, `#weather-text`, `#currency-panel`, `.currency-label`, and `#pulse-button` styles. Remove `#hud` since the shell is now `#app-shell`.

Replace the `#hud` block at line 3-10 with nothing (remove it). The `#top-bar` styles stay as-is. Remove everything from `/* Bottom area */` comment (line 48) through `.nav-btn:active` (line 118).

**Step 5: Verify compilation**

Run: `read_console` to check for errors.
Expected: No compilation errors (UXML/USS changes don't cause C# compilation issues).

**Step 6: Commit**

```
git add Assets/UI/Styles/BottomNav.uss Assets/UI/Documents/GardenRoot.uxml Assets/UI/Styles/Common.uss Assets/UI/Styles/HUD.uss
git commit -m "feat: restructure UXML to page-based layout with bottom nav"
```

---

### Task 3: Create BottomNavUI controller

**Files:**
- Create: `Assets/Scripts/UI/BottomNavUI.cs`

**Step 1: Create BottomNavUI.cs**

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        private Button[] tabs;
        private SwipeablePageView pageView;
        private int lockedTabIndex = 4;

        private static readonly string[] TabNames = {
            "tab-codex", "tab-shop", "tab-terrarium", "tab-greenhouse", "tab-locked"
        };

        public void Initialize(VisualElement root, SwipeablePageView pageView)
        {
            this.pageView = pageView;

            tabs = new Button[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                tabs[i] = root.Q<Button>(TabNames[i]);
                int index = i;
                tabs[i].clicked += () => OnTabClicked(index);
            }

            pageView.OnPageChanged += UpdateActiveTab;
            UpdateActiveTab(pageView.CurrentPageIndex);
        }

        private void OnTabClicked(int index)
        {
            if (index == lockedTabIndex) return;
            pageView.GoToPage(index);
        }

        private void UpdateActiveTab(int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].RemoveFromClassList("nav-tab--active");
            }
            if (activeIndex >= 0 && activeIndex < tabs.Length)
            {
                tabs[activeIndex].AddToClassList("nav-tab--active");
            }
        }

        private void OnDestroy()
        {
            if (pageView != null)
                pageView.OnPageChanged -= UpdateActiveTab;
        }
    }
}
```

**Step 2: Verify compilation**

Run: `read_console`
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/BottomNavUI.cs
git commit -m "feat: add BottomNavUI tab bar controller"
```

---

### Task 4: Rewrite HortusUI to use SwipeablePageView

**Files:**
- Modify: `Assets/Scripts/UI/HortusUI.cs` (full rewrite)

**Step 1: Rewrite HortusUI.cs**

Replace the entire file. This version creates the SwipeablePageView, reparents the page elements from UXML into it, initializes all controllers, and wires events.

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;

        private const int DefaultPageIndex = 2; // Terrarium

        private SwipeablePageView pageView;
        private BottomNavUI bottomNavUI;
        private HearthViewUI hearthViewUI;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private SeedShopUI seedShopUI;
        private GreenhouseUI greenhouseUI;
        private HarvestResultUI harvestResultUI;
        private DebugWeatherPanel debugPanel;

        // Location gate
        private VisualElement locationGate;
        private Label gateStatus;
        private Button gateRetry;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            // Get sub-controllers
            hearthViewUI = GetComponent<HearthViewUI>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            seedShopUI = GetComponent<SeedShopUI>();
            greenhouseUI = GetComponent<GreenhouseUI>();
            harvestResultUI = GetComponent<HarvestResultUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            // Build SwipeablePageView
            pageView = new SwipeablePageView();
            var viewport = root.Q<VisualElement>("page-viewport");
            viewport.Add(pageView);

            // Reparent pages from UXML into the page view
            string[] pageNames = { "codex-page", "shop-page", "terrarium-page", "greenhouse-page", "locked-page" };
            foreach (var name in pageNames)
            {
                var page = root.Q<VisualElement>(name);
                page.RemoveFromHierarchy();
                page.style.display = DisplayStyle.Flex;
                pageView.AddPage(page);
            }

            // Initialize sub-controllers
            hearthViewUI?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            seedShopUI?.Initialize(root);
            greenhouseUI?.Initialize(root);
            harvestResultUI?.Initialize(root);
            debugPanel?.Initialize(root);

            // Initialize bottom nav
            bottomNavUI = GetComponent<BottomNavUI>();
            bottomNavUI?.Initialize(root, pageView);

            // Page change callbacks — refresh content when page becomes visible
            pageView.OnPageChanged += OnPageChanged;

            // Wire hearth slot events
            if (hearthViewUI != null)
            {
                hearthViewUI.OnEmptySlotTapped += (envIdx, slotIdx) =>
                {
                    satchelUI?.Show(envIdx, slotIdx);
                };

                hearthViewUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                        harvestResultUI?.Show(result);
                    hearthViewUI?.RefreshAllSlots();
                };
            }

            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    hearthViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }

            // Start on terrarium page
            pageView.GoToPage(DefaultPageIndex, false);

            // Location gate
            locationGate = root.Q<VisualElement>("location-gate");
            gateStatus = root.Q<Label>("gate-status");
            gateRetry = root.Q<Button>("gate-retry");
            if (gateRetry != null)
                gateRetry.clicked += OnGateRetry;

            if (WeatherService.Instance != null)
            {
                if (WeatherService.Instance.IsLocationResolved)
                    OnLocationResolved(WeatherService.Instance.HasLocation);
                else
                    WeatherService.Instance.OnLocationResolved += OnLocationResolved;
            }
        }

        private void OnPageChanged(int pageIndex)
        {
            // Refresh page content on navigation
            switch (pageIndex)
            {
                case 0: codexUI?.Show(); break;
                case 1: seedShopUI?.Show(); break;
                case 3: greenhouseUI?.Show(); break;
            }
        }

        private void OnLocationResolved(bool success)
        {
            if (locationGate == null) return;
            if (success)
            {
                locationGate.style.display = DisplayStyle.None;
            }
            else
            {
                gateStatus.text = "Location access is required to play.\nPlease enable Location Services in Settings.";
                if (gateRetry != null)
                    gateRetry.style.display = DisplayStyle.Flex;
            }
        }

        private void OnGateRetry()
        {
            gateStatus.text = "Acquiring location...";
            if (gateRetry != null)
                gateRetry.style.display = DisplayStyle.None;
            WeatherService.Instance?.RetryLocation();
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnLocationResolved -= OnLocationResolved;
            if (pageView != null)
                pageView.OnPageChanged -= OnPageChanged;
        }
    }
}
```

**Step 2: Verify compilation**

Run: `read_console`
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/HortusUI.cs
git commit -m "feat: rewrite HortusUI for page-based navigation"
```

---

### Task 5: Update panel controllers for page lifecycle

**Files:**
- Modify: `Assets/Scripts/UI/CodexUI.cs`
- Modify: `Assets/Scripts/UI/SeedShopUI.cs`
- Modify: `Assets/Scripts/UI/GreenhouseUI.cs`

Each controller needs to:
1. Remove close button references and wiring
2. Update element name queries to match new UXML (e.g. `greenhouse-panel` → `greenhouse-page`)
3. Make `Show()` only refresh content (no display toggling — pages are always visible when in the viewport)
4. Remove `Hide()` or make it a no-op
5. Make `RefreshDisplay()` public where needed

**Step 1: Update CodexUI.cs**

Remove `closeButton` field and `closeButton.clicked += Hide` line. Change `panel` query from `"codex-panel"` to `"codex-page"`. Remove `Hide()` method. `Show()` just calls `RefreshCodex()` (no display toggle).

**Step 2: Update SeedShopUI.cs**

Remove `closeButton` field and wiring. Change `panel` query from `"shop-panel"` to `"shop-page"`. Remove `Hide()`. `Show()` just calls `RefreshDisplay()`.

**Step 3: Update GreenhouseUI.cs**

Remove `closeButton` field and wiring. Update queries to match new UXML names:
- `"greenhouse-panel"` → `"greenhouse-page"`
- `"dust-rate"` → `"greenhouse-dust-rate"`
- `"slots-text"` → `"greenhouse-slots-text"`
- `"plant-grid"` → `"greenhouse-grid"`
- `"expand-button"` → `"greenhouse-expand-button"`

Remove `Hide()`. `Show()` just calls `RefreshDisplay()`. Make `RefreshDisplay()` public.

**Step 4: Verify compilation**

Run: `read_console`
Expected: No compilation errors.

**Step 5: Commit**

```
git add Assets/Scripts/UI/CodexUI.cs Assets/Scripts/UI/SeedShopUI.cs Assets/Scripts/UI/GreenhouseUI.cs
git commit -m "refactor: adapt panel controllers for page lifecycle"
```

---

### Task 6: Convert SatchelUI to bottom sheet

**Files:**
- Modify: `Assets/Scripts/UI/SatchelUI.cs`

**Step 1: Update SatchelUI.cs**

Remove close button reference. Add scrim reference. Update `Show()` to display both scrim and panel. Update `Hide()` to hide both. Add scrim click-to-dismiss.

Key changes:
- Remove `closeButton` field and `closeButton.clicked += Hide`
- Add `scrim` field: `root.Q<VisualElement>("satchel-scrim")`
- `Show()`: set both `scrim` and `panel` to `DisplayStyle.Flex`
- `Hide()`: set both to `DisplayStyle.None`
- Register click on scrim to call `Hide()`

**Step 2: Verify compilation**

Run: `read_console`
Expected: No compilation errors.

**Step 3: Commit**

```
git add Assets/Scripts/UI/SatchelUI.cs
git commit -m "refactor: convert SatchelUI to bottom sheet overlay"
```

---

### Task 7: Add BottomNavUI component to scene and wire up

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity MCP)

**Step 1: Add BottomNavUI component to the "--- UI ---" GameObject**

Use `manage_components` to add `BottomNavUI` component to the UI GameObject (same one that has `HortusUI`).

**Step 2: Play-test in Unity**

Use `manage_editor` action `play` to enter play mode. Check:
- Pages render in the viewport
- Bottom nav tabs highlight correctly
- Tapping tabs switches pages with animation
- Swiping horizontally transitions between pages
- Satchel appears as bottom sheet when tapping empty hearth slot
- Locked tab does nothing
- Debug panel still accessible

Use `manage_editor` action `stop` to exit play mode.

**Step 3: Check console for runtime errors**

Run: `read_console`
Expected: No errors.

**Step 4: Commit scene changes**

```
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: add BottomNavUI component to UI GameObject"
```

---

### Task 8: Clean up dead code

**Files:**
- Modify: `Assets/Scripts/UI/TerrariumUI.cs` — no longer wired as a page (Hearth replaces it). Either remove or keep dormant for future zone expansion.

**Step 1: Evaluate TerrariumUI.cs**

TerrariumUI was the old "Greenhouse" panel showing all environments. Since the terrarium page now just shows the Hearth isometric view, TerrariumUI is currently unused. Keep the file but remove it from HortusUI initialization (already done in Task 4). It can be re-integrated when multi-zone support is added.

**Step 2: Remove old GreenhouseUI references from UXML if any remain**

Check that no orphaned elements reference the old panel IDs (`codex-close`, `shop-close`, `terrarium-close`, `satchel-close`, `greenhouse-close`). These were removed in the UXML rewrite (Task 2).

**Step 3: Commit if any cleanup was needed**

```
git add -A
git commit -m "chore: clean up dead panel toggle code"
```
