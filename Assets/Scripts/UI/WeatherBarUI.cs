using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class WeatherBarUI : MonoBehaviour
    {
        private VisualElement hudWeatherBar;
        private Label hudWeatherTemp;
        private Label hudWeatherHumidity;
        private Label hudWeatherStatus;
        private VisualElement hudMenuIcon;
        private VisualElement forecastBloom;
        private VisualElement bloomDismiss;
        private Label forecastTitle;
        private VisualElement forecastStats;
        private VisualElement forecastDays;
        private Label forecastSectionLabel;

        // Visiting name label (child of hud-profile)
        private Label visitingNameLabel;
        private VisualElement hudProfile;

        private bool isOpen;
        private bool menuIconLoaded;

        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };

        public bool IsOpen => isOpen;

        public void Initialize(VisualElement root)
        {
            hudWeatherBar = root.Q("hud-weather-bar");
            hudWeatherTemp = root.Q<Label>("hud-weather-temp");
            hudWeatherHumidity = root.Q<Label>("hud-weather-humidity");
            hudWeatherStatus = root.Q<Label>("hud-weather-status");
            hudMenuIcon = root.Q("hud-menu-icon");
            forecastBloom = root.Q("forecast-bloom");
            if (forecastBloom != null) forecastBloom.style.display = DisplayStyle.None;
            bloomDismiss = root.Q("bloom-dismiss");
            forecastTitle = root.Q<Label>("forecast-bloom-title");
            forecastStats = root.Q("forecast-bloom-stats");
            forecastDays = root.Q("forecast-bloom-days");
            forecastSectionLabel = root.Q<Label>("forecast-section-label");
            hudProfile = root.Q("hud-profile");

            hudWeatherBar?.RegisterCallback<ClickEvent>(OnWeatherClicked);

            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated += UpdateWeatherDisplay;
                WeatherService.Instance.OnForecastUpdated += PopulateForecast;
                UpdateWeatherDisplay(WeatherService.Instance.CurrentWeather);
                if (WeatherService.Instance.Forecast.Count > 0)
                    PopulateForecast();
            }
        }

        private void OnDestroy()
        {
            if (WeatherService.Instance != null)
            {
                WeatherService.Instance.OnWeatherUpdated -= UpdateWeatherDisplay;
                WeatherService.Instance.OnForecastUpdated -= PopulateForecast;
            }
        }

        private void Update()
        {
            if (!menuIconLoaded && SpriteService.Instance != null)
            {
                var tex = SpriteService.Instance.GetTexture("ui/delapouite-hamburger-menu");
                if (tex != null && hudMenuIcon != null)
                {
                    hudMenuIcon.style.backgroundImage = tex;
                    menuIconLoaded = true;
                }
            }
        }

        private void OnWeatherClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            ToggleBloom();
        }

        public void ToggleBloom()
        {
            if (isOpen) CloseBloom();
            else OpenBloom();
        }

        private void OpenBloom()
        {
            if (forecastBloom == null) return;
            isOpen = true;
            PopulateForecast();
            forecastBloom.style.display = DisplayStyle.Flex;
            forecastBloom.schedule.Execute(() => forecastBloom.AddToClassList("bloom-open"));
            bloomDismiss?.AddToClassList("bloom-dismiss-active");
        }

        public void CloseBloom()
        {
            if (forecastBloom == null) return;
            isOpen = false;
            forecastBloom.RemoveFromClassList("bloom-open");
            forecastBloom.style.display = DisplayStyle.None;
            bloomDismiss?.RemoveFromClassList("bloom-dismiss-active");
        }

        private void UpdateWeatherDisplay(WeatherData weather)
        {
            if (hudWeatherTemp != null)
                hudWeatherTemp.text = $"{weather.temperature:F0}\u00b0C";

            if (hudWeatherHumidity != null)
                hudWeatherHumidity.text = $"{weather.humidity:F0}%";

            if (hudWeatherStatus != null)
            {
                var condKey = $"ui.weather.{weather.condition.ToString().ToLower()}";
                hudWeatherStatus.text = Loc.Get(condKey, weather.condition.ToString());
            }
        }

        private void PopulateForecast()
        {
            if (forecastStats == null || forecastDays == null) return;
            forecastStats.Clear();
            forecastDays.Clear();

            var weather = WeatherService.Instance?.CurrentWeather ?? default;

            // Title
            if (forecastTitle != null)
            {
                var condKey = $"ui.weather.{weather.condition.ToString().ToLower()}";
                forecastTitle.text = $"{Loc.Get("ui.weather.today", "Today")} \u2014 {Loc.Get(condKey, weather.condition.ToString())}";
            }

            // Today's stats
            AddStatRow(forecastStats, Loc.Get("ui.weather.temp", "Temp"), $"{weather.temperature:F0}\u00b0");
            AddStatRow(forecastStats, Loc.Get("ui.weather.humidity", "Humidity"), $"{weather.humidity:F0}%");
            AddStatRow(forecastStats, Loc.Get("ui.weather.wind", "Wind"), $"{weather.windSpeed:F1} m/s");
            AddStatRow(forecastStats, Loc.Get("ui.weather.sunrise", "Sunrise"), FormatHour(weather.sunriseHour));
            AddStatRow(forecastStats, Loc.Get("ui.weather.sunset", "Sunset"), FormatHour(weather.sunsetHour));

            var moonTex = GetMoonTexture((int)weather.moonPhase);
            if (moonTex != null)
                AddStatRow(forecastStats, Loc.Get("ui.weather.moon", "Moon"), "", moonTex);

            // Forecast section label
            if (forecastSectionLabel != null)
                forecastSectionLabel.text = Loc.Get("ui.weather.forecast", "Forecast");

            // Future days
            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            foreach (var day in forecast)
            {
                var row = new VisualElement();
                row.AddToClassList("forecast-day-row");

                var dayLabelText = Loc.Get($"ui.weather.day_{day.dayLabel.ToLower()}", day.dayLabel);
                var dayLabel = new Label(dayLabelText.ToUpper());
                dayLabel.AddToClassList("forecast-day-name");
                row.Add(dayLabel);

                var icon = new VisualElement();
                icon.AddToClassList("forecast-day-icon");
                var dayTex = GetWeatherIcon(day.condition);
                if (dayTex != null)
                    icon.style.backgroundImage = dayTex;
                row.Add(icon);

                var tempLabel = new Label($"{day.tempHigh:F0}\u00b0/{day.tempLow:F0}\u00b0");
                tempLabel.AddToClassList("forecast-day-temp");
                row.Add(tempLabel);

                forecastDays.Add(row);
            }
        }

        private static void AddStatRow(VisualElement parent, string label, string value, Texture2D iconTex = null)
        {
            var row = new VisualElement();
            row.AddToClassList("forecast-stat-row");

            var lbl = new Label(label);
            lbl.AddToClassList("forecast-stat-label");
            row.Add(lbl);

            if (iconTex != null)
            {
                var icon = new VisualElement();
                icon.style.width = 24;
                icon.style.height = 24;
                icon.style.backgroundImage = iconTex;
                icon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                icon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                row.Add(icon);
            }
            else
            {
                var val = new Label(value);
                val.AddToClassList("forecast-stat-value");
                row.Add(val);
            }

            parent.Add(row);
        }

        public void SetVisitingName(string friendName)
        {
            if (hudProfile == null) return;

            if (friendName != null)
            {
                if (visitingNameLabel == null)
                {
                    visitingNameLabel = new Label();
                    visitingNameLabel.name = "hud-visiting-name";
                    hudProfile.Add(visitingNameLabel);
                }
                visitingNameLabel.text = $"{friendName}'s Camp";
                visitingNameLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                if (visitingNameLabel != null)
                    visitingNameLabel.style.display = DisplayStyle.None;
            }
        }

        private static string FormatHour(float fractionalHour)
        {
            int h = (int)fractionalHour;
            int m = (int)((fractionalHour - h) * 60);
            string ampm = h >= 12 ? "PM" : "AM";
            int h12 = h % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12}:{m:D2} {ampm}";
        }

        private Texture2D GetMoonTexture(int phaseIndex)
        {
            int spriteIdx = MoonPhaseToSpriteIndex[phaseIndex] - 1;
            return SpriteService.Instance?.GetTexture($"moon/phase-{spriteIdx + 1}");
        }

        private static readonly string[] WeatherConditionKeys =
        {
            "ui/weather-clear", "ui/weather-cloudy", "ui/weather-rain",
            "ui/weather-storm", "ui/weather-snow"
        };

        private Texture2D GetWeatherIcon(WeatherCondition condition)
        {
            int idx = (int)condition;
            if (idx < 0 || idx >= WeatherConditionKeys.Length) return null;
            return SpriteService.Instance?.GetTexture(WeatherConditionKeys[idx]);
        }
    }
}
