# Hearth Isometric View Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the CSS-only rotated-square hearth visualization with real isometric voxel tile sprites (voxelTile_55) rendered in Unity world space, with transparent UI Toolkit buttons overlaid for interaction.

**Architecture:** A new `HearthIsometricView` MonoBehaviour spawns SpriteRenderer tile GameObjects in world space arranged in an isometric east-row. `HearthViewUI` dynamically creates and positions absolute UI Toolkit overlay buttons by projecting tile world positions into panel space each frame. Plant visuals are rendered as colored circle child sprites on each tile rather than in UI Toolkit.

**Tech Stack:** Unity 6 URP 2D, UI Toolkit (`RuntimePanelUtils.ScreenToPanel`), SpriteRenderer, Kenney isometricBlocks voxelTile_55

---

### Task 1: Verify voxelTile_55 sprite import settings

The sprite must be imported as type "Sprite (2D and UI)", not "Texture".

**Files:**
- Read: `Assets/ThirdParty/kenney_isometricBlocks/PNG/Voxel tiles/voxelTile_55.png.meta`

**Step 1: Check the meta file**

Open `Assets/ThirdParty/kenney_isometricBlocks/PNG/Voxel tiles/voxelTile_55.png.meta` and look for the `textureType` field.
- `textureType: 8` = Sprite ✓
- `textureType: 0` = Default (needs fixing)

**Step 2: If it needs fixing, set it in the Unity Inspector**

In Unity, select `Assets/ThirdParty/kenney_isometricBlocks/PNG/Voxel tiles/voxelTile_55.png` → in Inspector, set Texture Type to "Sprite (2D and UI)" → click Apply.

**Step 3: Commit**

```bash
git add "Assets/ThirdParty/kenney_isometricBlocks/PNG/Voxel tiles/voxelTile_55.png.meta"
git commit -m "fix: set voxelTile_55 sprite import type"
```

---

### Task 2: Create `HearthIsometricView.cs`

**Files:**
- Create: `Assets/Scripts/UI/HearthIsometricView.cs`

**Step 1: Write the script**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class HearthIsometricView : MonoBehaviour
    {
        [SerializeField] private Sprite tileSprite;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private Vector3 gridAnchor = new Vector3(0f, -0.3f, 0f);

        private readonly List<GameObject> tiles = new();
        private readonly List<SpriteRenderer> plantRenderers = new();
        private Camera mainCam;

        private const int HearthEnvIndex = 0;

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Start()
        {
            if (EnvironmentManager.Instance == null) return;
            int count = EnvironmentManager.Instance.GetActiveSlotCount(HearthEnvIndex);
            RebuildGrid(count);
            EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != HearthEnvIndex) return;
            SpawnTile(tiles.Count);
            RecenterGrid();
        }

        public void RebuildGrid(int count)
        {
            foreach (var t in tiles) if (t) Destroy(t);
            tiles.Clear();
            plantRenderers.Clear();

            for (int i = 0; i < count; i++)
                SpawnTile(i);

            RecenterGrid();
        }

        private void SpawnTile(int index)
        {
            var tileGO = new GameObject($"HearthTile_{index}");
            tileGO.transform.SetParent(transform, false);

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;

            // Plant visual: colored circle disc, child of tile
            var plantGO = new GameObject("PlantDisc");
            plantGO.transform.SetParent(tileGO.transform, false);
            plantGO.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var plantSr = plantGO.AddComponent<SpriteRenderer>();
            plantSr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            plantSr.sortingLayerName = sortingLayerName;
            plantSr.sortingOrder = baseSortingOrder + 1;
            plantSr.color = Color.clear;

            // Scale disc to ~35% of tile width
            if (tileSprite != null)
            {
                float tileW = tileSprite.rect.width / tileSprite.pixelsPerUnit;
                plantGO.transform.localScale = Vector3.one * (tileW * 0.35f);
            }

            plantGO.SetActive(false);
            tiles.Add(tileGO);
            plantRenderers.Add(plantSr);

            PositionTile(index);
        }

        private void PositionTile(int index)
        {
            if (index >= tiles.Count || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            // Isometric east-row: each step = +w/2 right, -h/4 down
            tiles[index].transform.localPosition = new Vector3(index * w * 0.5f, index * -h * 0.25f, 0f);
        }

        private void RecenterGrid()
        {
            if (tiles.Count == 0 || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            int n = tiles.Count;
            // Center of tile cluster in local space
            float localCenterX = (n - 1) * w * 0.25f;
            float localCenterY = (n - 1) * -h * 0.125f;
            transform.position = gridAnchor - new Vector3(localCenterX, localCenterY, 0f);
        }

        /// <summary>Screen-space center of a tile in pixels (bottom-left origin, Y-up).</summary>
        public Vector2 GetTileScreenCenter(int index)
        {
            if (index < 0 || index >= tiles.Count || mainCam == null)
                return Vector2.zero;
            return mainCam.WorldToScreenPoint(tiles[index].transform.position);
        }

        /// <summary>Screen-space bounds of a tile sprite in pixels (bottom-left origin, Y-up).</summary>
        public Rect GetTileScreenBounds(int index)
        {
            if (index < 0 || index >= tiles.Count || tileSprite == null || mainCam == null)
                return Rect.zero;
            var worldPos = tiles[index].transform.position;
            float ppu = tileSprite.pixelsPerUnit;
            float halfW = tileSprite.rect.width * 0.5f / ppu;
            float halfH = tileSprite.rect.height * 0.5f / ppu;
            var bl = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3(-halfW, -halfH));
            var tr = (Vector2)mainCam.WorldToScreenPoint(worldPos + new Vector3(halfW, halfH));
            return new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
        }

        public void SetPlantVisual(int index, PlantState state, Color color)
        {
            if (index < 0 || index >= plantRenderers.Count) return;
            var go = plantRenderers[index].gameObject;
            if (state == PlantState.Empty)
            {
                go.SetActive(false);
                return;
            }
            go.SetActive(true);
            plantRenderers[index].color = color;
        }

        public void SetPlantScale(int index, float uniformScale)
        {
            if (index < 0 || index >= plantRenderers.Count) return;
            plantRenderers[index].transform.localScale = Vector3.one * uniformScale;
        }
    }
}
```

**Step 2: Compile check**

In Unity, check the console for errors after the script compiles. There should be none.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/HearthIsometricView.cs Assets/Scripts/UI/HearthIsometricView.cs.meta
git commit -m "feat: add HearthIsometricView world-space tile renderer"
```

---

### Task 3: Add HearthIso GameObject to scene

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity Editor)

**Step 1: Create the GameObject in Unity Editor**

In the scene Hierarchy:
- Right-click → Create Empty → name it `HearthIso`
- Set Transform position to `(0, 0, 0)` (RecenterGrid will adjust it at runtime)

**Step 2: Add component + wire sprite**

- Select `HearthIso` → Add Component → `HearthIsometricView`
- Drag `Assets/ThirdParty/kenney_isometricBlocks/PNG/Voxel tiles/voxelTile_55.png` into the `Tile Sprite` field
- Set `Sorting Layer Name` to `Default`, `Base Sorting Order` to `0`
- Leave `Grid Anchor` at `(0, -0.3, 0)` for now (adjust after Play mode visual check)

**Step 3: Verify sorting order is correct**

LivingCanvas uses a MeshRenderer. Check its sorting order in the Inspector:
- Select `LivingCanvas` → check `Mesh Renderer` sorting layer and order
- The `HearthIso` tiles need a higher sort order than LivingCanvas
- If LivingCanvas is "Background" layer, "Default" layer will render on top ✓
- If LivingCanvas is "Default" layer with order -1, set `HearthIso` baseSortingOrder to 0 ✓

**Step 4: Commit**

```bash
git add Assets/Scenes/SampleScene.unity Assets/Scenes/SampleScene.unity.meta
git commit -m "feat: add HearthIso GameObject to scene"
```

---

### Task 4: Rewrite `HearthViewUI.cs`

**Files:**
- Modify: `Assets/Scripts/UI/HearthViewUI.cs`

**Step 1: Replace the file entirely**

```csharp
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HearthViewUI : MonoBehaviour
    {
        private const int HearthEnvIndex = 0;

        [SerializeField] private HearthIsometricView isometricView;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private readonly List<Button> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();

        private bool initialized;

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                int count = EnvironmentManager.Instance.GetActiveSlotCount(HearthEnvIndex);
                for (int i = 0; i < count; i++)
                    AddSlotButton(i);
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            }

            initialized = true;
            RefreshAllSlots();
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
        }

        private void AddSlotButton(int slotIndex)
        {
            var btn = new Button();
            btn.AddToClassList("hearth-slot-overlay");
            btn.style.position = Position.Absolute;

            var label = new Label("Tap to Plant");
            label.AddToClassList("hearth-slot-label");
            btn.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("hearth-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("hearth-progress-fill");
            progressBar.Add(fill);
            btn.Add(progressBar);

            int idx = slotIndex;
            btn.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

            terrariumPage.Add(btn);
            slotButtons.Add(btn);
            labels.Add(label);
            progressFills.Add(fill);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != HearthEnvIndex) return;
            AddSlotButton(slotButtons.Count);
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || isometricView == null || terrariumPage == null) return;

            // Re-project tile positions each frame (handles screen resize)
            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (i < labels.Count && labels[i] != null)
                        labels[i].text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                }
                else if (slot.state == PlantState.Mature)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    isometricView.SetPlantScale(i, pulse);
                }
            }
        }

        private void PositionButton(int i)
        {
            if (i >= slotButtons.Count || terrariumPage?.panel == null) return;

            var screenRect = isometricView.GetTileScreenBounds(i);
            var panel = terrariumPage.panel;

            // Convert screen-space corners (bottom-left origin) to panel space (top-left origin)
            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            // In panel space Y increases downward, so tr.y < bl.y (tr is visually higher)
            float panelLeft = Mathf.Min(bl.x, tr.x);
            float panelTop = Mathf.Min(bl.y, tr.y);
            float panelWidth = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            // Make coords relative to terrariumPage
            var pageOrigin = terrariumPage.worldBound;
            slotButtons[i].style.left = panelLeft - pageOrigin.x;
            slotButtons[i].style.top = panelTop - pageOrigin.y;
            slotButtons[i].style.width = panelWidth;
            slotButtons[i].style.height = panelHeight;
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < slotButtons.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null || i >= slotButtons.Count) return;

            var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, i);
            if (slot == null) return;

            var label = i < labels.Count ? labels[i] : null;
            var fill = i < progressFills.Count ? progressFills[i] : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (label != null) label.text = "Tap to Plant";
                    if (fill != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    break;

                case PlantState.Growing:
                    float hours = PlantManager.Instance.GetRemainingHours(HearthEnvIndex, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing, slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    if (label != null) label.text = "Harvest!";
                    if (fill != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("hearth-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature, slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(HearthEnvIndex, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty: OnEmptySlotTapped?.Invoke(HearthEnvIndex, slotIndex); break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(HearthEnvIndex, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != HearthEnvIndex) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
```

**Step 2: Compile check — verify no errors in Unity console**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/HearthViewUI.cs
git commit -m "feat: rewrite HearthViewUI with dynamic slots and isometric overlay buttons"
```

---

### Task 5: Update UXML and USS

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`
- Modify: `Assets/UI/Styles/Hearth.uss`

**Step 1: Remove static hearth slots from UXML**

In `GardenRoot.uxml`, find the `terrarium-page` block and replace:

```xml
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
```

With:

```xml
<!-- Page 2: Terrarium (Hearth isometric view) -->
<ui:VisualElement name="terrarium-page" class="page-content" style="display: none;">
    <ui:Label name="hearth-title" text="The Hearth" class="hearth-title" />
    <!-- Slot overlay buttons are added at runtime by HearthViewUI -->
</ui:VisualElement>
```

**Step 2: Update Hearth.uss**

Replace the entire file with:

```css
/* Hearth: Isometric farm – overlay buttons positioned at runtime by HearthViewUI */

.hearth-title {
    color: var(--color-text-dim);
    font-size: var(--font-sm);
    margin-top: var(--spacing-md);
    margin-left: var(--spacing-md);
    -unity-text-align: middle-left;
}

/* Transparent overlay button sized and positioned to cover each isometric tile */
.hearth-slot-overlay {
    background-color: rgba(0, 0, 0, 0);
    border-width: 0;
    padding: 0;
    flex-direction: column;
    justify-content: flex-end;
    align-items: center;
}

.hearth-slot-overlay.hearth-slot-mature {
    border-color: var(--color-highlight);
    border-width: 2px;
    border-radius: var(--radius-sm);
}

.hearth-slot-label {
    color: var(--color-text-dim);
    font-size: var(--font-xs);
    -unity-text-align: middle-center;
    margin-bottom: 4px;
}

.hearth-progress-bar {
    width: 80%;
    height: 6px;
    background-color: rgba(20, 35, 50, 0.50);
    border-radius: 4px;
    overflow: hidden;
    margin-bottom: var(--spacing-xs);
}

.hearth-progress-fill {
    height: 100%;
    width: 0;
    background-color: var(--color-text-accent);
    border-radius: 4px;
    transition-property: width;
    transition-duration: 0.3s;
}
```

**Step 3: Compile check**

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml Assets/UI/Styles/Hearth.uss
git commit -m "feat: update UXML and USS for isometric hearth overlay"
```

---

### Task 6: Wire HearthViewUI → HearthIsometricView in scene, handle page visibility

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity Editor)
- Modify: `Assets/Scripts/UI/HortusUI.cs`

**Step 1: Wire the inspector reference in Unity Editor**

- Select `--- UI ---` in the Hierarchy
- Find the `HearthViewUI` component in the Inspector
- Drag the `HearthIso` GameObject into the `Isometric View` field

**Step 2: Hide tiles when not on the terrarium page**

In `HortusUI.cs`, find `OnPageChanged` and add tile visibility toggling. The terrarium page is index 2:

```csharp
// Add field at top of class (after existing fields):
[SerializeField] private HearthIsometricView hearthIsoView;

// In OnPageChanged method, replace the existing body:
private void OnPageChanged(int pageIndex)
{
    // Show/hide isometric tiles only on terrarium page
    if (hearthIsoView != null)
        hearthIsoView.gameObject.SetActive(pageIndex == 2);

    switch (pageIndex)
    {
        case 0: codexUI?.Show(); break;
        case 1: seedShopUI?.Show(); break;
        case 3: greenhouseUI?.Show(); break;
    }
}
```

**Step 3: Wire hearthIsoView in the Unity Inspector**

- Select `--- UI ---` → find `HortusUI` component
- Drag `HearthIso` into the `Hearth Iso View` field

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/HortusUI.cs Assets/Scenes/SampleScene.unity
git commit -m "feat: wire HearthIsometricView to HortusUI, hide tiles on non-terrarium pages"
```

---

### Task 7: Play mode verification + visual tuning

**Step 1: Enter Play mode in Unity**

Expected: one grass block (voxelTile_55) visible in the center of the terrarium page, with "Tap to Plant" label below it, a thin progress bar at the bottom.

**Step 2: Check for console errors**

Common issues:
- `NullReferenceException` in `PositionButton` → `terrariumPage.panel` is null on first frame (harmless, resolved after layout pass)
- Tiles not visible → check sorting layer (see Task 3 Step 3)
- Tiles render behind background → increase `baseSortingOrder` on HearthIso component

**Step 3: Tune gridAnchor if tiles are off-center**

On the `HearthIso` GameObject, adjust `Grid Anchor` Y value:
- Tiles too high: decrease Y (e.g. `-0.5`)
- Tiles too low: increase Y (e.g. `0.0`)

**Step 4: Test planting**

Tap the tile → Satchel opens → select a seed → plant → tile should show a colored disc growing.

**Step 5: Test page navigation**

Swipe to Codex → tiles should disappear. Swipe back to Terrarium → tiles reappear.

**Step 6: Commit any tuning changes**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "fix: tune HearthIso grid anchor position"
```

---

### Task 8: Final commit

```bash
git add -A
git commit -m "$(cat <<'EOF'
feat: isometric hearth visualization using voxelTile_55

Replaces CSS-only rotated-square with real world-space SpriteRenderer
tiles arranged in an isometric east-row. HearthViewUI dynamically
creates transparent overlay buttons sized to each tile's screen bounds
via RuntimePanelUtils.ScreenToPanel. Plant state shown as colored disc
child sprites. Tiles hidden when navigating away from terrarium page.

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>
EOF
)"
```
