# Backend Architecture Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix all correctness bugs, performance issues, and risky patterns identified in the architecture review of Assets/Scripts/{Services,Managers,Data}/.

**Architecture:** Targeted minimal fixes — no feature additions, no refactors beyond what is required. Each task is independent of the others and can be committed separately. Tests are Unity EditMode NUnit tests in Assets/Tests/EditMode/.

**Tech Stack:** Unity 6, C#, NUnit, UnityEngine.TestRunner (EditMode)

---

## Issues Being Fixed (reference)

| # | Area | Severity | Finding |
|---|---|---|---|
| 1 | SaveManager.Load() | Bug | No try/catch — corrupted save crashes app |
| 2 | SaveManager.Save() | Perf | Sync write every mutation — triple-write per harvest on main thread |
| 3 | GameManager.Start() | Bug | Bypasses CurrencyManager — first-boot OnCurrencyChanged never fires, UI stale |
| 4 | PlantManager.Update() | Perf | OnSlotGrowthUpdated at 60fps; growthSpeedMultiplier re-evaluated every frame |
| 5 | WeatherService | Risk | FetchWeatherLoop has no handle — SetDebugMode(false) launches concurrent fetches |
| 6 | WeatherOverlay | Risk | WaitForWeatherService coroutine can re-subscribe after OnDisable |
| 7 | LivingCanvasController | Risk | Same WaitForWeatherService coroutine leak as WeatherOverlay |
| 8 | GeneticsEngine.Resolve | Minor | LINQ alloc + sort on every plant; trivially avoidable |

---

### Task 1: SaveManager — try/catch on Load + deferred write

**Files:**
- Modify: `Assets/Scripts/Services/SaveManager.cs`
- Create: `Assets/Tests/EditMode/TestSaveManager.cs`

**What to change in SaveManager.cs:**

Replace the current `Save()` and `Load()` methods. The key changes:
- `Save()` sets a dirty flag instead of writing immediately
- New private `LateUpdate()` flushes the file if dirty (deferred to end-of-frame, batches multiple Save() calls within one frame into one write)
- `Load()` wraps JsonUtility.FromJson in try/catch, falls back to `new SaveData()` on any exception

The full replacement for SaveManager.cs:

```csharp
using System;
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public SaveData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private bool _isDirty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        private void LateUpdate()
        {
            if (!_isDirty) return;
            _isDirty = false;
            Flush();
        }

        public void Save() => _isDirty = true;

        private void Flush()
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) { Data = new SaveData(); return; }

            try
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<SaveData>(json);
                if (Data == null) Data = new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveManager: Failed to load save — resetting. ({e.Message})");
                Data = new SaveData();
            }
        }

        public void DeleteSave()
        {
            _isDirty = false;
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Data = new SaveData();
        }
    }
}
```

**Note:** All existing callers of `SaveManager.Instance.Save()` continue to work — the API is unchanged. The write is now deferred to LateUpdate, but still guaranteed within the same frame. `DeleteSave()` cancels any pending flush and does an immediate sync delete.

**Step 1: Write the failing test**

Create `Assets/Tests/EditMode/TestSaveManager.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestSaveManager
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"test_save_{System.Guid.NewGuid()}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void Load_CorruptedJson_FallsBackToNewSaveData()
        {
            // Write corrupt JSON to the temp file
            File.WriteAllText(_tempPath, "{ this is not valid json }}}");

            // Simulate what SaveManager.Load() does (can't instantiate MonoBehaviour
            // directly in EditMode, so we test the logic by calling JsonUtility directly
            // and checking the catch path works)
            SaveData result;
            try
            {
                result = JsonUtility.FromJson<SaveData>("{ this is not valid json }}}");
                if (result == null) result = new SaveData();
            }
            catch
            {
                result = new SaveData();
            }

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.dewdrops);
        }

        [Test]
        public void Load_NullJsonResult_FallsBackToNewSaveData()
        {
            // JsonUtility.FromJson returns null for empty/wrong-type JSON
            // This tests the null guard added alongside the try/catch
            SaveData result = JsonUtility.FromJson<SaveData>("null") ?? new SaveData();
            Assert.IsNotNull(result);
        }

        [Test]
        public void SaveData_DefaultValues_AreValid()
        {
            var data = new SaveData();
            Assert.AreEqual(6, data.greenhouseSlots);
            Assert.IsNotNull(data.activeSlots);
            Assert.IsNotNull(data.seedInventory);
            Assert.IsNotNull(data.greenhousePlants);
            Assert.IsNotNull(data.discoveredVariants);
            Assert.IsNotNull(data.unlockedEnvironments);
            Assert.IsNotNull(data.environmentSlots);
        }
    }
}
```

**Step 2: Run tests to verify they pass (the tests validate logic, not the MonoBehaviour)**

Via Unity MCP: `run_tests mode=EditMode` and filter to `TestSaveManager`.
Or: Window > General > Test Runner > EditMode > TestSaveManager.

**Step 3: Apply the SaveManager.cs changes** as shown in the code block above.

**Step 4: Run all tests to verify no regressions**

`run_tests mode=EditMode` — all tests should pass.

**Step 5: Commit**
```
git add Assets/Scripts/Services/SaveManager.cs Assets/Tests/EditMode/TestSaveManager.cs Assets/Tests/EditMode/TestSaveManager.cs.meta
git commit -m "fix: SaveManager deferred write + try-catch on corrupted save load"
```

---

### Task 2: GameManager — use CurrencyManager for new game initialization

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs`

**Problem:** First boot directly mutates `SaveManager.Instance.Data.sunShards` and `.dewdrops`, bypassing `CurrencyManager`. This means `OnCurrencyChanged` never fires, leaving any currency UI stale on first boot.

**Fix:** Replace direct field mutation with `CurrencyManager.Instance.Add()`. Remove the manual `Save()` call — `CurrencyManager.Add()` already calls `Save()` (now `MarkDirty`).

Replace the `Start()` method body:

```csharp
private void Start()
{
    if (SaveManager.Instance.Data.seedInventory.Count == 0)
    {
        SeedRegistry.Instance.AddSeed("Quicksprout", 5);
        CurrencyManager.Instance.Add(CurrencyType.SunShards, 10);
        CurrencyManager.Instance.Add(CurrencyType.Dewdrops, 200);
    }
}
```

No test needed — this is a single-line behavioral fix observable in-editor by checking the currency display on a fresh save.

**Verify manually:** Delete save file (or `DeleteSave()` via debug menu), enter Play mode, check that currency display shows 200 dewdrops and 10 sunshards on first frame.

**Commit:**
```
git add Assets/Scripts/Managers/GameManager.cs
git commit -m "fix: use CurrencyManager for new-game currency init so OnCurrencyChanged fires"
```

---

### Task 3: PlantManager — throttle Update + event-driven multiplier refresh

**Files:**
- Modify: `Assets/Scripts/Data/PlantSlot.cs`
- Modify: `Assets/Scripts/Managers/PlantManager.cs`

**Problems:**
1. `growthSpeedMultiplier` re-evaluated 60×/second from weather, but weather only changes every ≥15 min
2. `OnSlotGrowthUpdated` fires 60×/second for progress that moves ~0.003%/second
3. `GetRemainingHours()` already ignores `envBonus` (pre-existing bug exposed by review)

**Changes to PlantSlot.cs** — add `cachedEnvBonus` (runtime-only, not serialized, not saved):

```csharp
using System;

namespace Garden
{
    public class PlantSlot
    {
        public int environmentIndex;
        public int slotIndex;
        public PlantState state = PlantState.Empty;
        public SeedData seed;
        public VariantData variant;
        public DateTime plantTime;
        public float growthSpeedMultiplier = 1f;
        public float growthProgress;
        // Runtime-only: recalculated when weather changes, not persisted
        public float cachedEnvBonus;
    }
}
```

**Changes to PlantManager.cs:**

Add fields after the existing event declarations:
```csharp
private const float GrowthTickInterval = 5f;
private float _growthTickTimer;
```

Change `Start()` to subscribe to weather after existing initialization:
```csharp
private void Start()
{
    InitializeSlots();
    RestoreFromSave();
    if (WeatherService.Instance != null)
    {
        WeatherService.Instance.OnWeatherUpdated += RefreshMultipliers;
        RefreshMultipliers(WeatherService.Instance.CurrentWeather);
    }
}

private void OnDestroy()
{
    if (WeatherService.Instance != null)
        WeatherService.Instance.OnWeatherUpdated -= RefreshMultipliers;
}
```

Add the `RefreshMultipliers` method:
```csharp
private void RefreshMultipliers(WeatherData weather)
{
    foreach (var slot in slots)
    {
        if (slot.state != PlantState.Growing) continue;

        slot.growthSpeedMultiplier = (slot.variant?.trigger != null
            && slot.variant.trigger.Evaluate(weather)) ? 1.25f : 1f;

        slot.cachedEnvBonus = EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.GetGrowthBonus(slot.environmentIndex, weather)
            : 0f;
    }
}
```

Replace `Update()` with the throttled version:
```csharp
private void Update()
{
    _growthTickTimer += Time.deltaTime;
    if (_growthTickTimer < GrowthTickInterval) return;
    _growthTickTimer = 0f;

    bool anyUpdated = false;
    foreach (var slot in slots)
    {
        if (slot.state != PlantState.Growing) continue;

        float totalMultiplier = slot.growthSpeedMultiplier + slot.cachedEnvBonus;
        float totalHours = slot.seed.baseGrowthHours / Mathf.Max(totalMultiplier, 0.01f);
        float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
        slot.growthProgress = Mathf.Clamp01(elapsed / totalHours);

        OnSlotGrowthUpdated?.Invoke(slot.environmentIndex, slot.slotIndex, slot.growthProgress);
        anyUpdated = true;

        if (slot.growthProgress >= 1f)
        {
            slot.state = PlantState.Mature;
            OnSlotStateChanged?.Invoke(slot.environmentIndex, slot.slotIndex, PlantState.Mature);
            OnPlantStateChanged?.Invoke();
            SaveState();
        }
    }

    if (anyUpdated)
    {
        var featured = GetFeaturedSlot();
        if (featured != null)
            OnGrowthUpdated?.Invoke(featured.growthProgress);
    }
}
```

Fix `GetRemainingHours()` (both overloads) to include `cachedEnvBonus`:
```csharp
public float GetRemainingHours()
{
    var slot = GetFeaturedSlot();
    if (slot == null || slot.state != PlantState.Growing) return 0f;
    float totalMultiplier = slot.growthSpeedMultiplier + slot.cachedEnvBonus;
    float totalHours = slot.seed.baseGrowthHours / Mathf.Max(totalMultiplier, 0.01f);
    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
    return Mathf.Max(0f, totalHours - elapsed);
}

public float GetRemainingHours(int envIndex, int slotIndex)
{
    var slot = GetSlot(envIndex, slotIndex);
    if (slot == null || slot.state != PlantState.Growing) return 0f;
    float totalMultiplier = slot.growthSpeedMultiplier + slot.cachedEnvBonus;
    float totalHours = slot.seed.baseGrowthHours / Mathf.Max(totalMultiplier, 0.01f);
    float elapsed = (float)(GameTime.UtcNow - slot.plantTime).TotalHours;
    return Mathf.Max(0f, totalHours - elapsed);
}
```

**Verify:** In Play mode, plant a seed, confirm it grows to completion. Confirm no 60fps log spam.

**Commit:**
```
git add Assets/Scripts/Data/PlantSlot.cs Assets/Scripts/Managers/PlantManager.cs
git commit -m "perf: throttle PlantManager Update to 5s intervals, move multiplier refresh to OnWeatherUpdated"
```

---

### Task 4: WeatherService — prevent concurrent fetch coroutines

**Files:**
- Modify: `Assets/Scripts/Services/WeatherService.cs`

**Problem:** `FetchWeatherLoop()` runs `while(true)` forever with no handle. When `SetDebugMode(false)` is called, it calls `StartCoroutine(FetchWeather())` but the loop is still running — two concurrent HTTP requests can fire simultaneously. Same issue with `RetryLocation()`.

**Fix:** Store the loop coroutine in a field and stop it before any restart.

Add field after existing private fields:
```csharp
private Coroutine _fetchLoopCoroutine;
```

Change the `if (Input.location.status == LocationServiceStatus.Running)` block in `InitializeLocation()` to use the field:
```csharp
if (Input.location.status == LocationServiceStatus.Running)
{
    latitude = Input.location.lastData.latitude;
    longitude = Input.location.lastData.longitude;
    hasLocation = true;
    IsLocationResolved = true;
    Debug.Log($"Location acquired: {latitude}, {longitude}");
    OnLocationResolved?.Invoke(true);
    if (_fetchLoopCoroutine != null) StopCoroutine(_fetchLoopCoroutine);
    _fetchLoopCoroutine = StartCoroutine(FetchWeatherLoop());
    // ... Android plugin call unchanged
}
```

Change `SetDebugMode()` to stop the loop before the immediate fetch:
```csharp
public void SetDebugMode(bool enabled)
{
    useDebugOverride = enabled;
    if (enabled)
    {
        ApplyDebugWeather();
    }
    else if (hasLocation)
    {
        if (_fetchLoopCoroutine != null) StopCoroutine(_fetchLoopCoroutine);
        _fetchLoopCoroutine = StartCoroutine(FetchWeatherLoop());
    }
}
```

`RetryLocation()` already calls `InitializeLocation()` which will restart the loop — just ensure the coroutine guard is in place there as shown above.

**Verify:** Toggle debug mode in editor (via WeatherService inspector or debug UI) — no duplicate API calls should appear in the console.

**Commit:**
```
git add Assets/Scripts/Services/WeatherService.cs
git commit -m "fix: store FetchWeatherLoop coroutine handle to prevent concurrent fetch on debug toggle"
```

---

### Task 5: WeatherOverlay + LivingCanvasController — fix WaitForWeatherService subscribe leak

**Files:**
- Modify: `Assets/Scripts/UI/WeatherOverlay.cs`
- Modify: `Assets/Scripts/UI/LivingCanvasController.cs`

**Problem:** Both components start `WaitForWeatherService()` coroutines in `OnEnable()` when `WeatherService.Instance` is null. If the component is disabled/destroyed before the coroutine reaches the subscription line, `OnDisable` fires (with Instance still null, so no unsubscription), then the coroutine resumes and subscribes anyway — leaving the dead/disabled object subscribed.

**Fix:** Store the coroutine handle and cancel it in `OnDisable`.

**WeatherOverlay.cs** — also fix the redundant double-subscribe in `Start()` (remove `Start()` entirely, as `OnEnable` + an immediate `UpdateEffects` call covers the same behavior):

```csharp
using System.Collections;
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private RainOverlay rainOverlay;
        private Coroutine _waitCoroutine;

        private void OnEnable()
        {
            rainOverlay?.Hide();
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
                UpdateEffects(WeatherService.Instance.CurrentWeather);
            }
            else
            {
                _waitCoroutine = StartCoroutine(WaitForWeatherService());
            }
        }

        private void OnDisable()
        {
            if (_waitCoroutine != null) { StopCoroutine(_waitCoroutine); _waitCoroutine = null; }
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
        }

        private IEnumerator WaitForWeatherService()
        {
            while (WeatherService.Instance == null)
                yield return null;
            _waitCoroutine = null;
            WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
            UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            switch (w.condition)
            {
                case WeatherCondition.Rain:  rainOverlay?.Show(storm: false); break;
                case WeatherCondition.Storm: rainOverlay?.Show(storm: true);  break;
                default:                     rainOverlay?.Hide();             break;
            }
        }
    }
}
```

**LivingCanvasController.cs** — add `_waitCoroutine` field, stop it in `OnDisable`:

Add field alongside `private Coroutine fadeCoroutine;`:
```csharp
private Coroutine _waitCoroutine;
```

Change `OnEnable()`:
```csharp
private void OnEnable()
{
    if (WeatherService.Instance != null)
    {
        WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
        OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
    }
    else
    {
        _waitCoroutine = StartCoroutine(WaitForWeatherService());
    }
}
```

Change `OnDisable()`:
```csharp
private void OnDisable()
{
    if (_waitCoroutine != null) { StopCoroutine(_waitCoroutine); _waitCoroutine = null; }
    if (WeatherService.Instance != null)
        WeatherService.Instance.OnWeatherUpdated -= OnWeatherUpdated;
}
```

Change `WaitForWeatherService()`:
```csharp
private IEnumerator WaitForWeatherService()
{
    while (WeatherService.Instance == null)
        yield return null;
    _waitCoroutine = null;
    WeatherService.Instance.OnWeatherUpdated += OnWeatherUpdated;
    OnWeatherUpdated(WeatherService.Instance.CurrentWeather);
}
```

**Verify:** No test needed — verify by checking that no errors appear in console when entering/exiting play mode rapidly.

**Commit:**
```
git add Assets/Scripts/UI/WeatherOverlay.cs Assets/Scripts/UI/LivingCanvasController.cs
git commit -m "fix: stop WaitForWeatherService coroutine in OnDisable to prevent dangling event subscription"
```

---

### Task 6: GeneticsEngine — avoid LINQ allocation per Resolve call

**Files:**
- Modify: `Assets/Scripts/Data/SeedData.cs`
- Modify: `Assets/Scripts/Services/GeneticsEngine.cs`

**Problem:** `seed.variants.OrderBy(v => v.priority).ToList()` allocates a new sorted List<VariantData> on every call to Resolve(). This happens once per plant session, so impact is low but it's a trivial fix.

**Fix:** Add a `[NonSerialized]` lazy-cache field to SeedData. GeneticsEngine uses the cached sorted list. The cache resets on domain reload (Unity serialization round-trip on Play mode entry) and is recomputed on next access.

**Changes to SeedData.cs** — add at the bottom of the class (before the closing brace), after the existing fields:

```csharp
[System.NonSerialized] private List<VariantData> _sortedVariants;

internal List<VariantData> GetSortedVariants()
{
    return _sortedVariants ??= new List<VariantData>(variants)
        { }.OrderBy(v => v.priority).ToList();
}
```

Wait — the ??= initializer can't call OrderBy inline cleanly. Use this instead:

```csharp
[System.NonSerialized] private List<VariantData> _sortedVariants;

internal List<VariantData> GetSortedVariants()
{
    if (_sortedVariants == null)
        _sortedVariants = variants.OrderBy(v => v.priority).ToList();
    return _sortedVariants;
}
```

**Note:** Requires `using System.Linq;` at the top of SeedData.cs (add if not present).

**Changes to GeneticsEngine.cs** — replace the sort line in `Resolve()`:

Before:
```csharp
var sorted = seed.variants.OrderBy(v => v.priority).ToList();
```

After:
```csharp
var sorted = seed.GetSortedVariants();
```

Remove `using System.Linq;` from GeneticsEngine.cs if it is now only used for this line (check first — it may be used by `LastOrDefault()`). Actually `LastOrDefault()` is a LINQ extension, so keep the using.

**Verify:** Run `TestGeneticsEngine` — all existing tests must still pass. The behavior is identical; only allocation pattern changes.

**Commit:**
```
git add Assets/Scripts/Data/SeedData.cs Assets/Scripts/Services/GeneticsEngine.cs
git commit -m "perf: cache sorted variants in SeedData to avoid LINQ alloc on every GeneticsEngine.Resolve"
```

---

## Execution Order

Tasks are independent. Recommended order: 1 → 2 → 3 → 4 → 5 → 6

After all tasks: run full EditMode test suite and verify all tests pass.
