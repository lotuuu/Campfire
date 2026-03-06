using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class MallumManager : MonoBehaviour
    {
        public static MallumManager Instance { get; private set; }

        [SerializeField] private MallumHouseConfig houseConfig;

        private QuestData[] allQuests;
        private BuildingCostConfig buildingCostConfig;

        public MallumHouseConfig HouseConfig => houseConfig;

        private BuildingCostConfig LoadBuildingCostConfig()
        {
            if (buildingCostConfig == null)
                buildingCostConfig = Resources.Load<BuildingCostConfig>("Config/BuildingCostConfig");
            return buildingCostConfig;
        }

        public BuildingCost GetNextHouseCost()
        {
            return LoadBuildingCostConfig()?.GetHouseCost(SaveManager.Instance.Data.mallumHouses.Count - 1);
        }
        public event Action OnMallumsChanged;

        public void NotifyChanged() => OnMallumsChanged?.Invoke();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            allQuests = Resources.LoadAll<QuestData>("Quests");
            ApplyServerQuestConfigs();
            ApplyServerHouseConfig();
        }

        /// <summary>
        /// Overlays server quest config values onto local QuestData assets.
        /// Keeps SeedData sprite references from local assets.
        /// </summary>
        private void ApplyServerQuestConfigs()
        {
            var cs = ConfigService.Instance;
            if (cs == null || !cs.IsLoaded) return;

            foreach (var quest in allQuests)
            {
                var serverQuest = cs.GetQuest(quest.questName);
                if (serverQuest == null) continue;

                quest.durationMinutes = serverQuest.durationMinutes;
                quest.requiredFlameLevel = serverQuest.requiredFlameLevel;
                quest.rewardRolls = serverQuest.rewardRolls;

                // Overlay reward pool — match by seed_name, keep local SeedData refs
                var serverPool = cs.GetQuestRewardPool(quest.questName);
                if (serverPool != null && serverPool.Count > 0)
                {
                    // Build a lookup of existing seed references by name
                    var seedLookup = new Dictionary<string, SeedData>();
                    foreach (var r in quest.rewardPool)
                        if (r.seed != null && !seedLookup.ContainsKey(r.seed.name))
                            seedLookup[r.seed.name] = r.seed;

                    // Also load all seeds for any new entries
                    foreach (var s in Resources.LoadAll<SeedData>("Seeds"))
                        if (!seedLookup.ContainsKey(s.name))
                            seedLookup[s.name] = s;

                    quest.rewardPool.Clear();
                    foreach (var sr in serverPool)
                    {
                        string seedName = sr.TryGetValue("seed", out var sn) && sn is string s ? s
                            : sr.TryGetValue("seed_name", out var sn2) && sn2 is string s2 ? s2 : null;
                        if (seedName == null) continue;

                        float weight = sr.TryGetValue("weight", out var w) ? ToFloat(w) : 1f;
                        int min = sr.TryGetValue("minCount", out var mn) ? (int)ToFloat(mn)
                            : sr.TryGetValue("min", out var mn2) ? (int)ToFloat(mn2) : 1;
                        int max = sr.TryGetValue("maxCount", out var mx) ? (int)ToFloat(mx)
                            : sr.TryGetValue("max", out var mx2) ? (int)ToFloat(mx2) : 1;

                        seedLookup.TryGetValue(seedName, out var seedRef);

                        quest.rewardPool.Add(new QuestReward
                        {
                            seed = seedRef,
                            weight = weight,
                            minCount = min,
                            maxCount = max
                        });
                    }
                }
            }
        }

        private void ApplyServerHouseConfig()
        {
            var cs = ConfigService.Instance;
            if (cs == null || !cs.IsLoaded || cs.MallumHouseConfig == null) return;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            // Apply mallumsPerHouse to MallumHouseConfig
            typeof(MallumHouseConfig).GetField("mallumsPerHouse", flags)?.SetValue(houseConfig, cs.MallumHouseConfig.mallums_per_house);

            // Apply house costs to BuildingCostConfig
            var serverCosts = cs.HouseCosts;
            if (serverCosts != null && serverCosts.Count > 0)
            {
                var bcc = LoadBuildingCostConfig();
                if (bcc == null) return;

                var costs = new List<BuildingCost>();
                foreach (var sc in serverCosts)
                {
                    var cost = new BuildingCost();
                    cost.manaCost = sc.TryGetValue("mana", out var m) ? ToFloat(m) : 0f;

                    if (sc.TryGetValue("harvests", out var hObj) && hObj is List<object> harvests)
                    {
                        foreach (var h in harvests)
                        {
                            if (h is Dictionary<string, object> hd)
                            {
                                string item = hd.TryGetValue("item", out var n) && n is string s ? s : null;
                                int count = hd.TryGetValue("count", out var c) ? (int)ToFloat(c) : 0;
                                if (item != null)
                                    cost.harvestCosts.Add(new HarvestCost { itemName = item, count = count });
                            }
                        }
                    }
                    costs.Add(cost);
                }
                typeof(BuildingCostConfig).GetField("houseCosts", flags)?.SetValue(bcc, costs);
            }
        }

        private static float ToFloat(object val)
        {
            if (val is double d) return (float)d;
            if (val is long l) return l;
            if (val is float f) return f;
            if (val is int i) return i;
            return 0f;
        }

        private void Start()
        {
            var data = SaveManager.Instance.Data;
            int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
            EnsureMallumCount(data.mallums, max);
        }

        private void Update()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;

            for (int i = 0; i < data.mallums.Count; i++)
            {
                var mallum = data.mallums[i];
                if (mallum.state == MallumState.FetchingWater)
                {
                    if (mallum.assignedVaseIndex >= 0 &&
                        mallum.assignedVaseIndex < data.vases.Count)
                    {
                        var vase = data.vases[mallum.assignedVaseIndex];
                        if (vase.state != VaseState.Filling)
                        {
                            FreeMallumFromWater(mallum);
                            NotificationService.Instance?.CancelWaterFetchNotification(i);
                            changed = true;
                        }
                    }
                }
                else if (mallum.state == MallumState.OnQuest)
                {
                    if (IsQuestTimerComplete(mallum))
                    {
                        CompleteQuest(mallum);
                        NotificationService.Instance?.CancelQuestNotification(i);
                        changed = true;

                        // Notify server
                        if (GameService.Instance != null && GameService.Instance.IsOnline && mallum.serverId > 0)
                        {
                            _ = GameService.Instance.CheckQuest(mallum.serverId);
                        }
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

            if (NotificationService.Instance != null)
            {
                int mallumIndex = data.mallums.FindIndex(m => m.state == MallumState.FetchingWater && m.assignedVaseIndex == vaseIndex);
                if (mallumIndex >= 0)
                {
                    double seconds = VaseManager.Instance.Config.FillDurationMinutes * 60.0;
                    NotificationService.Instance.ScheduleWaterFetchNotification(mallumIndex, seconds);
                }
            }

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            // Note: VaseManager.SendToCollect already notifies server via GameService.FillVase

            return true;
        }

        public bool SendOnQuest(QuestData quest)
        {
            var data = SaveManager.Instance.Data;
            if (!ClaimMallumForQuest(data.mallums, quest.questName, GameTime.UtcNow.ToString("o")))
                return false;

            if (NotificationService.Instance != null)
            {
                int mallumIndex = data.mallums.FindIndex(m => m.state == MallumState.OnQuest && m.assignedQuestName == quest.questName);
                if (mallumIndex >= 0)
                {
                    double seconds = quest.durationMinutes * 60.0;
                    NotificationService.Instance.ScheduleQuestNotification(mallumIndex, quest.questName, seconds);
                }
            }

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = GameService.Instance.StartQuest(quest.questName);
            }

            return true;
        }

        public List<RewardEntry> CollectQuestRewards(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return null;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.QuestComplete) return null;

            int serverId = mallum.serverId;
            var rewards = CollectRewards(mallum);

            foreach (var r in rewards)
                ApothekeManager.Instance.AddSeed(r.seedName, r.count);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            // Notify server for authoritative rewards
            if (GameService.Instance != null && GameService.Instance.IsOnline && serverId > 0)
            {
                _ = NotifyServerCollectQuest(serverId);
            }

            return rewards;
        }

        private async Task NotifyServerCollectQuest(int mallumServerId)
        {
            var resp = await GameService.Instance.CollectQuest(mallumServerId);
            if (resp != null)
            {
                // Server response is authoritative — sync economy for seed rewards
                await EconomyService.Instance.SyncFromServer();
            }
        }

        public bool CraftMallumHouse(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = LoadBuildingCostConfig()?.GetHouseCost(data.mallumHouses.Count - 1);
            if (cost == null) return false;

            // Check mana
            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;

            // Check harvests
            if (!CanAffordHarvests(data.items, cost.harvestCosts)) return false;

            // Spend mana
            if (!CurrencyManager.Instance.SpendMana(cost.manaCost)) return false;

            // Spend harvests
            if (!CurrencyManager.FreeMode)
            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.items.Find(i => i.itemName == hc.itemName);
                entry.count -= hc.count;
                if (entry.count <= 0) data.items.Remove(entry);
            }

            // Place house
            data.mallumHouses.Add(new MallumHouseSave { gridX = gridX, gridY = gridY });

            // Add new mallums
            int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
            EnsureMallumCount(data.mallums, max);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = NotifyServerCraftHouse(gridX, gridY);
            }

            return true;
        }

        private async Task NotifyServerCraftHouse(int gridX, int gridY)
        {
            var result = await GameService.Instance.CraftMallumHouse(gridX, gridY);
            if (result != null)
            {
                var data = SaveManager.Instance.Data;
                // Find the house we just created at these coordinates and set its serverId
                var house = data.mallumHouses.Find(h => h.gridX == gridX && h.gridY == gridY && h.serverId == 0);
                if (house != null)
                {
                    house.serverId = result.id;
                    SaveManager.Instance.Save();
                }
            }
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

        private const string SpeedPotionItem = "Speed_Potion";

        public bool CanSpeedUpQuest()
        {
            if (CurrencyManager.FreeMode) return true;
            var item = SaveManager.Instance.Data.items.Find(i => i.itemName == SpeedPotionItem);
            return item != null && item.count > 0;
        }

        public int GetSpeedPotionCount()
        {
            var item = SaveManager.Instance.Data.items.Find(i => i.itemName == SpeedPotionItem);
            return item?.count ?? 0;
        }

        public bool ConsumeSpeedPotion()
        {
            if (CurrencyManager.FreeMode) return true;
            var data = SaveManager.Instance.Data;
            var potion = data.items.Find(i => i.itemName == SpeedPotionItem);
            if (potion == null || potion.count <= 0) return false;
            potion.count--;
            if (potion.count <= 0) data.items.Remove(potion);
            SaveManager.Instance.Save();
            return true;
        }

        public bool SpeedUpQuest(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return false;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.OnQuest) return false;

            // Consume Speed Potion
            var potion = data.items.Find(i => i.itemName == SpeedPotionItem);
            if (!CurrencyManager.FreeMode)
            {
                if (potion == null || potion.count <= 0) return false;
                potion.count--;
                if (potion.count <= 0) data.items.Remove(potion);
            }

            int serverId = mallum.serverId;
            CompleteQuest(mallum);
            NotificationService.Instance?.CancelQuestNotification(mallumIndex);
            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline && serverId > 0)
            {
                _ = GameService.Instance.SpeedUpQuest(serverId);
            }

            return true;
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

        public static bool CanAffordHarvests(List<InventoryItem> items, List<HarvestCost> harvestCosts)
        {
            if (CurrencyManager.FreeMode) return true;
            foreach (var hc in harvestCosts)
            {
                var entry = items.Find(i => i.itemName == hc.itemName);
                if (entry == null || entry.count < hc.count) return false;
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
                            seedName = r.seed.name,
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
