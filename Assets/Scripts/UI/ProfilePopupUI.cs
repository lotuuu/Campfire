using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Garden
{
    public class ProfilePopupUI : MonoBehaviour
    {
        private VisualElement hudProfile;
        private VisualElement profileBloom;
        private VisualElement bloomDismiss;
        private VisualElement profilePicLarge;
        private VisualElement hudProfilePic;

        private TextField displayNameInput;
        private Label friendCodeLabel;
        private Label flameLevelLabel;
        private Label dateTimeLabel;
        private Label weatherSummaryLabel;

        private Slider musicSlider;
        private Slider sfxSlider;
        private Label musicValue;
        private Label sfxValue;
        private DropdownField langDropdown;
        private Toggle vibrationToggle;

        private Label playerIdLabel;
        private Label serverLabel;
        private Label versionLabel;

        private Button deleteBtn;
        private VisualElement confirmRow;
        private Button confirmCancel;
        private Button confirmDelete;
        private Button debugBtn;

        private VisualElement profilePicGrid;

        private bool isOpen;

        private static readonly string[] ProfilePicIds =
            { "alchemist", "elder", "farmer", "herbalist", "traveler" };

        public void Initialize(VisualElement root)
        {
            hudProfile = root.Q("hud-profile");
            profileBloom = root.Q("profile-bloom");
            bloomDismiss = root.Q("bloom-dismiss");
            hudProfilePic = root.Q("hud-profile-pic");
            profilePicLarge = root.Q("profile-pic-large");

            displayNameInput = root.Q<TextField>("profile-display-name");
            friendCodeLabel = root.Q<Label>("profile-friend-code");
            flameLevelLabel = root.Q<Label>("profile-flame-level");
            dateTimeLabel = root.Q<Label>("profile-date-time");
            weatherSummaryLabel = root.Q<Label>("profile-weather-summary");

            musicSlider = root.Q<Slider>("profile-music-slider");
            sfxSlider = root.Q<Slider>("profile-sfx-slider");
            musicValue = root.Q<Label>("profile-music-value");
            sfxValue = root.Q<Label>("profile-sfx-value");
            langDropdown = root.Q<DropdownField>("profile-language-dropdown");
            vibrationToggle = root.Q<Toggle>("profile-vibration-toggle");

            playerIdLabel = root.Q<Label>("profile-player-id");
            serverLabel = root.Q<Label>("profile-server");
            versionLabel = root.Q<Label>("profile-version");

            deleteBtn = root.Q<Button>("profile-delete-btn");
            confirmRow = root.Q<VisualElement>("profile-confirm-row");
            confirmCancel = root.Q<Button>("profile-confirm-cancel");
            confirmDelete = root.Q<Button>("profile-confirm-delete");
            debugBtn = root.Q<Button>("profile-debug-btn");
            profilePicGrid = root.Q("profile-pic-grid");

            // Profile pic click -> toggle bloom
            hudProfile?.RegisterCallback<ClickEvent>(OnProfileClicked);

            // Load profile pic and build picker
            LoadProfilePics();
            BuildProfilePicGrid();

            // Settings: audio
            var data = SaveManager.Instance?.Data;
            if (data != null && musicSlider != null && sfxSlider != null)
            {
                musicSlider.value = data.musicVolume * 100f;
                sfxSlider.value = data.sfxVolume * 100f;
                if (musicValue != null) musicValue.text = $"{data.musicVolume * 100f:F0}%";
                if (sfxValue != null) sfxValue.text = $"{data.sfxVolume * 100f:F0}%";
            }

            musicSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetMusicVolume(vol);
                if (musicValue != null) musicValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.musicVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            sfxSlider?.RegisterValueChangedCallback(evt =>
            {
                float vol = evt.newValue / 100f;
                AudioManager.Instance?.SetSFXVolume(vol);
                if (sfxValue != null) sfxValue.text = $"{evt.newValue:F0}%";
                if (SaveManager.Instance?.Data != null)
                {
                    SaveManager.Instance.Data.sfxVolume = vol;
                    SaveManager.Instance.Save();
                }
            });

            // Settings: vibration
            if (vibrationToggle != null && data != null)
            {
                vibrationToggle.value = data.vibrationEnabled;
                vibrationToggle.RegisterValueChangedCallback(evt =>
                {
                    if (SaveManager.Instance?.Data != null)
                    {
                        SaveManager.Instance.Data.vibrationEnabled = evt.newValue;
                        SaveManager.Instance.Save();
                    }
                });
            }

            // Settings: language
            if (langDropdown != null)
            {
                RefreshLanguageDropdown();
                langDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (LocalizationService.Instance != null)
                        _ = LocalizationService.Instance.SwitchLocale(evt.newValue);
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
                friendCodeLabel.text = $"Code: {socialData.friendCode ?? "---"}";

            // Display name
            if (displayNameInput != null)
            {
                var name = SocialSaveManager.Instance?.Data?.displayName;
                if (!string.IsNullOrEmpty(name))
                    displayNameInput.SetValueWithoutNotify(name);
                displayNameInput.RegisterValueChangedCallback(evt =>
                {
                    if (SocialService.Instance != null)
                        SocialService.Instance.UpdateDisplayName(evt.newValue);
                });
            }

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

            // Locale changes
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLocaleChanged -= OnLocaleChanged;
        }

        private void Update()
        {
            if (isOpen && dateTimeLabel != null)
            {
                var now = GameTime.Now;
                dateTimeLabel.text = now.ToString("dd MMM  h:mm tt").ToUpper();
            }
        }

        public void RefreshLanguageDropdown()
        {
            if (langDropdown == null || LocalizationService.Instance == null) return;
            langDropdown.choices = LocalizationService.Instance.SupportedLocales;
            langDropdown.SetValueWithoutNotify(LocalizationService.Instance.CurrentLocale);
        }

        public void RefreshContent()
        {
            // Flame level
            if (flameLevelLabel != null && SaveManager.Instance?.Data != null)
                flameLevelLabel.text = $"Flame Level {SaveManager.Instance.Data.flameLevel}";

            // Weather summary
            if (weatherSummaryLabel != null && WeatherService.Instance != null)
            {
                var w = WeatherService.Instance.CurrentWeather;
                weatherSummaryLabel.text = $"{w.condition} {w.temperature:F0}\u00b0";
            }
        }

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
                {
                    child.RemoveFromClassList("pfp-selected");
                }
                // Re-highlight the selected one
                int idx = System.Array.IndexOf(ProfilePicIds, picId);
                if (idx >= 0 && idx < profilePicGrid.childCount)
                    profilePicGrid[idx].AddToClassList("pfp-selected");
            }
        }

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
            if (profileBloom == null) return;
            isOpen = true;
            RefreshContent();
            profileBloom.AddToClassList("bloom-open");
            bloomDismiss?.AddToClassList("bloom-dismiss-active");
        }

        public void CloseBloom()
        {
            if (profileBloom == null) return;
            isOpen = false;
            profileBloom.RemoveFromClassList("bloom-open");
            bloomDismiss?.RemoveFromClassList("bloom-dismiss-active");

            // Reset delete confirm state
            if (deleteBtn != null) deleteBtn.style.display = DisplayStyle.Flex;
            if (confirmRow != null) confirmRow.style.display = DisplayStyle.None;
        }

        private void OnDebugClicked()
        {
            CloseBloom();
            var debugPanelElement = profileBloom?.panel?.visualTree?.Q("debug-panel");
            if (debugPanelElement != null)
                CampFireUI.Instance?.OpenOverlay("Debug", debugPanelElement);
        }

        private void OnLocaleChanged()
        {
            RefreshLanguageDropdown();
        }
    }
}
