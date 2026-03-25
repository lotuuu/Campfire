using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Garden
{
    public class ProfilePopupUI : MonoBehaviour
    {
        private VisualElement hudProfile;
        private VisualElement profileBackdrop;
        private VisualElement profilePopup;
        private VisualElement profilePicLarge;
        private VisualElement hudProfilePic;

        // Name row
        private Label displayNameLabel;
        private Button nameEditBtn;
        private TextField displayNameInput;

        private Label friendCodeLabel;
        private Label flameLevelLabel;

        private Label playerIdLabel;
        private Label serverLabel;
        private Label versionLabel;

        private Button deleteBtn;
        private VisualElement confirmRow;
        private Button confirmCancel;
        private Button confirmDelete;
        private Button debugBtn;

        // Profile pic selector
        private Button picEditBtn;
        private VisualElement picSelector;
        private VisualElement profilePicGrid;
        private Button picCloseBtn;

        private bool isOpen;

        private static readonly string[] ProfilePicIds =
            { "alchemist", "elder", "farmer", "herbalist", "traveler" };

        public void Initialize(VisualElement root)
        {
            hudProfile = root.Q("hud-profile");
            profileBackdrop = root.Q("profile-backdrop");
            profilePopup = root.Q("profile-popup");
            profileBackdrop?.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == profileBackdrop) CloseBloom();
            });
            hudProfilePic = root.Q("hud-profile-pic");
            profilePicLarge = root.Q("profile-pic-large");

            // Name row
            displayNameLabel = root.Q<Label>("profile-display-name-label");
            nameEditBtn = root.Q<Button>("profile-name-edit-btn");
            displayNameInput = root.Q<TextField>("profile-display-name");

            friendCodeLabel = root.Q<Label>("profile-friend-code");
            flameLevelLabel = root.Q<Label>("profile-flame-level");

            playerIdLabel = root.Q<Label>("profile-player-id");
            serverLabel = root.Q<Label>("profile-server");
            versionLabel = root.Q<Label>("profile-version");

            deleteBtn = root.Q<Button>("profile-delete-btn");
            confirmRow = root.Q<VisualElement>("profile-confirm-row");
            confirmCancel = root.Q<Button>("profile-confirm-cancel");
            confirmDelete = root.Q<Button>("profile-confirm-delete");
            debugBtn = root.Q<Button>("profile-debug-btn");

            // Profile pic selector
            picEditBtn = root.Q<Button>("profile-pic-edit-btn");
            picSelector = root.Q("profile-pic-selector");
            profilePicGrid = root.Q("profile-pic-grid");
            picCloseBtn = root.Q<Button>("profile-pic-close-btn");

            // Profile pic click -> toggle bloom
            hudProfile?.RegisterCallback<ClickEvent>(OnProfileClicked);

            // Load profile pic
            LoadProfilePics();

            // Pic edit button -> show selector
            if (picEditBtn != null)
                picEditBtn.clicked += () => ShowPicSelector(true);
            if (picCloseBtn != null)
                picCloseBtn.clicked += () => ShowPicSelector(false);

            // Name edit button -> toggle text field
            if (nameEditBtn != null)
                nameEditBtn.clicked += OnNameEditClicked;

            // Display name: set initial value
            var playerName = SocialSaveManager.Instance?.Data?.displayName;
            if (displayNameLabel != null)
                displayNameLabel.text = string.IsNullOrEmpty(playerName) ? "Camper" : playerName;
            if (displayNameInput != null)
            {
                if (!string.IsNullOrEmpty(playerName))
                    displayNameInput.SetValueWithoutNotify(playerName);
                displayNameInput.RegisterValueChangedCallback(evt =>
                {
                    if (SocialService.Instance != null)
                        _ = SocialService.Instance.UpdateDisplayName(evt.newValue);
                    if (displayNameLabel != null)
                        displayNameLabel.text = evt.newValue;
                });
            }

            // Account info
            var socialData = SocialSaveManager.Instance?.Data;
            if (playerIdLabel != null)
                playerIdLabel.text = !string.IsNullOrEmpty(socialData?.uid) ? socialData.uid : "---";
            if (serverLabel != null)
                serverLabel.text = ServerConfig.Current.name;
            if (versionLabel != null)
                versionLabel.text = Application.version;

            // Friend code
            if (friendCodeLabel != null && socialData != null)
                friendCodeLabel.text = $"Friend Code: {socialData.friendCode ?? "---"}";

            // Delete save
            if (deleteBtn != null)
            {
                deleteBtn.clicked += () =>
                {
                    deleteBtn.style.display = DisplayStyle.None;
                    if (confirmRow != null) confirmRow.style.display = DisplayStyle.Flex;
                };
            }

            if (confirmCancel != null)
            {
                confirmCancel.clicked += () =>
                {
                    if (confirmRow != null) confirmRow.style.display = DisplayStyle.None;
                    if (deleteBtn != null) deleteBtn.style.display = DisplayStyle.Flex;
                };
            }

            if (confirmDelete != null)
            {
                confirmDelete.clicked += () =>
                {
                    SaveManager.Instance?.DeleteSave();
                    SocialSaveManager.Instance?.DeleteSave();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                };
            }

            // Debug button
            if (debugBtn != null)
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    debugBtn.style.display = DisplayStyle.Flex;
                    debugBtn.clicked += OnDebugClicked;
                }
            }

        }

        public void RefreshContent()
        {
            if (flameLevelLabel != null && SaveManager.Instance?.Data != null)
                flameLevelLabel.text = $"Flame Level {SaveManager.Instance.Data.flameLevel}";
        }

        // ── Profile pic ──

        private void LoadProfilePics()
        {
            var picId = SaveManager.Instance?.Data?.profilePicId ?? "farmer";
            ApplyProfilePic(picId);
        }

        private void ApplyProfilePic(string picId)
        {
            var tex = SpriteService.Instance?.GetTexture($"ui/profile/{picId}");
            if (tex != null)
            {
                if (hudProfilePic != null)
                    hudProfilePic.style.backgroundImage = tex;
                if (profilePicLarge != null)
                    profilePicLarge.style.backgroundImage = tex;
            }
        }

        private void ShowPicSelector(bool show)
        {
            if (picSelector == null) return;
            if (show)
            {
                BuildProfilePicGrid();
                picSelector.style.display = DisplayStyle.Flex;
            }
            else
            {
                picSelector.style.display = DisplayStyle.None;
            }
        }

        private void BuildProfilePicGrid()
        {
            if (profilePicGrid == null || SpriteService.Instance == null) return;
            profilePicGrid.Clear();

            var currentId = SaveManager.Instance?.Data?.profilePicId ?? "farmer";

            foreach (var picId in ProfilePicIds)
            {
                var option = new VisualElement();
                option.AddToClassList("pfp-option");
                if (picId == currentId)
                    option.AddToClassList("pfp-selected");

                var tex = SpriteService.Instance.GetTexture($"ui/profile/{picId}");
                if (tex != null)
                    option.style.backgroundImage = tex;

                var capturedId = picId;
                option.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    SelectProfilePic(capturedId);
                });

                profilePicGrid.Add(option);
            }
        }

        private void SelectProfilePic(string picId)
        {
            if (SaveManager.Instance?.Data == null) return;
            SaveManager.Instance.Data.profilePicId = picId;
            SaveManager.Instance.Save();

            ApplyProfilePic(picId);

            // Update grid selection highlight
            if (profilePicGrid != null)
            {
                foreach (var child in profilePicGrid.Children())
                    child.RemoveFromClassList("pfp-selected");

                int idx = System.Array.IndexOf(ProfilePicIds, picId);
                if (idx >= 0 && idx < profilePicGrid.childCount)
                    profilePicGrid[idx].AddToClassList("pfp-selected");
            }
        }

        // ── Name editing ──

        private void OnNameEditClicked()
        {
            if (displayNameInput == null || displayNameLabel == null) return;

            // Show text field, hide label row
            displayNameLabel.parent.style.display = DisplayStyle.None;
            displayNameInput.style.display = DisplayStyle.Flex;
            displayNameInput.Focus();

            // When focus is lost, switch back to label
            displayNameInput.RegisterCallback<FocusOutEvent>(OnNameEditDone);
        }

        private void OnNameEditDone(FocusOutEvent evt)
        {
            if (displayNameInput == null || displayNameLabel == null) return;
            displayNameInput.UnregisterCallback<FocusOutEvent>(OnNameEditDone);

            displayNameInput.style.display = DisplayStyle.None;
            displayNameLabel.parent.style.display = DisplayStyle.Flex;
        }

        // ── Bloom open/close ──

        private void OnProfileClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ToggleBloom();
        }

        public void ToggleBloom()
        {
            if (isOpen)
                CloseBloom();
            else
                OpenBloom();
        }

        public void OpenBloom()
        {
            if (profileBackdrop == null) return;
            isOpen = true;
            RefreshContent();
            profileBackdrop.style.display = DisplayStyle.Flex;
            if (profilePopup != null)
            {
                profilePopup.style.display = DisplayStyle.Flex;
                profilePopup.schedule.Execute(() => profilePopup.AddToClassList("popup-visible"));
            }
        }

        public void CloseBloom()
        {
            if (profileBackdrop == null) return;
            isOpen = false;
            profilePopup?.RemoveFromClassList("popup-visible");
            profileBackdrop.style.display = DisplayStyle.None;
            if (profilePopup != null) profilePopup.style.display = DisplayStyle.None;

            // Reset states
            if (deleteBtn != null) deleteBtn.style.display = DisplayStyle.Flex;
            if (confirmRow != null) confirmRow.style.display = DisplayStyle.None;
            ShowPicSelector(false);

            // Close name edit if open
            if (displayNameInput != null)
                displayNameInput.style.display = DisplayStyle.None;
            if (displayNameLabel?.parent != null)
                displayNameLabel.parent.style.display = DisplayStyle.Flex;
        }

        private void OnDebugClicked()
        {
            CloseBloom();
            var debugPanelElement = profileBackdrop?.panel?.visualTree?.Q("debug-panel");
            if (debugPanelElement != null)
                CampFireUI.Instance?.OpenOverlay("Debug", debugPanelElement);
        }

    }
}
