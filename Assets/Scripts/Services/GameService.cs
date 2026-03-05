using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class GameService : MonoBehaviour
    {
        public static GameService Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsOnline { get; private set; }

        public event Action OnStateLoaded;

        private static string ServerBaseUrl =>
#if UNITY_EDITOR
            "http://localhost:4000";
#else
            DevServerConfig.BaseUrl;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Initialization ──

        public async void Initialize()
        {
            if (!SocialService.Instance.IsSignedIn) return;

            try
            {
                using var req = GetAuth("/game/state");
                await SendAsync(req);

                if (req.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<GameStateResponse>(req.downloadHandler.text);
                    ApplyGameState(state);
                    IsInitialized = true;
                    IsOnline = true;
                    OnStateLoaded?.Invoke();
                    return;
                }

                Debug.LogWarning($"GameService: Could not load game state (HTTP {req.responseCode}), running offline.");
                IsInitialized = true;
                IsOnline = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameService: Init failed ({e.Message}), running offline.");
                IsInitialized = true;
                IsOnline = false;
            }
        }

        private void ApplyGameState(GameStateResponse state)
        {
            var data = SaveManager.Instance.Data;

            // Plots
            data.plots.Clear();
            if (state.plots != null)
            {
                foreach (var sp in state.plots)
                {
                    data.plots.Add(new PlotSave
                    {
                        serverId = sp.id,
                        seedName = sp.seedName,
                        state = ParsePlotState(sp.state),
                        plantTimeUtc = sp.plantTimeUtc,
                        waterCount = sp.waterCount,
                        lastWateredUtc = sp.lastWateredUtc,
                        gridX = sp.gridX,
                        gridY = sp.gridY,
                        skinName = sp.skinName,
                        unlockedSkins = sp.unlockedSkins ?? new List<string>()
                    });
                }
            }

            // Vases
            data.vases.Clear();
            if (state.vases != null)
            {
                foreach (var sv in state.vases)
                {
                    data.vases.Add(new VaseSave
                    {
                        serverId = sv.id,
                        capacity = sv.capacity,
                        currentWater = sv.currentWater,
                        state = ParseVaseState(sv.state),
                        fillStartTimeUtc = sv.fillStartTimeUtc,
                        gridX = sv.gridX,
                        gridY = sv.gridY,
                        skinName = sv.skinName,
                        unlockedSkins = sv.unlockedSkins ?? new List<string>()
                    });
                }
            }

            // Gardens
            data.gardens.Clear();
            if (state.gardens != null)
            {
                foreach (var sg in state.gardens)
                {
                    data.gardens.Add(new GardenSave
                    {
                        serverId = sg.id,
                        plantName = sg.plantName,
                        plantTimeUtc = sg.plantTimeUtc,
                        lastYieldTimeUtc = sg.lastYieldTimeUtc,
                        mature = sg.mature,
                        gridX = sg.gridX,
                        gridY = sg.gridY
                    });
                }
            }

            // Mallums
            data.mallums.Clear();
            if (state.mallums != null)
            {
                foreach (var sm in state.mallums)
                {
                    var mallum = new MallumSave
                    {
                        serverId = sm.id,
                        state = ParseMallumState(sm.state),
                        assignedQuestName = sm.assignedQuestName,
                        startTimeUtc = sm.startTimeUtc,
                        assignedVaseIndex = FindVaseIndexByServerId(data.vases, sm.assignedVaseId)
                    };

                    if (sm.pendingRewards != null)
                    {
                        foreach (var r in sm.pendingRewards)
                            mallum.pendingRewards.Add(new RewardEntry { seedName = r.seed_name, count = r.count });
                    }

                    data.mallums.Add(mallum);
                }
            }

            SaveManager.Instance.Save();
        }

        private static int FindVaseIndexByServerId(List<VaseSave> vases, int serverId)
        {
            if (serverId <= 0) return -1;
            for (int i = 0; i < vases.Count; i++)
                if (vases[i].serverId == serverId) return i;
            return -1;
        }

        private static PlotState ParsePlotState(string s)
        {
            return s switch
            {
                "growing" => PlotState.Growing,
                "mature" => PlotState.Mature,
                _ => PlotState.Empty
            };
        }

        private static VaseState ParseVaseState(string s)
        {
            return s switch
            {
                "filling" => VaseState.Filling,
                "full" => VaseState.Full,
                _ => VaseState.Empty
            };
        }

        private static MallumState ParseMallumState(string s)
        {
            return s switch
            {
                "fetching_water" => MallumState.FetchingWater,
                "on_quest" => MallumState.OnQuest,
                "quest_complete" => MallumState.QuestComplete,
                _ => MallumState.Idle
            };
        }

        // ── Plot Endpoints ──

        public async Task<ServerPlot> CraftPlot(int gridX, int gridY)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new CraftRequest { gridX = gridX, gridY = gridY });
                using var req = PostJson("/game/plot/craft", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CraftPlot failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CraftPlot failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerPlot> PlantSeed(int plotId, string seedName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new PlantRequest { plotId = plotId, seedName = seedName });
                using var req = PostJson("/game/plot/plant", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: PlantSeed failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: PlantSeed failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerPlot> WaterPlot(int plotId, int vaseId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new WaterRequest { plotId = plotId, vaseId = vaseId });
                using var req = PostJson("/game/plot/water", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: WaterPlot failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: WaterPlot failed: {e.Message}"); }
            return null;
        }

        public async Task<HarvestResponse> Harvest(int plotId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new HarvestRequest { plotId = plotId });
                using var req = PostJson("/game/plot/harvest", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<HarvestResponse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: Harvest failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: Harvest failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerPlot> SetPlotSkin(int plotId, string skinName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new PlotSkinRequest { plotId = plotId, skinName = skinName });
                using var req = PostJson("/game/plot/set-skin", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: SetPlotSkin failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SetPlotSkin failed: {e.Message}"); }
            return null;
        }

        // ── Vase Endpoints ──

        public async Task<ServerVase> CraftVase(int gridX, int gridY)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new CraftRequest { gridX = gridX, gridY = gridY });
                using var req = PostJson("/game/vase/craft", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerVase>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CraftVase failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CraftVase failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerVase> FillVase(int vaseId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new FillVaseRequest { vaseId = vaseId });
                using var req = PostJson("/game/vase/fill", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerVase>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: FillVase failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: FillVase failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerVase> CheckVase(int vaseId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new CheckVaseRequest { vaseId = vaseId });
                using var req = PostJson("/game/vase/check", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerVase>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CheckVase failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CheckVase failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerVase> SetVaseSkin(int vaseId, string skinName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new VaseSkinRequest { vaseId = vaseId, skinName = skinName });
                using var req = PostJson("/game/vase/set-skin", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerVase>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: SetVaseSkin failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SetVaseSkin failed: {e.Message}"); }
            return null;
        }

        // ── Garden Endpoints ──

        public async Task<ServerGarden> PlantGarden(string plantName, int gridX, int gridY)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new PlantGardenRequest { plantName = plantName, gridX = gridX, gridY = gridY });
                using var req = PostJson("/game/garden/plant", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerGarden>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: PlantGarden failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: PlantGarden failed: {e.Message}"); }
            return null;
        }

        public async Task<CollectGardenResponse> CollectGarden(int gardenId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new CollectGardenRequest { gardenId = gardenId });
                using var req = PostJson("/game/garden/collect", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<CollectGardenResponse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CollectGarden failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CollectGarden failed: {e.Message}"); }
            return null;
        }

        // ── Quest Endpoints ──

        public async Task<ServerMallum> StartQuest(string questName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new QuestRequest { questName = questName });
                using var req = PostJson("/game/quest/start", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerMallum>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: StartQuest failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: StartQuest failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerMallum> CheckQuest(int mallumId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new MallumIdRequest { mallumId = mallumId });
                using var req = PostJson("/game/quest/check", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerMallum>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CheckQuest failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CheckQuest failed: {e.Message}"); }
            return null;
        }

        public async Task<CollectQuestResponse> CollectQuest(int mallumId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new MallumIdRequest { mallumId = mallumId });
                using var req = PostJson("/game/quest/collect", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<CollectQuestResponse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CollectQuest failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CollectQuest failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerMallum> SpeedUpQuest(int mallumId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new MallumIdRequest { mallumId = mallumId });
                using var req = PostJson("/game/quest/speed-up", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerMallum>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: SpeedUpQuest failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SpeedUpQuest failed: {e.Message}"); }
            return null;
        }

        // ── Weather Endpoints ──

        public async Task SubmitLocation(float lat, float lon)
        {
            if (!IsOnline) return;
            try
            {
                var body = JsonUtility.ToJson(new LocationRequest { lat = lat, lon = lon });
                using var req = PostJson("/weather/location", body);
                await SendAsync(req);

                if (req.responseCode < 200 || req.responseCode >= 300)
                    Debug.LogWarning($"GameService: SubmitLocation failed (HTTP {req.responseCode})");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SubmitLocation failed: {e.Message}"); }
        }

        public async Task<ServerWeather> GetWeather()
        {
            if (!IsOnline) return null;
            try
            {
                using var req = GetAuth("/weather/current");
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerWeather>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: GetWeather failed (HTTP {req.responseCode})");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: GetWeather failed: {e.Message}"); }
            return null;
        }

        // ── Cosmetic State ──

        public async Task SaveCosmeticState(string jsonData)
        {
            if (!IsOnline) return;
            try
            {
                using var req = PutJson("/game/state", jsonData);
                await SendAsync(req);

                if (req.responseCode < 200 || req.responseCode >= 300)
                    Debug.LogWarning($"GameService: SaveCosmeticState failed (HTTP {req.responseCode})");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SaveCosmeticState failed: {e.Message}"); }
        }

        // ── HTTP Helpers (same pattern as EconomyService) ──

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PutJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "PUT");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private void SetAuthHeader(UnityWebRequest request)
        {
            var token = SocialSaveManager.Instance?.Data?.authToken;
            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        private static Task<UnityWebRequest> SendAsync(UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<UnityWebRequest>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(request);
            return tcs.Task;
        }
    }
}
