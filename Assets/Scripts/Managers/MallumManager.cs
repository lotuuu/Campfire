using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class MallumManager : MonoBehaviour
    {
        public static MallumManager Instance { get; private set; }

        [SerializeField] private MallumConfig config;
        [SerializeField] private MallumHouseConfig houseConfig;

        private QuestData[] allQuests;

        public MallumConfig Config => config;
        public MallumHouseConfig HouseConfig => houseConfig;
        public event Action OnMallumsChanged;

        public void NotifyChanged() => OnMallumsChanged?.Invoke();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allQuests = Resources.LoadAll<QuestData>("Quests");
        }

        private void Start()
        {
            var data = SaveManager.Instance.Data;
            int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
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
            // Mallum count now determined by houses, not flame level
        }

        private void Update()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;

            foreach (var mallum in data.mallums)
            {
                if (mallum.state == MallumState.FetchingWater)
                {
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

        public int GetTotalMallumCount()
        {
            return SaveManager.Instance.Data.mallums.Count;
        }

        public int GetMaxMallumCount()
        {
            return houseConfig.GetMaxMallums(SaveManager.Instance.Data.mallumHouses.Count);
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

        public bool CraftMallumHouse(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = houseConfig.GetNextHouseCost(data.mallumHouses.Count);
            if (cost == null) return false;

            // Check mana
            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;

            // Check seeds
            if (!CanAffordSeeds(data.seedInventory, cost.seedCosts)) return false;

            // Spend mana
            if (!CurrencyManager.Instance.SpendMana(cost.manaCost)) return false;

            // Spend seeds
            foreach (var seedCost in cost.seedCosts)
            {
                var entry = data.seedInventory.Find(s => s.seedName == seedCost.seedName);
                entry.count -= seedCost.count;
                if (entry.count <= 0) data.seedInventory.Remove(entry);
            }

            // Place house
            data.mallumHouses.Add(new MallumHouseSave { gridX = gridX, gridY = gridY });

            // Add new mallums
            int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
            EnsureMallumCount(data.mallums, max);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            return true;
        }

        public QuestData[] GetAllQuests() => allQuests;

        public List<QuestData> GetAvailableQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var available = new List<QuestData>();
            foreach (var q in allQuests)
                if (q.requiredFlameLevel <= level)
                    available.Add(q);
            available.Sort((a, b) => a.requiredFlameLevel.CompareTo(b.requiredFlameLevel));
            return available;
        }

        public List<QuestData> GetLockedQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var locked = new List<QuestData>();
            foreach (var q in allQuests)
                if (q.requiredFlameLevel > level)
                    locked.Add(q);
            locked.Sort((a, b) => a.requiredFlameLevel.CompareTo(b.requiredFlameLevel));
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

        public static bool CanAffordSeeds(List<SeedInventoryEntry> inventory, List<SeedCost> seedCosts)
        {
            foreach (var seedCost in seedCosts)
            {
                var entry = inventory.Find(s => s.seedName == seedCost.seedName);
                if (entry == null || entry.count < seedCost.count) return false;
            }
            return true;
        }

        public static void EnsureMallumCount(List<MallumSave> mallums, int targetCount)
        {
            while (mallums.Count < targetCount)
                mallums.Add(new MallumSave());
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
                    if (roll < cumulative)
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
            if (quest == null) return true;

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
