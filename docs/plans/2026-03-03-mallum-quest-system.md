# Mallum Quest System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a quest system where players send limited Mallums on timed expeditions that yield seeds, with Mallums as a unified resource shared between water-fetching and quests.

**Architecture:** New `MallumManager` singleton owns all Mallum state (idle/fetching/questing). `QuestData` ScriptableObjects define quests with duration, flame level requirement, and weighted seed reward pools. VaseManager's `SendToCollect()` is gated through MallumManager. A floating button opens a quest overlay panel.

**Tech Stack:** Unity 6 / C# / UI Toolkit (UXML + USS) / ScriptableObjects / NUnit tests

---

### Task 1: Data classes — QuestData, MallumConfig, MallumSave

**Files:**
- Create: `Assets/Scripts/Data/QuestData.cs`
- Create: `Assets/Scripts/Data/MallumConfig.cs`
- Create: `Assets/Scripts/Data/MallumSave.cs`
- Modify: `Assets/Scripts/Data/SaveData.cs:7-20`
- Test: `Assets/Tests/EditMode/TestMallumData.cs`

**Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/TestMallumData.cs
using NUnit.Framework;

namespace Garden.Tests
{
    public class TestMallumData
    {
        [Test]
        public void MallumSave_DefaultState_IsIdle()
        {
            var mallum = new MallumSave();
            Assert.AreEqual(MallumState.Idle, mallum.state);
        }

        [Test]
        public void MallumSave_PendingRewards_StartsEmpty()
        {
            var mallum = new MallumSave();
            Assert.IsNotNull(mallum.pendingRewards);
            Assert.AreEqual(0, mallum.pendingRewards.Count);
        }

        [Test]
        public void SaveData_Mallums_StartsEmpty()
        {
            var data = new SaveData();
            Assert.IsNotNull(data.mallums);
            Assert.AreEqual(0, data.mallums.Count);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run Unity EditMode tests. Expected: FAIL — `MallumSave`, `MallumState`, `SaveData.mallums` don't exist.

**Step 3: Implement data classes**

```csharp
// Assets/Scripts/Data/MallumSave.cs
using System;
using System.Collections.Generic;

namespace Garden
{
    public enum MallumState
    {
        Idle,
        FetchingWater,
        OnQuest,
        QuestComplete
    }

    [Serializable]
    public class MallumSave
    {
        public MallumState state = MallumState.Idle;
        public int assignedVaseIndex = -1;
        public string assignedQuestName;
        public string startTimeUtc;
        public List<RewardEntry> pendingRewards = new();
    }

    [Serializable]
    public class RewardEntry
    {
        public string seedName;
        public int count;
    }
}
```

```csharp
// Assets/Scripts/Data/QuestData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewQuest", menuName = "CampFire/Quest Data")]
    public class QuestData : ScriptableObject
    {
        public string questName;
        [TextArea] public string description;
        public int durationMinutes = 30;
        public int requiredFlameLevel = 1;
        public int rewardRolls = 2;
        public List<QuestReward> rewardPool = new();
    }

    [Serializable]
    public class QuestReward
    {
        public SeedData seed;
        public float weight = 1f;
        public int minCount = 1;
        public int maxCount = 1;
    }
}
```

```csharp
// Assets/Scripts/Data/MallumConfig.cs
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "MallumConfig", menuName = "CampFire/Mallum Config")]
    public class MallumConfig : ScriptableObject
    {
        [SerializeField] private int[] maxMallumsPerFlameLevel = { 1, 1, 2, 2, 3 };

        public int GetMaxMallums(int flameLevel)
        {
            int index = Mathf.Clamp(flameLevel - 1, 0, maxMallumsPerFlameLevel.Length - 1);
            return maxMallumsPerFlameLevel[index];
        }
    }
}
```

Modify `SaveData.cs` — add mallums field after line 19 (`lastVisitorDateUtc`):

```csharp
public List<MallumSave> mallums = new();
```

**Step 4: Run tests to verify they pass**

Run Unity EditMode tests. Expected: All 3 new tests PASS.

**Step 5: Commit**

```
feat: add QuestData, MallumConfig, and MallumSave data classes
```

---

### Task 2: MallumConfig asset file

**Files:**
- Create: `Assets/Resources/Config/MallumConfig.asset`

**Step 1: Create the MallumConfig asset**

Write the YAML asset file directly. Use the same Unity MonoBehaviour YAML format as VaseConfig/FlameConfig. The script GUID must be obtained after Task 1 compiles — read it from `Assets/Scripts/Data/MallumConfig.cs.meta`.

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <GUID_FROM_META>, type: 3}
  m_Name: MallumConfig
  m_EditorClassIdentifier:
  maxMallumsPerFlameLevel:
  - 1
  - 1
  - 2
  - 2
  - 3
```

Replace `<GUID_FROM_META>` with the actual GUID from `MallumConfig.cs.meta`.

**Step 2: Commit**

```
feat: add MallumConfig asset (1/1/2/2/3 mallums per flame level)
```

---

### Task 3: MallumManager — core logic

**Files:**
- Create: `Assets/Scripts/Managers/MallumManager.cs`
- Test: `Assets/Tests/EditMode/TestMallumManager.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Tests/EditMode/TestMallumManager.cs
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestMallumManager
    {
        [Test]
        public void EnsureMallumCount_AddsIdleMallums_WhenBelowCap()
        {
            var mallums = new List<MallumSave>();
            MallumManager.EnsureMallumCount(mallums, 2);
            Assert.AreEqual(2, mallums.Count);
            Assert.AreEqual(MallumState.Idle, mallums[0].state);
            Assert.AreEqual(MallumState.Idle, mallums[1].state);
        }

        [Test]
        public void EnsureMallumCount_DoesNotRemove_WhenAboveCap()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.OnQuest },
                new() { state = MallumState.Idle },
                new() { state = MallumState.Idle }
            };
            MallumManager.EnsureMallumCount(mallums, 2);
            Assert.AreEqual(3, mallums.Count); // never remove active mallums
        }

        [Test]
        public void GetAvailableCount_CountsOnlyIdle()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle },
                new() { state = MallumState.OnQuest },
                new() { state = MallumState.FetchingWater },
                new() { state = MallumState.Idle }
            };
            Assert.AreEqual(2, MallumManager.GetAvailableCount(mallums));
        }

        [Test]
        public void ClaimMallumForWater_SetsStateAndVaseIndex()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle }
            };
            bool result = MallumManager.ClaimMallumForWater(mallums, 0, "2026-01-01T00:00:00Z");
            Assert.IsTrue(result);
            Assert.AreEqual(MallumState.FetchingWater, mallums[0].state);
            Assert.AreEqual(0, mallums[0].assignedVaseIndex);
        }

        [Test]
        public void ClaimMallumForWater_FailsWhenNoneIdle()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.OnQuest }
            };
            bool result = MallumManager.ClaimMallumForWater(mallums, 0, "2026-01-01T00:00:00Z");
            Assert.IsFalse(result);
        }

        [Test]
        public void ClaimMallumForQuest_SetsStateAndQuestName()
        {
            var mallums = new List<MallumSave>
            {
                new() { state = MallumState.Idle }
            };
            bool result = MallumManager.ClaimMallumForQuest(mallums, "Swamp Forage", "2026-01-01T00:00:00Z");
            Assert.IsTrue(result);
            Assert.AreEqual(MallumState.OnQuest, mallums[0].state);
            Assert.AreEqual("Swamp Forage", mallums[0].assignedQuestName);
        }

        [Test]
        public void RollRewards_ProducesCorrectRollCount()
        {
            var pool = new List<QuestReward>
            {
                new() { seed = ScriptableObject.CreateInstance<SeedData>(), weight = 1f, minCount = 1, maxCount = 1 }
            };
            pool[0].seed.seedName = "Fern";

            var rewards = MallumManager.RollRewards(pool, 3);
            Assert.AreEqual(3, rewards.Count);
            Assert.AreEqual("Fern", rewards[0].seedName);
        }

        [Test]
        public void CollectRewards_ClearsStateAndRewards()
        {
            var mallum = new MallumSave
            {
                state = MallumState.QuestComplete,
                assignedQuestName = "Swamp Forage",
                pendingRewards = new List<RewardEntry>
                {
                    new() { seedName = "Fern", count = 2 }
                }
            };
            var rewards = MallumManager.CollectRewards(mallum);
            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(MallumState.Idle, mallum.state);
            Assert.IsNull(mallum.assignedQuestName);
            Assert.AreEqual(0, mallum.pendingRewards.Count);
        }

        [Test]
        public void FreeMallumFromWater_ReturnsToIdle()
        {
            var mallum = new MallumSave
            {
                state = MallumState.FetchingWater,
                assignedVaseIndex = 1,
                startTimeUtc = "2026-01-01T00:00:00Z"
            };
            MallumManager.FreeMallumFromWater(mallum);
            Assert.AreEqual(MallumState.Idle, mallum.state);
            Assert.AreEqual(-1, mallum.assignedVaseIndex);
            Assert.IsNull(mallum.startTimeUtc);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `MallumManager` doesn't exist.

**Step 3: Implement MallumManager**

```csharp
// Assets/Scripts/Managers/MallumManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class MallumManager : MonoBehaviour
    {
        public static MallumManager Instance { get; private set; }

        [SerializeField] private MallumConfig config;

        private QuestData[] allQuests;

        public MallumConfig Config => config;
        public event Action OnMallumsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allQuests = Resources.LoadAll<QuestData>("Quests");
        }

        private void Start()
        {
            var data = SaveManager.Instance.Data;
            int max = config.GetMaxMallums(data.flameLevel);
            EnsureMallumCount(data.mallums, max);

            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += OnFlameUpgraded;
        }

        private void OnDestroy()
        {
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= OnFlameUpgraded;
        }

        private void OnFlameUpgraded()
        {
            var data = SaveManager.Instance.Data;
            int max = config.GetMaxMallums(data.flameLevel);
            int before = data.mallums.Count;
            EnsureMallumCount(data.mallums, max);
            if (data.mallums.Count != before)
            {
                SaveManager.Instance.Save();
                OnMallumsChanged?.Invoke();
            }
        }

        private void Update()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;

            foreach (var mallum in data.mallums)
            {
                if (mallum.state == MallumState.FetchingWater)
                {
                    // VaseManager handles its own fill completion via CheckFillCompletion().
                    // We check: if the vase is no longer Filling, free the mallum.
                    if (mallum.assignedVaseIndex >= 0 &&
                        mallum.assignedVaseIndex < data.vases.Count)
                    {
                        var vase = data.vases[mallum.assignedVaseIndex];
                        if (vase.state != VaseState.Filling)
                        {
                            FreeMallumFromWater(mallum);
                            changed = true;
                        }
                    }
                }
                else if (mallum.state == MallumState.OnQuest)
                {
                    if (IsQuestTimerComplete(mallum))
                    {
                        CompleteQuest(mallum);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                SaveManager.Instance.Save();
                OnMallumsChanged?.Invoke();
            }
        }

        // --- Public API ---

        public int GetTotalMallumCount()
        {
            return SaveManager.Instance.Data.mallums.Count;
        }

        public int GetAvailableMallumCount()
        {
            return GetAvailableCount(SaveManager.Instance.Data.mallums);
        }

        public bool SendToFetchWater(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (!ClaimMallumForWater(data.mallums, vaseIndex, GameTime.UtcNow.ToString("o")))
                return false;

            VaseManager.Instance.SendToCollect(vaseIndex);
            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            return true;
        }

        public bool SendOnQuest(QuestData quest)
        {
            var data = SaveManager.Instance.Data;
            if (!ClaimMallumForQuest(data.mallums, quest.questName, GameTime.UtcNow.ToString("o")))
                return false;

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            return true;
        }

        public List<RewardEntry> CollectQuestRewards(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return null;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.QuestComplete) return null;

            var rewards = CollectRewards(mallum);

            foreach (var r in rewards)
                ApothekeManager.Instance.AddSeed(r.seedName, r.count);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            return rewards;
        }

        public QuestData[] GetAllQuests() => allQuests;

        public List<QuestData> GetAvailableQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var available = new List<QuestData>();
            foreach (var q in allQuests)
                if (q.requiredFlameLevel <= level)
                    available.Add(q);
            return available;
        }

        public List<QuestData> GetLockedQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var locked = new List<QuestData>();
            foreach (var q in allQuests)
                if (q.requiredFlameLevel > level)
                    locked.Add(q);
            return locked;
        }

        public int GetCompletedQuestCount()
        {
            int count = 0;
            foreach (var m in SaveManager.Instance.Data.mallums)
                if (m.state == MallumState.QuestComplete)
                    count++;
            return count;
        }

        public float GetQuestRemainingSeconds(MallumSave mallum)
        {
            if (mallum.state != MallumState.OnQuest || string.IsNullOrEmpty(mallum.startTimeUtc))
                return 0f;

            var quest = FindQuest(mallum.assignedQuestName);
            if (quest == null) return 0f;

            var startTime = DateTime.Parse(mallum.startTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - startTime).TotalSeconds;
            float total = quest.durationMinutes * 60f;
            return Mathf.Max(0f, total - elapsed);
        }

        public float GetQuestProgress(MallumSave mallum)
        {
            if (mallum.state != MallumState.OnQuest || string.IsNullOrEmpty(mallum.startTimeUtc))
                return 0f;

            var quest = FindQuest(mallum.assignedQuestName);
            if (quest == null) return 0f;

            var startTime = DateTime.Parse(mallum.startTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsed = (float)(GameTime.UtcNow - startTime).TotalMinutes;
            return Mathf.Clamp01(elapsed / quest.durationMinutes);
        }

        private QuestData FindQuest(string questName)
        {
            foreach (var q in allQuests)
                if (q.questName == questName)
                    return q;
            return null;
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static void EnsureMallumCount(List<MallumSave> mallums, int targetCount)
        {
            while (mallums.Count < targetCount)
                mallums.Add(new MallumSave());
            // Never remove — active mallums should not be destroyed
        }

        public static int GetAvailableCount(List<MallumSave> mallums)
        {
            int count = 0;
            foreach (var m in mallums)
                if (m.state == MallumState.Idle)
                    count++;
            return count;
        }

        public static bool ClaimMallumForWater(List<MallumSave> mallums, int vaseIndex, string utcNow)
        {
            foreach (var m in mallums)
            {
                if (m.state == MallumState.Idle)
                {
                    m.state = MallumState.FetchingWater;
                    m.assignedVaseIndex = vaseIndex;
                    m.startTimeUtc = utcNow;
                    return true;
                }
            }
            return false;
        }

        public static bool ClaimMallumForQuest(List<MallumSave> mallums, string questName, string utcNow)
        {
            foreach (var m in mallums)
            {
                if (m.state == MallumState.Idle)
                {
                    m.state = MallumState.OnQuest;
                    m.assignedQuestName = questName;
                    m.startTimeUtc = utcNow;
                    return true;
                }
            }
            return false;
        }

        public static List<RewardEntry> RollRewards(List<QuestReward> pool, int rolls)
        {
            var rewards = new List<RewardEntry>();
            float totalWeight = 0f;
            foreach (var r in pool)
                totalWeight += r.weight;

            for (int i = 0; i < rolls; i++)
            {
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;
                foreach (var r in pool)
                {
                    cumulative += r.weight;
                    if (roll <= cumulative)
                    {
                        int count = UnityEngine.Random.Range(r.minCount, r.maxCount + 1);
                        rewards.Add(new RewardEntry
                        {
                            seedName = r.seed.seedName,
                            count = count
                        });
                        break;
                    }
                }
            }
            return rewards;
        }

        public static List<RewardEntry> CollectRewards(MallumSave mallum)
        {
            var rewards = new List<RewardEntry>(mallum.pendingRewards);
            mallum.pendingRewards.Clear();
            mallum.state = MallumState.Idle;
            mallum.assignedQuestName = null;
            mallum.startTimeUtc = null;
            return rewards;
        }

        public static void FreeMallumFromWater(MallumSave mallum)
        {
            mallum.state = MallumState.Idle;
            mallum.assignedVaseIndex = -1;
            mallum.startTimeUtc = null;
        }

        private bool IsQuestTimerComplete(MallumSave mallum)
        {
            if (string.IsNullOrEmpty(mallum.startTimeUtc)) return false;
            var quest = FindQuest(mallum.assignedQuestName);
            if (quest == null) return true; // quest deleted, auto-complete

            var startTime = DateTime.Parse(mallum.startTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            return (GameTime.UtcNow - startTime).TotalMinutes >= quest.durationMinutes;
        }

        private void CompleteQuest(MallumSave mallum)
        {
            var quest = FindQuest(mallum.assignedQuestName);
            if (quest != null)
                mallum.pendingRewards = RollRewards(quest.rewardPool, quest.rewardRolls);

            mallum.state = MallumState.QuestComplete;
            mallum.startTimeUtc = null;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run Unity EditMode tests. Expected: All 8 new tests PASS (plus previous 3 from Task 1).

**Step 5: Commit**

```
feat: add MallumManager with quest and water-fetch logic
```

---

### Task 4: Quest ScriptableObject assets

**Files:**
- Create: `Assets/Resources/Quests/SwampForage.asset`
- Create: `Assets/Resources/Quests/MeadowExpedition.asset`
- Create: `Assets/Resources/Quests/DeepWoodsTrek.asset`

**Step 1: Get GUIDs**

Read these `.meta` files to get GUIDs needed for the assets:
- `Assets/Scripts/Data/QuestData.cs.meta` — for the script GUID
- `Assets/Resources/Seeds/Fern.asset.meta` — for Fern seed reference
- `Assets/Resources/Seeds/Sunflower.asset.meta` — for Sunflower seed reference
- `Assets/Resources/Seeds/Moonvine.asset.meta` — for Moonvine seed reference

**Step 2: Create quest assets**

Create `Assets/Resources/Quests/` directory, then write each `.asset` YAML file.

SwampForage.asset (30 min, flame level 1, rewards: Fern high/Moonvine low):
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: <QUEST_DATA_GUID>, type: 3}
  m_Name: SwampForage
  m_EditorClassIdentifier:
  questName: Swamp Forage
  description: Send your Mallum to forage in the nearby swamp. Short and safe.
  durationMinutes: 30
  requiredFlameLevel: 1
  rewardRolls: 2
  rewardPool:
  - seed: {fileID: 11400000, guid: <FERN_GUID>, type: 2}
    weight: 3
    minCount: 1
    maxCount: 2
  - seed: {fileID: 11400000, guid: <MOONVINE_GUID>, type: 2}
    weight: 1
    minCount: 1
    maxCount: 1
```

MeadowExpedition.asset (120 min, flame level 2, rewards: Sunflower high/Fern medium):
- Same YAML pattern, `durationMinutes: 120`, `requiredFlameLevel: 2`, `rewardRolls: 3`
- Pool: Sunflower weight 3 (1-2), Fern weight 2 (1-1)

DeepWoodsTrek.asset (360 min, flame level 3, rewards: Moonvine high/Sunflower medium/Fern low):
- Same YAML pattern, `durationMinutes: 360`, `requiredFlameLevel: 3`, `rewardRolls: 4`
- Pool: Moonvine weight 3 (1-2), Sunflower weight 2 (1-1), Fern weight 1 (1-1)

**Step 3: Commit**

```
feat: add 3 starter quest assets (Swamp Forage, Meadow Expedition, Deep Woods Trek)
```

---

### Task 5: VaseManager integration — gate water-fetch through MallumManager

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs:900-906`
- Test: `Assets/Tests/EditMode/TestMallumManager.cs` (add test)

**Step 1: Write failing test**

Add to `TestMallumManager.cs`:

```csharp
[Test]
public void ClaimMallumForWater_DoesNotDoubleAssignSameVase()
{
    var mallums = new List<MallumSave>
    {
        new() { state = MallumState.FetchingWater, assignedVaseIndex = 0 },
        new() { state = MallumState.Idle }
    };
    bool result = MallumManager.ClaimMallumForWater(mallums, 0, "2026-01-01T00:00:00Z");
    Assert.IsTrue(result); // claims the idle one
    Assert.AreEqual(MallumState.FetchingWater, mallums[1].state);
}
```

**Step 2: Run test, verify pass** (should pass with existing implementation)

**Step 3: Modify CampsiteViewUI — vase "Send Mallum" button**

In `CampsiteViewUI.cs` around line 900-906, change the `collectBtn` callback and add availability check:

Replace the existing `VaseState.Empty` case button code (lines 900-906):

```csharp
// Old:
var collectBtn = new Button(() =>
{
    VaseManager.Instance.SendToCollect(index);
    CloseInteractionPanel();
}) { text = "Send Mallum" };

// New:
int available = MallumManager.Instance != null ? MallumManager.Instance.GetAvailableMallumCount() : 1;
int total = MallumManager.Instance != null ? MallumManager.Instance.GetTotalMallumCount() : 1;
var collectBtn = new Button(() =>
{
    if (MallumManager.Instance != null)
    {
        MallumManager.Instance.SendToFetchWater(index);
    }
    else
    {
        VaseManager.Instance.SendToCollect(index);
    }
    CloseInteractionPanel();
}) { text = $"Send Mallum ({available}/{total})" };
collectBtn.SetEnabled(available > 0);
```

**Step 4: Verify in Unity** — open scene, tap empty vase, confirm button shows Mallum count and routes through MallumManager.

**Step 5: Commit**

```
feat: gate vase water-fetch through MallumManager
```

---

### Task 6: Quest panel UXML and USS

**Files:**
- Create: `Assets/UI/Styles/Quest.uss`
- Create: `Assets/Resources/UI/Templates/QuestCard.uxml`
- Modify: `Assets/UI/Documents/CampFireRoot.uxml` (add stylesheet import + quest panel + floating button)

**Step 1: Create Quest.uss**

```css
/* Floating quest button */
#quest-float-btn {
    position: absolute;
    bottom: 200px;
    left: 24px;
    width: 100px;
    height: 100px;
    border-radius: 50px;
    background-color: var(--color-accent);
    align-items: center;
    justify-content: center;
    -unity-font-style: bold;
    font-size: var(--font-md);
    color: var(--color-text-light);
    border-width: 0;
    transition-property: scale;
    transition-duration: 150ms;
}

#quest-float-btn:hover {
    scale: 1.08;
}

#quest-float-icon {
    width: 48px;
    height: 48px;
}

#quest-badge {
    position: absolute;
    top: -4px;
    right: -4px;
    min-width: 36px;
    height: 36px;
    border-radius: 18px;
    background-color: #e74c3c;
    color: white;
    font-size: var(--font-xs);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
    padding-left: 8px;
    padding-right: 8px;
}

/* Quest panel */
#quests-panel {
    flex-direction: column;
    flex-grow: 1;
}

.quest-section-title {
    font-size: var(--font-sm);
    -unity-font-style: bold;
    color: var(--color-text-secondary);
    margin-bottom: 12px;
    margin-top: 24px;
    padding-left: 8px;
}

.quest-section-title-first {
    margin-top: 0;
}

/* Quest card */
.quest-card {
    flex-direction: column;
    background-color: var(--color-surface);
    border-radius: var(--radius-md);
    padding: 20px;
    margin-bottom: 12px;
    border-width: 2px;
    border-color: var(--color-border);
    transition-property: scale;
    transition-duration: 100ms;
}

.quest-card:hover {
    scale: 1.01;
}

.quest-card--active {
    border-color: var(--color-accent);
}

.quest-card--complete {
    border-color: #27ae60;
}

.quest-card--locked {
    opacity: 0.5;
}

.quest-card-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
}

.quest-card-name {
    font-size: var(--font-md);
    -unity-font-style: bold;
    color: var(--color-text-primary);
    flex-shrink: 1;
}

.quest-card-duration {
    font-size: var(--font-sm);
    color: var(--color-text-secondary);
    flex-shrink: 0;
    margin-left: 12px;
}

.quest-card-description {
    font-size: var(--font-sm);
    color: var(--color-text-secondary);
    margin-bottom: 12px;
    white-space: normal;
}

.quest-card-rewards {
    flex-direction: row;
    flex-wrap: wrap;
    margin-bottom: 12px;
}

.quest-reward-chip {
    flex-direction: row;
    align-items: center;
    background-color: var(--color-background);
    border-radius: 12px;
    padding: 4px 12px;
    margin-right: 8px;
    margin-bottom: 4px;
}

.quest-reward-name {
    font-size: var(--font-xs);
    color: var(--color-text-primary);
}

.quest-card-progress {
    height: 8px;
    background-color: var(--color-background);
    border-radius: 4px;
    margin-bottom: 12px;
}

.quest-card-progress-fill {
    height: 100%;
    background-color: var(--color-accent);
    border-radius: 4px;
}

.quest-card-timer {
    font-size: var(--font-sm);
    color: var(--color-accent);
    -unity-text-align: middle-center;
    margin-bottom: 8px;
}

.quest-card-action {
    align-self: stretch;
    height: 64px;
    border-radius: var(--radius-sm);
    background-color: var(--color-accent);
    color: var(--color-text-light);
    font-size: var(--font-sm);
    -unity-font-style: bold;
    border-width: 0;
}

.quest-card-action:hover {
    opacity: 0.9;
}

.quest-card-action:disabled {
    opacity: 0.4;
}

.quest-collect-btn {
    background-color: #27ae60;
}

.quest-locked-label {
    font-size: var(--font-xs);
    color: var(--color-text-secondary);
    -unity-font-style: italic;
    -unity-text-align: middle-center;
}

.quest-mallum-status {
    font-size: var(--font-sm);
    color: var(--color-text-secondary);
    -unity-text-align: middle-center;
    margin-bottom: 16px;
    padding: 12px;
    background-color: var(--color-surface);
    border-radius: var(--radius-sm);
}

.quest-empty-text {
    font-size: var(--font-sm);
    color: var(--color-text-secondary);
    -unity-font-style: italic;
    -unity-text-align: middle-center;
    padding: 24px;
}
```

**Step 2: Create QuestCard.uxml template**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="quest-card">
        <ui:VisualElement class="quest-card-header">
            <ui:Label name="quest-name" class="quest-card-name" />
            <ui:Label name="quest-duration" class="quest-card-duration" />
        </ui:VisualElement>
        <ui:Label name="quest-description" class="quest-card-description" />
        <ui:VisualElement name="quest-rewards" class="quest-card-rewards" />
        <ui:VisualElement name="quest-progress-container" class="quest-card-progress">
            <ui:VisualElement name="quest-progress-fill" class="quest-card-progress-fill" />
        </ui:VisualElement>
        <ui:Label name="quest-timer" class="quest-card-timer" />
        <ui:Button name="quest-action" class="quest-card-action" />
        <ui:Label name="quest-locked" class="quest-locked-label" />
    </ui:VisualElement>
</ui:UXML>
```

**Step 3: Add to CampFireRoot.uxml**

Add stylesheet import (after line 14, before line 16):
```xml
    <Style src="project://database/Assets/UI/Styles/Quest.uss" />
```

Add floating quest button (after bottom-nav closing tag, line 88, before overlay-container):
```xml
        <!-- Floating quest button -->
        <ui:Button name="quest-float-btn">
            <ui:VisualElement name="quest-float-icon" />
            <ui:Label name="quest-badge" text="0" />
        </ui:Button>
```

Add quest panel inside overlay-body (after build-panel, around line 173):
```xml
                    <!-- Quest panel -->
                    <ui:VisualElement name="quests-panel">
                        <ui:Label name="quest-mallum-status" class="quest-mallum-status" />
                        <ui:VisualElement name="quest-active-section" />
                        <ui:VisualElement name="quest-available-section" />
                        <ui:VisualElement name="quest-locked-section" />
                    </ui:VisualElement>
```

**Step 4: Commit**

```
feat: add quest panel UXML structure, USS styles, and QuestCard template
```

---

### Task 7: QuestUI controller

**Files:**
- Create: `Assets/Scripts/UI/QuestUI.cs`

**Step 1: Implement QuestUI**

```csharp
// Assets/Scripts/UI/QuestUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestUI : MonoBehaviour
    {
        private VisualElement root;
        private Label mallumStatusLabel;
        private VisualElement activeSection;
        private VisualElement availableSection;
        private VisualElement lockedSection;
        private VisualTreeAsset questCardTemplate;

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            mallumStatusLabel = root.Q<Label>("quest-mallum-status");
            activeSection = root.Q("quest-active-section");
            availableSection = root.Q("quest-available-section");
            lockedSection = root.Q("quest-locked-section");
            questCardTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/QuestCard");
        }

        public void Refresh()
        {
            if (MallumManager.Instance == null) return;

            UpdateMallumStatus();
            BuildActiveSection();
            BuildAvailableSection();
            BuildLockedSection();
        }

        private void UpdateMallumStatus()
        {
            int available = MallumManager.Instance.GetAvailableMallumCount();
            int total = MallumManager.Instance.GetTotalMallumCount();
            mallumStatusLabel.text = $"Mallums: {available} / {total} available";
        }

        private void BuildActiveSection()
        {
            activeSection.Clear();
            var data = SaveManager.Instance.Data;
            bool hasActive = false;

            for (int i = 0; i < data.mallums.Count; i++)
            {
                var mallum = data.mallums[i];
                if (mallum.state == MallumState.Idle) continue;

                hasActive = true;
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var progressFill = card.Q("quest-progress-fill");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                lockedLabel.style.display = DisplayStyle.None;
                rewardsContainer.style.display = DisplayStyle.None;
                descLabel.style.display = DisplayStyle.None;

                int mallumIndex = i;

                switch (mallum.state)
                {
                    case MallumState.FetchingWater:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = "Fetching Water";
                        float waterRemaining = VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex);
                        durationLabel.text = FormatTime(waterRemaining);
                        float waterProgress = VaseManager.Instance.GetFillProgress(mallum.assignedVaseIndex);
                        progressFill.style.width = new StyleLength(new Length(waterProgress * 100f, LengthUnit.Percent));
                        timerLabel.style.display = DisplayStyle.None;
                        actionBtn.style.display = DisplayStyle.None;
                        break;

                    case MallumState.OnQuest:
                        cardRoot.AddToClassList("quest-card--active");
                        nameLabel.text = mallum.assignedQuestName;
                        float remaining = MallumManager.Instance.GetQuestRemainingSeconds(mallum);
                        float progress = MallumManager.Instance.GetQuestProgress(mallum);
                        durationLabel.text = FormatTime(remaining);
                        progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                        timerLabel.style.display = DisplayStyle.None;
                        actionBtn.style.display = DisplayStyle.None;
                        break;

                    case MallumState.QuestComplete:
                        cardRoot.AddToClassList("quest-card--complete");
                        nameLabel.text = mallum.assignedQuestName;
                        durationLabel.text = "Complete!";
                        progressContainer.style.display = DisplayStyle.None;
                        timerLabel.style.display = DisplayStyle.None;

                        // Show pending rewards
                        rewardsContainer.style.display = DisplayStyle.Flex;
                        foreach (var reward in mallum.pendingRewards)
                        {
                            var chip = new VisualElement();
                            chip.AddToClassList("quest-reward-chip");
                            var chipLabel = new Label($"{reward.seedName} x{reward.count}");
                            chipLabel.AddToClassList("quest-reward-name");
                            chip.Add(chipLabel);
                            rewardsContainer.Add(chip);
                        }

                        actionBtn.text = "Collect";
                        actionBtn.AddToClassList("quest-collect-btn");
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.CollectQuestRewards(mallumIndex);
                            Refresh();
                        };
                        break;
                }

                activeSection.Add(card);
            }

            if (!hasActive)
            {
                var empty = new Label("No active Mallums");
                empty.AddToClassList("quest-empty-text");
                activeSection.Add(empty);
            }
            else
            {
                var title = new Label("Active");
                title.AddToClassList("quest-section-title");
                title.AddToClassList("quest-section-title-first");
                activeSection.Insert(0, title);
            }
        }

        private void BuildAvailableSection()
        {
            availableSection.Clear();
            var quests = MallumManager.Instance.GetAvailableQuests();
            if (quests.Count == 0) return;

            var title = new Label("Available Quests");
            title.AddToClassList("quest-section-title");
            availableSection.Add(title);

            int available = MallumManager.Instance.GetAvailableMallumCount();

            foreach (var quest in quests)
            {
                var card = questCardTemplate.CloneTree();
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                nameLabel.text = quest.questName;
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                lockedLabel.style.display = DisplayStyle.None;

                // Show reward pool preview
                foreach (var reward in quest.rewardPool)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("quest-reward-chip");
                    var chipLabel = new Label(reward.seed != null ? reward.seed.seedName : "?");
                    chipLabel.AddToClassList("quest-reward-name");
                    chip.Add(chipLabel);
                    rewardsContainer.Add(chip);
                }

                var capturedQuest = quest;
                actionBtn.text = $"Send Mallum ({available} available)";
                actionBtn.SetEnabled(available > 0);
                actionBtn.clicked += () =>
                {
                    MallumManager.Instance.SendOnQuest(capturedQuest);
                    Refresh();
                };

                availableSection.Add(card);
            }
        }

        private void BuildLockedSection()
        {
            lockedSection.Clear();
            var locked = MallumManager.Instance.GetLockedQuests();
            if (locked.Count == 0) return;

            var title = new Label("Locked");
            title.AddToClassList("quest-section-title");
            lockedSection.Add(title);

            foreach (var quest in locked)
            {
                var card = questCardTemplate.CloneTree();
                var cardRoot = card.Q(className: "quest-card");
                var nameLabel = card.Q<Label>("quest-name");
                var durationLabel = card.Q<Label>("quest-duration");
                var descLabel = card.Q<Label>("quest-description");
                var rewardsContainer = card.Q("quest-rewards");
                var progressContainer = card.Q("quest-progress-container");
                var timerLabel = card.Q<Label>("quest-timer");
                var actionBtn = card.Q<Button>("quest-action");
                var lockedLabel = card.Q<Label>("quest-locked");

                cardRoot.AddToClassList("quest-card--locked");
                nameLabel.text = quest.questName;
                durationLabel.text = FormatDuration(quest.durationMinutes);
                descLabel.text = quest.description;
                progressContainer.style.display = DisplayStyle.None;
                timerLabel.style.display = DisplayStyle.None;
                actionBtn.style.display = DisplayStyle.None;
                lockedLabel.text = $"Requires Flame Level {quest.requiredFlameLevel}";
            }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "Done";
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            int s = (int)(seconds % 60);
            if (h > 0) return $"{h}h {m}m";
            if (m > 0) return $"{m}m {s}s";
            return $"{s}s";
        }

        private static string FormatDuration(int minutes)
        {
            if (minutes >= 60)
            {
                int h = minutes / 60;
                int m = minutes % 60;
                return m > 0 ? $"{h}h {m}m" : $"{h}h";
            }
            return $"{minutes}m";
        }
    }
}
```

**Step 2: Check console for compilation errors**

**Step 3: Commit**

```
feat: add QuestUI controller for quest overlay panel
```

---

### Task 8: QuestButtonUI — floating button with badge

**Files:**
- Create: `Assets/Scripts/UI/QuestButtonUI.cs`

**Step 1: Implement QuestButtonUI**

```csharp
// Assets/Scripts/UI/QuestButtonUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class QuestButtonUI : MonoBehaviour
    {
        private Button floatBtn;
        private Label badge;

        public void Initialize(VisualElement root)
        {
            floatBtn = root.Q<Button>("quest-float-btn");
            badge = root.Q<Label>("quest-badge");
            UpdateBadge();
        }

        public void UpdateBadge()
        {
            if (MallumManager.Instance == null)
            {
                badge.style.display = DisplayStyle.None;
                return;
            }

            int completed = MallumManager.Instance.GetCompletedQuestCount();
            if (completed > 0)
            {
                badge.text = completed.ToString();
                badge.style.display = DisplayStyle.Flex;
            }
            else
            {
                badge.style.display = DisplayStyle.None;
            }
        }
    }
}
```

**Step 2: Commit**

```
feat: add QuestButtonUI floating button with badge
```

---

### Task 9: Wire everything into CampFireUI

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs`

**Step 1: Add quest UI wiring to CampFireUI**

Add fields (after line 21, `debugPanel`):
```csharp
private QuestUI questUI;
private QuestButtonUI questButton;
```

Add panel field (after line 30, `debugPanelElement`):
```csharp
private VisualElement questsPanel;
```

In `Start()`, after line 51 (`debugPanel = GetComponent...`), add:
```csharp
questUI = GetComponent<QuestUI>();
questButton = GetComponent<QuestButtonUI>();
```

After line 60 (`debugPanel?.Initialize(root)`), add:
```csharp
questUI?.Initialize(root);
questButton?.Initialize(root);
```

After line 70 (`debugPanelElement = root.Q("debug-panel")`), add:
```csharp
questsPanel = root.Q("quests-panel");
```

In `HideAllPanels()` method, after line 144 (`debugPanelElement`), add:
```csharp
if (questsPanel != null) questsPanel.style.display = DisplayStyle.None;
```

After bottom-nav wiring (after line 84), add quest button click handler:
```csharp
// Wire quest float button
var questFloatBtn = root.Q<Button>("quest-float-btn");
if (questFloatBtn != null)
{
    questFloatBtn.clicked += () =>
    {
        questUI?.Refresh();
        OpenOverlay("Quests", questsPanel);
    };
}
```

Wire MallumManager events to refresh badge (after quest float button wiring):
```csharp
if (MallumManager.Instance != null)
{
    MallumManager.Instance.OnMallumsChanged += () => questButton?.UpdateBadge();
}
```

**Step 2: Add QuestUI and QuestButtonUI components to the "--- UI ---" GameObject in the scene**

This must be done via Unity MCP or by editing the scene YAML. Use `manage_components` to add `QuestUI` and `QuestButtonUI` to the `--- UI ---` GameObject.

**Step 3: Add MallumManager to the scene**

Use `manage_components` to add `MallumManager` to the `--- Managers ---` or equivalent manager GameObject. Wire the `config` serialized field to the MallumConfig asset.

**Step 4: Verify in Unity** — check console for errors, tap floating button, confirm quest panel opens.

**Step 5: Commit**

```
feat: wire QuestUI, QuestButtonUI, and MallumManager into scene
```

---

### Task 10: GameManager — initialize mallums for new players

**Files:**
- Modify: `Assets/Scripts/Managers/GameManager.cs:35-48`

**Step 1: Add mallum initialization**

In `GameManager.InitializeNewPlayer()` (line 35-48), add before the final `SaveManager.Instance.Save()` call (line 47):

```csharp
// Initialize mallums based on flame level
if (MallumManager.Instance != null)
{
    int maxMallums = MallumManager.Instance.Config.GetMaxMallums(data.flameLevel);
    MallumManager.EnsureMallumCount(data.mallums, maxMallums);
}
```

**Step 2: Write test**

Add to `TestMallumData.cs`:

```csharp
[Test]
public void MallumConfig_GetMaxMallums_Level1_Returns1()
{
    var config = ScriptableObject.CreateInstance<MallumConfig>();
    // Default values: [1, 1, 2, 2, 3]
    Assert.AreEqual(1, config.GetMaxMallums(1));
}
```

Note: This test depends on the default values in the code. Since MallumConfig uses `[SerializeField] private int[]`, the code default is `{ 1, 1, 2, 2, 3 }`, but the asset will override. The test validates the code path works.

**Step 3: Run all tests**

Expected: All tests pass (previous + new).

**Step 4: Commit**

```
feat: initialize mallums for new players in GameManager
```

---

### Task 11: Final integration test and cleanup

**Step 1: Run all EditMode tests**

Verify all tests pass.

**Step 2: Open Unity, enter Play mode**

Verify:
- Floating quest button appears bottom-left
- Tapping it opens quest overlay with "Swamp Forage" available
- "Send Mallum" sends a mallum, card moves to Active section
- After timer completes (use debug time skip), card shows "Collect"
- Collecting adds seeds to Apotheke
- Vase "Send Mallum" shows availability count
- When all mallums are busy, both quest send and vase send are disabled

**Step 3: Check console for errors/warnings**

**Step 4: Final commit**

```
feat: complete Mallum quest system integration
```
