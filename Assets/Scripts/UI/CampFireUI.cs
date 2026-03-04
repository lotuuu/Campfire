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
        private MerchantUI merchantUI;
        private DialogueUI dialogueUI;

        private VisualElement overlayContainer;
        private VisualElement overlayBackdrop;
        private Label overlayTitle;
        private VisualElement overlayBody;
        private VisualElement apothekePanel;
        private VisualElement lettersPanel;
        private VisualElement buildPanel;
        private VisualElement debugPanelElement;
        private VisualElement questsPanel;
        private VisualElement merchantPanel;

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
            merchantUI = GetComponent<MerchantUI>();
            merchantUI?.Initialize(root);
            dialogueUI = GetComponent<DialogueUI>();
            dialogueUI?.Initialize(root);

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
            merchantPanel = root.Q("merchant-panel");

            var closeBtn = root.Q<Button>("overlay-close");
            closeBtn?.RegisterCallback<ClickEvent>(_ => CloseOverlay());
            overlayBackdrop?.RegisterCallback<ClickEvent>(_ => CloseOverlay());

            CloseOverlay();

            // Wire bottom nav
            if (bottomNav != null)
            {
                bottomNav.OnApothekeClicked += () =>
                {
                    apotheke?.Refresh();
                    OpenOverlay("Seeds", apothekePanel);
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

            // Wire debug button (editor + development builds)
            var debugBtn = root.Q<Button>("btn-debug");
            if (debugBtn != null)
            {
                if (Application.isEditor || UnityEngine.Debug.isDebugBuild)
                    debugBtn.clicked += () => OpenOverlay("Debug", debugPanelElement);
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
                    OpenOverlay("Seeds", apothekePanel);
                };
            }

            // Wire Merchant tile tap
            if (campsiteView != null)
                campsiteView.OnMerchantTapped += index =>
                {
                    var data = SaveManager.Instance?.Data;
                    if (data == null || index < 0 || index >= data.merchants.Count) return;
                    var merchant = data.merchants[index];

                    if (!merchant.dialogueSeen && merchant.dialogueLines.Count > 0 && dialogueUI != null)
                    {
                        // Find MerchantData for portrait
                        Texture2D portrait = null;
                        var allMerchants = Resources.LoadAll<MerchantData>("Merchants");
                        foreach (var md in allMerchants)
                        {
                            if (md.merchantName == merchant.merchantName) { portrait = md.portrait; break; }
                        }

                        dialogueUI.Show(merchant.merchantName, merchant.dialogueLines, () =>
                        {
                            merchant.dialogueSeen = true;
                            SaveManager.Instance.Save();
                            merchantUI?.ShowMerchant(index);
                            OpenOverlay("Night Merchant", merchantPanel);
                        }, portrait);
                    }
                    else
                    {
                        merchantUI?.ShowMerchant(index);
                        OpenOverlay("Night Merchant", merchantPanel);
                    }
                };

            // Location gate
            if (WeatherService.Instance != null && !WeatherService.Instance.IsLocationResolved)
            {
                WeatherService.Instance.OnLocationResolved += OnLocationResolved;
            }
        }

        private void UpdateQuestBadge()
        {
            if (bottomNav == null || MallumManager.Instance == null) return;
            int completed = MallumManager.Instance.GetCompletedQuestCount();
            bottomNav.UpdateQuestBadge(completed);
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
            if (buildPanel != null) buildPanel.style.display = DisplayStyle.None;
            if (debugPanelElement != null) debugPanelElement.style.display = DisplayStyle.None;
            if (questsPanel != null) questsPanel.style.display = DisplayStyle.None;
            if (merchantPanel != null) merchantPanel.style.display = DisplayStyle.None;
        }
    }
}
