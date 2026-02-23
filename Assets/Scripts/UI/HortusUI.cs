using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlantVisual plantVisual;
        [SerializeField] private UIDocument uiDocument;

        // Sub-controllers (on same GameObject or children)
        private PulseButton pulseButton;
        private ResonanceBar resonanceBar;
        private CurrencyDisplay currencyDisplay;
        private SatchelUI satchelUI;
        private CodexUI codexUI;
        private GreenhouseUI greenhouseUI;
        private DebugWeatherPanel debugPanel;

        // Panel roots for toggling
        private VisualElement satchelPanel;
        private VisualElement codexPanel;
        private VisualElement greenhousePanel;
        private VisualElement debugPanelRoot;

        private void Start()
        {
            var root = uiDocument.rootVisualElement;

            // Get sub-controllers
            pulseButton = GetComponent<PulseButton>();
            resonanceBar = GetComponent<ResonanceBar>();
            currencyDisplay = GetComponent<CurrencyDisplay>();
            satchelUI = GetComponent<SatchelUI>();
            codexUI = GetComponent<CodexUI>();
            greenhouseUI = GetComponent<GreenhouseUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            // Initialize all sub-controllers with the root
            pulseButton?.Initialize(root);
            resonanceBar?.Initialize(root);
            currencyDisplay?.Initialize(root);
            satchelUI?.Initialize(root);
            codexUI?.Initialize(root);
            greenhouseUI?.Initialize(root);
            debugPanel?.Initialize(root);

            // Cache panel roots
            satchelPanel = root.Q<VisualElement>("satchel-panel");
            codexPanel = root.Q<VisualElement>("codex-panel");
            greenhousePanel = root.Q<VisualElement>("greenhouse-panel");
            debugPanelRoot = root.Q<VisualElement>("debug-panel");

            // Nav button wiring
            pulseButton.OnPulse += OpenSatchel;
            root.Q<Button>("codex-button").clicked += () => TogglePanel(codexPanel, codexUI);
            root.Q<Button>("greenhouse-button").clicked += () => TogglePanel(greenhousePanel, greenhouseUI);
            root.Q<Button>("debug-button").clicked += () => TogglePanel(debugPanelRoot, debugPanel);

            // Plant state
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged += RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated += OnGrowth;
                RefreshPlantVisual();
            }

            CloseAllPanels();
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged -= RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated -= OnGrowth;
            }
        }

        private void RefreshPlantVisual()
        {
            var pm = PlantManager.Instance;
            if (pm.State == PlantState.Empty)
            {
                plantVisual.Clear();
            }
            else
            {
                plantVisual.SetVariant(pm.CurrentVariant);
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

        private void TogglePanel(VisualElement panel, object controller)
        {
            bool wasVisible = panel.resolvedStyle.display == DisplayStyle.Flex;
            CloseAllPanels();
            if (!wasVisible)
            {
                if (controller is SatchelUI s) s.Show();
                else if (controller is CodexUI c) c.Show();
                else if (controller is GreenhouseUI g) g.Show();
                else if (controller is DebugWeatherPanel d) d.Show();
            }
        }

        private void CloseAllPanels()
        {
            satchelUI?.Hide();
            codexUI?.Hide();
            greenhouseUI?.Hide();
            debugPanel?.Hide();
        }
    }
}
