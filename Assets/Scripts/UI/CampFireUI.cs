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
        private CraftUI craft;
        private ResourceDisplayUI resourceDisplay;
        private DebugWeatherPanel debugPanel;

        private VisualElement overlayContainer;
        private VisualElement overlayBackdrop;
        private Label overlayTitle;
        private VisualElement overlayBody;
        private VisualElement apothekePanel;
        private VisualElement lettersPanel;
        private VisualElement craftPanel;
        private VisualElement debugPanelElement;

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
            craft = GetComponent<CraftUI>();
            resourceDisplay = GetComponent<ResourceDisplayUI>();
            debugPanel = GetComponent<DebugWeatherPanel>();

            weatherBar?.Initialize(root);
            bottomNav?.Initialize(root);
            campsiteView?.Initialize(root);
            apotheke?.Initialize(root);
            letters?.Initialize(root);
            craft?.Initialize(root);
            resourceDisplay?.Initialize(root);
            debugPanel?.Initialize(root);

            // Overlay setup
            overlayContainer = root.Q("overlay-container");
            overlayBackdrop = root.Q("overlay-backdrop");
            overlayTitle = root.Q<Label>("overlay-title");
            overlayBody = root.Q("overlay-body");
            apothekePanel = root.Q("apotheke-panel");
            lettersPanel = root.Q("letters-panel");
            craftPanel = root.Q("craft-panel");
            debugPanelElement = root.Q("debug-panel");

            var closeBtn = root.Q<Button>("overlay-close");
            closeBtn?.RegisterCallback<ClickEvent>(_ => CloseOverlay());
            overlayBackdrop?.RegisterCallback<ClickEvent>(_ => CloseOverlay());

            CloseOverlay();

            // Wire bottom nav
            if (bottomNav != null)
            {
                bottomNav.OnApothekeClicked += () => OpenOverlay("Apotheke", apothekePanel);
                bottomNav.OnLettersClicked += () => OpenOverlay("Letters", lettersPanel);
                bottomNav.OnCraftClicked += () => OpenOverlay("Craft", craftPanel);
            }

            // Wire debug button
            var debugBtn = root.Q<Button>("btn-debug");
            if (debugBtn != null)
            {
#if UNITY_EDITOR
                debugBtn.clicked += () => OpenOverlay("Debug", debugPanelElement);
#else
                debugBtn.style.display = DisplayStyle.None;
#endif
            }

            // Wire craft placement mode
            if (craft != null && campsiteView != null)
            {
                craft.OnRequestPlacement += type =>
                {
                    CloseOverlay();
                    campsiteView.EnterPlacementMode(type);
                };
            }

            // Location gate
            if (WeatherService.Instance != null && !WeatherService.Instance.IsLocationResolved)
            {
                WeatherService.Instance.OnLocationResolved += OnLocationResolved;
            }
        }

        private void OnLocationResolved(bool success)
        {
            WeatherService.Instance.OnLocationResolved -= OnLocationResolved;
        }

        public void OpenOverlay(string title, VisualElement panel)
        {
            HideAllPanels();
            overlayTitle.text = title;
            panel.style.display = DisplayStyle.Flex;
            overlayContainer.style.display = DisplayStyle.Flex;
        }

        public void CloseOverlay()
        {
            HideAllPanels();
            overlayContainer.style.display = DisplayStyle.None;
        }

        private void HideAllPanels()
        {
            if (apothekePanel != null) apothekePanel.style.display = DisplayStyle.None;
            if (lettersPanel != null) lettersPanel.style.display = DisplayStyle.None;
            if (craftPanel != null) craftPanel.style.display = DisplayStyle.None;
            if (debugPanelElement != null) debugPanelElement.style.display = DisplayStyle.None;
        }
    }
}
