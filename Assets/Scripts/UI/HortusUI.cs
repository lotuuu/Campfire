using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;


        private SwipeablePageView pageView;
        private BottomNavUI bottomNavUI;
        private BackyardViewUI backyardViewUI;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private SeedShopUI seedShopUI;
        private GreenhouseUI greenhouseUI;
        private ConstructionUI constructionUI;
        private HarvestResultUI harvestResultUI;
        private DiscoveryPopupUI discoveryPopupUI;
        private HarvestResult _pendingDiscoveryResult;
        private DebugWeatherPanel debugPanel;
        private EnvironmentSwitcherBar envSwitcherBar;
        [SerializeField] private BackyardIsometricView backyardIsoView;

        private Camera mainCam;
        private const int TerrariumPageIndex = 2;

        // Iso land slide tracking — mirrors CSS transition independently so Update()
        // sees a smoothly animated value instead of the snapped resolvedStyle.translate.x
        private float _isoContainerX;
        private float _isoAnimStartX;
        private float _isoAnimTargetX;
        private float _isoAnimStartTime = -1f;
        private const float IsoDuration = SwipeablePageView.AnimationDurationMs / 1000f;

        // Location gate
        private VisualElement locationGate;
        private Label gateStatus;
        private Button gateRetry;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            // Get sub-controllers
            backyardViewUI = GetComponent<BackyardViewUI>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            seedShopUI = GetComponent<SeedShopUI>();
            greenhouseUI = GetComponent<GreenhouseUI>();
            constructionUI = GetComponent<ConstructionUI>();
            harvestResultUI = GetComponent<HarvestResultUI>();
            discoveryPopupUI = GetComponent<DiscoveryPopupUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            // Build SwipeablePageView
            pageView = new SwipeablePageView();
            var viewport = root.Q<VisualElement>("page-viewport");
            viewport.Add(pageView);

            // Reparent pages from UXML into the page view
            string[] pageNames = { "codex-page", "shop-page", "terrarium-page", "greenhouse-page", "construction-page" };
            foreach (var name in pageNames)
            {
                var page = root.Q<VisualElement>(name);
                page.RemoveFromHierarchy();
                page.style.display = DisplayStyle.Flex;
                pageView.AddPage(page);
            }

            // Initialize sub-controllers
            backyardViewUI?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            seedShopUI?.Initialize(root);
            greenhouseUI?.Initialize(root);
            constructionUI?.Initialize(root);
            harvestResultUI?.Initialize(root);
            discoveryPopupUI?.Initialize(root);
            debugPanel?.Initialize(root);

            // Initialize bottom nav
            bottomNavUI = GetComponent<BottomNavUI>();
            bottomNavUI?.Initialize(root, pageView);

            // Initialize env switcher bar
            envSwitcherBar = GetComponent<EnvironmentSwitcherBar>();
            envSwitcherBar?.Initialize(root);

            // Wire env switcher events
            if (bottomNavUI != null)
                bottomNavUI.OnTerrariumReactivated += OnTerrariumReactivated;
            if (envSwitcherBar != null)
                envSwitcherBar.OnEnvironmentSelected += OnEnvironmentSelected;

            // Page change callbacks — refresh content when page becomes visible
            pageView.OnPageChanged += OnPageChanged;

            // Wire backyard slot events
            if (backyardViewUI != null)
            {
                backyardViewUI.OnEmptySlotTapped += (envIdx, slotIdx) =>
                {
                    satchelUI?.Show(envIdx, slotIdx);
                };

                backyardViewUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                    {
                        if (result.isNewDiscovery)
                        {
                            _pendingDiscoveryResult = result;
                            discoveryPopupUI?.Show(result);
                        }
                        else
                            harvestResultUI?.Show(result);
                    }
                    backyardViewUI?.RefreshAllSlots();
                };
            }

            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    backyardViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }
            if (discoveryPopupUI != null)
            {
                discoveryPopupUI.OnDismissed += () =>
                {
                    harvestResultUI?.Show(_pendingDiscoveryResult);
                };
            }

            mainCam = Camera.main;

            // Start on terrarium page
            pageView.GoToPage(TerrariumPageIndex, false);
            backyardIsoView?.gameObject.SetActive(true);
            backyardViewUI?.SetPageActive(true);

            // Location gate
            locationGate = root.Q<VisualElement>("location-gate");
            gateStatus = root.Q<Label>("gate-status");
            gateRetry = root.Q<Button>("gate-retry");
            if (gateRetry != null)
                gateRetry.clicked += OnGateRetry;

            if (WeatherService.Instance != null)
            {
                if (WeatherService.Instance.IsLocationResolved)
                    OnLocationResolved(WeatherService.Instance.HasLocation);
                else
                    WeatherService.Instance.OnLocationResolved += OnLocationResolved;
            }
        }

        private void Update()
        {
            if (backyardIsoView == null || pageView == null || mainCam == null) return;
            var panel = pageView.panel;
            if (panel == null || pageView.PageWidth <= 0) return;

            // Advance our own lerped container position (CSS resolvedStyle.translate.x snaps to
            // the target immediately during a transition, so we can't use it for smooth iso sync)
            if (_isoAnimStartTime >= 0f)
            {
                float elapsed = Time.time - _isoAnimStartTime;
                if (elapsed < IsoDuration)
                {
                    float t = elapsed / IsoDuration;
                    t = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                    _isoContainerX = Mathf.Lerp(_isoAnimStartX, _isoAnimTargetX, t);
                }
                else
                {
                    _isoContainerX = _isoAnimTargetX;
                    _isoAnimStartTime = -1f;
                }
            }
            else
            {
                // Not animating to a new page — track the real value directly (accurate during drag
                // because SwipeablePageView sets transitionDuration=0 while dragging)
                _isoContainerX = pageView.CurrentPageContainerX;
            }

            // Compute how far the terrarium page is from its centered rest position (panel points)
            float panelDelta = _isoContainerX + TerrariumPageIndex * pageView.PageWidth;

            // Panel points → screen pixels
            float screenDeltaX = panelDelta * panel.scaledPixelsPerPoint;

            // Screen pixels → world units via camera
            float z = Mathf.Abs(mainCam.transform.position.z);
            float worldDeltaX = mainCam.ScreenToWorldPoint(new Vector3(screenDeltaX, 0, z)).x
                              - mainCam.ScreenToWorldPoint(new Vector3(0, 0, z)).x;

            backyardIsoView.SetSlideOffset(worldDeltaX);
        }

        private void OnPageChanged(int pageIndex)
        {
            // Kick off our own lerp to mirror the CSS transition in SwipeablePageView
            _isoAnimStartX = _isoContainerX;
            _isoAnimTargetX = -pageIndex * pageView.PageWidth;
            _isoAnimStartTime = Time.time;

            backyardViewUI?.SetPageActive(pageIndex == 2);

            if (pageIndex != TerrariumPageIndex)
                backyardViewUI?.CloseConsumablePicker();
            if (pageIndex != TerrariumPageIndex)
                envSwitcherBar?.Hide();

            switch (pageIndex)
            {
                case 0: codexUI?.Show(); break;
                case 1: seedShopUI?.Show(); break;
                case 3: greenhouseUI?.Show(); break;
                case 4: constructionUI?.Show(); break;
            }
        }

        private void OnTerrariumReactivated()
        {
            if (EnvironmentManager.Instance == null) return;
            int unlocked = 0;
            for (int i = 0; i < EnvironmentManager.Instance.Environments.Count; i++)
                if (EnvironmentManager.Instance.IsUnlocked(i)) unlocked++;
            if (unlocked <= 1) return;
            envSwitcherBar?.Toggle();
        }

        private void OnEnvironmentSelected(int envIndex)
        {
            EnvironmentManager.Instance?.SetActiveEnvironment(envIndex);
            envSwitcherBar?.Hide();
        }

        private void OnLocationResolved(bool success)
        {
            if (locationGate == null) return;
            if (success)
            {
                locationGate.style.display = DisplayStyle.None;
            }
            else
            {
                gateStatus.text = "Location access is required to play.\nPlease enable Location Services in Settings.";
                if (gateRetry != null)
                    gateRetry.style.display = DisplayStyle.Flex;
            }
        }

        private void OnGateRetry()
        {
            gateStatus.text = "Acquiring location...";
            if (gateRetry != null)
                gateRetry.style.display = DisplayStyle.None;
            WeatherService.Instance?.RetryLocation();
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnLocationResolved -= OnLocationResolved;
            if (pageView != null)
                pageView.OnPageChanged -= OnPageChanged;
            if (bottomNavUI != null)
                bottomNavUI.OnTerrariumReactivated -= OnTerrariumReactivated;
            if (envSwitcherBar != null)
                envSwitcherBar.OnEnvironmentSelected -= OnEnvironmentSelected;
        }
    }
}
