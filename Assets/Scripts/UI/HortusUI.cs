using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument uiDocument;

        private const int DefaultPageIndex = 2; // Terrarium

        private SwipeablePageView pageView;
        private BottomNavUI bottomNavUI;
        private HearthViewUI hearthViewUI;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private SeedShopUI seedShopUI;
        private GreenhouseUI greenhouseUI;
        private HarvestResultUI harvestResultUI;
        private DebugWeatherPanel debugPanel;
        [SerializeField] private HearthIsometricView hearthIsoView;

        // Location gate
        private VisualElement locationGate;
        private Label gateStatus;
        private Button gateRetry;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            // Get sub-controllers
            hearthViewUI = GetComponent<HearthViewUI>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            seedShopUI = GetComponent<SeedShopUI>();
            greenhouseUI = GetComponent<GreenhouseUI>();
            harvestResultUI = GetComponent<HarvestResultUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            // Build SwipeablePageView
            pageView = new SwipeablePageView();
            var viewport = root.Q<VisualElement>("page-viewport");
            viewport.Add(pageView);

            // Reparent pages from UXML into the page view
            string[] pageNames = { "codex-page", "shop-page", "terrarium-page", "greenhouse-page", "locked-page" };
            foreach (var name in pageNames)
            {
                var page = root.Q<VisualElement>(name);
                page.RemoveFromHierarchy();
                page.style.display = DisplayStyle.Flex;
                pageView.AddPage(page);
            }

            // Initialize sub-controllers
            hearthViewUI?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            seedShopUI?.Initialize(root);
            greenhouseUI?.Initialize(root);
            harvestResultUI?.Initialize(root);
            debugPanel?.Initialize(root);

            // Initialize bottom nav
            bottomNavUI = GetComponent<BottomNavUI>();
            bottomNavUI?.Initialize(root, pageView);

            // Page change callbacks — refresh content when page becomes visible
            pageView.OnPageChanged += OnPageChanged;

            // Wire hearth slot events
            if (hearthViewUI != null)
            {
                hearthViewUI.OnEmptySlotTapped += (envIdx, slotIdx) =>
                {
                    satchelUI?.Show(envIdx, slotIdx);
                };

                hearthViewUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                        harvestResultUI?.Show(result);
                    hearthViewUI?.RefreshAllSlots();
                };
            }

            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    hearthViewUI?.RefreshAllSlots();
                    greenhouseUI?.RefreshDisplay();
                };
            }

            // Start on terrarium page
            pageView.GoToPage(DefaultPageIndex, false);
            hearthIsoView?.gameObject.SetActive(true);
            hearthViewUI?.SetPageActive(true);

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

        private void OnPageChanged(int pageIndex)
        {
            // Show/hide isometric tiles only on terrarium page (index 2)
            hearthIsoView?.gameObject.SetActive(pageIndex == 2);
            hearthViewUI?.SetPageActive(pageIndex == 2);

            switch (pageIndex)
            {
                case 0: codexUI?.Show(); break;
                case 1: seedShopUI?.Show(); break;
                case 3: greenhouseUI?.Show(); break;
            }
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
        }
    }
}
