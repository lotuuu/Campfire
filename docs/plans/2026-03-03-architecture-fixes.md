# Architecture Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 10 architectural issues covering data integrity, performance, UI staleness, dead code, server hardening, package pinning, testability, and asset pipeline.

**Architecture:** Atomic save writes + auto-save in SaveManager, cached Resources.LoadAll lookups, refresh-on-open for overlay panels, dead code removal, express-rate-limit on server, static helper extraction for testability.

**Tech Stack:** Unity 6 C# (namespace `Garden`), Node.js/Express, NUnit EditMode tests

---

### Task 1: Atomic Save Writes

**Files:**
- Modify: `Assets/Scripts/Services/SaveManager.cs`
- Modify: `Assets/Scripts/Services/SocialSaveManager.cs`

**Step 1: Implement atomic Flush in SaveManager**

Replace the `Flush()` and `Load()` methods in `SaveManager.cs`. The new pattern: write to `.tmp`, rename existing to `.bak`, rename `.tmp` to final path. On load, fall back to `.bak` if primary fails.

```csharp
// In SaveManager.cs, replace Flush() (line 32-40) with:
private void Flush()
{
    var json = JsonUtility.ToJson(Data, true);
    var tmpPath = SavePath + ".tmp";
    var bakPath = SavePath + ".bak";

    File.WriteAllText(tmpPath, json);

    if (File.Exists(SavePath))
        File.Replace(tmpPath, SavePath, bakPath);
    else
        File.Move(tmpPath, SavePath);

    if (SocialService.Instance != null && SocialService.Instance.IsSignedIn)
        _ = SocialService.Instance.PushVillageSnapshot();
}

// Replace Load() (line 42-57) with:
public void Load()
{
    if (TryLoadFrom(SavePath)) return;
    if (TryLoadFrom(SavePath + ".bak"))
    {
        Debug.LogWarning("SaveManager: Primary save corrupt, restored from backup.");
        return;
    }
    Data = new SaveData();
}

private bool TryLoadFrom(string path)
{
    if (!File.Exists(path)) return false;
    try
    {
        var json = File.ReadAllText(path);
        var loaded = JsonUtility.FromJson<SaveData>(json);
        if (loaded == null) return false;
        Data = loaded;
        return true;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"SaveManager: Failed to load {path} ({e.Message})");
        return false;
    }
}
```

**Step 2: Same pattern in SocialSaveManager**

Replace `Flush()` and `Load()` in `SocialSaveManager.cs` with the same atomic pattern:

```csharp
// Replace Flush() (line 32-36):
private void Flush()
{
    var json = JsonUtility.ToJson(Data, true);
    var tmpPath = SavePath + ".tmp";
    var bakPath = SavePath + ".bak";

    File.WriteAllText(tmpPath, json);

    if (File.Exists(SavePath))
        File.Replace(tmpPath, SavePath, bakPath);
    else
        File.Move(tmpPath, SavePath);
}

// Replace Load() (line 38-53):
public void Load()
{
    if (TryLoadFrom(SavePath)) return;
    if (TryLoadFrom(SavePath + ".bak"))
    {
        Debug.LogWarning("SocialSaveManager: Primary save corrupt, restored from backup.");
        return;
    }
    Data = new SocialData();
}

private bool TryLoadFrom(string path)
{
    if (!File.Exists(path)) return false;
    try
    {
        var json = File.ReadAllText(path);
        var loaded = JsonUtility.FromJson<SocialData>(json);
        if (loaded == null) return false;
        Data = loaded;
        return true;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"SocialSaveManager: Failed to load {path} ({e.Message})");
        return false;
    }
}
```

**Step 3: Verify compilation**

Check Unity console for errors after script changes.

**Step 4: Commit**

```
feat: atomic save writes with backup recovery in SaveManager and SocialSaveManager
```

---

### Task 2: Auto-Save and Application Lifecycle

**Files:**
- Modify: `Assets/Scripts/Services/SaveManager.cs`

**Step 1: Add auto-save timer and lifecycle hooks**

Add `Update()` method with 30-second timer, plus `OnApplicationPause` and `OnApplicationFocus` handlers.

```csharp
// Add field after line 14 (_isDirty):
private const float AutoSaveIntervalSeconds = 30f;
private float _autoSaveTimer;

// Add new methods after LateUpdate():
private void Update()
{
    _autoSaveTimer += Time.deltaTime;
    if (_autoSaveTimer >= AutoSaveIntervalSeconds && _isDirty)
    {
        _autoSaveTimer = 0f;
        _isDirty = false;
        Flush();
    }
}

private void OnApplicationPause(bool paused)
{
    if (paused && _isDirty)
    {
        _isDirty = false;
        Flush();
    }
}

private void OnApplicationFocus(bool hasFocus)
{
    if (!hasFocus && _isDirty)
    {
        _isDirty = false;
        Flush();
    }
}
```

Note: `LateUpdate` still handles the normal deferred write. `Update` catches the case where mana accumulates for >30s without any explicit `Save()` call (FlameManager adds mana every frame but never calls `Save()`). The lifecycle hooks catch app backgrounding/force-quit.

**Step 2: Verify compilation**

Check Unity console for errors.

**Step 3: Commit**

```
feat: add periodic auto-save and app lifecycle flush to SaveManager
```

---

### Task 3: Cache Resources.LoadAll in PlotManager

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs`

**Step 1: Add seed cache and populate in Awake**

```csharp
// Add field after line 16 (RainTriggerMinutes):
private static Dictionary<string, SeedData> _seedCache;

// In Awake() (line 81-85), add after Instance = this:
private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;

    if (_seedCache == null)
    {
        _seedCache = new Dictionary<string, SeedData>();
        foreach (var seed in Resources.LoadAll<SeedData>("Seeds"))
            _seedCache[seed.seedName] = seed;
    }
}

// Add using directive at top:
using System.Collections.Generic;  // already exists (line 2)
```

**Step 2: Replace LoadSeed with cache lookup**

```csharp
// Replace LoadSeed (lines 308-315):
private static SeedData LoadSeed(string seedName)
{
    if (string.IsNullOrEmpty(seedName)) return null;
    if (_seedCache != null && _seedCache.TryGetValue(seedName, out var seed))
        return seed;
    return null;
}
```

**Step 3: Verify compilation**

Check Unity console for errors.

**Step 4: Commit**

```
perf: cache seed data lookup in PlotManager instead of per-frame Resources.LoadAll
```

---

### Task 4: Cache Resources.LoadAll in GardenManager

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs`

**Step 1: Add plant data cache and populate in Awake**

```csharp
// Add using at top (already has System.Collections.Generic):
// Add field after line 12 (OnYieldCollected):
private static Dictionary<string, GardenPlantData> _plantCache;

// Replace Awake (lines 14-18):
private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;

    if (_plantCache == null)
    {
        _plantCache = new Dictionary<string, GardenPlantData>();
        foreach (var plant in Resources.LoadAll<GardenPlantData>("GardenPlants"))
            _plantCache[plant.plantName] = plant;
    }
}
```

**Step 2: Replace LoadPlantData with cache lookup**

```csharp
// Replace LoadPlantData (lines 112-119):
private static GardenPlantData LoadPlantData(string plantName)
{
    if (string.IsNullOrEmpty(plantName)) return null;
    if (_plantCache != null && _plantCache.TryGetValue(plantName, out var plant))
        return plant;
    return null;
}
```

**Step 3: Verify compilation**

Check Unity console for errors.

**Step 4: Commit**

```
perf: cache plant data lookup in GardenManager instead of per-frame Resources.LoadAll
```

---

### Task 5: Cache MerchantData in MerchantUI

**Files:**
- Modify: `Assets/Scripts/UI/MerchantUI.cs`

**Step 1: Cache MerchantData array in Initialize**

```csharp
// Add field after existing fields (around line 12):
private MerchantData[] allMerchants;

// In Initialize (line 14-19), add at end:
public void Initialize(VisualElement root)
{
    merchantFlavor = root.Q<Label>("merchant-flavor");
    merchantList = root.Q("merchant-list");
    offerTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/MerchantOfferRow");
    allMerchants = Resources.LoadAll<MerchantData>("Merchants");
}
```

**Step 2: Replace per-Refresh load with cached lookup**

In `Refresh()`, replace `var allMerchants = Resources.LoadAll<MerchantData>("Merchants");` (line 39) with a lookup from the cached field:

```csharp
// Replace line 39-44:
MerchantData merchantData = null;
foreach (var md in allMerchants)
{
    if (md.merchantName == merchant.merchantName) { merchantData = md; break; }
}
```

Just remove the local `var allMerchants = Resources.LoadAll<MerchantData>("Merchants");` line — the field `allMerchants` is already populated.

**Step 3: Verify compilation**

Check Unity console for errors.

**Step 4: Commit**

```
perf: cache MerchantData in MerchantUI.Initialize instead of loading on every Refresh
```

---

### Task 6: Refresh Panels on Open

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

**Step 1: Add Refresh calls before OpenOverlay**

In `CampFireUI.cs`, the bottom nav callbacks (lines 94-96) open panels without refreshing. Add refresh calls. Also add refresh for the Apotheke building tap (line 138).

```csharp
// Replace lines 94-96:
bottomNav.OnApothekeClicked += () =>
{
    apotheke?.Refresh();
    OpenOverlay("Seeds", apothekePanel);
};
bottomNav.OnLettersClicked += () => OpenOverlay("Mail", lettersPanel);
bottomNav.OnBuildClicked += () =>
{
    build?.Refresh();
    OpenOverlay("Craft", buildPanel);
};

// Replace line 138:
campsiteView.OnApothekeTapped += () =>
{
    apotheke?.Refresh();
    OpenOverlay("Seeds", apothekePanel);
};
```

Note: MerchantUI already refreshes via `ShowMerchant()`, QuestUI already calls `Refresh()` (line 105), LettersUI doesn't have stale data concerns. Only Build and Apotheke need fixing.

**Step 2: Verify compilation**

Check Unity console for errors.

**Step 3: Commit**

```
fix: refresh Build and Apotheke panels when opened to prevent stale data
```

---

### Task 7: Remove Dead Code

**Files:**
- Delete: `Assets/Scripts/Data/TriggerCondition.cs` and `Assets/Scripts/Data/TriggerCondition.cs.meta`
- Modify: `Assets/Scripts/Managers/MallumManager.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs`
- Modify: `Assets/Tests/EditMode/TestSaveData.cs`

**Step 1: Delete TriggerCondition.cs**

```bash
rm Assets/Scripts/Data/TriggerCondition.cs Assets/Scripts/Data/TriggerCondition.cs.meta
```

No other `.cs` files reference `TriggerCondition` — only docs/plans.

**Step 2: Remove dead MallumConfig field and OnFlameUpgraded from MallumManager**

```csharp
// In MallumManager.cs:
// Remove line 11:    [SerializeField] private MallumConfig config;
// Remove line 16:    public MallumConfig Config => config;
// Remove lines 43-45 (OnFlameUpgraded method — empty no-op body)
// In Start() lines 34-35, remove the FlameManager subscription:
//     if (FlameManager.Instance != null)
//         FlameManager.Instance.OnFlameUpgraded += OnFlameUpgraded;
// In OnDestroy() lines 39-40, remove the unsubscribe:
//     if (FlameManager.Instance != null)
//         FlameManager.Instance.OnFlameUpgraded -= OnFlameUpgraded;
```

Note: Keep `MallumConfig.cs` itself — the ScriptableObject class still exists and the test `TestMallumData.cs` tests it. Just remove the unused reference from MallumManager.

**Step 3: Remove lastManaCollectTime from SaveData**

```csharp
// In SaveData.cs, delete line 18:
//     public float lastManaCollectTime;
```

**Step 4: Update TestSaveData to remove lastManaCollectTime references**

```csharp
// In TestSaveData.cs:
// Delete line 20:    Assert.AreEqual(0f, data.lastManaCollectTime);
// Delete line 30:    lastManaCollectTime = 1000f,
```

**Step 5: Verify compilation and run tests**

Run all EditMode tests to confirm nothing breaks.

**Step 6: Commit**

```
chore: remove dead code — TriggerCondition, unused MallumConfig ref, lastManaCollectTime
```

---

### Task 8: Server Rate Limiting

**Files:**
- Modify: `server/package.json`
- Modify: `server/src/index.js`

**Step 1: Install express-rate-limit**

```bash
cd server && npm install express-rate-limit
```

**Step 2: Add rate limiters to index.js**

```javascript
// At top of index.js, after existing requires:
const rateLimit = require('express-rate-limit');

const globalLimiter = rateLimit({
  windowMs: 60 * 1000,
  max: 100,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many requests, please try again later' }
});

const registerLimiter = rateLimit({
  windowMs: 60 * 1000,
  max: 5,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many registration attempts, please try again later' }
});

// After app.use(express.json()):
app.use(globalLimiter);

// Replace auth route mount:
app.use('/auth', registerLimiter, authRoutes);
```

Note: `registerLimiter` stacks with `globalLimiter`. Both apply to `/auth/register`.

**Step 3: Commit**

```
feat: add rate limiting to server — 100 req/min global, 5 req/min on /auth
```

---

### Task 9: Village Snapshot Validation

**Files:**
- Modify: `server/src/routes/villages.js`

**Step 1: Add size and type validation to PUT /village**

```javascript
// In villages.js, replace the PUT handler:
router.put('/', async (req, res) => {
  const { snapshot } = req.body;
  if (snapshot === undefined) {
    return res.status(400).json({ error: 'snapshot is required' });
  }

  if (typeof snapshot !== 'object' || snapshot === null || Array.isArray(snapshot)) {
    return res.status(400).json({ error: 'snapshot must be a JSON object' });
  }

  const snapshotStr = JSON.stringify(snapshot);
  if (snapshotStr.length > 102400) {
    return res.status(413).json({ error: 'Village snapshot too large (max 100KB)' });
  }

  try {
    await pool.query(
      `INSERT INTO villages (player_uid, snapshot, updated_at)
       VALUES ($1, $2, NOW())
       ON CONFLICT (player_uid)
       DO UPDATE SET snapshot = $2, updated_at = NOW()`,
      [req.user.uid, snapshotStr]
    );
    res.json({ message: 'Village updated' });
  } catch (err) {
    res.status(500).json({ error: 'Failed to update village' });
  }
});
```

**Step 2: Commit**

```
feat: validate village snapshot type and enforce 100KB size limit
```

---

### Task 10: Pin Unity Packages

**Files:**
- Modify: `Packages/manifest.json`

**Step 1: Get current commit SHAs**

```bash
# Get the current commit SHA for unity-mcp
git ls-remote https://github.com/CoplayDev/unity-mcp.git refs/heads/main | cut -f1

# Get the current commit SHA for UnityNativeShare
git ls-remote https://github.com/yasirkula/UnityNativeShare.git HEAD | cut -f1
```

**Step 2: Pin to specific commits in manifest.json**

Replace the floating `#main` references with the exact commit SHAs retrieved above:

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#<SHA>",
"com.yasirkula.nativeshare": "https://github.com/yasirkula/UnityNativeShare.git#<SHA>",
```

**Step 3: Commit**

```
chore: pin unity-mcp and nativeshare packages to specific commit SHAs
```

---

### Task 11: Extract Static Helpers — FlameManager

**Files:**
- Modify: `Assets/Scripts/Managers/FlameManager.cs`
- Create: `Assets/Tests/EditMode/TestFlameManager.cs`

**Step 1: Extract AccumulateMana as static pure function**

```csharp
// In FlameManager.cs, add static helper:
public static float AccumulateMana(float currentMana, float manaPerSecond, float deltaTime)
{
    return currentMana + manaPerSecond * deltaTime;
}

// Replace Update() (line 38-40):
private void Update()
{
    SaveManager.Instance.Data.mana = AccumulateMana(
        SaveManager.Instance.Data.mana, ManaPerSecond, Time.deltaTime);
}
```

**Step 2: Write test**

```csharp
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestFlameManager
    {
        [Test]
        public void AccumulateMana_AddsCorrectAmount()
        {
            float result = FlameManager.AccumulateMana(100f, 2f, 0.5f);
            Assert.AreEqual(101f, result, 0.001f);
        }

        [Test]
        public void AccumulateMana_ZeroDelta_NoChange()
        {
            float result = FlameManager.AccumulateMana(50f, 5f, 0f);
            Assert.AreEqual(50f, result, 0.001f);
        }
    }
}
```

**Step 3: Verify compilation and run tests**

**Step 4: Commit**

```
refactor: extract FlameManager.AccumulateMana as static helper with tests
```

---

### Task 12: Extract Static Helpers — GardenManager

**Files:**
- Modify: `Assets/Scripts/Managers/GardenManager.cs`
- Modify: `Assets/Tests/EditMode/TestGardenManager.cs`

**Step 1: Extract static helpers**

```csharp
// Add to GardenManager.cs:

public static float GetGrowthProgress(GardenSave garden, float growthDurationHours, DateTime utcNow)
{
    if (garden.mature) return 1f;
    if (string.IsNullOrEmpty(garden.plantTimeUtc)) return 0f;
    var plantTime = DateTime.Parse(garden.plantTimeUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind);
    float elapsed = (float)(utcNow - plantTime).TotalHours;
    return Mathf.Clamp01(elapsed / growthDurationHours);
}

public static bool CheckYieldReady(GardenSave garden, float yieldIntervalHours, DateTime utcNow)
{
    if (!garden.mature || string.IsNullOrEmpty(garden.lastYieldTimeUtc)) return false;
    var lastYield = DateTime.Parse(garden.lastYieldTimeUtc, null,
        System.Globalization.DateTimeStyles.RoundtripKind);
    return (float)(utcNow - lastYield).TotalHours >= yieldIntervalHours;
}
```

**Step 2: Refactor instance methods to use static helpers**

```csharp
// Replace instance GetGrowthProgress (lines 49-64):
public float GetGrowthProgress(int gardenIndex)
{
    var data = SaveManager.Instance.Data;
    if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return 0f;
    var garden = data.gardens[gardenIndex];
    if (string.IsNullOrEmpty(garden.plantName)) return 0f;

    var plantData = LoadPlantData(garden.plantName);
    if (plantData == null) return 0f;

    return GetGrowthProgress(garden, plantData.growthDurationHours, GameTime.UtcNow);
}

// In CheckGrowthAndYields (line 76), replace inline growth check:
if (!garden.mature && GetGrowthProgress(i) >= 1f)

// In yield check (lines 84-98), replace inline interval check:
if (garden.mature && !string.IsNullOrEmpty(garden.lastYieldTimeUtc))
{
    var plantData = LoadPlantData(garden.plantName);
    if (plantData == null) continue;

    if (CheckYieldReady(garden, plantData.yieldIntervalHours, GameTime.UtcNow))
    {
        AddItem(data, plantData.yieldItem, plantData.yieldAmount);
        garden.lastYieldTimeUtc = GameTime.UtcNow.ToString("o");
        changed = true;
        OnYieldCollected?.Invoke(i, plantData.yieldItem, plantData.yieldAmount);
    }
}
```

**Step 3: Replace test file with static-helper-based tests**

```csharp
using System;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestGardenManager
    {
        [Test]
        public void GetGrowthProgress_HalfwayThrough_Returns05()
        {
            var garden = new GardenSave
            {
                plantTimeUtc = DateTime.UtcNow.AddHours(-12).ToString("o"),
                mature = false
            };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(0.5f, progress, 0.05f);
        }

        [Test]
        public void GetGrowthProgress_Mature_Returns1()
        {
            var garden = new GardenSave { mature = true };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(1f, progress);
        }

        [Test]
        public void GetGrowthProgress_NoPlantTime_Returns0()
        {
            var garden = new GardenSave { mature = false, plantTimeUtc = null };
            float progress = GardenManager.GetGrowthProgress(garden, 24f, DateTime.UtcNow);
            Assert.AreEqual(0f, progress);
        }

        [Test]
        public void CheckYieldReady_PastInterval_ReturnsTrue()
        {
            var garden = new GardenSave
            {
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-13).ToString("o")
            };
            Assert.IsTrue(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }

        [Test]
        public void CheckYieldReady_BeforeInterval_ReturnsFalse()
        {
            var garden = new GardenSave
            {
                mature = true,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-6).ToString("o")
            };
            Assert.IsFalse(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }

        [Test]
        public void CheckYieldReady_NotMature_ReturnsFalse()
        {
            var garden = new GardenSave
            {
                mature = false,
                lastYieldTimeUtc = DateTime.UtcNow.AddHours(-100).ToString("o")
            };
            Assert.IsFalse(GardenManager.CheckYieldReady(garden, 12f, DateTime.UtcNow));
        }
    }
}
```

**Step 4: Verify compilation and run tests**

**Step 5: Commit**

```
refactor: extract GardenManager static helpers with tests
```

---

### Task 13: Extract Static Helpers — VisitorSystem

**Files:**
- Modify: `Assets/Scripts/Managers/VisitorSystem.cs`
- Create: `Assets/Tests/EditMode/TestVisitorSystem.cs`

**Step 1: Extract DetermineGift as static**

```csharp
// In VisitorSystem.cs, change DetermineGift from private to public static:
public static VisitorGift DetermineGift(List<VaseSave> vases)
{
    int totalWater = 0;
    foreach (var v in vases) totalWater += v.currentWater;
    if (totalWater <= 2)
        return new VisitorGift { type = VisitorGiftType.Water, amount = 3 };
    return new VisitorGift { type = VisitorGiftType.Seed, seedName = "Chamomile", amount = 1 };
}

// Update caller in CheckForVisitor (line 35):
var gift = DetermineGift(data.vases);
```

Add `using System.Collections.Generic;` at the top.

**Step 2: Extract ApplyGift as static**

```csharp
// Change ApplyGift from private to public static:
public static void ApplyGift(List<VaseSave> vases, VisitorGift gift)
{
    switch (gift.type)
    {
        case VisitorGiftType.Water:
            foreach (var vase in vases)
            {
                int space = vase.capacity - vase.currentWater;
                if (space > 0)
                {
                    int fill = Math.Min(space, gift.amount);
                    vase.currentWater += fill;
                    gift.amount -= fill;
                    if (gift.amount <= 0) break;
                }
            }
            break;
        case VisitorGiftType.Seed:
            ApothekeManager.Instance?.AddSeed(gift.seedName, gift.amount);
            break;
    }
}

// Update caller in CheckForVisitor (line 36):
ApplyGift(data.vases, gift);
```

**Step 3: Write tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestVisitorSystem
    {
        [Test]
        public void DetermineGift_LowWater_GivesWater()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 1, capacity = 5 },
                new VaseSave { currentWater = 1, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Water, gift.type);
            Assert.AreEqual(3, gift.amount);
        }

        [Test]
        public void DetermineGift_HighWater_GivesSeed()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 5, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Seed, gift.type);
            Assert.AreEqual("Chamomile", gift.seedName);
        }

        [Test]
        public void DetermineGift_ExactlyTwo_GivesWater()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 2, capacity = 5 }
            };
            var gift = VisitorSystem.DetermineGift(vases);
            Assert.AreEqual(VisitorGiftType.Water, gift.type);
        }

        [Test]
        public void ApplyGift_Water_FillsVases()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 3, capacity = 5 },
                new VaseSave { currentWater = 0, capacity = 5 }
            };
            var gift = new VisitorGift { type = VisitorGiftType.Water, amount = 3 };
            VisitorSystem.ApplyGift(vases, gift);
            Assert.AreEqual(5, vases[0].currentWater);
            Assert.AreEqual(1, vases[1].currentWater);
        }

        [Test]
        public void ApplyGift_Water_DoesNotOverfill()
        {
            var vases = new List<VaseSave>
            {
                new VaseSave { currentWater = 4, capacity = 5 }
            };
            var gift = new VisitorGift { type = VisitorGiftType.Water, amount = 10 };
            VisitorSystem.ApplyGift(vases, gift);
            Assert.AreEqual(5, vases[0].currentWater);
        }
    }
}
```

**Step 4: Verify compilation and run tests**

**Step 5: Commit**

```
refactor: extract VisitorSystem static helpers with tests
```

---

### Task 14: Git LFS Setup

**Files:**
- Create: `.gitattributes`

**IMPORTANT:** This task rewrites git history. Confirm with user before executing `git lfs migrate import`.

**Step 1: Install Git LFS if not already installed**

```bash
git lfs install
```

**Step 2: Create .gitattributes**

```
# Images
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.jpeg filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.bmp filter=lfs diff=lfs merge=lfs -text
*.gif filter=lfs diff=lfs merge=lfs -text

# Audio
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text

# 3D Models
*.fbx filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text

# Fonts
*.ttf filter=lfs diff=lfs merge=lfs -text
*.otf filter=lfs diff=lfs merge=lfs -text
```

**Step 3: ASK USER before proceeding**

The next step (`git lfs migrate import`) rewrites all commits containing these file types. This changes every commit SHA in the repo. All local branches and any remote must be force-pushed.

If user approves:

```bash
git lfs migrate import --include="*.png,*.jpg,*.jpeg,*.psd,*.tga,*.bmp,*.gif,*.wav,*.mp3,*.ogg,*.fbx,*.obj,*.ttf,*.otf" --everything
```

**Step 4: Commit .gitattributes (if not already included in migration)**

```
chore: add .gitattributes for Git LFS tracking of binary assets
```

---

### Task 15: Run All Tests

**Step 1: Run full EditMode test suite**

Use Unity Test Runner or MCP `run_tests` with `mode: "EditMode"`.

**Step 2: Verify all tests pass**

Expected: all existing tests pass, plus the new tests from Tasks 11-13.

**Step 3: Check Unity console for compilation errors or warnings**

---

### Task 16: Final Commit Summary

Create one final commit for any remaining cleanup, or verify all tasks committed. Run `git log --oneline -15` to confirm the commit history.

Expected commits (oldest first):
1. `feat: atomic save writes with backup recovery`
2. `feat: add periodic auto-save and app lifecycle flush`
3. `perf: cache seed data lookup in PlotManager`
4. `perf: cache plant data lookup in GardenManager`
5. `perf: cache MerchantData in MerchantUI`
6. `fix: refresh Build and Apotheke panels when opened`
7. `chore: remove dead code`
8. `feat: add rate limiting to server`
9. `feat: validate village snapshot`
10. `chore: pin Unity packages to commit SHAs`
11. `refactor: extract FlameManager.AccumulateMana with tests`
12. `refactor: extract GardenManager static helpers with tests`
13. `refactor: extract VisitorSystem static helpers with tests`
14. `chore: Git LFS setup` (if approved)
