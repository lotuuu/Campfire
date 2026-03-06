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
        private VisualElement questFloatBtn;

        private static readonly int[] MoonPhaseToSpriteIndex = { 5, 6, 7, 8, 1, 2, 3, 4 };
        private VisualElement humidityIconEl;
        private VisualElement tempIconEl;
        private bool iconsLoaded;

        public void Initialize(VisualElement root)
        {
            weatherIcon = root.Q("weather-icon");
            weatherConditionLabel = root.Q<Label>("weather-condition-label");
            weatherHumidity = root.Q<Label>("weather-humidity");
            weatherTemp = root.Q<Label>("weather-temp");
            weatherMoon = root.Q("weather-moon");
            playerName = root.Q<Label>("player-name");
            dateTime = root.Q<Label>("date-time");

            humidityIconEl = root.Q("weather-humidity-icon");
            tempIconEl = root.Q("weather-temp-icon");


            weatherBar = root.Q("weather-bar");
            forecastPanel = root.Q("forecast-panel");
            forecastDays = root.Q("forecast-days");
            campRoot = root.Q("camp-root");
            questFloatBtn = root.Q("quest-float-btn");

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

        private static string TruncateName(string name, int max = 10)
        {
            if (string.IsNullOrEmpty(name)) return "Camper";
            return name.Length > max ? name[..max] : name;
        }

        private void UpdatePlayerName()
        {
            if (playerName == null) return;
            var name = SocialSaveManager.Instance?.Data?.displayName;
            playerName.text = TruncateName(name) + "'s Campfire";
        }

        private void OnDisplayNameUpdated(string newName)
        {
            if (playerName != null)
                playerName.text = TruncateName(newName) + "'s Campfire";
        }

        public void SetVisitingName(string friendName)
        {
            if (playerName == null) return;
            if (friendName != null)
                playerName.text = TruncateName(friendName) + "'s Campfire";
            else
                UpdatePlayerName();
        }

        private void OnWeatherBarClicked(ClickEvent evt)
        {
            if (forecastPanel == null) return;
            forecastPanel.ToggleInClassList("forecast-visible");
            bool visible = forecastPanel.ClassListContains("forecast-visible");
            if (questFloatBtn != null)
                questFloatBtn.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
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
            if (questFloatBtn != null)
                questFloatBtn.style.display = DisplayStyle.Flex;
        }

        private void PopulateForecast()
        {
            if (forecastDays == null) return;
            forecastDays.Clear();

            // Today card
            var weather = WeatherService.Instance?.CurrentWeather ?? default;
            AddTodayCard(weather);

            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            // Reuse today's sunrise/sunset for future days (changes ~1 min/day)
            float sunriseHour = weather.sunriseHour;
            float sunsetHour = weather.sunsetHour;

            foreach (var day in forecast)
            {
                var card = new VisualElement();
                card.AddToClassList("forecast-day");

                // Header: day name + icon + condition + moon icon (right-aligned)
                var header = new VisualElement();
                header.AddToClassList("forecast-day-header");

                var dayLabel = new Label(day.dayLabel.ToUpper());
                dayLabel.AddToClassList("forecast-day-name");
                header.Add(dayLabel);

                var icon = new VisualElement();
                icon.AddToClassList("forecast-day-icon");
                var dayWeatherTex = GetWeatherIcon(day.condition);
                if (dayWeatherTex != null)
                    icon.style.backgroundImage = dayWeatherTex;
                header.Add(icon);

                var condLabel = new Label(day.condition.ToString());
                condLabel.AddToClassList("forecast-day-condition");
                header.Add(condLabel);

                var moonTex = GetMoonTexture((int)day.moonPhase);
                if (moonTex != null)
                {
                    var moonIcon = new VisualElement();
                    moonIcon.AddToClassList("forecast-day-moon");
                    moonIcon.style.backgroundImage = moonTex;
                    header.Add(moonIcon);
                }

                card.Add(header);

                // Row 1: sunrise, sunset, temp
                var statsRow1 = new VisualElement();
                statsRow1.AddToClassList("forecast-stats-row");
                AddStatCell(statsRow1, FormatHour(sunriseHour), "Sunrise");
                AddStatCell(statsRow1, FormatHour(sunsetHour), "Sunset");
                AddStatCell(statsRow1, $"{day.tempHigh:F0}\u00b0/{day.tempLow:F0}\u00b0", "Temp",
                    TempClass(day.tempHigh));
                card.Add(statsRow1);

                // Row 2: humidity, wind, cloud
                var statsRow2 = new VisualElement();
                statsRow2.AddToClassList("forecast-stats-row");
                AddStatCell(statsRow2, $"{day.humidity:F0}%", "Humidity",
                    HumidityClass(day.humidity));
                AddStatCell(statsRow2, $"{day.windSpeed:F1} m/s", "Wind",
                    WindClass(day.windSpeed));
                AddStatCell(statsRow2, $"{day.cloudCover:F0}%", "Cloud");
                card.Add(statsRow2);

                forecastDays.Add(card);
            }
        }

        private void AddTodayCard(WeatherData weather)
        {
            var card = new VisualElement();
            card.AddToClassList("forecast-day");
            card.AddToClassList("forecast-today");

            // Header
            var header = new VisualElement();
            header.AddToClassList("forecast-day-header");

            var dayLabel = new Label("TODAY");
            dayLabel.AddToClassList("forecast-day-name");
            header.Add(dayLabel);

            var icon = new VisualElement();
            icon.AddToClassList("forecast-day-icon");
            var todayWeatherTex = GetWeatherIcon(weather.condition);
            if (todayWeatherTex != null)
                icon.style.backgroundImage = todayWeatherTex;
            header.Add(icon);

            var condLabel = new Label(weather.condition.ToString());
            condLabel.AddToClassList("forecast-day-condition");
            header.Add(condLabel);

            var moonTex = GetMoonTexture((int)weather.moonPhase);
            if (moonTex != null)
            {
                var moonIcon = new VisualElement();
                moonIcon.AddToClassList("forecast-day-moon");
                moonIcon.style.backgroundImage = moonTex;
                header.Add(moonIcon);
            }

            card.Add(header);

            // Row 1: sunrise, sunset, temp
            var row1 = new VisualElement();
            row1.AddToClassList("forecast-stats-row");
            AddStatCell(row1, FormatHour(weather.sunriseHour), "Sunrise");
            AddStatCell(row1, FormatHour(weather.sunsetHour), "Sunset");
            AddStatCell(row1, $"{weather.temperature:F0}\u00b0", "Temp",
                TempClass(weather.temperature));
            card.Add(row1);

            // Row 2: humidity, wind, cloud
            var row2 = new VisualElement();
            row2.AddToClassList("forecast-stats-row");
            AddStatCell(row2, $"{weather.humidity:F0}%", "Humidity",
                HumidityClass(weather.humidity));
            AddStatCell(row2, $"{weather.windSpeed:F1} m/s", "Wind",
                WindClass(weather.windSpeed));
            AddStatCell(row2, $"{weather.cloudCover:F0}%", "Cloud");
            card.Add(row2);

            forecastDays.Add(card);
        }

        private static string FormatHour(float hour)
        {
            int h = Mathf.Clamp((int)hour, 0, 23);
            int m = Mathf.Clamp((int)((hour - h) * 60f), 0, 59);
            bool pm = h >= 12;
            int display = h % 12;
            if (display == 0) display = 12;
            return $"{display}:{m:D2} {(pm ? "PM" : "AM")}";
        }

        private static string TempClass(float temp)
        {
            if (temp >= 35f) return "stat-hot";
            if (temp <= 5f) return "stat-cold";
            return null;
        }

        private static string HumidityClass(float humidity)
        {
            if (humidity <= 20f) return "stat-dry";
            if (humidity >= 85f) return "stat-humid";
            return null;
        }

        private static string WindClass(float windSpeed)
        {
            if (windSpeed >= 8f) return "stat-windy";
            return null;
        }

        private static void AddStatCell(VisualElement parent, string value, string label,
            string outlierClass = null)
        {
            var cell = new VisualElement();
            cell.AddToClassList("forecast-stat-cell");

            var val = new Label(value);
            val.AddToClassList("forecast-stat-value");
            if (outlierClass != null) val.AddToClassList(outlierClass);
            cell.Add(val);

            var lbl = new Label(label);
            lbl.AddToClassList("forecast-stat-label");
            cell.Add(lbl);

            parent.Add(cell);
        }

        private void UpdateWeather(WeatherData weather)
        {
            EnsureStaticIcons();

            var tex = GetWeatherIcon(weather.condition);
            if (weatherIcon != null && tex != null)
                weatherIcon.style.backgroundImage = tex;
            if (weatherConditionLabel != null) weatherConditionLabel.text = weather.condition.ToString().ToUpper();
            if (weatherHumidity != null) weatherHumidity.text = $"{weather.humidity:F0}";
            if (weatherTemp != null) weatherTemp.text = $"{weather.temperature:F0}\u00b0";
            if (weatherMoon != null)
            {
                var moonTex = GetMoonTexture((int)weather.moonPhase);
                if (moonTex != null)
                    weatherMoon.style.backgroundImage = moonTex;
            }
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

        private void EnsureStaticIcons()
        {
            if (iconsLoaded) return;
            if (SpriteService.Instance == null) return;

            var tex = SpriteService.Instance.GetTexture("ui/weather-humidity");
            if (tex != null && humidityIconEl != null)
                humidityIconEl.style.backgroundImage = tex;

            tex = SpriteService.Instance.GetTexture("ui/weather-temp");
            if (tex != null && tempIconEl != null)
                tempIconEl.style.backgroundImage = tex;

            iconsLoaded = true;
        }
    }
}
