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

            // Today card
            AddTodayCard(forecastStats, weather);

            // Forecast section label
            if (forecastSectionLabel != null)
                forecastSectionLabel.text = Loc.Get("ui.weather.forecast", "Forecast");

            // Future days
            var forecast = WeatherService.Instance?.Forecast;
            if (forecast == null) return;

            foreach (var day in forecast)
                AddDayCard(forecastDays, day);
        }

        private void AddTodayCard(VisualElement parent, WeatherData weather)
        {
            // Row 1: sunrise, sunset, temp
            var row1 = new VisualElement();
            row1.AddToClassList("forecast-stats-row");
            AddStatCell(row1, FormatHour(weather.sunriseHour), Loc.Get("ui.weather.sunrise", "Sunrise"));
            AddStatCell(row1, FormatHour(weather.sunsetHour), Loc.Get("ui.weather.sunset", "Sunset"));
            AddStatCell(row1, $"{weather.temperature:F0}\u00b0", Loc.Get("ui.weather.temp", "Temp"),
                TempClass(weather.temperature));
            parent.Add(row1);

            // Row 2: humidity, wind, cloud
            var row2 = new VisualElement();
            row2.AddToClassList("forecast-stats-row");
            AddStatCell(row2, $"{weather.humidity:F0}%", Loc.Get("ui.weather.humidity", "Humidity"),
                HumidityClass(weather.humidity));
            AddStatCell(row2, $"{weather.windSpeed:F1} m/s", Loc.Get("ui.weather.wind", "Wind"),
                WindClass(weather.windSpeed));
            AddStatCell(row2, $"{weather.cloudCover:F0}%", Loc.Get("ui.weather.cloud", "Cloud"));
            parent.Add(row2);

            // Row 3: moon phase
            var row3 = new VisualElement();
            row3.AddToClassList("forecast-stats-row");
            var moonTex = GetMoonTexture((int)weather.moonPhase);
            AddStatCellWithIcon(row3, moonTex, FormatMoonPhase(weather.moonPhase),
                Loc.Get("ui.weather.moon", "Moon"));
            parent.Add(row3);
        }

        private void AddDayCard(VisualElement parent, DailyForecast day)
        {
            var card = new VisualElement();
            card.AddToClassList("forecast-day");

            // Header: day name + icon + condition + moon
            var header = new VisualElement();
            header.AddToClassList("forecast-day-header");

            var dayLabelText = Loc.Get($"ui.weather.day_{day.dayLabel.ToLower()}", day.dayLabel);
            var dayLabel = new Label(dayLabelText.ToUpper());
            dayLabel.AddToClassList("forecast-day-name");
            header.Add(dayLabel);

            var icon = new VisualElement();
            icon.AddToClassList("forecast-day-icon");
            var dayTex = GetWeatherIcon(day.condition);
            if (dayTex != null)
                icon.style.backgroundImage = dayTex;
            header.Add(icon);

            var condKey = $"ui.weather.{day.condition.ToString().ToLower()}";
            var condLabel = new Label(Loc.Get(condKey, day.condition.ToString()));
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

            // Stats row 1: temp, humidity, wind
            var statsRow1 = new VisualElement();
            statsRow1.AddToClassList("forecast-stats-row");
            AddStatCell(statsRow1, $"{day.tempHigh:F0}\u00b0/{day.tempLow:F0}\u00b0",
                Loc.Get("ui.weather.temp", "Temp"), TempClass(day.tempHigh));
            AddStatCell(statsRow1, $"{day.humidity:F0}%",
                Loc.Get("ui.weather.humidity", "Humidity"), HumidityClass(day.humidity));
            AddStatCell(statsRow1, $"{day.windSpeed:F1} m/s",
                Loc.Get("ui.weather.wind", "Wind"), WindClass(day.windSpeed));
            card.Add(statsRow1);

            // Stats row 2: cloud, moon phase, plant hint
            var statsRow2 = new VisualElement();
            statsRow2.AddToClassList("forecast-stats-row");
            AddStatCell(statsRow2, $"{day.cloudCover:F0}%",
                Loc.Get("ui.weather.cloud", "Cloud"));
            AddStatCell(statsRow2, FormatMoonPhase(day.moonPhase),
                Loc.Get("ui.weather.moon_phase", "Moon Phase"));
            AddStatCell(statsRow2, ConditionHint(day.condition),
                Loc.Get("ui.weather.for_plants", "For Plants"));
            card.Add(statsRow2);

            parent.Add(card);
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

        private static void AddStatCellWithIcon(VisualElement parent, Texture2D iconTex,
            string value, string label)
        {
            var cell = new VisualElement();
            cell.AddToClassList("forecast-stat-cell");

            if (iconTex != null)
            {
                var icon = new VisualElement();
                icon.AddToClassList("forecast-stat-icon");
                icon.style.backgroundImage = iconTex;
                cell.Add(icon);
            }

            var val = new Label(value);
            val.AddToClassList("forecast-stat-value");
            cell.Add(val);

            var lbl = new Label(label);
            lbl.AddToClassList("forecast-stat-label");
            cell.Add(lbl);

            parent.Add(cell);
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

        private static string FormatMoonPhase(MoonPhase phase)
        {
            return phase switch
            {
                MoonPhase.NewMoon => "New Moon",
                MoonPhase.WaxingCrescent => "Wax. Crescent",
                MoonPhase.FirstQuarter => "First Quarter",
                MoonPhase.WaxingGibbous => "Wax. Gibbous",
                MoonPhase.FullMoon => "Full Moon",
                MoonPhase.WaningGibbous => "Wan. Gibbous",
                MoonPhase.LastQuarter => "Last Quarter",
                MoonPhase.WaningCrescent => "Wan. Crescent",
                _ => phase.ToString()
            };
        }

        private static string ConditionHint(WeatherCondition cond)
        {
            return cond switch
            {
                WeatherCondition.Rain => "Rain bonus",
                WeatherCondition.Storm => "Storm bonus",
                WeatherCondition.Clear => "Sun bonus",
                WeatherCondition.Snow => "Cold snap",
                _ => "Neutral"
            };
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
