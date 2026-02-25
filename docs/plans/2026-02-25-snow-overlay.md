# Snow Overlay Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a `SnowOverlay` MonoBehaviour that renders animated snowflakes as filled circles via Painter2D on a `snow-overlay` VisualElement, triggered by `WeatherCondition.Snow`.

**Architecture:** Mirrors `RainOverlay` exactly. A new `SnowOverlay.cs` owns a `snow-overlay` VisualElement registered to `generateVisualContent`. Each snowflake has a sinusoidal horizontal drift using a per-flake phase and frequency. `WeatherOverlay` gains a `snowOverlay` field and shows it on `WeatherCondition.Snow`.

**Tech Stack:** Unity 6 UI Toolkit (`VisualElement`, `MeshGenerationContext`, `Painter2D`, `Painter2D.Arc`), C# MonoBehaviour, existing `WeatherService` event pattern.

---

### Task 1: Add `snow-overlay` VisualElement to UXML

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml`

**Step 1: Insert element after rain-overlay**

Open `Assets/UI/Documents/GardenRoot.uxml`. Find lines 38–40 (the `rain-overlay` element):

```xml
        <!-- Rain overlay (Painter2D, always screen-space) -->
        <ui:VisualElement name="rain-overlay" picking-mode="Ignore"
            style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

Add the following **immediately after** it:

```xml
        <!-- Snow overlay (Painter2D, always screen-space) -->
        <ui:VisualElement name="snow-overlay" picking-mode="Ignore"
            style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

**Step 2: Verify**

Open the file and confirm both `rain-overlay` and `snow-overlay` elements are present inside `app-shell`, before `bottom-nav`.

**Step 3: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml
git commit -m "feat: add snow-overlay VisualElement to GardenRoot.uxml"
```

---

### Task 2: Create `SnowOverlay.cs`

**Files:**
- Create: `Assets/Scripts/UI/SnowOverlay.cs`

**Step 1: Create the file**

Create `Assets/Scripts/UI/SnowOverlay.cs` with this complete content:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SnowOverlay : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _overlay;
        private Snowflake[] _flakes;
        private bool _active;
        private float _time;

        private const int k_Count = 120;
        private const float k_DriftAmp = 0.02f; // ±2% screen width sinusoidal sway

        private struct Snowflake
        {
            public float baseX;      // 0..1, horizontal center of oscillation
            public float y;          // 0..1, vertical position (0 = top)
            public float speed;      // screen-heights per second
            public float alpha;
            public float radius;     // pixels
            public float driftPhase; // per-flake phase offset (radians)
            public float driftFreq;  // oscillation cycles per second
        }

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = FindFirstObjectByType<UIDocument>();

            _overlay = uiDocument.rootVisualElement.Q("snow-overlay");
            if (_overlay == null)
            {
                Debug.LogError("[SnowOverlay] 'snow-overlay' VisualElement not found in UIDocument.", this);
                return;
            }
            _overlay.generateVisualContent += Paint;
            _flakes = new Snowflake[k_Count];
        }

        private void OnDestroy()
        {
            if (_overlay != null)
                _overlay.generateVisualContent -= Paint;
        }

        public void Show()
        {
            if (_overlay == null) return;
            _time = 0f;
            InitFlakes();
            _overlay.style.display = DisplayStyle.Flex;
            _active = true;
        }

        public void Hide()
        {
            if (_overlay == null) return;
            _active = false;
            _overlay.style.display = DisplayStyle.None;
        }

        private void InitFlakes()
        {
            for (int i = 0; i < k_Count; i++)
            {
                _flakes[i] = new Snowflake
                {
                    baseX      = Random.value,
                    y          = Random.value,                   // start spread across screen
                    speed      = Random.Range(0.10f, 0.22f),
                    alpha      = Random.Range(0.35f, 0.75f),
                    radius     = Random.Range(2f, 5f),
                    driftPhase = Random.Range(0f, Mathf.PI * 2f),
                    driftFreq  = Random.Range(0.2f, 0.5f),
                };
            }
        }

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            _time += dt;

            for (int i = 0; i < k_Count; i++)
            {
                _flakes[i].y += _flakes[i].speed * dt;
                if (_flakes[i].y > 1.05f)
                    ResetFlake(ref _flakes[i]);
            }
            _overlay.MarkDirtyRepaint();
        }

        private void ResetFlake(ref Snowflake f)
        {
            f.baseX      = Random.value;
            f.y          = -0.05f;
            f.driftPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Paint(MeshGenerationContext ctx)
        {
            if (!_active) return;

            float w = _overlay.resolvedStyle.width;
            float h = _overlay.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            var p = ctx.painter2D;

            for (int i = 0; i < k_Count; i++)
            {
                ref var f = ref _flakes[i];
                float driftX   = k_DriftAmp * Mathf.Sin(_time * f.driftFreq + f.driftPhase);
                float screenX  = (f.baseX + driftX) * w;
                float screenY  = f.y * h;

                p.fillColor = new Color(1f, 1f, 1f, f.alpha);
                p.BeginPath();
                p.Arc(new Vector2(screenX, screenY), f.radius,
                      Angle.Degrees(0), Angle.Degrees(360), ArcDirection.Clockwise);
                p.Fill();
            }
        }
    }
}
```

**Step 2: Compile**

Save the file and wait for Unity to compile. Check the console — no errors expected. The only new types used are `Painter2D.Arc`, `Painter2D.Fill`, `Painter2D.fillColor`, and `Angle.Degrees` — all standard Unity 6 UI Toolkit APIs.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/SnowOverlay.cs Assets/Scripts/UI/SnowOverlay.cs.meta
git commit -m "feat: SnowOverlay — Painter2D screen-space snowflakes with sinusoidal drift"
```

---

### Task 3: Add snow handling to `WeatherOverlay.cs`

**Files:**
- Modify: `Assets/Scripts/UI/WeatherOverlay.cs`

**Step 1: Add `snowOverlay` field**

In `WeatherOverlay.cs`, add a serialized field after `rainOverlay`:

```csharp
[SerializeField] private RainOverlay rainOverlay;
[SerializeField] private SnowOverlay snowOverlay;  // ← add this line
```

**Step 2: Hide snowOverlay on enable**

In `OnEnable()`, add `snowOverlay?.Hide();` alongside the existing `rainOverlay?.Hide();`:

```csharp
private void OnEnable()
{
    rainOverlay?.Hide();
    snowOverlay?.Hide();   // ← add this line
    // ... rest unchanged
```

**Step 3: Update `UpdateEffects` switch**

Change the `UpdateEffects` method to handle `WeatherCondition.Snow`:

```csharp
private void UpdateEffects(WeatherData w)
{
    switch (w.condition)
    {
        case WeatherCondition.Rain:
            rainOverlay?.Show(storm: false);
            snowOverlay?.Hide();
            break;
        case WeatherCondition.Storm:
            rainOverlay?.Show(storm: true);
            snowOverlay?.Hide();
            break;
        case WeatherCondition.Snow:
            rainOverlay?.Hide();
            snowOverlay?.Show();
            break;
        default:
            rainOverlay?.Hide();
            snowOverlay?.Hide();
            break;
    }
}
```

**Step 4: Compile**

Save and wait for compilation. No errors expected.

**Step 5: Commit**

```bash
git add Assets/Scripts/UI/WeatherOverlay.cs
git commit -m "feat: WeatherOverlay handles Snow condition via SnowOverlay"
```

---

### Task 4: Wire up in scene and verify

**Files:**
- Modify: `Assets/Scenes/Garden.unity` (via Unity Inspector/MCP)

**Step 1: Add `SnowOverlay` component**

Use `manage_components action=add` on the `WeatherOverlay` GameObject, component type `Garden.SnowOverlay`.

**Step 2: Assign UIDocument reference (optional)**

The `SnowOverlay` component has a `uiDocument` field. If null, `Start()` uses `FindFirstObjectByType<UIDocument>()` as fallback — acceptable. Optionally assign the `--- UI ---` GameObject explicitly.

**Step 3: Assign `snowOverlay` reference on `WeatherOverlay`**

On the `WeatherOverlay` component, set the `snowOverlay` field to the `SnowOverlay` component on the same GameObject.

**Step 4: Verify — Snow**

1. Enter Play mode.
2. Open the debug panel (⚙ button).
3. Set Condition dropdown to **Snow**, click **Apply**.
4. Expected: soft white dots of varying sizes fall slowly, each gently swaying left-right as they descend.
5. Set Condition to **Clear**, click **Apply**. Expected: snow stops immediately.

**Step 5: Verify — Rain still works**

Set Condition to **Rain**. Expected: rain streaks, no snow. Set to **Storm**: heavier rain, no snow.

**Step 6: Verify — Blizzard preset (optional)**

Click the **Blizzard** preset button. Expected: `WeatherCondition.Snow` is applied and snow overlay appears (same as step 4).

**Step 7: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat: wire SnowOverlay in scene, verified snow effect"
```
