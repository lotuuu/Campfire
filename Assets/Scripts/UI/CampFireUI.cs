using UnityEngine;
using UnityEngine.UIElements;

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

        private VisualElement overlayContainer;
        private VisualElement overlayBackdrop;
        private Label overlayTitle;
        private VisualElement overlayBody;
        private VisualElement apothekePanel;
        private VisualElement lettersPanel;
        private VisualElement buildPanel;
        private VisualElement debugPanelElement;
        private VisualElement questsPanel;
        private VisualElement visitorPanel;
        private VisualElement settingsPanel;

        // Loading gate
        private VisualElement loadingGate;
        private Label loadingGateTitle;
        private Label loadingStatus;
        private VisualElement loadingBarTrack;
        private VisualElement loadingBarFill;
        private bool _weatherDone;
        private bool _socialDone;
        private bool _economyDone;
        private bool _gameDone;
        private bool _failed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
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
            visitorPanel = root.Q("visitor-panel");
            settingsPanel = root.Q("settings-panel");

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
            var settingsBtn = root.Q<Button>("btn-settings");
            if (settingsBtn != null)
                settingsBtn.clicked += () => OpenOverlay("Settings", settingsPanel);

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
                            visitorUI?.ShowVisitor();
                            OpenOverlay(visitor.visitorName, visitorPanel);
                        }, portrait);
                    }
                    else
                    {
                        visitorUI?.ShowVisitor();
                        OpenOverlay(visitor.visitorName, visitorPanel);
                    }
                };

            // Loading gate — block UI until all services are ready
            loadingGate = root.Q("loading-gate");
            loadingGateTitle = root.Q<Label>("loading-gate-title");
            loadingStatus = root.Q<Label>("loading-gate-status");
            loadingBarTrack = root.Q("loading-gate-bar-track");
            loadingBarFill = root.Q("loading-gate-bar-fill");

            // Server selector (editor + debug builds only)
            var serverSelector = root.Q("server-selector");
            if (serverSelector != null)
            {
                if (Application.isEditor || UnityEngine.Debug.isDebugBuild)
                {
                    foreach (var server in ServerConfig.Servers)
                    {
                        var btn = new Button { text = server.name };
                        btn.AddToClassList("server-btn");
                        if (server.id == ServerConfig.SelectedId)
                            btn.AddToClassList("server-active");
                        var capturedId = server.id;
                        btn.clicked += () => ServerConfig.Select(capturedId);
                        serverSelector.Add(btn);
                    }
                }
                else
                {
                    serverSelector.style.display = DisplayStyle.None;
                }
            }

            // Set loading screen image from cached sprites
            var loadingImage = root.Q("loading-gate-image");
            var loadingTex = SpriteService.Instance?.GetTexture("ui/loading_screen");
            if (loadingTex != null && loadingImage != null)
                loadingImage.style.backgroundImage = new StyleBackground(loadingTex);
            else if (loadingImage != null)
                loadingImage.style.display = DisplayStyle.None;

            // Subscribe to service completion + failure events
            if (WeatherService.Instance != null)
            {
                if (WeatherService.Instance.IsLocationResolved)
                    _weatherDone = true;
                else
                    WeatherService.Instance.OnLocationResolved += OnWeatherReady;
            }
            else _weatherDone = true;

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
            else _socialDone = true;

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
            else _economyDone = true;

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
            else _gameDone = true;

            UpdateLoadingGate();
        }

        private void UpdateQuestBadge()
        {
            if (bottomNav == null || MallumManager.Instance == null) return;
            int completed = MallumManager.Instance.GetCompletedQuestCount();
            bottomNav.UpdateQuestBadge(completed);
        }

        // ── Loading gate callbacks ──

        private void OnWeatherReady(bool success)
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnLocationResolved -= OnWeatherReady;
            _weatherDone = true;
            UpdateLoadingGate();
        }

        private void OnSocialReady()
        {
            if (SocialService.Instance != null)
                SocialService.Instance.OnSignedIn -= OnSocialReady;
            _socialDone = true;
            UpdateLoadingGate();
        }

        private void OnEconomyReady()
        {
            if (EconomyService.Instance != null)
                EconomyService.Instance.OnStateSynced -= OnEconomyReady;
            _economyDone = true;
            UpdateLoadingGate();
        }

        private void OnGameReady()
        {
            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded -= OnGameReady;
            _gameDone = true;
            UpdateLoadingGate();
        }

        private void OnServiceFailed(string reason)
        {
            if (_failed) return;
            _failed = true;

            if (loadingGate == null) return;
            loadingGateTitle.text = "Connection Failed";
            loadingStatus.text = reason;
            loadingBarTrack.style.display = DisplayStyle.None;
        }

        private void UpdateLoadingGate()
        {
            if (loadingGate == null || _failed) return;

            int done = (_socialDone ? 1 : 0) + (_economyDone ? 1 : 0)
                     + (_gameDone ? 1 : 0) + (_weatherDone ? 1 : 0);
            const int total = 4;
            float pct = (float)done / total;

            loadingBarFill.style.width = Length.Percent(pct * 100f);

            if (done >= total)
            {
                loadingGate.RemoveFromHierarchy();
                loadingGate = null;
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
        }

        public void CloseOverlay()
        {
            AudioManager.Instance?.PlaySFX("ui_panel_close");
            HideAllPanels();
            overlayContainer.style.display = DisplayStyle.None;
        }

        private void HideAllPanels()
        {
            if (apothekePanel != null) apothekePanel.style.display = DisplayStyle.None;
            if (lettersPanel != null) lettersPanel.style.display = DisplayStyle.None;
            if (buildPanel != null) buildPanel.style.display = DisplayStyle.None;
            if (debugPanelElement != null) debugPanelElement.style.display = DisplayStyle.None;
            if (questsPanel != null) questsPanel.style.display = DisplayStyle.None;
            if (visitorPanel != null) visitorPanel.style.display = DisplayStyle.None;
            if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
        }
    }
}
