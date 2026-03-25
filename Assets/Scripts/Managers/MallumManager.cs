using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class MallumManager : MonoBehaviour
    {
        public static MallumManager Instance { get; private set; }

        public event Action OnMallumsChanged;

        public void NotifyChanged() => OnMallumsChanged?.Invoke();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var houseConfig = ConfigService.Instance?.MallumHouseConfig;
            if (houseConfig == null) return;

            var data = SaveManager.Instance.Data;
            int max = houseConfig.GetMaxMallums(data.mallumHouses.Count);
            EnsureMallumCount(data.mallums, max);
        }

        private void Update()
        {
            if (SaveManager.Instance?.Data == null) return;
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
                        AudioManager.Instance?.PlaySFX("quest_complete");
                        NotificationService.Instance?.CancelQuestNotification(i);
                        changed = true;

                        // Notify server
                        if (GameService.Instance != null && GameService.Instance.IsOnline
                            && !string.IsNullOrEmpty(mallum.assignedQuestName))
                        {
                            _ = GameService.Instance.OptimisticAction(GameService.Instance.CheckQuest(mallum.assignedQuestName), "CheckQuest");
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

        private void OnApplicationPause(bool paused)
        {
            if (paused) ScheduleAllMallumNotifications();
        }

        private void ScheduleAllMallumNotifications()
        {
            var ns = NotificationService.Instance;
            if (ns == null) return;

            var data = SaveManager.Instance.Data;
            for (int i = 0; i < data.mallums.Count; i++)
            {
                var mallum = data.mallums[i];
                if (mallum.state == MallumState.OnQuest)
                {
                    float remaining = GetQuestRemainingSeconds(mallum);
                    if (remaining > 0)
                        ns.ScheduleQuestNotification(i, mallum.assignedQuestName, remaining);
                }
                else if (mallum.state == MallumState.FetchingWater && mallum.assignedVaseIndex >= 0)
                {
                    float remaining = VaseManager.Instance != null
                        ? VaseManager.Instance.GetRemainingSeconds(mallum.assignedVaseIndex)
                        : 0f;
                    if (remaining > 0)
                        ns.ScheduleWaterFetchNotification(i, remaining);
                }
            }
        }

        public int GetTotalMallumCount()
        {
            return SaveManager.Instance.Data.mallums.Count;
        }

        public int GetMaxMallumCount()
        {
            return ConfigService.Instance.MallumHouseConfig.GetMaxMallums(SaveManager.Instance.Data.mallumHouses.Count);
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
                    double seconds = ConfigService.Instance.VaseConfig.fill_duration_minutes * 60.0;
                    NotificationService.Instance.ScheduleWaterFetchNotification(mallumIndex, seconds);
                }
            }

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            AudioManager.Instance?.PlaySFXWithFadeOut("mallum_footsteps", 1.5f);

            // Note: VaseManager.SendToCollect already notifies server via GameService.FillVase

            return true;
        }

        public bool SendOnQuest(ServerQuestConfig quest)
        {
            var data = SaveManager.Instance.Data;
            if (!ClaimMallumForQuest(data.mallums, quest.questKey, GameTime.UtcNow.ToString("o")))
                return false;

            if (NotificationService.Instance != null)
            {
                int mallumIndex = data.mallums.FindIndex(m => m.state == MallumState.OnQuest && m.assignedQuestName == quest.questKey);
                if (mallumIndex >= 0)
                {
                    double seconds = quest.durationMinutes * 60.0;
                    NotificationService.Instance.ScheduleQuestNotification(mallumIndex, quest.questName, seconds);
                }
            }

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            AudioManager.Instance?.PlaySFX("mallum_gear_up");
            AudioManager.Instance?.PlaySFXWithFadeOut("mallum_footsteps", 1.5f);

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = GameService.Instance.OptimisticAction(GameService.Instance.StartQuest(quest.questKey), "StartQuest");
            }

            return true;
        }

        public List<RewardEntry> CollectQuestRewards(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return null;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.QuestComplete) return null;

            string questName = mallum.assignedQuestName;
            var rewards = CollectRewards(mallum);

            foreach (var r in rewards)
                ApothekeManager.Instance.AddItem(r.itemKey, r.count);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            AudioManager.Instance?.PlaySFX("quest_collect_rewards");

            // Notify server for authoritative rewards
            if (GameService.Instance != null && GameService.Instance.IsOnline
                && !string.IsNullOrEmpty(questName))
            {
                _ = GameService.Instance.OptimisticAction(GameService.Instance.CollectQuest(questName), "CollectQuest");
            }

            return rewards;
        }

        public async Task<bool> BuildMallumHouse(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = GetNextHouseCost();
            if (cost == null) return false;

            // Check mana
            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;

            // Check harvests
            if (!CanAffordHarvests(data.inventory, cost.harvestCosts)) return false;

            // Server-first: call server to build house
            if (GameService.Instance == null || !GameService.Instance.IsOnline)
            {
                CampFireUI.Instance?.ShowToast("Could not reach server");
                return false;
            }

            var serverResult = await GameService.Instance.BuildMallumHouse(gridX, gridY);
            if (serverResult == null)
            {
                CampFireUI.Instance?.ShowToast("Could not reach server");
                return false;
            }

            // Server confirmed — spend resources locally
            if (!CurrencyManager.Instance.SpendMana(cost.manaCost)) return false;

            if (!CurrencyManager.FreeMode)
            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.inventory.Find(i => i.itemKey == hc.itemKey);
                if (entry == null) continue;
                entry.count -= hc.count;
                if (entry.count <= 0) data.inventory.Remove(entry);
            }

            // Place house with server-assigned ID
            data.mallumHouses.Add(new MallumHouseSave
            {
                gridX = gridX,
                gridY = gridY,
                serverId = serverResult.id
            });

            // Add new mallums
            int max = ConfigService.Instance.MallumHouseConfig.GetMaxMallums(data.mallumHouses.Count);
            EnsureMallumCount(data.mallums, max);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            return true;
        }

        public BuildingCost GetNextHouseCost()
        {
            return ConfigService.Instance?.GetHouseCost(SaveManager.Instance.Data.mallumHouses.Count);
        }

        public List<ServerQuestConfig> GetAvailableQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var allQuests = ConfigService.Instance?.GetAllQuests();
            if (allQuests == null) return new List<ServerQuestConfig>();

            var available = new List<ServerQuestConfig>();
            foreach (var q in allQuests)
                if (q.requiredFlameLevel <= level)
                    available.Add(q);
            available.Sort((a, b) => a.requiredFlameLevel.CompareTo(b.requiredFlameLevel));
            return available;
        }

        public List<ServerQuestConfig> GetLockedQuests()
        {
            int level = SaveManager.Instance.Data.flameLevel;
            var allQuests = ConfigService.Instance?.GetAllQuests();
            if (allQuests == null) return new List<ServerQuestConfig>();

            var locked = new List<ServerQuestConfig>();
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

        private static string QuestSpeedItem =>
            ConfigService.Instance.MallumHouseConfig.quest_speed_item;

        private static string VaseSpeedItem =>
            ConfigService.Instance.VaseConfig.speed_item;

        public bool CanUseQuestSpeedItem()
        {
            if (CurrencyManager.FreeMode) return true;
            var item = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == QuestSpeedItem);
            return item != null && item.count > 0;
        }

        public int GetQuestSpeedItemCount()
        {
            var item = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == QuestSpeedItem);
            return item?.count ?? 0;
        }

        private bool ConsumeQuestSpeedItem()
        {
            if (CurrencyManager.FreeMode) return true;
            var data = SaveManager.Instance.Data;
            var drink = data.inventory.Find(i => i.itemKey == QuestSpeedItem);
            if (drink == null || drink.count <= 0) return false;
            drink.count--;
            if (drink.count <= 0) data.inventory.Remove(drink);
            SaveManager.Instance.Save();
            return true;
        }

        public bool CanUseVaseSpeedItem()
        {
            if (CurrencyManager.FreeMode) return true;
            var item = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == VaseSpeedItem);
            return item != null && item.count > 0;
        }

        public int GetVaseSpeedItemCount()
        {
            var item = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == VaseSpeedItem);
            return item?.count ?? 0;
        }

        private bool ConsumeVaseSpeedItem()
        {
            if (CurrencyManager.FreeMode) return true;
            var data = SaveManager.Instance.Data;
            var drink = data.inventory.Find(i => i.itemKey == VaseSpeedItem);
            if (drink == null || drink.count <= 0) return false;
            drink.count--;
            if (drink.count <= 0) data.inventory.Remove(drink);
            SaveManager.Instance.Save();
            return true;
        }

        public bool SpeedUpQuest(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return false;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.OnQuest) return false;

            if (!ConsumeQuestSpeedItem()) return false;

            string questName = mallum.assignedQuestName;
            CompleteQuest(mallum);
            NotificationService.Instance?.CancelQuestNotification(mallumIndex);
            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

            if (GameService.Instance != null && GameService.Instance.IsOnline
                && !string.IsNullOrEmpty(questName))
            {
                _ = GameService.Instance.OptimisticAction(GameService.Instance.SpeedUpQuest(questName), "SpeedUpQuest");
            }

            return true;
        }

        /// <summary>
        /// Speed up a quest AND collect rewards in one atomic operation.
        /// Returns the reward list, or null on failure.
        /// The server handles this as a single call — no separate collect needed.
        /// </summary>
        public List<RewardEntry> SpeedUpAndCollectQuest(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return null;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.OnQuest) return null;

            if (!ConsumeQuestSpeedItem()) return null;

            string questName = mallum.assignedQuestName;

            // Complete quest (rolls rewards into pendingRewards)
            CompleteQuest(mallum);
            NotificationService.Instance?.CancelQuestNotification(mallumIndex);

            // Immediately collect rewards (sets state to Idle)
            var rewards = CollectRewards(mallum);
            foreach (var r in rewards)
                ApothekeManager.Instance.AddItem(r.itemKey, r.count);

            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();
            AudioManager.Instance?.PlaySFX("quest_collect_rewards");

            // Single server call — speed_up_quest on server now does speed-up + collect atomically
            if (GameService.Instance != null && GameService.Instance.IsOnline
                && !string.IsNullOrEmpty(questName))
            {
                _ = GameService.Instance.OptimisticAction(GameService.Instance.SpeedUpQuest(questName), "SpeedUpQuest");
            }

            return rewards;
        }

        public bool SpeedUpWaterFetch(int mallumIndex)
        {
            var data = SaveManager.Instance.Data;
            if (mallumIndex < 0 || mallumIndex >= data.mallums.Count) return false;
            var mallum = data.mallums[mallumIndex];
            if (mallum.state != MallumState.FetchingWater) return false;

            if (!ConsumeVaseSpeedItem()) return false;

            int vaseIndex = mallum.assignedVaseIndex;
            if (vaseIndex >= 0 && vaseIndex < data.vases.Count)
                VaseManager.Instance.InstantFinish(vaseIndex);

            FreeMallumFromWater(mallum);
            NotificationService.Instance?.CancelWaterFetchNotification(mallumIndex);
            SaveManager.Instance.Save();
            OnMallumsChanged?.Invoke();

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

        private ServerQuestConfig FindQuest(string questName)
        {
            return ConfigService.Instance?.GetQuest(questName);
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static bool CanAffordHarvests(List<InventoryItem> items, List<HarvestCost> harvestCosts)
        {
            if (CurrencyManager.FreeMode) return true;
            foreach (var hc in harvestCosts)
            {
                var entry = items.Find(i => i.itemKey == hc.itemKey);
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

        public static List<RewardEntry> RollRewards(List<ServerQuestReward> pool, int rolls)
        {
            var rewards = new List<RewardEntry>();
            float totalWeight = 0f;
            foreach (var r in pool)
            {
                if (!string.IsNullOrEmpty(r.itemKey))
                    totalWeight += r.weight;
            }

            if (totalWeight <= 0f) return rewards;

            for (int i = 0; i < rolls; i++)
            {
                float roll = UnityEngine.Random.Range(0f, totalWeight);
                float cumulative = 0f;
                foreach (var r in pool)
                {
                    if (string.IsNullOrEmpty(r.itemKey)) continue;
                    cumulative += r.weight;
                    if (roll < cumulative)
                    {
                        int count = UnityEngine.Random.Range(r.minCount, r.maxCount + 1);
                        rewards.Add(new RewardEntry
                        {
                            itemKey = r.itemKey,
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
            if (quest == null) return false;

            var startTime = DateTime.Parse(mallum.startTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            return (GameTime.UtcNow - startTime).TotalMinutes >= quest.durationMinutes;
        }

        private void CompleteQuest(MallumSave mallum)
        {
            var quest = FindQuest(mallum.assignedQuestName);
            if (quest != null)
                mallum.pendingRewards = RollRewards(quest.rewardPool, quest.rewardRolls);

            // During tutorial, guarantee at least 1 Cress seed so the player can proceed
            if (TutorialManager.Instance != null && !TutorialManager.Instance.IsComplete)
            {
                // Look up the item key for cress seeds from config
                var cressSeed = ConfigService.Instance?.GetSeed("cress");
                string cressItemKey = cressSeed?.item_key ?? "cress_seed";

                bool hasCress = false;
                foreach (var r in mallum.pendingRewards)
                    if (r.itemKey == cressItemKey) { hasCress = true; break; }
                if (!hasCress)
                    mallum.pendingRewards.Add(new RewardEntry { itemKey = cressItemKey, count = 1 });
            }

            mallum.state = MallumState.QuestComplete;
            mallum.startTimeUtc = null;
            HapticService.Vibrate();
        }
    }
}
