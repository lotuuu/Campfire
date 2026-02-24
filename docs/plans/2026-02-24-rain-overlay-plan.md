# Rain Overlay Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Replace the broken world-space particle rain with a screen-space Painter2D rain drawn inside a full-screen UI Toolkit VisualElement.

**Architecture:** A new `RainOverlay` MonoBehaviour owns a `VisualElement` named `rain-overlay` in the existing `GardenRoot.uxml`. It uses the `generateVisualContent` callback to draw animated rain streaks with `Painter2D` each frame. `WeatherOverlay` is simplified to call `rainOverlay.Show/Hide` instead of controlling particle systems.

**Tech Stack:** Unity 6 UI Toolkit (`VisualElement`, `MeshGenerationContext`, `Painter2D`), C# MonoBehaviour, existing `WeatherService` event pattern.

---

### Task 1: Add `rain-overlay` to UXML and disable old particle children

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml` (add VisualElement before location-gate)
- Scene: disable `RainEffect`, `SnowEffect`, `WindLines` GameObjects under `WeatherOverlay`

**Step 1: Add VisualElement to UXML**

Open `Assets/UI/Documents/GardenRoot.uxml`. Find the `<!-- Location Gate -->` comment near the bottom. Insert this **immediately before** it:

```xml
    <!-- Rain overlay (Painter2D, always screen-space) -->
    <ui:VisualElement name="rain-overlay" picking-mode="Ignore"
        style="position: absolute; left: 0; right: 0; top: 0; bottom: 0; display: none;" />
```

**Step 2: Disable particle system children in scene**

Use Unity MCP or the Inspector to set `RainEffect`, `SnowEffect`, and `WindLines` GameObjects (children of the `WeatherOverlay` GameObject) to inactive (`set_active: false`). Do NOT delete them.

**Step 3: Verify**

Enter Play mode. No white rectangles should fall. The UI should look normal. Exit Play mode.

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml Assets/Scenes/SampleScene.unity
git commit -m "feat: add rain-overlay VisualElement, disable old particle children"
```

---

### Task 2: Create `RainOverlay.cs`

**Files:**
- Create: `Assets/Scripts/UI/RainOverlay.cs`

This MonoBehaviour manages an array of `RainDrop` structs, updates their Y position each frame, and paints them with `Painter2D` via `generateVisualContent`.

**Step 1: Create the file**

Create `Assets/Scripts/UI/RainOverlay.cs` with this complete content:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class RainOverlay : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _overlay;
        private RainDrop[] _drops;
        private int _activeCount;
        private bool _active;
        private float _dropWidth;
        private float _windAngle;
        private Color _dropColor;

        private const int k_StormCount = 180;

        private const float k_RainWidth  = 1.5f;
        private const float k_StormWidth = 2.0f;
        private const float k_RainAngle  = 0.12f;
        private const float k_StormAngle = 0.22f;

        private struct RainDrop
        {
            public float x;       // 0..1 normalized horizontal
            public float y;       // 0..1 normalized vertical (0 = top)
            public float speed;   // screen-heights per second
            public float alpha;
            public float length;  // fraction of screen height
        }

        private void Start()
        {
            if (uiDocument == null)
                uiDocument = FindFirstObjectByType<UIDocument>();

            _overlay = uiDocument.rootVisualElement.Q("rain-overlay");
            _overlay.generateVisualContent += Paint;
            _drops = new RainDrop[k_StormCount];
        }

        private void OnDestroy()
        {
            if (_overlay != null)
                _overlay.generateVisualContent -= Paint;
        }

        public void Show(bool storm)
        {
            _activeCount = storm ? k_StormCount : 80;
            _dropWidth   = storm ? k_StormWidth  : k_RainWidth;
            _windAngle   = storm ? k_StormAngle  : k_RainAngle;
            _dropColor   = storm
                ? new Color(0.71f, 0.82f, 1f)
                : new Color(0.78f, 0.88f, 1f);

            InitDrops(storm);
            _overlay.style.display = DisplayStyle.Flex;
            _active = true;
        }

        public void Hide()
        {
            _active = false;
            _overlay.style.display = DisplayStyle.None;
        }

        private void InitDrops(bool storm)
        {
            float minLen = storm ? 0.07f : 0.05f;
            float maxLen = storm ? 0.12f : 0.09f;
            float minSpd = storm ? 1.0f  : 0.7f;
            float maxSpd = storm ? 1.6f  : 1.1f;
            float minAlp = storm ? 0.20f : 0.15f;
            float maxAlp = storm ? 0.55f : 0.40f;

            for (int i = 0; i < _activeCount; i++)
            {
                _drops[i] = new RainDrop
                {
                    x      = Random.value,
                    y      = Random.value,   // start spread across screen
                    speed  = Random.Range(minSpd, maxSpd),
                    alpha  = Random.Range(minAlp, maxAlp),
                    length = Random.Range(minLen, maxLen),
                };
            }
        }

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _activeCount; i++)
            {
                _drops[i].y += _drops[i].speed * dt;
                if (_drops[i].y > 1f + _drops[i].length)
                    ResetDrop(ref _drops[i]);
            }
            _overlay.MarkDirtyRepaint();
        }

        private void ResetDrop(ref RainDrop d)
        {
            d.x = Random.value;
            d.y = -d.length;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            if (!_active) return;

            float w = _overlay.resolvedStyle.width;
            float h = _overlay.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;

            var p = ctx.painter2D;
            p.lineWidth = _dropWidth;
            p.lineCap   = LineCap.Round;

            for (int i = 0; i < _activeCount; i++)
            {
                ref var d = ref _drops[i];
                float x0  = d.x * w;
                float y0  = d.y * h;
                float len = d.length * h;
                float dx  = _windAngle * len;

                p.strokeColor = new Color(_dropColor.r, _dropColor.g, _dropColor.b, d.alpha);
                p.BeginPath();
                p.MoveTo(new Vector2(x0,      y0));
                p.LineTo(new Vector2(x0 + dx, y0 + len));
                p.Stroke();
            }
        }
    }
}
```

**Step 2: Compile**

Save the file and wait for Unity to compile. Check the console — no errors expected.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/RainOverlay.cs Assets/Scripts/UI/RainOverlay.cs.meta
git commit -m "feat: RainOverlay — Painter2D screen-space rain VisualElement"
```

---

### Task 3: Rewrite `WeatherOverlay.cs`

**Files:**
- Modify: `Assets/Scripts/UI/WeatherOverlay.cs`

Replace the entire file content — drop all `ParticleSystem` fields, keep the `WeatherService` subscription pattern, call `RainOverlay` instead.

**Step 1: Replace file content**

```csharp
using System.Collections;
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private RainOverlay rainOverlay;

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
        }

        private void Start()
        {
            rainOverlay?.Hide();

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
                UpdateEffects(WeatherService.Instance.CurrentWeather);
            }
            else
            {
                StartCoroutine(WaitForWeatherService());
            }
        }

        private IEnumerator WaitForWeatherService()
        {
            while (WeatherService.Instance == null)
                yield return null;
            WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
            WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
            UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            switch (w.condition)
            {
                case WeatherCondition.Rain:
                    rainOverlay?.Show(storm: false);
                    break;
                case WeatherCondition.Storm:
                    rainOverlay?.Show(storm: true);
                    break;
                default:
                    rainOverlay?.Hide();
                    break;
            }
        }
    }
}
```

**Step 2: Compile**

Save and wait for compilation. No errors expected — `RainOverlay`, `WeatherService`, `WeatherData`, `WeatherCondition` all exist.

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/WeatherOverlay.cs
git commit -m "refactor: WeatherOverlay uses RainOverlay instead of particle systems"
```

---

### Task 4: Wire up in scene and verify

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity` (via Unity Inspector/MCP)

**Step 1: Add `RainOverlay` component to `WeatherOverlay` GameObject**

Use `manage_components action=add` on the `WeatherOverlay` GameObject, component type `RainOverlay`.

**Step 2: Assign `UIDocument` reference**

The `RainOverlay` component has a `uiDocument` field. Find the `--- UI ---` GameObject (which has the `UIDocument` component) and assign it. If left null, `RainOverlay.Start()` will use `FindFirstObjectByType<UIDocument>()` as a fallback — acceptable for now.

**Step 3: Assign `rainOverlay` reference on `WeatherOverlay`**

On the `WeatherOverlay` component, set the `rainOverlay` field to the `RainOverlay` component on the same GameObject.

**Step 4: Verify — Rain**

1. Enter Play mode.
2. Open the debug panel (⚙ button).
3. Set Condition dropdown to **Rain**, click **Apply**.
4. Expected: thin, semi-transparent blue-white diagonal streaks filling the screen, falling smoothly.
5. Set Condition to **Clear**, click **Apply**. Expected: rain stops immediately.

**Step 5: Verify — Storm**

1. Set Condition to **Storm**, click **Apply**.
2. Expected: more streaks, slightly wider, steeper angle, slightly faster than rain.

**Step 6: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: wire RainOverlay in scene, verified rain + storm effects"
```
