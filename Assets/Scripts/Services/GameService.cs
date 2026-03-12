using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Garden
{
    public class GameService : MonoBehaviour
    {
        public static GameService Instance { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsOnline { get; private set; }

        public event Action OnStateLoaded;
        public event Action<string> OnInitFailed;

        private static string ServerBaseUrl => ServerConfig.BaseUrl;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Initialization ──

        private const long SlowStepMs = 500;

        public async void Initialize()
        {
            if (SocialService.Instance == null || !SocialService.Instance.IsSignedIn) return;

            var totalSw = Stopwatch.StartNew();

            try
            {
                var sw = Stopwatch.StartNew();

                // Fetch server configs before game state
                if (ConfigService.Instance != null)
                {
                    var configLoaded = await ConfigService.Instance.FetchConfigs();
                    if (sw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] ConfigService.FetchConfigs took {sw.ElapsedMilliseconds}ms");
                    sw.Restart();

                    if (!configLoaded)
                    {
                        Debug.LogError("GameService: Config fetch failed — server configs are required.");
                        IsInitialized = true;
                        IsOnline = false;
                        OnInitFailed?.Invoke("Failed to fetch server configs");
                        return;
                    }
                }

                // Sync sprites from server
                if (SpriteService.Instance != null && ConfigService.Instance != null)
                {
                    await SpriteService.Instance.SyncSprites(ConfigService.Instance.SpriteManifest);
                    if (sw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] SpriteService.SyncSprites took {sw.ElapsedMilliseconds}ms");
                    sw.Restart();
                }

                using var req = GetAuth("/game/state");
                await SendAsync(req);
                if (sw.ElapsedMilliseconds > SlowStepMs)
                    Debug.LogWarning($"[INIT SLOW] GET /game/state took {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                if (req.responseCode == 200)
                {
                    var state = JsonUtility.FromJson<GameStateResponse>(req.downloadHandler.text);
                    if (state == null)
                    {
                        Debug.LogWarning("GameService: Failed to parse game state response.");
                        IsInitialized = true;
                        IsOnline = false;
                        OnInitFailed?.Invoke("Failed to parse game state");
                        return;
                    }
                    ApplyGameState(state);
                    if (sw.ElapsedMilliseconds > SlowStepMs)
                        Debug.LogWarning($"[INIT SLOW] ApplyGameState took {sw.ElapsedMilliseconds}ms");

                    Debug.Log($"[INIT] GameService total: {totalSw.ElapsedMilliseconds}ms");
                    IsInitialized = true;
                    IsOnline = true;
                    OnStateLoaded?.Invoke();
                    // Fetch forecast in background (don't block init)
                    _ = FetchAndApplyForecast();
                    return;
                }

                Debug.LogWarning($"GameService: Could not load game state (HTTP {req.responseCode}).");
                IsInitialized = true;
                IsOnline = false;
                OnInitFailed?.Invoke("Could not load game state from server");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameService: Init failed ({e.Message}).");
                IsInitialized = true;
                IsOnline = false;
                OnInitFailed?.Invoke("Could not load game state from server");
            }
        }

        private void ApplyGameState(GameStateResponse state)
        {
            var data = SaveManager.Instance.Data;

            // Server is authoritative — do NOT preserve local-only entities (serverId == 0).
            // Both client and server independently create starter buildings at different
            // random positions, so preserving local entities causes duplicates.

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
            // Mallum Houses
            data.mallumHouses.Clear();
            if (state.mallumHouses != null)
            {
                foreach (var sh in state.mallumHouses)
                {
                    data.mallumHouses.Add(new MallumHouseSave
                    {
                        serverId = sh.id,
                        gridX = sh.gridX,
                        gridY = sh.gridY,
                        skinName = sh.skinName,
                        unlockedSkins = sh.unlockedSkins ?? new List<string>()
                    });
                }
            }
            // Birds
            data.birds.Clear();
            if (state.birds != null)
            {
                foreach (var sb in state.birds)
                {
                    data.birds.Add(new BirdSave
                    {
                        serverId = sb.id,
                        gridX = sb.gridX,
                        gridY = sb.gridY,
                        seedName = sb.seedName,
                        seedCount = sb.seedCount
                    });
                }
            }

            // Apotheke position
            if (state.apotheke != null)
            {
                data.apothekeServerId = state.apotheke.id;
                data.apothekeGridX = state.apotheke.gridX;
                data.apothekeGridY = state.apotheke.gridY;
            }

            // Economy (mana, gems, flame level, seeds, items)
            if (state.economy != null)
            {
                data.mana = state.economy.mana;
                data.gems = state.economy.gems;
                data.flameLevel = state.economy.flameLevel;

                data.inventory.Clear();
                if (state.economy.inventory != null)
                    foreach (var i in state.economy.inventory)
                        data.inventory.Add(new InventoryItem { itemName = i.itemName, count = i.count });
            }

            // Apply server weather if available
            if (state.weather != null && WeatherService.Instance != null)
                WeatherService.Instance.ApplyServerWeather(state.weather);

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
                var body = JsonUtility.ToJson(new CraftRequest { gridX = gridX, gridY = gridY, freeMode = CurrencyManager.FreeMode });
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
                var body = JsonUtility.ToJson(new PlantRequest { plotId = plotId, seedName = seedName, freeMode = CurrencyManager.FreeMode });
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
                var body = JsonUtility.ToJson(new CraftRequest { gridX = gridX, gridY = gridY, freeMode = CurrencyManager.FreeMode });
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

        public async Task<ServerPlot> InstantFinishPlot(int plotId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new InstantFinishPlotRequest { plotId = plotId });
                using var req = PostJson("/game/plot/instant-finish", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: InstantFinishPlot failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: InstantFinishPlot failed: {e.Message}"); }
            return null;
        }

        public async Task<ServerVase> InstantFinishVase(int vaseId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new InstantFinishVaseRequest { vaseId = vaseId });
                using var req = PostJson("/game/vase/instant-finish", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerVase>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: InstantFinishVase failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: InstantFinishVase failed: {e.Message}"); }
            return null;
        }

        // ── Garden Endpoints ──

        public async Task<ServerGarden> PlantGarden(string plantName, int gridX, int gridY)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new PlantGardenRequest { plantName = plantName, gridX = gridX, gridY = gridY, freeMode = CurrencyManager.FreeMode });
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
                var body = JsonUtility.ToJson(new QuestRequest { questName = questName, freeMode = CurrencyManager.FreeMode });
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

        // ── Mallum House Endpoints ──

        public async Task<ServerMallumHouse> CraftMallumHouse(int gridX, int gridY)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new CraftRequest { gridX = gridX, gridY = gridY, freeMode = CurrencyManager.FreeMode });
                using var req = PostJson("/game/mallum-house/craft", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerMallumHouse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CraftMallumHouse failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CraftMallumHouse failed: {e.Message}"); }
            return null;
        }

        // ── Mallum House Skin ──

        public async Task<ServerMallumHouse> SetMallumHouseSkin(int houseId, string skinName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new HouseSkinRequest { houseId = houseId, skinName = skinName });
                using var req = PostJson("/game/mallum-house/set-skin", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ServerMallumHouse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: SetMallumHouseSkin failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: SetMallumHouseSkin failed: {e.Message}"); }
            return null;
        }

        // ── Move Building ──

        public async Task MoveBuilding(string type, int id, int gridX, int gridY)
        {
            if (!IsOnline) return;
            try
            {
                var body = JsonUtility.ToJson(new MoveBuildingRequest { type = type, id = id, gridX = gridX, gridY = gridY });
                using var req = PostJson("/game/move-building", body);
                await SendAsync(req);

                if (req.responseCode < 200 || req.responseCode >= 300)
                    Debug.LogWarning($"GameService: MoveBuilding failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: MoveBuilding failed: {e.Message}"); }
        }

        // ── Bird Endpoints ──

        public async Task<List<ServerBird>> CheckBirds()
        {
            if (!IsOnline) return null;
            try
            {
                using var req = PostJson("/game/bird/check", "{}");
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                {
                    var response = JsonUtility.FromJson<BirdCheckResponse>(req.downloadHandler.text);
                    return response?.newBirds;
                }

                Debug.LogWarning($"GameService: CheckBirds failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CheckBirds failed: {e.Message}"); }
            return null;
        }

        public async Task<BirdCollectResponse> CollectBird(int birdId)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new BirdCollectRequest { birdId = birdId });
                using var req = PostJson("/game/bird/collect", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<BirdCollectResponse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CollectBird failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CollectBird failed: {e.Message}"); }
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
                {
                    Debug.LogWarning($"GameService: SubmitLocation failed (HTTP {req.responseCode})");
                    return;
                }

                // Now that server has our location, fetch and apply server weather
                var weather = await GetWeather();
                if (weather != null && WeatherService.Instance != null)
                    WeatherService.Instance.ApplyServerWeather(weather);
                // Also fetch forecast
                await FetchAndApplyForecast();
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
                {
                    var resp = JsonUtility.FromJson<ServerWeatherResponse>(req.downloadHandler.text);
                    return resp?.weather;
                }

                Debug.LogWarning($"GameService: GetWeather failed (HTTP {req.responseCode})");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: GetWeather failed: {e.Message}"); }
            return null;
        }

        private async Task FetchAndApplyForecast()
        {
            var days = await GetForecast();
            if (days != null && WeatherService.Instance != null)
                WeatherService.Instance.ApplyServerForecast(days);
        }

        public async Task<List<ServerForecastDay>> GetForecast()
        {
            if (!IsOnline) return null;
            try
            {
                using var req = GetAuth("/weather/forecast");
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                {
                    var resp = JsonUtility.FromJson<ServerForecastResponse>(req.downloadHandler.text);
                    return resp?.forecast;
                }

                // 404 is expected when location hasn't been submitted yet
                if (req.responseCode != 404)
                    Debug.LogWarning($"GameService: GetForecast failed (HTTP {req.responseCode})");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: GetForecast failed: {e.Message}"); }
            return null;
        }

        // ── Apotheke Endpoints ──

        public async Task<ApothekeCraftResponse> CraftApotheke(string recipeName)
        {
            if (!IsOnline) return null;
            try
            {
                var body = JsonUtility.ToJson(new ApothekeCraftRequest { recipeName = recipeName, freeMode = CurrencyManager.FreeMode });
                using var req = PostJson("/game/apotheke/craft", body);
                await SendAsync(req);

                if (req.responseCode >= 200 && req.responseCode < 300)
                    return JsonUtility.FromJson<ApothekeCraftResponse>(req.downloadHandler.text);

                Debug.LogWarning($"GameService: CraftApotheke failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
            }
            catch (Exception e) { Debug.LogWarning($"GameService: CraftApotheke failed: {e.Message}"); }
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

        private const int RequestTimeoutSeconds = 15;

        private UnityWebRequest GetAuth(string path)
        {
            var request = UnityWebRequest.Get(ServerBaseUrl + path);
            request.timeout = RequestTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerBuffer();
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PostJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "POST");
            request.timeout = RequestTimeoutSeconds;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            SetAuthHeader(request);
            return request;
        }

        private UnityWebRequest PutJson(string path, string json)
        {
            var request = new UnityWebRequest(ServerBaseUrl + path, "PUT");
            request.timeout = RequestTimeoutSeconds;
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
