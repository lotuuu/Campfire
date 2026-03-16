using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Garden
{
    public class CampFireUI : MonoBehaviour
    {
        public static CampFireUI Instance { get; private set; }

        private UIDocument uiDocument;
        private VisualElement root;

        // Sub-controllers found on same GameObject
        private WeatherBarUI weatherBar;
        private BottomNavUI bottomNav;
        private CampsiteViewUI campsiteView;
        private ApothekeUI apotheke;
        private LettersUI letters;
        private BuildUI build;
        private ResourceDisplayUI resourceDisplay;
        private DebugWeatherPanel debugPanel;
        private QuestUI questUI;
        private VisitorUI visitorUI;
        private DialogueUI dialogueUI;

        private SettingsUI settingsUI;
        private TutorialUI tutorialUI;
        private RewardRevealUI rewardRevealUI;

        private VisualElement overlayContainer;
        private VisualElement overlayBackdrop;
        private Label overlayTitle;
        private VisualElement overlayBody;
        private VisualElement apothekePanel;
        private VisualElement lettersPanel;
        private VisualElement buildPanel;
        private VisualElement debugPanelElement;
        private VisualElement questsPanel;
        private VisualElement settingsPanel;

        // Deferred icon loading
        private Button settingsBtn;
        private bool _settingsIconLoaded;

        // Loading gate
        private VisualElement loadingGate;
        private Label loadingGateTitle;
        private Label loadingStatus;
        private VisualElement loadingBarTrack;
        private VisualElement loadingBarFill;
        private VisualElement loadingServices;
        private Label loadingElapsed;
        private Label loadingStall;
        private bool _weatherDone;
        private bool _socialDone;
        private bool _economyDone;
        private bool _gameDone;
        private bool _failed;
        private Stopwatch _initStopwatch;
        private float _lastProgressTime;
        private int _lastDoneCount;

        // Service row labels (icon labels for updating status)
        private Label _iconSocial, _iconEconomy, _iconGame, _iconWeather;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            BootTimer.Mark("CampFireUI.Start (scene loaded)");
            _initStopwatch = Stopwatch.StartNew();
            uiDocument = GetComponentInChildren<UIDocument>();
            root = uiDocument.rootVisualElement;

            // Initialize sub-controllers
            weatherBar = GetComponent<WeatherBarUI>();
            bottomNav = GetComponent<BottomNavUI>();
            campsiteView = GetComponent<CampsiteViewUI>();
            apotheke = GetComponent<ApothekeUI>();
            letters = GetComponent<LettersUI>();
            build = GetComponent<BuildUI>();
            resourceDisplay = GetComponent<ResourceDisplayUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();
            questUI = GetComponent<QuestUI>();

            weatherBar?.Initialize(root);
            bottomNav?.Initialize(root);
            campsiteView?.Initialize(root);
            apotheke?.Initialize(root);
            letters?.Initialize(root);
            build?.Initialize(root);
            resourceDisplay?.Initialize(root);
            debugPanel?.Initialize(root);
            questUI?.Initialize(root);
            visitorUI = GetComponent<VisitorUI>();
            visitorUI?.Initialize(root);
            dialogueUI = GetComponent<DialogueUI>();
            dialogueUI?.Initialize(root);
            settingsUI = GetComponent<SettingsUI>();
            settingsUI?.Initialize(root);

            tutorialUI = GetComponent<TutorialUI>();
            tutorialUI?.Initialize(root);
            rewardRevealUI = GetComponent<RewardRevealUI>();
            rewardRevealUI?.Initialize(root);

            // Overlay setup
            overlayContainer = root.Q("overlay-container");
            overlayBackdrop = root.Q("overlay-backdrop");
            overlayTitle = root.Q<Label>("overlay-title");
            overlayBody = root.Q("overlay-body");
            apothekePanel = root.Q("apotheke-panel");
            lettersPanel = root.Q("letters-panel");
            buildPanel = root.Q("build-panel");
            debugPanelElement = root.Q("debug-panel");
            questsPanel = root.Q("quests-panel");
            settingsPanel = root.Q("settings-panel");

            // Patch all ScrollViews: kill momentum on taps so buttons don't cause drift
            foreach (var sv in overlayBody.Query<ScrollView>().ToList())
                PatchTapScrollMomentum(sv);

            var closeBtn = root.Q<Button>("overlay-close");
            closeBtn?.RegisterCallback<ClickEvent>(_ => CloseOverlay());
            overlayBackdrop?.RegisterCallback<ClickEvent>(_ => CloseOverlay());

            // Hide overlay on init (skip CloseOverlay to avoid playing sound)
            HideAllPanels();
            overlayContainer.style.display = DisplayStyle.None;

            // Wire bottom nav
            if (bottomNav != null)
            {
                bottomNav.OnApothekeClicked += () =>
                {
                    apotheke?.Refresh();
                    OpenOverlay("Apotheke", apothekePanel);
                };
                bottomNav.OnLettersClicked += () => OpenOverlay("Social", lettersPanel);
                bottomNav.OnQuestClicked += () =>
                {
                    questUI?.Refresh();
                    OpenOverlay("Quests", questsPanel);
                };
            }

            // Update quest badge on mallum changes
            if (MallumManager.Instance != null)
            {
                MallumManager.Instance.OnMallumsChanged += UpdateQuestBadge;
                UpdateQuestBadge();
            }

            // Update social badge from letters
            if (letters != null)
            {
                letters.OnBadgeCountChanged += count => bottomNav?.UpdateSocialBadge(count);
            }

            // Wire settings button
            settingsBtn = root.Q<Button>("btn-settings");
            if (settingsBtn != null)
            {
                TryLoadSettingsIcon();
                settingsBtn.clicked += () => OpenOverlay("Settings", settingsPanel);
            }

            // Wire debug button (editor + development builds)
            var debugBtn = root.Q<Button>("btn-debug");
            if (debugBtn != null)
            {
                if (Application.isEditor || UnityEngine.Debug.isDebugBuild)
                {
                    if (GetComponent<DebugService>() == null)
                        gameObject.AddComponent<DebugService>();
                    debugBtn.clicked += () => OpenOverlay("Debug", debugPanelElement);
                }
                else
                    debugBtn.style.display = DisplayStyle.None;
            }

            // Wire build placement mode (from flame popup craft items)
            if (build != null && campsiteView != null)
            {
                build.OnRequestPlacement += type =>
                {
                    CloseOverlay();
                    campsiteView.EnterPlacementMode(type);
                };
            }

            // Wire Apotheke building tap
            if (campsiteView != null)
            {
                campsiteView.OnApothekeTapped += () =>
                {
                    apotheke?.Refresh();
                    OpenOverlay("Apotheke", apothekePanel);
                };
            }

            // Wire Visitor tile tap
            if (campsiteView != null)
                campsiteView.OnVisitorTapped += () =>
                {
                    var data = SaveManager.Instance?.Data;
                    if (data?.currentVisitor == null) return;
                    var visitor = data.currentVisitor;

                    if (!visitor.dialogueSeen && visitor.dialogueLines != null && visitor.dialogueLines.Count > 0 && dialogueUI != null)
                    {
                        Texture2D portrait = SpriteService.Instance?.GetTexture($"portraits/{visitor.portraitId}");

                        dialogueUI.Show(visitor.visitorName, visitor.dialogueLines, () =>
                        {
                            visitor.dialogueSeen = true;
                            SaveManager.Instance.Save();
                            visitorUI?.ShowModal();
                        }, portrait);
                    }
                    else
                    {
                        visitorUI?.ShowModal();
                    }
                };

            // Loading gate — block UI until all services are ready
            loadingGate = root.Q("loading-gate");
            loadingGateTitle = root.Q<Label>("loading-gate-title");
            loadingStatus = root.Q<Label>("loading-gate-status");
            loadingBarTrack = root.Q("loading-gate-bar-track");
            loadingBarFill = root.Q("loading-gate-bar-fill");
            loadingServices = root.Q("loading-gate-services");
            loadingElapsed = root.Q<Label>("loading-gate-elapsed");
            loadingStall = root.Q<Label>("loading-gate-stall");

            // Build per-service status rows
            _iconSocial = AddServiceRow(loadingServices, "Social");
            _iconEconomy = AddServiceRow(loadingServices, "Economy");
            _iconGame = AddServiceRow(loadingServices, "Game state");
            _iconWeather = AddServiceRow(loadingServices, "Weather");

            // Server selector — populate buttons (shown on failure or in editor/debug)
            var serverSelector = root.Q("server-selector");
            if (serverSelector != null)
            {
                foreach (var server in ServerConfig.Servers)
                {
                    var btn = new Button { text = server.name };
                    btn.AddToClassList("server-btn");
                    if (server.id == ServerConfig.SelectedId)
                    {
                        btn.AddToClassList("server-active");
                        btn.SetEnabled(false);
                    }
                    var capturedId = server.id;
                    btn.clicked += () => ServerConfig.Select(capturedId);
                    serverSelector.Add(btn);
                }

                var reloadBtn = new Button { text = "Reload" };
                reloadBtn.AddToClassList("server-btn");
                reloadBtn.clicked += () => UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                serverSelector.Add(reloadBtn);

                // Show immediately in editor/debug builds
                if (Application.isEditor || UnityEngine.Debug.isDebugBuild)
                    serverSelector.style.display = DisplayStyle.Flex;
            }

            // Set loading screen image — prefer server sprite, fall back to local asset
            var loadingImage = root.Q("loading-gate-image");
            if (loadingImage != null)
            {
                var loadingTex = SpriteService.Instance?.GetTexture("ui/loading_screen");
                if (loadingTex == null)
                    loadingTex = Resources.Load<Texture2D>("UI/Images/loading_screen");
                if (loadingTex != null)
                    loadingImage.style.backgroundImage = new StyleBackground(loadingTex);
                else
                    loadingImage.style.display = DisplayStyle.None;
            }

            // Subscribe to service completion + failure events
            // All services are required — never skip any
            if (WeatherService.Instance != null)
            {
                if (WeatherService.Instance.HasWeather)
                    _weatherDone = true;
                else
                    WeatherService.Instance.OnWeatherUpdated += OnWeatherDataReady;
            }

            if (SocialService.Instance != null)
            {
                if (SocialService.Instance.IsSignedIn)
                    _socialDone = true;
                else
                {
                    SocialService.Instance.OnSignedIn += OnSocialReady;
                    SocialService.Instance.OnInitFailed += OnServiceFailed;
                }
            }

            if (EconomyService.Instance != null)
            {
                if (EconomyService.Instance.IsInitialized)
                {
                    _economyDone = EconomyService.Instance.IsOnline;
                    if (!_economyDone) OnServiceFailed("Could not sync economy with server");
                }
                else
                {
                    EconomyService.Instance.OnStateSynced += OnEconomyReady;
                    EconomyService.Instance.OnInitFailed += OnServiceFailed;
                }
            }

            if (GameService.Instance != null)
            {
                if (GameService.Instance.IsInitialized)
                {
                    _gameDone = GameService.Instance.IsOnline;
                    if (!_gameDone) OnServiceFailed("Could not load game state from server");
                }
                else
                {
                    GameService.Instance.OnStateLoaded += OnGameReady;
                    GameService.Instance.OnInitFailed += OnServiceFailed;
                }
            }

            UpdateLoadingGate();
        }

        private void OnDestroy()
        {
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged -= UpdateQuestBadge;
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherDataReady;
            if (SocialService.Instance != null)
            {
                SocialService.Instance.OnSignedIn -= OnSocialReady;
                SocialService.Instance.OnInitFailed -= OnServiceFailed;
            }
            if (EconomyService.Instance != null)
            {
                EconomyService.Instance.OnStateSynced -= OnEconomyReady;
                EconomyService.Instance.OnInitFailed -= OnServiceFailed;
            }
            if (GameService.Instance != null)
            {
                GameService.Instance.OnStateLoaded -= OnGameReady;
                GameService.Instance.OnInitFailed -= OnServiceFailed;
            }
        }

        private void Update()
        {
            TryLoadSettingsIcon();
            UpdateLoadingElapsed();
        }

        private void TryLoadSettingsIcon()
        {
            if (_settingsIconLoaded || settingsBtn == null) return;
            var gearTex = SpriteService.Instance?.GetTexture("ui/gear");
            if (gearTex == null) return;
            settingsBtn.style.backgroundImage = new StyleBackground(gearTex);
            _settingsIconLoaded = true;
        }

        private static Label AddServiceRow(VisualElement container, string name)
        {
            var row = new VisualElement();
            row.AddToClassList("loading-service-row");
            var icon = new Label("-");
            icon.AddToClassList("loading-service-icon");
            icon.AddToClassList("loading-service-icon--waiting");
            var label = new Label(name);
            label.AddToClassList("loading-service-name");
            row.Add(icon);
            row.Add(label);
            container.Add(row);
            return icon;
        }

        private static void SetServiceIcon(Label icon, string status)
        {
            icon.RemoveFromClassList("loading-service-icon--done");
            icon.RemoveFromClassList("loading-service-icon--waiting");
            icon.RemoveFromClassList("loading-service-icon--failed");

            switch (status)
            {
                case "done":
                    icon.text = "OK";
                    icon.AddToClassList("loading-service-icon--done");
                    break;
                case "failed":
                    icon.text = "X";
                    icon.AddToClassList("loading-service-icon--failed");
                    break;
                default:
                    icon.text = "-";
                    icon.AddToClassList("loading-service-icon--waiting");
                    break;
            }
        }

        private void UpdateLoadingElapsed()
        {
            if (loadingGate == null || _initStopwatch == null) return;

            float elapsed = _initStopwatch.ElapsedMilliseconds / 1000f;

            // Show elapsed time after 10 seconds
            if (elapsed >= 10f && loadingElapsed != null)
            {
                loadingElapsed.style.display = DisplayStyle.Flex;
                loadingElapsed.text = $"{elapsed:F0}s elapsed";
            }

            // Show service detail rows after 5 seconds so user can see what's stuck
            if (elapsed >= 5f && loadingServices != null)
                loadingServices.style.display = DisplayStyle.Flex;

            // Stall warning after 20 seconds of no progress
            if (elapsed >= 20f && loadingStall != null)
            {
                loadingStall.style.display = DisplayStyle.Flex;
                string stuck = GetStuckServices();
                loadingStall.text = $"Stalled on: {stuck}";
            }
        }

        private string GetStuckServices()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!_socialDone) parts.Add("Social");
            if (!_economyDone) parts.Add("Economy");
            if (!_gameDone) parts.Add("Game state");
            if (!_weatherDone) parts.Add("Weather");
            return parts.Count > 0 ? string.Join(", ", parts) : "unknown";
        }

        private void UpdateQuestBadge()
        {
            if (bottomNav == null || MallumManager.Instance == null) return;
            int completed = MallumManager.Instance.GetCompletedQuestCount();
            bottomNav.UpdateQuestBadge(completed);
        }

        // ── Loading gate callbacks ──

        private void OnWeatherDataReady(WeatherData _)
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= OnWeatherDataReady;
            _weatherDone = true;
            Debug.Log($"[INIT] Weather ready at {_initStopwatch?.ElapsedMilliseconds ?? 0}ms");
            UpdateLoadingGate();
        }

        private void OnSocialReady()
        {
            if (SocialService.Instance != null)
                SocialService.Instance.OnSignedIn -= OnSocialReady;
            _socialDone = true;
            Debug.Log($"[INIT] Social ready at {_initStopwatch?.ElapsedMilliseconds ?? 0}ms");
            UpdateLoadingGate();
        }

        private void OnEconomyReady()
        {
            if (EconomyService.Instance != null)
                EconomyService.Instance.OnStateSynced -= OnEconomyReady;
            _economyDone = true;
            Debug.Log($"[INIT] Economy ready at {_initStopwatch?.ElapsedMilliseconds ?? 0}ms");
            UpdateLoadingGate();
        }

        private void OnGameReady()
        {
            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded -= OnGameReady;
            _gameDone = true;
            Debug.Log($"[INIT] Game ready at {_initStopwatch?.ElapsedMilliseconds ?? 0}ms");
            UpdateLoadingGate();
        }

        private void OnServiceFailed(string reason)
        {
            if (_failed) return;
            _failed = true;

            Debug.LogError($"[INIT] Service failed: {reason}");

            if (loadingGate == null) return;
            loadingGateTitle.text = "Connection Failed";
            loadingStatus.text = reason;
            loadingBarTrack.style.display = DisplayStyle.None;

            // Update service icons to reflect failure
            SetServiceIcon(_iconSocial, _socialDone ? "done" : "failed");
            SetServiceIcon(_iconEconomy, _economyDone ? "done" : "failed");
            SetServiceIcon(_iconGame, _gameDone ? "done" : "failed");
            SetServiceIcon(_iconWeather, _weatherDone ? "done" : "failed");
            if (loadingServices != null)
                loadingServices.style.display = DisplayStyle.Flex;

            // Hide stall/elapsed — failure message is sufficient
            if (loadingStall != null) loadingStall.style.display = DisplayStyle.None;
            if (loadingElapsed != null) loadingElapsed.style.display = DisplayStyle.None;

            // Show server selector so the user can switch servers
            var serverSelector = loadingGate.Q("server-selector");
            if (serverSelector != null)
                serverSelector.style.display = DisplayStyle.Flex;

            // Add a retry button
            var retryBtn = new Button { text = "Retry" };
            retryBtn.AddToClassList("server-btn");
            retryBtn.clicked += () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            loadingGate.Add(retryBtn);
        }

        private void UpdateLoadingGate()
        {
            if (loadingGate == null || _failed) return;

            // Update per-service icons
            SetServiceIcon(_iconSocial, _socialDone ? "done" : "waiting");
            SetServiceIcon(_iconEconomy, _economyDone ? "done" : "waiting");
            SetServiceIcon(_iconGame, _gameDone ? "done" : "waiting");
            SetServiceIcon(_iconWeather, _weatherDone ? "done" : "waiting");

            int done = (_socialDone ? 1 : 0) + (_economyDone ? 1 : 0)
                     + (_gameDone ? 1 : 0) + (_weatherDone ? 1 : 0);
            const int total = 4;
            float pct = (float)done / total;

            // Track progress for stall detection
            if (done > _lastDoneCount)
            {
                _lastDoneCount = done;
                _lastProgressTime = _initStopwatch?.ElapsedMilliseconds / 1000f ?? 0f;
            }

            loadingBarFill.style.width = Length.Percent(pct * 100f);

            if (done >= total)
            {
                Debug.Log($"[INIT] ===== App fully loaded in {_initStopwatch?.ElapsedMilliseconds ?? 0}ms =====");
                BootTimer.Mark("All services ready — loading gate dismissed");
                BootTimer.Complete();
                loadingGate.RemoveFromHierarchy();
                loadingGate = null;

                // Refresh all UI now that server state is fully loaded
                resourceDisplay?.Refresh();
                campsiteView?.RebuildGrid();
                UpdateQuestBadge();

                // Start tutorial after all services are ready
                if (TutorialManager.Instance != null && dialogueUI != null && tutorialUI != null)
                    TutorialManager.Instance.Initialize(tutorialUI, dialogueUI, campsiteView);

                return;
            }

            // Show status of what's currently loading
            if (!_socialDone) loadingStatus.text = "Connecting...";
            else if (!_economyDone) loadingStatus.text = "Syncing economy...";
            else if (!_gameDone) loadingStatus.text = "Loading game state...";
            else if (!_weatherDone) loadingStatus.text = "Reading the skies...";
        }

        public void OpenOverlay(string title, VisualElement panel)
        {
            AudioManager.Instance?.PlaySFX("ui_panel_open");
            HideAllPanels();
            overlayTitle.text = title;
            panel.style.display = DisplayStyle.Flex;
            overlayContainer.style.display = DisplayStyle.Flex;
            overlayContainer.BringToFront();
        }

        public void CloseOverlay()
        {
            AudioManager.Instance?.PlaySFX("ui_panel_close");
            HideAllPanels();
            overlayContainer.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// On taps (pointer barely moved), temporarily zero the deceleration rate
        /// so the ScrollView doesn't drift from residual touch velocity.
        /// Drag-scrolling is unaffected because the pointer moves past the threshold.
        /// </summary>
        private static void PatchTapScrollMomentum(ScrollView sv)
        {
            Vector3 downPos = Vector3.zero;
            sv.RegisterCallback<PointerDownEvent>(e => downPos = e.position, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerUpEvent>(e =>
            {
                if (Vector3.Distance(downPos, e.position) < 10f)
                {
                    float saved = sv.scrollDecelerationRate;
                    sv.scrollDecelerationRate = 0f;
                    sv.schedule.Execute(() => sv.scrollDecelerationRate = saved);
                }
            }, TrickleDown.TrickleDown);
        }

        private void HideAllPanels()
        {
            if (apothekePanel != null) apothekePanel.style.display = DisplayStyle.None;
            if (lettersPanel != null) lettersPanel.style.display = DisplayStyle.None;
            if (buildPanel != null) buildPanel.style.display = DisplayStyle.None;
            if (debugPanelElement != null) debugPanelElement.style.display = DisplayStyle.None;
            if (questsPanel != null) questsPanel.style.display = DisplayStyle.None;
            if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
        }
    }
}
