using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlantVisual plantVisual;
        [SerializeField] private UIDocument uiDocument;

        private PulseButton pulseButton;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private TerrariumUI terrariumUI;
        private SeedShopUI seedShopUI;
        private HarvestResultUI harvestResultUI;
        private DebugWeatherPanel debugPanel;

        private VisualElement satchelPanel;
        private VisualElement codexPanel;
        private VisualElement terrariumPanel;
        private VisualElement shopPanel;
        private VisualElement debugPanelRoot;

        // Location gate
        private VisualElement locationGate;
        private Label gateStatus;
        private Button gateRetry;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            pulseButton = GetComponent<PulseButton>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            terrariumUI = GetComponent<TerrariumUI>();
            seedShopUI = GetComponent<SeedShopUI>();
            harvestResultUI = GetComponent<HarvestResultUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            pulseButton?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            terrariumUI?.Initialize(root);
            seedShopUI?.Initialize(root);
            harvestResultUI?.Initialize(root);
            debugPanel?.Initialize(root);

            satchelPanel = root.Q<VisualElement>("satchel-panel");
            codexPanel = root.Q<VisualElement>("codex-panel");
            terrariumPanel = root.Q<VisualElement>("terrarium-panel");
            shopPanel = root.Q<VisualElement>("shop-panel");
            debugPanelRoot = root.Q<VisualElement>("debug-panel");

            pulseButton.OnPulse += () =>
            {
                var pm = PlantManager.Instance;
                if (pm.GetMatureCount() > 0 || pm.GetGrowingCount() > 0)
                    OpenTerrarium();
                else
                    OpenSatchel();
            };

            root.Q<Button>("codex-button").clicked += () => TogglePanel(codexPanel, codexUI);
            root.Q<Button>("greenhouse-button").clicked += () => TogglePanel(terrariumPanel, terrariumUI);
            root.Q<Button>("shop-button").clicked += () => TogglePanel(shopPanel, seedShopUI);
            root.Q<Button>("debug-button").clicked += () => TogglePanel(debugPanelRoot, debugPanel);

            if (terrariumUI != null)
            {
                terrariumUI.OnEmptySlotTapped += (envIdx, slotIdx) =>
                {
                    CloseAllPanels();
                    satchelUI?.Show(envIdx, slotIdx);
                };

                terrariumUI.OnMatureSlotTapped += (envIdx, slotIdx) =>
                {
                    var result = PlantManager.Instance.Harvest(envIdx, slotIdx);
                    if (result.seed != null)
                    {
                        harvestResultUI?.Show(result);
                    }
                    terrariumUI?.RefreshDisplay();
                };
            }

            if (harvestResultUI != null)
            {
                harvestResultUI.OnDismissed += () =>
                {
                    terrariumUI?.RefreshDisplay();
                    RefreshPlantVisual();
                };
            }

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged += RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated += OnGrowth;
                RefreshPlantVisual();
            }

            CloseAllPanels();

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

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged -= RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated -= OnGrowth;
            }
        }

        private void RefreshPlantVisual()
        {
            var pm = PlantManager.Instance;
            var featured = pm.CurrentVariant;
            if (featured == null)
            {
                plantVisual.Clear();
            }
            else
            {
                plantVisual.SetVariant(featured);
                plantVisual.SetGrowth(pm.GrowthProgress);
            }
        }

        private void OnGrowth(float progress)
        {
            plantVisual.SetGrowth(progress);
        }

        private void OpenSatchel()
        {
            CloseAllPanels();
            satchelUI?.Show();
        }

        private void OpenTerrarium()
        {
            CloseAllPanels();
            terrariumUI?.Show();
        }

        private void TogglePanel(VisualElement panel, object controller)
        {
            bool wasVisible = panel != null && panel.resolvedStyle.display == DisplayStyle.Flex;
            CloseAllPanels();
            if (!wasVisible)
            {
                if (controller is SatchelUI s) s.Show();
                else if (controller is CodexUI c) c.Show();
                else if (controller is TerrariumUI t) t.Show();
                else if (controller is SeedShopUI sh) sh.Show();
                else if (controller is DebugWeatherPanel d) d.Show();
            }
        }

        private void CloseAllPanels()
        {
            satchelUI?.Hide();
            codexUI?.Hide();
            terrariumUI?.Hide();
            seedShopUI?.Hide();
            debugPanel?.Hide();
        }
    }
}
