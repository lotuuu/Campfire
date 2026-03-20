using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class VisitorManager : MonoBehaviour
    {
        public static VisitorManager Instance { get; private set; }

        public event Action OnVisitorArrived;
        public event Action OnVisitorDeparted;

        /// <summary>When true, visitor stays regardless of time of day.</summary>
        public bool DebugKeepVisitor { get; set; }

        public void NotifyVisitorArrived()
        {
            OnVisitorArrived?.Invoke();
            AudioManager.Instance?.PlaySFX("visitor_arrive");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            // Clean stale visitor from a previous day
            if (data.currentVisitor != null)
            {
                if (string.IsNullOrEmpty(data.currentVisitor.appearedAtUtc))
                {
                    DismissVisitor(data);
                    SaveManager.Instance.Save();
                }
                else
                {
                    var appearedUtc = DateTime.Parse(data.currentVisitor.appearedAtUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    if (appearedUtc.Date != GameTime.UtcNow.Date)
                    {
                        DismissVisitor(data);
                        SaveManager.Instance.Save();
                    }
                }
            }

            int before = data.activeQuests.Count;
            CleanExpiredQuests(data, GameTime.UtcNow);
            if (data.activeQuests.Count < before)
                SaveManager.Instance.Save();
        }

        private bool _fetching;

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            var now = GameTime.Now;
            var utcNow = GameTime.UtcNow;

            // Departure: remove visitor if outside visitor hour (unless debug-forced)
            if (data.currentVisitor != null && !IsVisitorHour(now) && !DebugKeepVisitor)
            {
                DismissVisitor(data);
                SaveManager.Instance.Save();
                OnVisitorDeparted?.Invoke();
                return;
            }

            // Arrival: fetch if in visitor hour, no current visitor, not already fetched today, and tutorial complete
            if (IsVisitorHour(now) && data.currentVisitor == null && !_fetching
                && (TutorialManager.Instance == null || TutorialManager.Instance.IsComplete))
            {
                string todayUtc = utcNow.Date.ToString("o");
                if (data.lastVisitorFetchDateUtc == todayUtc) return;

                if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn) return;

                _fetching = true;
                _ = FetchTonightVisitorAsync(data, todayUtc);
            }
        }

        private async Task FetchTonightVisitorAsync(SaveData data, string todayUtc)
        {
            try
            {
                var visitor = await FetchTonightVisitor(data, todayUtc);
                if (visitor != null)
                {
                    data.currentVisitor = visitor;
                    data.lastVisitorFetchDateUtc = todayUtc;
                    SaveManager.Instance.Save();
                    OnVisitorArrived?.Invoke();
                    AudioManager.Instance?.PlaySFX("visitor_arrive");
                }
                else
                {
                    // Mark fetched so we don't retry
                    data.lastVisitorFetchDateUtc = todayUtc;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"VisitorManager: FetchTonightVisitor failed: {e.Message}");
            }
            finally
            {
                _fetching = false;
            }
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static bool IsVisitorHour(DateTime localTime)
        {
            return localTime.Hour >= 22;
        }

        public static void DismissVisitor(SaveData data)
        {
            data.currentVisitor = null;
        }

        public static void CleanExpiredQuests(SaveData data, DateTime utcNow)
        {
            data.activeQuests.RemoveAll(q =>
            {
                if (string.IsNullOrEmpty(q.returnDateUtc)) return true;
                var returnDate = DateTime.Parse(q.returnDateUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                return utcNow > returnDate.AddDays(1);
            });
        }

        public static void ApplyGift(VisitorSave visitor, SaveData data)
        {
            if (visitor.giftClaimed) return;

            switch (visitor.giftType)
            {
                case "water":
                    int remaining = visitor.giftAmount;
                    foreach (var vase in data.vases)
                    {
                        int space = vase.capacity - vase.currentWater;
                        if (space > 0)
                        {
                            int fill = Math.Min(space, remaining);
                            vase.currentWater += fill;
                            remaining -= fill;
                            if (remaining <= 0) break;
                        }
                    }
                    break;
                case "seed":
                case "item":
                    ApothekeManager.Instance?.AddItem(visitor.giftName, visitor.giftAmount);
                    break;
            }

            visitor.giftClaimed = true;
        }

        public static bool CanAffordOffer(MerchantOfferSave offer, List<InventoryItem> inventory)
        {
            if (CurrencyManager.FreeMode) return true;
            foreach (var cost in offer.costs)
            {
                var item = inventory.Find(i => i.itemKey == cost.itemKey);
                if (item == null || item.count < cost.count) return false;
            }
            return true;
        }

        public static void ExecuteTrade(MerchantOfferSave offer, List<InventoryItem> inventory)
        {
            // Consume items
            if (!CurrencyManager.FreeMode)
            {
                foreach (var cost in offer.costs)
                {
                    var item = inventory.Find(i => i.itemKey == cost.itemKey);
                    if (item == null) continue;
                    item.count -= cost.count;
                    if (item.count <= 0) inventory.Remove(item);
                }
            }

            // Add reward item
            var entry = inventory.Find(i => i.itemKey == offer.rewardItemKey);
            if (entry != null)
                entry.count += offer.rewardCount;
            else
                inventory.Add(new InventoryItem
                    { itemKey = offer.rewardItemKey, count = offer.rewardCount });
        }

        public static VisitorSave BuildVisitorSave(VisitorResponse response, int gridX, int gridY, string dateUtc)
        {
            var save = new VisitorSave
            {
                gridX = gridX,
                gridY = gridY,
                visitorId = response.visitor_id,
                visitorName = response.name,
                portraitId = response.portrait_id,
                appearedAtUtc = dateUtc,
                fetchedDateUtc = dateUtc
            };

            // Parse visitor type
            switch (response.visitor_type)
            {
                case "merchant":
                    save.type = VisitorType.Merchant;
                    break;
                case "gifter":
                    save.type = VisitorType.Gifter;
                    break;
                case "quester":
                    save.type = VisitorType.Quester;
                    break;
            }

            // Dialogue
            if (response.dialogue != null)
                save.dialogueLines = new List<string>(response.dialogue);

            // Merchant offers — List<> is empty (not null) when absent from JSON
            if (response.offers != null && response.offers.Count > 0)
            {
                save.offers = new List<MerchantOfferSave>();
                foreach (var offer in response.offers)
                {
                    var offerSave = new MerchantOfferSave
                    {
                        rewardItemKey = offer.rewardItemKey,
                        rewardCount = offer.rewardCount
                    };
                    if (offer.costs != null)
                        offerSave.costs = new List<TradeCost>(offer.costs);
                    save.offers.Add(offerSave);
                }
            }

            // Gift — JsonUtility never returns null for nested [Serializable] fields,
            // so check whether any gift data is actually present.
            bool hasGift = response.gift != null &&
                (!string.IsNullOrEmpty(response.gift.type) || !string.IsNullOrEmpty(response.gift.name) || response.gift.amount > 0);
            if (hasGift)
            {
                if (string.IsNullOrEmpty(response.gift.type))
                    Debug.LogWarning($"[VisitorManager] Gift missing 'type' field for visitor '{response.visitor_id}'");
                if (string.IsNullOrEmpty(response.gift.name))
                    Debug.LogWarning($"[VisitorManager] Gift missing 'name' field for visitor '{response.visitor_id}'");
                if (response.gift.amount <= 0)
                    Debug.LogWarning($"[VisitorManager] Gift has invalid amount ({response.gift.amount}) for visitor '{response.visitor_id}'");
                save.giftType = response.gift.type;
                save.giftName = response.gift.name;
                save.giftAmount = response.gift.amount;
            }

            // Quest — JsonUtility never returns null for nested [Serializable] fields,
            // so check whether any quest data is actually present.
            bool hasQuest = response.quest != null &&
                (!string.IsNullOrEmpty(response.quest.request_item) || response.quest.is_return || response.quest.quest_id > 0);
            if (hasQuest)
            {
                if (string.IsNullOrEmpty(response.quest.request_item) && !response.quest.is_return)
                    Debug.LogWarning($"[VisitorManager] Quest missing 'request_item' for visitor '{response.visitor_id}'");
                if (response.quest.reward != null && string.IsNullOrEmpty(response.quest.reward.name))
                    Debug.LogWarning($"[VisitorManager] Quest reward missing 'name' for visitor '{response.visitor_id}'");
                save.requestItem = response.quest.request_item;
                save.requestCount = response.quest.request_count;
                save.returnDays = response.quest.return_days;
                save.rewardJson = JsonUtility.ToJson(response.quest.reward);
                save.isReturnVisit = response.quest.is_return;
                if (response.quest.return_dialogue != null)
                    save.returnDialogue = new List<string>(response.quest.return_dialogue);
            }

            return save;
        }

        // --- Async methods (need Instance / network) ---

        public async Task<VisitorSave> FetchTonightVisitor(SaveData data, string todayUtc)
        {
            var url = SocialService.ServerBaseUrl + "/visitors/tonight";
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {SocialService.Instance.AuthToken}");

            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"VisitorManager: FetchTonightVisitor failed: {request.error} — {request.downloadHandler.text}");
                return null;
            }

            var response = JsonUtility.FromJson<VisitorResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrEmpty(response.visitor_id))
                return null;

            int gridRadius = ConfigService.Instance != null
                ? ConfigService.Instance.FlameConfig.GetGridSize(data.flameLevel)
                : 2;

            var freeTiles = BirdManager.GetFreeTiles(data, gridRadius);
            if (freeTiles.Count == 0)
            {
                Debug.LogWarning("VisitorManager: No free tiles for visitor");
                return null;
            }

            var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];
            return BuildVisitorSave(response, tile.q, tile.r, todayUtc);
        }

        public async Task<bool> AcceptQuest(VisitorSave visitor)
        {
            if (visitor == null || visitor.type != VisitorType.Quester) return false;

            var questReward = JsonUtility.FromJson<QuestReward>(visitor.rewardJson);
            var acceptRequest = new QuestAcceptRequest
            {
                visitor_id = visitor.visitorId,
                request_item = visitor.requestItem,
                request_count = visitor.requestCount,
                return_days = visitor.returnDays > 0 ? visitor.returnDays : 7,
                reward = questReward,
                return_dialogue = visitor.returnDialogue
            };

            var body = JsonUtility.ToJson(acceptRequest);
            var url = SocialService.ServerBaseUrl + "/visitors/quest/accept";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SocialService.Instance.AuthToken}");

            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"VisitorManager: AcceptQuest failed: {request.error} — {request.downloadHandler.text}");
                return false;
            }

            var response = JsonUtility.FromJson<QuestAcceptResponse>(request.downloadHandler.text);

            var data = SaveManager.Instance.Data;
            data.activeQuests.Add(new ActiveVisitorQuest
            {
                serverQuestId = response.quest_id,
                visitorId = visitor.visitorId,
                visitorName = visitor.visitorName,
                portraitId = visitor.portraitId,
                requestItem = visitor.requestItem,
                requestCount = visitor.requestCount,
                returnDateUtc = response.return_date,
                rewardJson = visitor.rewardJson,
                returnDialogue = visitor.returnDialogue != null ? new List<string>(visitor.returnDialogue) : new List<string>()
            });

            SaveManager.Instance.Save();
            return true;
        }

        public async Task<bool> CompleteQuest(ActiveVisitorQuest quest)
        {
            if (quest == null) return false;

            var body = JsonUtility.ToJson(new QuestCompleteRequest { quest_id = quest.serverQuestId });
            var url = SocialService.ServerBaseUrl + "/visitors/quest/complete";
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {SocialService.Instance.AuthToken}");

            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"VisitorManager: CompleteQuest failed: {request.error} — {request.downloadHandler.text}");
                return false;
            }

            // Consume requested items from player inventory
            var data = SaveManager.Instance.Data;
            var requestedItem = data.inventory.Find(i => i.itemKey == quest.requestItem);
            if (requestedItem != null)
            {
                requestedItem.count -= quest.requestCount;
                if (requestedItem.count <= 0) data.inventory.Remove(requestedItem);
            }

            // Apply reward
            var reward = JsonUtility.FromJson<QuestReward>(quest.rewardJson);
            if (reward != null)
            {
                if (string.IsNullOrEmpty(reward.name))
                    Debug.LogWarning($"[VisitorManager] Quest reward has no name — rewardJson: {quest.rewardJson}");
                else
                    ApothekeManager.Instance?.AddItem(reward.name, reward.count);
            }

            // Remove from active quests
            data.activeQuests.Remove(quest);
            SaveManager.Instance.Save();
            return true;
        }

        // --- JSON Serialization Types ---

        [Serializable]
        public class VisitorResponse
        {
            public string visitor_type;
            public string visitor_id;
            public string name;
            public string portrait_id;
            public List<string> dialogue;
            public List<OfferResponse> offers;
            public GiftResponse gift;
            public QuestResponse quest;
        }

        [Serializable]
        public class OfferResponse
        {
            public List<TradeCost> costs;
            public string rewardItemKey;
            public int rewardCount;
        }

        [Serializable]
        public class GiftResponse
        {
            public string type;
            public string name;
            public int amount;
        }

        [Serializable]
        public class QuestResponse
        {
            public int quest_id;
            public string request_item;
            public int request_count;
            public int return_days;
            public QuestReward reward;
            public List<string> return_dialogue;
            public bool is_return;
        }

        [Serializable]
        public class QuestReward
        {
            public string type;
            public string name;
            public int count;
        }

        [Serializable]
        private class QuestAcceptRequest
        {
            public string visitor_id;
            public string request_item;
            public int request_count;
            public int return_days;
            public QuestReward reward;
            public List<string> return_dialogue;
        }

        [Serializable]
        private class QuestAcceptResponse
        {
            public int quest_id;
            public string return_date;
        }

        [Serializable]
        private class QuestCompleteRequest
        {
            public int quest_id;
        }
    }
}
