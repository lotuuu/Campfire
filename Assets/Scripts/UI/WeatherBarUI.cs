using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private VisualElement weatherIcon;
        private Label weatherConditionLabel;
        private Label weatherHumidity;
        private Label weatherTemp;
        private VisualElement weatherMoon;
        private Label playerName;
        private Label dateTime;

        private VisualElement weatherBar;
        private VisualElement forecastPanel;
        private VisualElement forecastDays;
        private VisualElement campRoot;

        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };
        private Texture2D[] moonTextures;
        private Dictionary<WeatherCondition, Texture2D> weatherIcons;

        public void Initialize(VisualElement root)
        {
            weatherIcon = root.Q("weather-icon");
            weatherConditionLabel = root.Q<Label>("weather-condition-label");
            weatherHumidity = root.Q<Label>("weather-humidity");
            weatherTemp = root.Q<Label>("weather-temp");
            weatherMoon = root.Q("weather-moon");
            playerName = root.Q<Label>("player-name");
            dateTime = root.Q<Label>("date-time");

            // Load moon textures
            moonTextures = new Texture2D[8];
            for (int i = 0; i < 8; i++)
                moonTextures[i] = Resources.Load<Texture2D>($"MoonPhases/Moon_Phase_{i + 1}");

            // Load weather condition icons
            weatherIcons = new Dictionary<WeatherCondition, Texture2D>
            {
                { WeatherCondition.Clear, Resources.Load<Texture2D>("UI/Icons/weather-clear") },
                { WeatherCondition.Cloudy, Resources.Load<Texture2D>("UI/Icons/weather-cloudy") },
                { WeatherCondition.Rain, Resources.Load<Texture2D>("UI/Icons/weather-rain") },
                { WeatherCondition.Storm, Resources.Load<Texture2D>("UI/Icons/weather-storm") },
                { WeatherCondition.Snow, Resources.Load<Texture2D>("UI/Icons/weather-snow") },
            };

            // Load static icons
            var humidityIcon = root.Q("weather-humidity-icon");
            SetIcon(humidityIcon, "UI/Icons/weather-humidity");

            var tempIcon = root.Q("weather-temp-icon");
            SetIcon(tempIcon, "UI/Icons/weather-temp");

            var debugIcon = root.Q("btn-debug-icon");
            SetIcon(debugIcon, "UI/Icons/gear");

            weatherBar = root.Q("weather-bar");
            forecastPanel = root.Q("forecast-panel");
            forecastDays = root.Q("forecast-days");
            campRoot = root.Q("camp-root");

            weatherBar?.RegisterCallback<ClickEvent>(OnWeatherBarClicked);
            campRoot?.RegisterCallback<ClickEvent>(OnRootClicked);

            UpdatePlayerName();
            if (SocialService.Instance != null)
                SocialService.Instance.OnDisplayNameUpdated += OnDisplayNameUpdated;

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeather;
                WeatherService.Instance.OnForecastUpdated += PopulateForecast;
                UpdateWeather(WeatherService.Instance.CurrentWeather);
                if (WeatherService.Instance.Forecast.Count > 0)
                    PopulateForecast();
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeather;
                WeatherService.Instance.OnForecastUpdated -= PopulateForecast;
            }
            if (SocialService.Instance != null)
                SocialService.Instance.OnDisplayNameUpdated -= OnDisplayNameUpdated;
        }

        private void Update()
        {
            if (dateTime != null)
            {
                var now = GameTime.Now;
                dateTime.text = now.ToString("dd MMM  h:mm tt").ToUpper();
            }
        }

        private void UpdatePlayerName()
        {
            if (playerName == null) return;
            var name = SocialSaveManager.Instance?.Data?.displayName;
            playerName.text = string.IsNullOrEmpty(name) ? "Camper" : name;
        }

        private void OnDisplayNameUpdated(string newName)
        {
            if (playerName != null)
                playerName.text = string.IsNullOrEmpty(newName) ? "Camper" : newName;
        }

        private void OnWeatherBarClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            forecastPanel.ToggleInClassList("forecast-visible");
            evt.StopPropagation();
        }

        private void OnRootClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            if (!forecastPanel.ClassListContains("forecast-visible")) return;

            var target = evt.target as VisualElement;
            while (target != null)
            {
                if (target == forecastPanel || target == weatherBar) return;
                target = target.parent;
            }

            forecastPanel.RemoveFromClassList("forecast-visible");
        }

        private void PopulateForecast()
        {
            if (forecastDays == null) return;
            forecastDays.Clear();

            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            foreach (var day in forecast)
            {
                var col = new VisualElement();
                col.AddToClassList("forecast-day");

                var label = new Label(day.dayLabel);
                label.AddToClassList("forecast-day-label");
                col.Add(label);

                var icon = new VisualElement();
                icon.AddToClassList("forecast-day-icon");
                if (weatherIcons.TryGetValue(day.condition, out var tex) && tex != null)
                    icon.style.backgroundImage = tex;
                col.Add(icon);

                var temp = new Label($"{day.tempHigh:F0}/{day.tempLow:F0}");
                temp.AddToClassList("forecast-day-temp");
                col.Add(temp);

                forecastDays.Add(col);
            }
        }

        private void UpdateWeather(WeatherData weather)
        {
            if (weatherIcon != null && weatherIcons.TryGetValue(weather.condition, out var tex) && tex != null)
                weatherIcon.style.backgroundImage = tex;
            if (weatherConditionLabel != null) weatherConditionLabel.text = weather.condition.ToString().ToUpper();
            if (weatherHumidity != null) weatherHumidity.text = $"{weather.humidity:F0}";
            if (weatherTemp != null) weatherTemp.text = $"{weather.temperature:F0}\u00b0";
            if (weatherMoon != null)
            {
                int spriteIdx = MoonPhaseToSpriteIndex[(int)weather.moonPhase] - 1;
                var moonTex = moonTextures[spriteIdx];
                if (moonTex != null)
                    weatherMoon.style.backgroundImage = moonTex;
            }
        }

        private static void SetIcon(VisualElement el, string resourcePath)
        {
            if (el == null) return;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
                el.style.backgroundImage = tex;
        }
    }
}
