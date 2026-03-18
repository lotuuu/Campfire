using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class DebugWeatherPanel : MonoBehaviour
    {
        private VisualElement panel;
        private Slider tempSlider;
        private Slider humiditySlider;
        private Slider windSlider;
        private DropdownField conditionDropdown;
        private DropdownField moonPhaseDropdown;
        private DropdownField timeOfDayDropdown;
        private DropdownField calendarEventDropdown;
        private Label tempValue;
        private Label humidityValue;
        private Label windValue;
        private IntegerField timeSkipField;
        private Label currentTimeLabel;

        private Coroutine _timeAccelCoroutine;
        private float _activeTimeScale = 1f;
        private static readonly float TickIntervalSeconds = 2f;

        public void Initialize(VisualElement root)
        {
            panel = root.Q<VisualElement>("debug-panel");

            // Server selector
            var serverRow = root.Q("debug-server-selector");
            if (serverRow != null)
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
                    serverRow.Add(btn);
                }

                var reloadBtn = new Button { text = "Reload" };
                reloadBtn.AddToClassList("server-btn");
                reloadBtn.clicked += () => UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                serverRow.Add(reloadBtn);
            }

            tempSlider = root.Q<Slider>("temp-slider");
            humiditySlider = root.Q<Slider>("humidity-slider");
            windSlider = root.Q<Slider>("wind-slider");
            conditionDropdown = root.Q<DropdownField>("condition-dropdown");
            moonPhaseDropdown = root.Q<DropdownField>("moon-phase-dropdown");
            timeOfDayDropdown = root.Q<DropdownField>("time-of-day-dropdown");
            calendarEventDropdown = root.Q<DropdownField>("calendar-event-dropdown");
            tempValue = root.Q<Label>("temp-value");
            humidityValue = root.Q<Label>("humidity-value");
            windValue = root.Q<Label>("wind-value");

            timeSkipField = root.Q<IntegerField>("time-skip-field");
            currentTimeLabel = root.Q<Label>("current-time-label");

            // Slider callbacks
            tempSlider?.RegisterValueChangedCallback(evt => tempValue.text = $"{evt.newValue:F0}\u00b0C");
            humiditySlider?.RegisterValueChangedCallback(evt => humidityValue.text = $"{evt.newValue:F0}%");
            windSlider?.RegisterValueChangedCallback(evt => windValue.text = $"{evt.newValue:F1} m/s");

            // Default dropdown indices
            if (conditionDropdown != null) conditionDropdown.index = 0;
            if (moonPhaseDropdown != null) moonPhaseDropdown.index = 0;
            if (timeOfDayDropdown != null) timeOfDayDropdown.index = 0;
            if (calendarEventDropdown != null) calendarEventDropdown.index = 0;

            // Wire weather buttons
            root.Q<Button>("apply-button")?.RegisterCallback<ClickEvent>(_ => ApplySettings());

            root.Q<Button>("blizzard-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(-5, 60, 15, 4, 0, 4));
            root.Q<Button>("thunderstorm-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(20, 90, 25, 3, 0, 3));
            root.Q<Button>("clear-night-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(15, 40, 2, 0, 1, 4));
            root.Q<Button>("golden-hour-button")?.RegisterCallback<ClickEvent>(_ =>
                ApplyPreset(22, 50, 5, 0, 2, 1));

            // Wire time skip
            root.Q<Button>("time-skip-button")?.RegisterCallback<ClickEvent>(_ => SkipTime());

            // Wire time scale buttons
            root.Q<Button>("time-scale-1")?.RegisterCallback<ClickEvent>(_ => SetTimeScale(1f));
            root.Q<Button>("time-scale-2")?.RegisterCallback<ClickEvent>(_ => SetTimeScale(2f));
            root.Q<Button>("time-scale-5")?.RegisterCallback<ClickEvent>(_ => SetTimeScale(5f));
            root.Q<Button>("time-scale-100")?.RegisterCallback<ClickEvent>(_ => SetTimeScale(100f));
            root.Q<Button>("time-scale-2000")?.RegisterCallback<ClickEvent>(_ => SetTimeScale(2000f));
            root.Q<Button>("time-reset")?.RegisterCallback<ClickEvent>(_ => ResetTime());

            // Wire economy buttons
            root.Q<Button>("set-mana-button")?.RegisterCallback<ClickEvent>(evt =>
            {
                var field = root.Q<FloatField>("debug-mana-field");
                if (field != null) _ = DebugService.Instance?.SetCurrency(mana: field.value);
            });
            root.Q<Button>("set-gems-button")?.RegisterCallback<ClickEvent>(evt =>
            {
                var field = root.Q<IntegerField>("debug-gems-field");
                if (field != null) _ = DebugService.Instance?.SetCurrency(gems: field.value);
            });
            root.Q<Button>("set-flame-button")?.RegisterCallback<ClickEvent>(evt =>
            {
                var field = root.Q<IntegerField>("debug-flame-field");
                if (field != null) _ = DebugService.Instance?.SetFlameLevel(field.value);
            });

            // Wire inventory buttons
            root.Q<Button>("grant-seeds-button")?.RegisterCallback<ClickEvent>(evt =>
            {
                var nameField = root.Q<TextField>("debug-seed-name");
                var countField = root.Q<IntegerField>("debug-seed-count");
                if (nameField != null && countField != null)
                    _ = DebugService.Instance?.GrantSeeds(nameField.value, countField.value);
            });
            root.Q<Button>("grant-items-button")?.RegisterCallback<ClickEvent>(evt =>
            {
                var nameField = root.Q<TextField>("debug-item-name");
                var countField = root.Q<IntegerField>("debug-item-count");
                if (nameField != null && countField != null)
                    _ = DebugService.Instance?.GrantItems(nameField.value, countField.value);
            });

            // Wire quick action buttons
            root.Q<Button>("spawn-bird-button")?.RegisterCallback<ClickEvent>(evt =>
            { _ = DebugService.Instance?.SpawnBird(); });
            root.Q<Button>("complete-quests-button")?.RegisterCallback<ClickEvent>(evt =>
            { _ = DebugService.Instance?.CompleteQuests(); });
            root.Q<Button>("fill-vases-button")?.RegisterCallback<ClickEvent>(evt =>
            { _ = DebugService.Instance?.FillVases(); });
            root.Q<Button>("mature-plots-button")?.RegisterCallback<ClickEvent>(evt =>
            { _ = DebugService.Instance?.MaturePlots(); });
            root.Q<Button>("receive-visitor-button")?.RegisterCallback<ClickEvent>(evt =>
            { _ = DebugService.Instance?.ReceiveVisitor(); });

            // Wire free mode toggle
            var freeModeToggle = root.Q<Toggle>("free-mode-toggle");
            if (freeModeToggle != null)
            {
                freeModeToggle.value = CurrencyManager.FreeMode;
                freeModeToggle.RegisterValueChangedCallback(evt =>
                {
                    CurrencyManager.FreeMode = evt.newValue;
                    Debug.Log($"[Debug] Free Mode {(evt.newValue ? "ON" : "OFF")}");
                });
            }

            // Wire clear save
            root.Q<Button>("clear-save-button")?.RegisterCallback<ClickEvent>(_ => ClearSaveData());
        }

        private void Update()
        {
            if (currentTimeLabel != null && panel != null && panel.resolvedStyle.display == DisplayStyle.Flex)
            {
                var scaleText = GameTime.TimeScale > 1f ? $" (x{GameTime.TimeScale:G0})" : "";
                currentTimeLabel.text = $"{GameTime.Now:yyyy-MM-dd HH:mm:ss}{scaleText}";
            }
        }

        private void ApplyPreset(float temp, float humidity, float wind, int condIdx, int todIdx, int moonIdx)
        {
            if (tempSlider != null) tempSlider.value = temp;
            if (humiditySlider != null) humiditySlider.value = humidity;
            if (windSlider != null) windSlider.value = wind;
            if (conditionDropdown != null) conditionDropdown.index = condIdx;
            if (timeOfDayDropdown != null) timeOfDayDropdown.index = todIdx;
            if (moonPhaseDropdown != null) moonPhaseDropdown.index = moonIdx;
            ApplySettings();
        }

        private void ApplySettings()
        {
            var condition = (WeatherCondition)conditionDropdown.index;
            var timeOfDay = (TimeOfDay)timeOfDayDropdown.index;
            var moonPhase = (MoonPhase)moonPhaseDropdown.index;
            var calendarEvent = (CalendarEvent)calendarEventDropdown.index;

            var weather = new WeatherData
            {
                temperature = tempSlider.value,
                humidity = humiditySlider.value,
                windSpeed = windSlider.value,
                condition = condition,
                timeOfDay = timeOfDay,
                isNight = timeOfDay == TimeOfDay.Night,
                isGoldenHour = timeOfDay == TimeOfDay.GoldenHour,
                moonPhase = moonPhase,
                calendarEvent = calendarEvent,
                cloudCover = conditionDropdown.index >= 1 ? 70f : 10f
            };

            WeatherService.Instance.SetDebugWeather(weather);
        }

        private async void SkipTime()
        {
            int hours = Mathf.Max(1, timeSkipField != null ? timeSkipField.value : 1);
            await DebugService.Instance?.SkipTime(hours);
        }

        private void SetTimeScale(float scale)
        {
            // Stop existing acceleration
            if (_timeAccelCoroutine != null)
            {
                StopCoroutine(_timeAccelCoroutine);
                _timeAccelCoroutine = null;
            }

            _activeTimeScale = scale;
            GameTime.TimeScale = scale;

            if (scale > 1f)
            {
                _timeAccelCoroutine = StartCoroutine(TimeAccelerationLoop(scale));
            }
            else
            {
                // Returning to x1 — keep accelerated game time, just sync server state
                GameService.Instance?.Initialize();
            }

            Debug.Log($"[Debug] Time scale set to x{scale:G0}");
        }

        private void ResetTime()
        {
            SetTimeScale(1f);
            GameTime.ResetTimeScale();
            GameService.Instance?.Initialize();
            Debug.Log("[Debug] Time reset to real time");
        }

        private IEnumerator TimeAccelerationLoop(float scale)
        {
            while (true)
            {
                yield return new WaitForSeconds(TickIntervalSeconds);

                // Skip (scale - 1) * interval worth of server time each tick
                float extraHours = (scale - 1f) * TickIntervalSeconds / 3600f;
                if (DebugService.Instance != null)
                    _ = DebugService.Instance.SkipTimeQuiet(extraHours);
            }
        }

        private void OnDisable()
        {
            if (_timeAccelCoroutine != null)
            {
                StopCoroutine(_timeAccelCoroutine);
                _timeAccelCoroutine = null;
            }
            if (_activeTimeScale > 1f)
            {
                GameTime.ResetTimeScale();
                _activeTimeScale = 1f;
            }
        }

        private async void ClearSaveData()
        {
            Debug.Log("[Debug] Clearing save data...");
            if (DebugService.Instance != null)
            {
                await DebugService.Instance.ClearSave();
            }
            // Always delete local save and reload scene to trigger InitializeNewPlayer
            SaveManager.Instance.DeleteSave();
            SocialSaveManager.Instance?.DeleteSave();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
