# Environment Switcher Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let players switch between purchased environments by re-tapping the terrarium tab, which reveals a pill-button bar that slides up from above the main nav.

**Architecture:** Add `activeEnvironmentIndex` to save + `EnvironmentManager`; make `BackyardIsometricView` read its tile sprite from `EnvironmentData`; add a new `EnvironmentSwitcherBar` MonoBehaviour that wires to re-tap detection in `BottomNavUI` and is orchestrated by `HortusUI`.

**Tech Stack:** Unity 6 UI Toolkit (USS transitions, `TransitionEndEvent`), C# MonoBehaviour singletons, Unity ScriptableObject YAML assets.

---

### Task 1: Data model — SaveData + EnvironmentData

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs:27`
- Modify: `Assets/Scripts/Data/EnvironmentData.cs:18`
- Test: `Assets/Tests/EditMode/TestSaveManager.cs`

**Step 1: Add `activeEnvironmentIndex` to SaveData**

In `SaveData.cs`, add this field after `environmentSlots`:

```csharp
// v4: active environment shown in terrarium
public int activeEnvironmentIndex;
```

**Step 2: Add `tileSprite` to EnvironmentData**

In `EnvironmentData.cs`, add a `[Header("Visuals")]` section before the `[Header("Growth Bonus")]` block:

```csharp
[Header("Visuals")]
public Sprite tileSprite;
```

The final file should look like:

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewEnvironment", menuName = "Garden/Environment Data")]
    public class EnvironmentData : ScriptableObject
    {
        public string environmentName;
        public int slotCount = 1;
        public int maxSlotCount = 4;
        public int unlockCostGold;
        public int slotUnlockCostGold = 500;

        [Header("Visuals")]
        public Sprite tileSprite;

        [Header("Growth Bonus")]
        [Range(0f, 0.5f)] public float growthSpeedBonus;
        public TriggerCondition bonusCondition;

        [Header("Features")]
        public bool allowsCrossPollination;
    }
}
```

**Step 3: Write failing tests in TestSaveManager.cs**

Add two tests at the end of the `TestSaveManager` class:

```csharp
[Test]
public void SaveData_Default_ActiveEnvironmentIndex_IsZero()
{
    var data = new SaveData();
    Assert.AreEqual(0, data.activeEnvironmentIndex);
}

[Test]
public void SaveData_RoundTrip_Preserves_ActiveEnvironmentIndex()
{
    var data = new SaveData { activeEnvironmentIndex = 2 };
    var json = JsonUtility.ToJson(data, true);
    var restored = JsonUtility.FromJson<SaveData>(json);
    Assert.AreEqual(2, restored.activeEnvironmentIndex);
}
```

**Step 4: Run tests via Unity Test Runner**

Open Window > General > Test Runner > EditMode tab. Run the two new tests. They should PASS immediately since `int` fields default to 0 in C# and `JsonUtility` serializes them automatically.

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/EnvironmentData.cs Assets/Tests/EditMode/TestSaveManager.cs
git commit -m "feat: add activeEnvironmentIndex to SaveData, tileSprite to EnvironmentData"
```

---

### Task 2: EnvironmentManager — active environment support

**Files:**
- Modify: `Assets/Scripts/Managers/EnvironmentManager.cs:1-117`

**Step 1: Add `ActiveEnvironmentIndex`, `OnActiveEnvironmentChanged`, and `SetActiveEnvironment`**

Replace the entire `EnvironmentManager.cs` content with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        private List<EnvironmentData> environments = new();

        public event Action<int> OnEnvironmentUnlocked;
        public event Action<int> OnSlotUnlocked;
        public event Action<int> OnActiveEnvironmentChanged;

        public IReadOnlyList<EnvironmentData> Environments => environments;
        public int ActiveEnvironmentIndex { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var loaded = Resources.LoadAll<EnvironmentData>("Config/Environments");
            environments.AddRange(loaded);
            environments.Sort((a, b) => a.unlockCostGold.CompareTo(b.unlockCostGold));
        }

        private void Start()
        {
            // Restore active env from save — must run after SaveManager.Awake
            int saved = SaveManager.Instance.Data.activeEnvironmentIndex;
            ActiveEnvironmentIndex = (saved >= 0 && saved < environments.Count && IsUnlocked(saved))
                ? saved : 0;
        }

        public bool SetActiveEnvironment(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            if (!IsUnlocked(envIndex)) return false;
            if (ActiveEnvironmentIndex == envIndex) return false;

            ActiveEnvironmentIndex = envIndex;
            SaveManager.Instance.Data.activeEnvironmentIndex = envIndex;
            SaveManager.Instance.Save();
            OnActiveEnvironmentChanged?.Invoke(envIndex);
            return true;
        }

        public bool IsUnlocked(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            if (env.unlockCostGold == 0) return true;
            return SaveManager.Instance.Data.unlockedEnvironments.Contains(env.environmentName);
        }

        public bool Unlock(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            if (IsUnlocked(envIndex)) return false;

            var env = environments[envIndex];
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, env.unlockCostGold))
                return false;

            SaveManager.Instance.Data.unlockedEnvironments.Add(env.environmentName);
            SaveManager.Instance.Save();
            OnEnvironmentUnlocked?.Invoke(envIndex);
            return true;
        }

        public float GetGrowthBonus(int envIndex, WeatherData weather)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0f;
            var env = environments[envIndex];
            if (env.bonusCondition == null) return 0f;
            return env.bonusCondition.Evaluate(weather) ? env.growthSpeedBonus : 0f;
        }

        public int GetTotalUnlockedSlots()
        {
            int total = 0;
            for (int i = 0; i < environments.Count; i++)
            {
                if (IsUnlocked(i))
                    total += environments[i].slotCount;
            }
            return total;
        }

        public int GetSlotCount(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0;
            return environments[envIndex].slotCount;
        }

        public int GetActiveSlotCount(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0;
            var env = environments[envIndex];
            var entry = SaveManager.Instance.Data.environmentSlots
                .Find(e => e.environmentName == env.environmentName);
            return entry != null ? entry.unlockedSlots : env.slotCount;
        }

        public bool CanUnlockSlot(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            return GetActiveSlotCount(envIndex) < env.maxSlotCount
                && CurrencyManager.Instance.CanAfford(CurrencyType.Gold, env.slotUnlockCostGold);
        }

        public bool UnlockSlot(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            int current = GetActiveSlotCount(envIndex);
            if (current >= env.maxSlotCount) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Gold, env.slotUnlockCostGold))
                return false;

            var save = SaveManager.Instance.Data;
            var entry = save.environmentSlots.Find(e => e.environmentName == env.environmentName);
            if (entry == null)
            {
                entry = new EnvironmentSlotsSave { environmentName = env.environmentName, unlockedSlots = env.slotCount };
                save.environmentSlots.Add(entry);
            }
            entry.unlockedSlots++;
            SaveManager.Instance.Save();

            PlantManager.Instance.AddSlot(envIndex, current);
            OnSlotUnlocked?.Invoke(envIndex);
            return true;
        }
    }
}
```

Key changes vs original:
- `OnActiveEnvironmentChanged` event added
- `ActiveEnvironmentIndex` property added
- Initialization of `ActiveEnvironmentIndex` moved to `Start()` (needs SaveManager, which also runs `Awake`)
- `SetActiveEnvironment(int)` method added

**Step 2: Check for compilation errors**

In Unity, check the Console for any errors after the script recompiles. There should be none.

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/EnvironmentManager.cs
git commit -m "feat: EnvironmentManager active env index, SetActiveEnvironment, OnActiveEnvironmentChanged"
```

---

### Task 3: BackyardIsometricView — data-driven tile sprite

**Files:**
- Modify: `Assets/Scripts/UI/BackyardIsometricView.cs`

**Step 1: Remove the hardcoded Inspector field and constant; read sprite from EnvironmentData**

Key changes:
- Remove `[SerializeField] private Sprite tileSprite` (line 8) — replace with a plain `private Sprite tileSprite` (set at runtime)
- Remove `private const int BackyardEnvIndex = 0` (line 29)
- Change `Start()` to call `SetEnvironment(EnvironmentManager.Instance.ActiveEnvironmentIndex)` and subscribe to `OnActiveEnvironmentChanged`
- Add `SetEnvironment(int envIndex)` method
- Update `OnSlotUnlocked` to use `EnvironmentManager.Instance.ActiveEnvironmentIndex`

Replace the entire file with:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class BackyardIsometricView : MonoBehaviour
    {
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int baseSortingOrder = 0;
        [SerializeField] private Vector3 gridAnchor = new Vector3(0f, -0.3f, 0f);
        [SerializeField] private GameObject[] plantPrefabs;
        [SerializeField] private float plantScale = 0.3f;

        private readonly List<GameObject> tiles = new();
        private readonly List<GameObject> plantGOs = new();
        private readonly List<float> plantBaseScales = new();
        [SerializeField] private GameObject[] consumablePrefabs;

        private readonly Dictionary<int, List<GameObject>> _slotConsumableGOs = new();
        private readonly Dictionary<ConsumableType, GameObject> _envConsumableGOs = new();

        private Camera mainCam;
        private float slideOffsetX;
        private Sprite tileSprite;

        private const int GridColumns = 2;

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Start()
        {
            if (EnvironmentManager.Instance == null) return;
            EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
            EnvironmentManager.Instance.OnActiveEnvironmentChanged += SetEnvironment;
            SetEnvironment(EnvironmentManager.Instance.ActiveEnvironmentIndex);
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged -= SetEnvironment;
            }
        }

        public void SetEnvironment(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            var envs = EnvironmentManager.Instance.Environments;
            if (envIndex < 0 || envIndex >= envs.Count) return;

            tileSprite = envs[envIndex].tileSprite;
            if (tileSprite == null)
            {
                Debug.LogWarning($"[BackyardIsometricView] No tileSprite assigned on {envs[envIndex].environmentName}.", this);
            }

            int count = EnvironmentManager.Instance.GetActiveSlotCount(envIndex);
            RebuildGrid(count);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            if (envIndex != EnvironmentManager.Instance.ActiveEnvironmentIndex) return;
            if (tileSprite == null) return;
            SpawnTile(tiles.Count);
            RecenterGrid();
        }

        public void RebuildGrid(int count)
        {
            foreach (var t in tiles) if (t) Destroy(t);
            tiles.Clear();
            plantGOs.Clear();
            plantBaseScales.Clear();

            for (int i = 0; i < count; i++)
                SpawnTile(i);

            RecenterGrid();
        }

        private void SpawnTile(int index)
        {
            var tileGO = new GameObject($"BackyardTile_{index}");
            tileGO.transform.SetParent(transform, false);

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder + index / GridColumns;

            GameObject plantGO = null;
            if (plantPrefabs != null && plantPrefabs.Length > 0)
            {
                var prefab = plantPrefabs[index % plantPrefabs.Length];
                if (prefab != null)
                {
                    plantGO = Instantiate(prefab, tileGO.transform);
                    plantGO.transform.localPosition = new Vector3(0f, 0.15f, -0.5f);
                    plantGO.transform.localScale = Vector3.one * plantScale;
                }
            }
            plantBaseScales.Add(plantScale);
            if (plantGO != null) plantGO.SetActive(false);
            tiles.Add(tileGO);
            plantGOs.Add(plantGO);

            PositionTile(index);
        }

        private void PositionTile(int index)
        {
            if (index >= tiles.Count || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            int col = index % GridColumns;
            int row = index / GridColumns;
            tiles[index].transform.localPosition = new Vector3(
                (col - row) * w * 0.5f,
                (col + row) * -h * 0.25f,
                0f);
        }

        private void RecenterGrid()
        {
            if (tiles.Count == 0 || tileSprite == null) return;
            float ppu = tileSprite.pixelsPerUnit;
            float w = tileSprite.rect.width / ppu;
            float h = tileSprite.rect.height / ppu;
            int n = tiles.Count;
            float sumX = 0f, sumY = 0f;
            for (int i = 0; i < n; i++)
            {
                int col = i % GridColumns;
                int row = i / GridColumns;
                sumX += (col - row) * w * 0.5f;
                sumY += (col + row) * -h * 0.25f;
            }
            transform.position = gridAnchor - new Vector3(sumX / n - slideOffsetX, sumY / n, 0f);
        }

        public void SetSlideOffset(float worldDeltaX)
        {
            if (Mathf.Approximately(slideOffsetX, worldDeltaX)) return;
            slideOffsetX = worldDeltaX;
            RecenterGrid();
        }

        public Vector2 GetTileScreenCenter(int index)
        {
            if (index < 0 || index >= tiles.Count || mainCam == null)
                return Vector2.zero;
            return mainCam.WorldToScreenPoint(tiles[index].transform.position);
        }

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
            if (index < 0 || index >= plantGOs.Count) return;
            var go = plantGOs[index];
            if (go == null) return;
            go.SetActive(state != PlantState.Empty);
        }

        public void SetPlantScale(int index, float multiplier)
        {
            if (index < 0 || index >= plantGOs.Count) return;
            var go = plantGOs[index];
            if (go == null) return;
            float baseScale = index < plantBaseScales.Count ? plantBaseScales[index] : plantScale;
            go.transform.localScale = Vector3.one * (baseScale * multiplier);
        }

        public void SpawnSlotConsumableVisual(int slotIndex, ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length) return;
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null || slotIndex >= tiles.Count) return;

            if (!_slotConsumableGOs.ContainsKey(slotIndex))
                _slotConsumableGOs[slotIndex] = new List<GameObject>();

            int existing = _slotConsumableGOs[slotIndex].Count;
            var go = Instantiate(prefab, tiles[slotIndex].transform);
            go.transform.localPosition = new Vector3(0.25f + existing * 0.18f, 0.15f, -0.55f);
            _slotConsumableGOs[slotIndex].Add(go);
        }

        public void ClearSlotConsumableVisuals(int slotIndex)
        {
            if (!_slotConsumableGOs.TryGetValue(slotIndex, out var gos)) return;
            foreach (var go in gos) if (go) Destroy(go);
            gos.Clear();
            _slotConsumableGOs.Remove(slotIndex);
        }

        public void SpawnEnvConsumableVisual(ConsumableType type)
        {
            if (consumablePrefabs == null || (int)type >= consumablePrefabs.Length) return;
            var prefab = consumablePrefabs[(int)type];
            if (prefab == null) return;

            if (_envConsumableGOs.TryGetValue(type, out var existing))
            {
                if (existing) Destroy(existing);
                _envConsumableGOs.Remove(type);
            }

            var go = Instantiate(prefab, transform);
            int envCount = _envConsumableGOs.Count;
            go.transform.localPosition = new Vector3(-2.0f + envCount * 0.5f, 0.8f, -0.5f);
            _envConsumableGOs[type] = go;
        }

        public void ClearEnvConsumableVisual(ConsumableType type)
        {
            if (_envConsumableGOs.TryGetValue(type, out var go))
            {
                if (go) Destroy(go);
                _envConsumableGOs.Remove(type);
            }
        }
    }
}
```

**Step 2: Scene YAML — remove orphaned `tileSprite` reference**

The scene YAML (`Assets/Scenes/Garden.unity`) still has `tileSprite: {fileID: ..., guid: 7c6dc6b56455f43b695b3d59a7895a69, type: 3}` under the `BackyardIsometricView` component. Unity will silently ignore this unknown field, but to keep things clean, open the scene in Unity, select `HearthIso`, and verify there is no `tileSprite` field in the Inspector (it should be gone since we removed the `[SerializeField]`). Save the scene.

**Step 3: Check for compilation errors**

Check Unity Console. There should be none.

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/BackyardIsometricView.cs
git commit -m "feat: BackyardIsometricView reads tileSprite from EnvironmentData, responds to active env change"
```

---

### Task 4: BackyardViewUI — respond to active environment changes

**Files:**
- Modify: `Assets/Scripts/UI/BackyardViewUI.cs`

**Step 1: Replace `BackyardEnvIndex` constant with dynamic active env property; add `RebuildForEnvironment`**

Key changes:
- Remove `private const int BackyardEnvIndex = 0`
- Add property `private int ActiveEnv => EnvironmentManager.Instance != null ? EnvironmentManager.Instance.ActiveEnvironmentIndex : 0;`
- Cache `hearthTitle` label ref during `Initialize`
- Add `RebuildForEnvironment(int envIndex)` that tears down and rebuilds slot buttons
- Subscribe to `OnActiveEnvironmentChanged` → call `RebuildForEnvironment` and update title
- Replace all occurrences of `BackyardEnvIndex` with `ActiveEnv`

Replace the entire `BackyardViewUI.cs` with:

```csharp
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BackyardViewUI : MonoBehaviour
    {
        [SerializeField] private BackyardIsometricView isometricView;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        private Label hearthTitle;
        private readonly List<Button> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();
        private readonly List<string> _lastLabelText = new();

        private Button _pickerBtn;
        private VisualElement _dropdown;
        private ConsumableType? _pendingType;

        private bool initialized;
        private bool pageActive;

        private int ActiveEnv => EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.ActiveEnvironmentIndex : 0;

        public void SetPageActive(bool active) => pageActive = active;

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");
            hearthTitle = root.Q<Label>("hearth-title");

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged += OnActiveEnvironmentChanged;
                BuildSlotsForEnv(ActiveEnv);
            }

            BuildConsumablePicker();
            RestoreConsumableVisuals(ActiveEnv);

            initialized = true;
            RefreshAllSlots();
            UpdateTitle();
        }

        private void OnActiveEnvironmentChanged(int envIndex)
        {
            RebuildForEnvironment(envIndex);
        }

        private void RebuildForEnvironment(int envIndex)
        {
            // Tear down existing slot buttons
            foreach (var btn in slotButtons)
                btn.RemoveFromHierarchy();
            slotButtons.Clear();
            labels.Clear();
            progressFills.Clear();
            _lastLabelText.Clear();

            // Rebuild for new env
            BuildSlotsForEnv(envIndex);
            RestoreConsumableVisuals(envIndex);
            RefreshAllSlots();
            UpdateTitle();
        }

        private void BuildSlotsForEnv(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            int count = EnvironmentManager.Instance.GetActiveSlotCount(envIndex);
            for (int i = 0; i < count; i++)
                AddSlotButton(i);
        }

        private void RestoreConsumableVisuals(int envIndex)
        {
            if (isometricView == null) return;

            if (PlantManager.Instance != null)
            {
                foreach (var slot in PlantManager.Instance.Slots)
                {
                    if (slot.environmentIndex != envIndex) continue;
                    foreach (var c in slot.appliedConsumables)
                        isometricView.SpawnSlotConsumableVisual(slot.slotIndex, c.type);
                }
            }

            if (ConsumableManager.Instance != null)
            {
                foreach (var c in ConsumableManager.Instance.GetEnvConsumables(envIndex))
                    isometricView.SpawnEnvConsumableVisual(c.type);
            }
        }

        private void UpdateTitle()
        {
            if (hearthTitle == null || EnvironmentManager.Instance == null) return;
            var envs = EnvironmentManager.Instance.Environments;
            int idx = ActiveEnv;
            hearthTitle.text = (idx >= 0 && idx < envs.Count) ? envs[idx].environmentName : "Backyard";
        }

        private void BuildConsumablePicker()
        {
            _pickerBtn = new Button(ToggleDropdown);
            _pickerBtn.text = "🌿";
            _pickerBtn.AddToClassList("consumable-picker-btn");
            terrariumPage.Add(_pickerBtn);

            _dropdown = new VisualElement();
            _dropdown.AddToClassList("consumable-dropdown");
            _dropdown.style.display = DisplayStyle.None;
            terrariumPage.Add(_dropdown);
        }

        private void ToggleDropdown()
        {
            if (_pendingType.HasValue)
            {
                CancelApplyMode();
                return;
            }

            bool showing = _dropdown.style.display == DisplayStyle.Flex;
            if (showing)
            {
                _dropdown.style.display = DisplayStyle.None;
                return;
            }

            RefreshDropdown();
            _dropdown.style.display = DisplayStyle.Flex;
        }

        private void RefreshDropdown()
        {
            _dropdown.Clear();
            if (ConsumableManager.Instance == null) return;

            foreach (var c in ConsumableManager.Instance.AllConsumables)
            {
                int count = ConsumableManager.Instance.GetCount(c.type);
                if (count <= 0) continue;

                var row = new Button();
                row.AddToClassList("consumable-row");

                var nameLabel = new Label(c.displayName);
                nameLabel.AddToClassList("consumable-row-name");

                var countLabel = new Label($"x{count}");
                countLabel.AddToClassList("consumable-row-count");

                row.Add(nameLabel);
                row.Add(countLabel);

                var capturedType = c.type;
                var capturedIsEnvScoped = c.isEnvironmentScoped;
                row.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

                _dropdown.Add(row);
            }

            if (_dropdown.childCount == 0)
            {
                var empty = new Label("No consumables owned");
                empty.AddToClassList("consumable-row-name");
                empty.style.padding = new StyleLength(8);
                _dropdown.Add(empty);
            }
        }

        private void OnConsumableRowTapped(ConsumableType type, bool isEnvironmentScoped)
        {
            _dropdown.style.display = DisplayStyle.None;

            if (isEnvironmentScoped)
            {
                if (ConsumableManager.Instance != null &&
                    ConsumableManager.Instance.ApplyToEnvironment(type, ActiveEnv))
                {
                    isometricView?.SpawnEnvConsumableVisual(type);
                }
                return;
            }

            _pendingType = type;
            foreach (var btn in slotButtons)
                btn.AddToClassList("backyard-slot-apply-mode");
        }

        private void CancelApplyMode()
        {
            _pendingType = null;
            foreach (var btn in slotButtons)
                btn.RemoveFromClassList("backyard-slot-apply-mode");
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged -= OnActiveEnvironmentChanged;
            }
        }

        private void AddSlotButton(int slotIndex)
        {
            var btn = new Button();
            btn.AddToClassList("backyard-slot-overlay");
            btn.style.position = Position.Absolute;

            var label = new Label("Tap to Plant");
            label.AddToClassList("backyard-slot-label");
            btn.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("backyard-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("backyard-progress-fill");
            progressBar.Add(fill);
            btn.Add(progressBar);

            int idx = slotIndex;
            btn.RegisterCallback<ClickEvent>(_ => OnSlotClicked(idx));

            terrariumPage.Add(btn);
            slotButtons.Add(btn);
            labels.Add(label);
            _lastLabelText.Add(null);
            progressFills.Add(fill);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != ActiveEnv) return;
            AddSlotButton(slotButtons.Count);
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || !pageActive || isometricView == null || terrariumPage == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            if (PlantManager.Instance == null) return;

            int env = ActiveEnv;
            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(env, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(env, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < labels.Count && labels[i] != null && text != _lastLabelText[i])
                    {
                        labels[i].text = text;
                        _lastLabelText[i] = text;
                    }
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

            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            float panelLeft   = Mathf.Min(bl.x, tr.x);
            float panelTop    = Mathf.Min(bl.y, tr.y);
            float panelWidth  = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            var pageOrigin = terrariumPage.worldBound;
            if (pageOrigin.width <= 0) return;
            slotButtons[i].style.left   = panelLeft   - pageOrigin.x;
            slotButtons[i].style.top    = panelTop    - pageOrigin.y;
            slotButtons[i].style.width  = panelWidth;
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

            int env = ActiveEnv;
            var slot = PlantManager.Instance.GetSlot(env, i);
            if (slot == null) return;

            var label = i < labels.Count ? labels[i] : null;
            var fill  = i < progressFills.Count ? progressFills[i] : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    if (label != null) label.text = "Tap to Plant";
                    if (fill  != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    isometricView?.ClearSlotConsumableVisuals(i);
                    break;

                case PlantState.Growing:
                    float hours = PlantManager.Instance.GetRemainingHours(env, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < _lastLabelText.Count) _lastLabelText[i] = null;
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing,
                        slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    if (label != null) label.text = "Harvest!";
                    if (fill  != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature,
                        slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_pendingType.HasValue)
            {
                var type = _pendingType.Value;
                CancelApplyMode();
                if (PlantManager.Instance != null &&
                    PlantManager.Instance.ApplyConsumable(type, ActiveEnv, slotIndex))
                {
                    isometricView?.SpawnSlotConsumableVisual(slotIndex, type);
                }
                return;
            }

            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(ActiveEnv, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:  OnEmptySlotTapped?.Invoke(ActiveEnv, slotIndex);  break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(ActiveEnv, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != ActiveEnv) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != ActiveEnv) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
```

**Step 2: Verify no compile errors in Unity Console.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/BackyardViewUI.cs
git commit -m "feat: BackyardViewUI responds to active env changes, rebuilds slots and title dynamically"
```

---

### Task 5: BottomNavUI — detect terrarium re-tap

**Files:**
- Modify: `Assets/Scripts/UI/BottomNavUI.cs`

**Step 1: Add `OnTerrariumReactivated` and re-tap detection**

Replace the file with:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BottomNavUI : MonoBehaviour
    {
        public event Action OnTerrariumReactivated;

        private Button[] tabs;
        private SwipeablePageView pageView;

        private const int TerrariumIndex = 2;

        private static readonly string[] TabNames = {
            "tab-codex", "tab-shop", "tab-terrarium", "tab-greenhouse", "tab-construction"
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
            if (index == TerrariumIndex && pageView.CurrentPageIndex == TerrariumIndex)
            {
                OnTerrariumReactivated?.Invoke();
                return;
            }
            pageView.GoToPage(index);
        }

        private void UpdateActiveTab(int activeIndex)
        {
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].RemoveFromClassList("nav-tab-active");
            if (activeIndex >= 0 && activeIndex < tabs.Length)
                tabs[activeIndex].AddToClassList("nav-tab-active");
        }

        private void OnDestroy()
        {
            if (pageView != null)
                pageView.OnPageChanged -= UpdateActiveTab;
        }
    }
}
```

**Step 2: Verify no compile errors.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/BottomNavUI.cs
git commit -m "feat: BottomNavUI fires OnTerrariumReactivated on terrarium re-tap"
```

---

### Task 6: EnvironmentSwitcherBar — new controller script

**Files:**
- Create: `Assets/Scripts/UI/EnvironmentSwitcherBar.cs`

**Step 1: Create the script**

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class EnvironmentSwitcherBar : MonoBehaviour
    {
        public event Action<int> OnEnvironmentSelected;

        private VisualElement bar;
        private bool isVisible;

        public void Initialize(VisualElement root)
        {
            bar = root.Q<VisualElement>("env-switcher-bar");
            if (bar == null)
            {
                Debug.LogError("[EnvironmentSwitcherBar] #env-switcher-bar not found in UXML.");
                return;
            }

            // Start hidden and translated down
            bar.style.display = DisplayStyle.None;

            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked += _ => RefreshIfVisible();
        }

        private void OnDestroy()
        {
            if (EnvironmentManager.Instance != null)
                EnvironmentManager.Instance.OnEnvironmentUnlocked -= _ => RefreshIfVisible();
        }

        public void Toggle()
        {
            if (isVisible) Hide();
            else Show();
        }

        public void Show()
        {
            if (bar == null) return;
            RebuildPills();

            // Only show if there are 2+ unlocked environments
            if (bar.childCount == 0) return;

            isVisible = true;
            bar.style.display = DisplayStyle.Flex;

            // Start at translated-down position, then schedule slide-up on next frame
            bar.AddToClassList("env-switcher-bar--hidden");
            bar.schedule.Execute(() =>
            {
                bar.RemoveFromClassList("env-switcher-bar--hidden");
                bar.AddToClassList("env-switcher-bar--visible");
            }).ExecuteLater(16); // ~1 frame at 60fps
        }

        public void Hide()
        {
            if (bar == null || !isVisible) return;
            isVisible = false;

            bar.RemoveFromClassList("env-switcher-bar--visible");
            bar.AddToClassList("env-switcher-bar--hidden");

            // After transition completes, set display: none
            bar.RegisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
        }

        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            bar.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            if (!isVisible)
                bar.style.display = DisplayStyle.None;
        }

        private void RefreshIfVisible()
        {
            if (isVisible) RebuildPills();
        }

        private void RebuildPills()
        {
            bar.Clear();
            if (EnvironmentManager.Instance == null) return;

            var envs = EnvironmentManager.Instance.Environments;
            int activeEnv = EnvironmentManager.Instance.ActiveEnvironmentIndex;
            bool anyAdded = false;

            for (int i = 0; i < envs.Count; i++)
            {
                if (!EnvironmentManager.Instance.IsUnlocked(i)) continue;

                var pill = new Button();
                pill.AddToClassList("env-pill");
                pill.text = envs[i].environmentName;

                if (i == activeEnv)
                    pill.AddToClassList("env-pill--active");

                int captured = i;
                pill.clicked += () =>
                {
                    EnvironmentManager.Instance.SetActiveEnvironment(captured);
                    OnEnvironmentSelected?.Invoke(captured);
                };

                bar.Add(pill);
                anyAdded = true;
            }

            // If only 1 env is unlocked, clear the bar (nothing to switch to)
            if (bar.childCount <= 1)
                bar.Clear();
        }
    }
}
```

**Step 2: Add `EnvironmentSwitcherBar` component to the `--- UI ---` scene GameObject**

Open `Assets/Scenes/Garden.unity` in Unity. Select the `--- UI ---` GameObject in the Hierarchy. In the Inspector, click "Add Component" and add `EnvironmentSwitcherBar`. Save the scene.

**Step 3: Verify no compile errors.**

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/EnvironmentSwitcherBar.cs
git commit -m "feat: EnvironmentSwitcherBar controller with show/hide animation and pill buttons"
```

---

### Task 7: UXML + USS — add switcher bar to layout

**Files:**
- Modify: `Assets/UI/Documents/GardenRoot.uxml:46-63`
- Modify: `Assets/UI/Styles/BottomNav.uss`

**Step 1: Add `#env-switcher-bar` above `#bottom-nav` in GardenRoot.uxml**

In `GardenRoot.uxml`, replace the comment + `#bottom-nav` block (lines 46–63):

```xml
        <!-- Environment switcher bar (slides up on terrarium re-tap) -->
        <ui:VisualElement name="env-switcher-bar" class="env-switcher-bar" />

        <!-- Bottom navigation -->
        <ui:VisualElement name="bottom-nav">
            <ui:Button name="tab-codex" class="nav-tab">
                <ui:VisualElement class="nav-icon" />
            </ui:Button>
            <ui:Button name="tab-shop" class="nav-tab">
                <ui:VisualElement class="nav-icon" />
            </ui:Button>
            <ui:Button name="tab-terrarium" class="nav-tab nav-tab-active">
                <ui:VisualElement class="nav-icon" />
            </ui:Button>
            <ui:Button name="tab-greenhouse" class="nav-tab">
                <ui:VisualElement class="nav-icon" />
            </ui:Button>
            <ui:Button name="tab-construction" class="nav-tab">
                <ui:VisualElement class="nav-icon" />
            </ui:Button>
        </ui:VisualElement>
```

**Step 2: Add switcher bar styles to BottomNav.uss**

Append the following to the end of `Assets/UI/Styles/BottomNav.uss`:

```css
/* ─── Environment Switcher Bar ───────────────────────────── */

.env-switcher-bar {
    flex-direction: row;
    align-self: stretch;
    justify-content: center;
    padding: 12px 16px;
    background-color: rgba(8, 16, 24, 0.88);
    border-top-width: 1px;
    border-top-color: rgba(140, 230, 240, 0.15);
    transition-property: translate;
    transition-duration: 0.25s;
    transition-timing-function: ease-out;
    translate: 0 100%;
}

.env-switcher-bar--visible {
    translate: 0 0%;
}

.env-switcher-bar--hidden {
    translate: 0 100%;
}

.env-pill {
    background-color: transparent;
    border-width: 1px;
    border-color: rgba(140, 230, 240, 0.35);
    border-radius: 20px;
    padding: 8px 20px;
    margin: 0 6px;
    color: rgba(185, 210, 225, 0.85);
    font-size: 24px;
    transition-property: background-color, border-color, color;
    transition-duration: 0.15s;
}

.env-pill--active {
    background-color: rgba(100, 230, 230, 0.18);
    border-color: rgb(100, 230, 230);
    color: rgb(100, 230, 230);
}

.env-pill:hover {
    background-color: rgba(100, 230, 230, 0.10);
    border-color: rgba(140, 230, 240, 0.65);
}
```

**Step 3: Verify in Unity that the UXML/USS changes don't cause layout errors. Check the Console.**

**Step 4: Commit**

```bash
git add Assets/UI/Documents/GardenRoot.uxml Assets/UI/Styles/BottomNav.uss
git commit -m "feat: add #env-switcher-bar to UXML and USS styles"
```

---

### Task 8: HortusUI — wire everything together

**Files:**
- Modify: `Assets/Scripts/UI/HortusUI.cs`

**Step 1: Add `EnvironmentSwitcherBar` field and wire up all events**

Key changes to `HortusUI.cs`:
- Add `private EnvironmentSwitcherBar envSwitcherBar;` field
- In `Start()`: get component, call `Initialize`, subscribe to events
- `OnPageChanged`: hide bar when navigating away from terrarium page
- Subscribe to `bottomNavUI.OnTerrariumReactivated` → toggle bar (guard: only if >1 unlocked env)
- Subscribe to `envSwitcherBar.OnEnvironmentSelected` → hide bar

In `Start()`, after the line `bottomNavUI?.Initialize(root, pageView);`, add:

```csharp
envSwitcherBar = GetComponent<EnvironmentSwitcherBar>();
envSwitcherBar?.Initialize(root);

if (bottomNavUI != null)
    bottomNavUI.OnTerrariumReactivated += OnTerrariumReactivated;

if (envSwitcherBar != null)
    envSwitcherBar.OnEnvironmentSelected += _ => envSwitcherBar.Hide();
```

In `OnPageChanged`, after `backyardViewUI?.SetPageActive(pageIndex == 2);`, add:

```csharp
if (pageIndex != TerrariumPageIndex)
    envSwitcherBar?.Hide();
```

Add the new handler method before `OnLocationResolved`:

```csharp
private void OnTerrariumReactivated()
{
    // Only show if more than one environment is unlocked
    if (EnvironmentManager.Instance == null) return;
    int unlocked = 0;
    for (int i = 0; i < EnvironmentManager.Instance.Environments.Count; i++)
        if (EnvironmentManager.Instance.IsUnlocked(i)) unlocked++;
    if (unlocked <= 1) return;

    envSwitcherBar?.Toggle();
}
```

In `OnDestroy`, add:

```csharp
if (bottomNavUI != null)
    bottomNavUI.OnTerrariumReactivated -= OnTerrariumReactivated;
```

And add `private EnvironmentSwitcherBar envSwitcherBar;` to the field declarations at the top of the class.

**Step 2: Verify no compile errors.**

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/HortusUI.cs
git commit -m "feat: HortusUI wires EnvironmentSwitcherBar to terrarium re-tap and page change events"
```

---

### Task 9: Assign tileSprite to all EnvironmentData assets

The four `EnvironmentData` assets need a `tileSprite` assigned. The current isometric tile sprite has GUID `7c6dc6b56455f43b695b3d59a7895a69` and was previously used for Backyard.

**Step 1: Assign the Backyard tile sprite to Hearth.asset**

In `Assets/Resources/Config/Environments/Hearth.asset`, add the `tileSprite` field after `allowsCrossPollination`:

```yaml
  tileSprite: {fileID: 21300000, guid: 7c6dc6b56455f43b695b3d59a7895a69, type: 3}
```

**Step 2: Assign tile sprites to the other three environments**

For `Balcony.asset`, `WildPatch.asset`, and `Conservatory.asset`:
- If you have distinct tile sprites for each, assign their GUIDs in the same way
- If you want to reuse the same sprite as a placeholder while art is pending, copy the same reference as Hearth

For now, assign the same `tileSprite` reference as Hearth in all three assets so they render something (each environment will look visually different once unique sprites are created):

In each of `Balcony.asset`, `WildPatch.asset`, `Conservatory.asset`, add after `allowsCrossPollination: 0` (or `allowsCrossPollination: 1` for Conservatory):

```yaml
  tileSprite: {fileID: 21300000, guid: 7c6dc6b56455f43b695b3d59a7895a69, type: 3}
```

**Step 3: Verify in Unity**

Open Unity, let it reimport. Check the Console for errors. Open each `EnvironmentData` asset in the Inspector and confirm `tileSprite` is populated.

**Step 4: Commit**

```bash
git add Assets/Resources/Config/Environments/
git commit -m "feat: assign tileSprite on all EnvironmentData assets"
```

---

### Task 10: Smoke test

**Step 1: Enter Play Mode in Unity**

Start the game. Verify:
- Terrarium shows "Backyard" title and 1 slot (or however many are unlocked)
- Re-tapping the terrarium tab does nothing (only 1 env unlocked by default)

**Step 2: Use the Debug panel to give yourself enough Gold to unlock an environment**

Open the ⚙ debug panel → tap "Max Currency". You should now have enough Gold.

**Step 3: Go to the Construction tab and purchase "The Balcony"**

The Balcony costs 5,000 Gold. After purchase, verify the `OnEnvironmentUnlocked` event fires (no console errors).

**Step 4: Return to the terrarium tab and re-tap it**

The env switcher bar should slide up, showing two pills: "Backyard" (active/highlighted) and "The Balcony".

**Step 5: Tap "The Balcony"**

- The bar should slide down and dismiss
- The `hearth-title` label should change to "The Balcony"
- The iso grid should rebuild with The Balcony's tile sprite and slot count
- Slot buttons should reflect The Balcony's slots

**Step 6: Navigate away and back**

- Switch to another tab — bar should auto-hide if it was somehow open
- Return to terrarium — active env should still be The Balcony

**Step 7: Exit and re-enter Play Mode**

Verify that The Balcony is still the active environment on reload (save persistence check).

---

### Task 11: Final commit

```bash
git add -A
git commit -m "feat: environment switcher — complete implementation"
```
