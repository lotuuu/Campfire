# Garden MVP Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a full MVP of the Garden hyper-contextual plant simulation game with real weather API, variant genetics, persistent save data, greenhouse, and debug weather tools.

**Architecture:** ScriptableObject-driven data with singleton services communicating via C# events. uGUI for UI with glassmorphism aesthetic. OpenWeatherMap API for real weather with debug override panel.

**Tech Stack:** Unity 6 (6000.3.6f1), C#, 2D URP, uGUI, OpenWeatherMap API, JSON serialization

---

## Task 1: Project Structure & Enums

**Files:**
- Create: `Assets/Scripts/Data/GameEnums.cs`

**Step 1: Create directory structure**

```bash
mkdir -p Assets/Scripts/{Data,Services,Managers,UI,Debug,Utils}
mkdir -p Assets/Resources/{Seeds,Variants,Config}
mkdir -p Assets/Prefabs/{UI,Plants,Effects}
mkdir -p Assets/Materials/Plants
mkdir -p Assets/Tests/EditMode
```

**Step 2: Write GameEnums.cs**

```csharp
namespace Garden
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    public enum WeatherCondition { Clear, Cloudy, Rain, Storm, Snow }

    public enum MoonPhase
    {
        NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous,
        FullMoon, WaningGibbous, LastQuarter, WaningCrescent
    }

    public enum TimeOfDay { Day, Night, GoldenHour }

    public enum CalendarEvent { None, SpringEquinox, FallEquinox, LunarEclipse }

    public enum CurrencyType { Dewdrops, SunShards, AuraDust }

    public enum PlantState { Empty, Growing, Mature }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/GameEnums.cs
git commit -m "feat: add project structure and game enums"
```

---

## Task 2: Data ScriptableObjects

**Files:**
- Create: `Assets/Scripts/Data/TriggerCondition.cs`
- Create: `Assets/Scripts/Data/VariantData.cs`
- Create: `Assets/Scripts/Data/SeedData.cs`
- Create: `Assets/Scripts/Data/CurrencyConfig.cs`

**Step 1: Write TriggerCondition.cs**

```csharp
using UnityEngine;

namespace Garden
{
    [System.Serializable]
    public class TriggerCondition
    {
        [Header("Temperature")]
        public bool useTemperature;
        public float minTemp = -50f;
        public float maxTemp = 60f;

        [Header("Weather")]
        public bool useWeatherCondition;
        public WeatherCondition[] requiredConditions;

        [Header("Wind")]
        public bool useWindSpeed;
        public float minWindSpeed;

        [Header("Humidity")]
        public bool useHumidity;
        public float minHumidity;

        [Header("Time")]
        public bool useTimeOfDay;
        public TimeOfDay requiredTimeOfDay;

        [Header("Moon")]
        public bool useMoonPhase;
        public MoonPhase requiredMoonPhase;

        [Header("Calendar")]
        public bool useCalendarEvent;
        public CalendarEvent requiredCalendarEvent;

        public bool Evaluate(WeatherData weather)
        {
            if (useCalendarEvent && weather.calendarEvent != requiredCalendarEvent) return false;
            if (useTemperature && (weather.temperature < minTemp || weather.temperature > maxTemp)) return false;
            if (useWeatherCondition)
            {
                bool match = false;
                foreach (var c in requiredConditions)
                    if (c == weather.condition) { match = true; break; }
                if (!match) return false;
            }
            if (useWindSpeed && weather.windSpeed < minWindSpeed) return false;
            if (useHumidity && weather.humidity < minHumidity) return false;
            if (useTimeOfDay && weather.timeOfDay != requiredTimeOfDay) return false;
            if (useMoonPhase && weather.moonPhase != requiredMoonPhase) return false;
            return true;
        }
    }
}
```

**Step 2: Write VariantData.cs**

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewVariant", menuName = "Garden/Variant Data")]
    public class VariantData : ScriptableObject
    {
        public string variantName;
        [TextArea] public string description;
        [TextArea] public string discoveryHint;
        public Rarity rarity;
        [Range(1, 4)] public int priority = 4;
        public TriggerCondition trigger;

        [Header("Visuals")]
        public Color primaryColor = Color.green;
        public Color secondaryColor = Color.white;
        public Sprite variantSprite;
        public Material variantMaterial;
        public GameObject particleEffectPrefab;
    }
}
```

**Step 3: Write SeedData.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "Garden/Seed Data")]
    public class SeedData : ScriptableObject
    {
        public string seedName;
        public Sprite icon;
        [TextArea] public string description;
        [Range(0.01f, 72f)] public float baseGrowthHours = 24f;
        public List<VariantData> variants = new();
    }
}
```

**Step 4: Write CurrencyConfig.cs**

```csharp
using UnityEngine;

namespace Garden
{
    [CreateAssetMenu(fileName = "CurrencyConfig", menuName = "Garden/Currency Config")]
    public class CurrencyConfig : ScriptableObject
    {
        [Header("Dewdrops per Harvest (by rarity)")]
        public int commonDewdrops = 10;
        public int uncommonDewdrops = 25;
        public int rareDewdrops = 50;
        public int epicDewdrops = 100;
        public int legendaryDewdrops = 250;

        [Header("Aura Dust per Hour (by rarity)")]
        public float commonDustPerHour = 1f;
        public float uncommonDustPerHour = 3f;
        public float rareDustPerHour = 8f;
        public float epicDustPerHour = 20f;
        public float legendaryDustPerHour = 50f;

        [Header("Greenhouse")]
        public int defaultSlots = 6;
        public int slotCostSunShards = 50;

        public int GetDewdropsForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDewdrops,
            Rarity.Uncommon => uncommonDewdrops,
            Rarity.Rare => rareDewdrops,
            Rarity.Epic => epicDewdrops,
            Rarity.Legendary => legendaryDewdrops,
            _ => commonDewdrops
        };

        public float GetDustPerHourForRarity(Rarity r) => r switch
        {
            Rarity.Common => commonDustPerHour,
            Rarity.Uncommon => uncommonDustPerHour,
            Rarity.Rare => rareDustPerHour,
            Rarity.Epic => epicDustPerHour,
            Rarity.Legendary => legendaryDustPerHour,
            _ => commonDustPerHour
        };
    }
}
```

**Step 5: Commit**

```bash
git add Assets/Scripts/Data/
git commit -m "feat: add data ScriptableObjects - SeedData, VariantData, TriggerCondition, CurrencyConfig"
```

---

## Task 3: Utility Classes

**Files:**
- Create: `Assets/Scripts/Data/WeatherData.cs`
- Create: `Assets/Scripts/Utils/MoonPhaseCalculator.cs`
- Create: `Assets/Scripts/Utils/CalendarEvents.cs`
- Create: `Assets/Scripts/Utils/TimeUtils.cs`
- Create: `Assets/Tests/EditMode/TestMoonPhase.cs`
- Create: `Assets/Tests/EditMode/TestCalendarEvents.cs`

**Step 1: Write WeatherData.cs**

```csharp
namespace Garden
{
    [System.Serializable]
    public struct WeatherData
    {
        public float temperature;
        public float humidity;
        public float windSpeed;
        public WeatherCondition condition;
        public float cloudCover;
        public bool isNight;
        public bool isGoldenHour;
        public TimeOfDay timeOfDay;
        public MoonPhase moonPhase;
        public CalendarEvent calendarEvent;
    }
}
```

**Step 2: Write MoonPhaseCalculator.cs**

Uses Conway's algorithm to calculate moon phase from date.

```csharp
using System;

namespace Garden
{
    public static class MoonPhaseCalculator
    {
        public static MoonPhase Calculate(DateTime date)
        {
            int year = date.Year;
            int month = date.Month;
            int day = date.Day;

            if (month < 3) { year--; month += 12; }
            int a = year / 100;
            int b = a / 4;
            int c = 2 - a + b;
            int e = (int)(365.25 * (year + 4716));
            int f = (int)(30.6001 * (month + 1));
            double jd = c + day + e + f - 1524.5;
            double daysSinceNew = jd - 2451549.5;
            double cycles = daysSinceNew / 29.53058770576;
            double phase = (cycles - Math.Floor(cycles)) * 29.53;

            return phase switch
            {
                < 1.85 => MoonPhase.NewMoon,
                < 5.54 => MoonPhase.WaxingCrescent,
                < 9.23 => MoonPhase.FirstQuarter,
                < 12.91 => MoonPhase.WaxingGibbous,
                < 16.61 => MoonPhase.FullMoon,
                < 20.30 => MoonPhase.WaningGibbous,
                < 23.99 => MoonPhase.LastQuarter,
                < 27.68 => MoonPhase.WaningCrescent,
                _ => MoonPhase.NewMoon
            };
        }
    }
}
```

**Step 3: Write CalendarEvents.cs**

```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    public static class CalendarEvents
    {
        private static readonly Dictionary<(int month, int day), CalendarEvent> FixedEvents = new()
        {
            { (3, 20), CalendarEvent.SpringEquinox },
            { (9, 22), CalendarEvent.FallEquinox },
        };

        // Known lunar eclipse dates (update periodically)
        private static readonly HashSet<(int year, int month, int day)> LunarEclipses = new()
        {
            (2025, 3, 14), (2025, 9, 7),
            (2026, 3, 3), (2026, 8, 28),
            (2027, 2, 20), (2027, 7, 18), (2027, 8, 17),
            (2028, 1, 12), (2028, 7, 6), (2028, 12, 31),
        };

        public static CalendarEvent GetEvent(DateTime date)
        {
            if (LunarEclipses.Contains((date.Year, date.Month, date.Day)))
                return CalendarEvent.LunarEclipse;

            if (FixedEvents.TryGetValue((date.Month, date.Day), out var ev))
                return ev;

            // Allow +/- 1 day tolerance for equinoxes
            var yesterday = date.AddDays(-1);
            if (FixedEvents.TryGetValue((yesterday.Month, yesterday.Day), out var evY))
                return evY;
            var tomorrow = date.AddDays(1);
            if (FixedEvents.TryGetValue((tomorrow.Month, tomorrow.Day), out var evT))
                return evT;

            return CalendarEvent.None;
        }
    }
}
```

**Step 4: Write TimeUtils.cs**

```csharp
using System;

namespace Garden
{
    public static class TimeUtils
    {
        public static TimeOfDay GetTimeOfDay(DateTime time, float sunriseHour = 6f, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            float goldenStart = sunsetHour - 1f;

            if (hour >= goldenStart && hour <= sunsetHour)
                return TimeOfDay.GoldenHour;
            if (hour < sunriseHour || hour > sunsetHour)
                return TimeOfDay.Night;
            return TimeOfDay.Day;
        }

        public static bool IsNight(DateTime time, float sunriseHour = 6f, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            return hour < sunriseHour || hour > sunsetHour;
        }

        public static bool IsGoldenHour(DateTime time, float sunsetHour = 18f)
        {
            float hour = time.Hour + time.Minute / 60f;
            return hour >= sunsetHour - 1f && hour <= sunsetHour;
        }
    }
}
```

**Step 5: Write unit tests**

Create `Assets/Tests/EditMode/Garden.Tests.EditMode.asmdef`:
```json
{
    "name": "Garden.Tests.EditMode",
    "rootNamespace": "Garden.Tests",
    "references": ["Garden"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "noEngineReferences": false
}
```

Create `Assets/Scripts/Garden.asmdef`:
```json
{
    "name": "Garden",
    "rootNamespace": "Garden",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "noEngineReferences": false
}
```

Write `Assets/Tests/EditMode/TestMoonPhase.cs`:
```csharp
using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestMoonPhase
    {
        [Test]
        public void KnownNewMoon_ReturnsNewMoon()
        {
            // Jan 29, 2025 was a known new moon
            var result = MoonPhaseCalculator.Calculate(new DateTime(2025, 1, 29));
            Assert.AreEqual(MoonPhase.NewMoon, result);
        }

        [Test]
        public void KnownFullMoon_ReturnsFullMoon()
        {
            // Feb 12, 2025 was a known full moon
            var result = MoonPhaseCalculator.Calculate(new DateTime(2025, 2, 12));
            Assert.AreEqual(MoonPhase.FullMoon, result);
        }
    }
}
```

Write `Assets/Tests/EditMode/TestCalendarEvents.cs`:
```csharp
using NUnit.Framework;
using System;

namespace Garden.Tests
{
    public class TestCalendarEvents
    {
        [Test]
        public void SpringEquinox_Detected()
        {
            Assert.AreEqual(CalendarEvent.SpringEquinox, CalendarEvents.GetEvent(new DateTime(2026, 3, 20)));
        }

        [Test]
        public void LunarEclipse_Detected()
        {
            Assert.AreEqual(CalendarEvent.LunarEclipse, CalendarEvents.GetEvent(new DateTime(2026, 3, 3)));
        }

        [Test]
        public void NormalDay_ReturnsNone()
        {
            Assert.AreEqual(CalendarEvent.None, CalendarEvents.GetEvent(new DateTime(2026, 6, 15)));
        }
    }
}
```

**Step 6: Commit**

```bash
git add Assets/Scripts/Data/WeatherData.cs Assets/Scripts/Utils/ Assets/Scripts/Garden.asmdef Assets/Tests/
git commit -m "feat: add weather data struct, moon phase calculator, calendar events, time utils with tests"
```

---

## Task 4: Weather Service

**Files:**
- Create: `Assets/Scripts/Services/WeatherService.cs`

**Step 1: Write WeatherService.cs**

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Garden
{
    public class WeatherService : MonoBehaviour
    {
        public static WeatherService Instance { get; private set; }

        [Header("API Configuration")]
        [SerializeField] private string apiKey = "";
        [SerializeField] private float pollIntervalMinutes = 15f;

        [Header("Debug Override")]
        [SerializeField] private bool useDebugOverride;
        [SerializeField] private WeatherData debugWeather;

        public WeatherData CurrentWeather { get; private set; }
        public event Action<WeatherData> OnWeatherUpdated;
        public bool IsDebugMode => useDebugOverride;

        private float lastPollTime;
        private bool hasLocation;
        private float latitude;
        private float longitude;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(InitializeLocation());
        }

        private IEnumerator InitializeLocation()
        {
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("Location services disabled. Using debug weather.");
                useDebugOverride = true;
                ApplyDebugWeather();
                yield break;
            }

            Input.location.Start(500f, 500f);
            int timeout = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
            {
                yield return new WaitForSeconds(1);
                timeout--;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                latitude = Input.location.lastData.latitude;
                longitude = Input.location.lastData.longitude;
                hasLocation = true;
                StartCoroutine(FetchWeatherLoop());
            }
            else
            {
                Debug.LogWarning("Location failed. Using debug weather.");
                useDebugOverride = true;
                ApplyDebugWeather();
            }
        }

        private IEnumerator FetchWeatherLoop()
        {
            while (true)
            {
                if (!useDebugOverride && hasLocation)
                    yield return FetchWeather();
                yield return new WaitForSeconds(pollIntervalMinutes * 60f);
            }
        }

        private IEnumerator FetchWeather()
        {
            string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={apiKey}&units=metric";
            using var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Weather API error: {request.error}");
                yield break;
            }

            var json = JsonUtility.FromJson<OpenWeatherResponse>(request.downloadHandler.text);
            var now = DateTime.Now;

            var weather = new WeatherData
            {
                temperature = json.main.temp,
                humidity = json.main.humidity,
                windSpeed = json.wind.speed,
                cloudCover = json.clouds.all,
                condition = MapCondition(json.weather[0].id),
                timeOfDay = TimeUtils.GetTimeOfDay(now),
                isNight = TimeUtils.IsNight(now),
                isGoldenHour = TimeUtils.IsGoldenHour(now),
                moonPhase = MoonPhaseCalculator.Calculate(now),
                calendarEvent = CalendarEvents.GetEvent(now)
            };

            CurrentWeather = weather;
            OnWeatherUpdated?.Invoke(weather);
        }

        public void SetDebugWeather(WeatherData data)
        {
            useDebugOverride = true;
            debugWeather = data;
            ApplyDebugWeather();
        }

        public void SetDebugMode(bool enabled)
        {
            useDebugOverride = enabled;
            if (enabled) ApplyDebugWeather();
            else if (hasLocation) StartCoroutine(FetchWeather());
        }

        private void ApplyDebugWeather()
        {
            CurrentWeather = debugWeather;
            OnWeatherUpdated?.Invoke(debugWeather);
        }

        private static WeatherCondition MapCondition(int weatherId)
        {
            return weatherId switch
            {
                >= 200 and < 300 => WeatherCondition.Storm,
                >= 300 and < 600 => WeatherCondition.Rain,
                >= 600 and < 700 => WeatherCondition.Snow,
                >= 801 => WeatherCondition.Cloudy,
                _ => WeatherCondition.Clear
            };
        }

        // OpenWeatherMap JSON response classes
        [Serializable] private class OpenWeatherResponse
        {
            public MainData main;
            public WindData wind;
            public CloudData clouds;
            public WeatherInfo[] weather;
        }
        [Serializable] private class MainData { public float temp; public float humidity; }
        [Serializable] private class WindData { public float speed; }
        [Serializable] private class CloudData { public float all; }
        [Serializable] private class WeatherInfo { public int id; public string main; }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Services/WeatherService.cs
git commit -m "feat: add WeatherService with OpenWeatherMap API and debug override"
```

---

## Task 5: Genetics Engine

**Files:**
- Create: `Assets/Scripts/Services/GeneticsEngine.cs`
- Create: `Assets/Tests/EditMode/TestGeneticsEngine.cs`

**Step 1: Write GeneticsEngine.cs**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Garden
{
    public struct GeneticsResult
    {
        public VariantData variant;
        public float growthSpeedMultiplier;
    }

    public static class GeneticsEngine
    {
        public static GeneticsResult Resolve(SeedData seed, WeatherData weather)
        {
            var sorted = seed.variants.OrderBy(v => v.priority).ToList();

            foreach (var variant in sorted)
            {
                if (variant.trigger != null && variant.trigger.Evaluate(weather))
                {
                    return new GeneticsResult
                    {
                        variant = variant,
                        growthSpeedMultiplier = 1.25f
                    };
                }
            }

            // Fallback to highest priority number (default)
            var fallback = sorted.LastOrDefault();
            return new GeneticsResult
            {
                variant = fallback,
                growthSpeedMultiplier = 1.0f
            };
        }

        public static List<(VariantData variant, bool isHighProbability)> GetProbabilities(SeedData seed, WeatherData weather)
        {
            var result = new List<(VariantData, bool)>();
            foreach (var variant in seed.variants)
            {
                bool matches = variant.trigger != null && variant.trigger.Evaluate(weather);
                result.Add((variant, matches));
            }
            return result;
        }
    }
}
```

**Step 2: Write test**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class TestGeneticsEngine
    {
        [Test]
        public void Resolve_StormWeather_ReturnsHighestPriorityMatch()
        {
            var stormVariant = ScriptableObject.CreateInstance<VariantData>();
            stormVariant.variantName = "Static";
            stormVariant.priority = 2;
            stormVariant.trigger = new TriggerCondition
            {
                useWeatherCondition = true,
                requiredConditions = new[] { WeatherCondition.Storm }
            };

            var baseVariant = ScriptableObject.CreateInstance<VariantData>();
            baseVariant.variantName = "Base";
            baseVariant.priority = 4;
            baseVariant.trigger = new TriggerCondition(); // matches everything

            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.variants = new() { baseVariant, stormVariant };

            var weather = new WeatherData { condition = WeatherCondition.Storm };

            var result = GeneticsEngine.Resolve(seed, weather);
            Assert.AreEqual("Static", result.variant.variantName);
            Assert.AreEqual(1.25f, result.growthSpeedMultiplier);
        }

        [Test]
        public void Resolve_NoMatch_ReturnsFallback()
        {
            var rareVariant = ScriptableObject.CreateInstance<VariantData>();
            rareVariant.variantName = "Glacial";
            rareVariant.priority = 2;
            rareVariant.trigger = new TriggerCondition
            {
                useTemperature = true,
                minTemp = -50f,
                maxTemp = 5f
            };

            var baseVariant = ScriptableObject.CreateInstance<VariantData>();
            baseVariant.variantName = "Base";
            baseVariant.priority = 4;
            baseVariant.trigger = new TriggerCondition();

            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.variants = new() { rareVariant, baseVariant };

            var weather = new WeatherData { temperature = 22f };
            var result = GeneticsEngine.Resolve(seed, weather);
            Assert.AreEqual("Base", result.variant.variantName);
            Assert.AreEqual(1.0f, result.growthSpeedMultiplier);
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Services/GeneticsEngine.cs Assets/Tests/EditMode/TestGeneticsEngine.cs
git commit -m "feat: add GeneticsEngine with priority-based variant resolution and tests"
```

---

## Task 6: Save System

**Files:**
- Create: `Assets/Scripts/Services/SaveManager.cs`
- Create: `Assets/Scripts/Data/SaveData.cs`

**Step 1: Write SaveData.cs**

```csharp
using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SaveData
    {
        public int dewdrops;
        public int sunShards;
        public int auraDust;

        public ActivePlantSave activePlant;
        public List<GreenhousePlantSave> greenhousePlants = new();
        public List<string> discoveredVariants = new();
        public List<SeedInventoryEntry> seedInventory = new();
        public int greenhouseSlots = 6;
    }

    [Serializable]
    public class ActivePlantSave
    {
        public string seedName;
        public string variantName;
        public string plantTimeUtc;
        public float growthSpeedMultiplier = 1f;
        public bool isActive;
    }

    [Serializable]
    public class GreenhousePlantSave
    {
        public string seedName;
        public string variantName;
        public string harvestTimeUtc;
    }

    [Serializable]
    public class SeedInventoryEntry
    {
        public string seedName;
        public int count;
    }
}
```

**Step 2: Write SaveManager.cs**

```csharp
using System.IO;
using UnityEngine;

namespace Garden
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public SaveData Data { get; private set; } = new();

        private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<SaveData>(json);
            }
            else
            {
                Data = new SaveData();
            }
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Data = new SaveData();
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Services/SaveManager.cs
git commit -m "feat: add SaveManager with JSON persistence and SaveData structure"
```

---

## Task 7: Currency Manager

**Files:**
- Create: `Assets/Scripts/Services/CurrencyManager.cs`

**Step 1: Write CurrencyManager.cs**

```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        [SerializeField] private CurrencyConfig config;

        public CurrencyConfig Config => config;
        public int Dewdrops => SaveManager.Instance.Data.dewdrops;
        public int SunShards => SaveManager.Instance.Data.sunShards;
        public int AuraDust => SaveManager.Instance.Data.auraDust;

        public event Action<CurrencyType, int, int> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Add(CurrencyType type, int amount)
        {
            var data = SaveManager.Instance.Data;
            int old;
            switch (type)
            {
                case CurrencyType.Dewdrops:
                    old = data.dewdrops; data.dewdrops += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.dewdrops); break;
                case CurrencyType.SunShards:
                    old = data.sunShards; data.sunShards += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.sunShards); break;
                case CurrencyType.AuraDust:
                    old = data.auraDust; data.auraDust += amount;
                    OnCurrencyChanged?.Invoke(type, old, data.auraDust); break;
            }
            SaveManager.Instance.Save();
        }

        public bool Spend(CurrencyType type, int amount)
        {
            if (!CanAfford(type, amount)) return false;
            Add(type, -amount);
            return true;
        }

        public bool CanAfford(CurrencyType type, int amount)
        {
            return type switch
            {
                CurrencyType.Dewdrops => Dewdrops >= amount,
                CurrencyType.SunShards => SunShards >= amount,
                CurrencyType.AuraDust => AuraDust >= amount,
                _ => false
            };
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Services/CurrencyManager.cs
git commit -m "feat: add CurrencyManager with add/spend/canAfford and save integration"
```

---

## Task 8: Plant Manager

**Files:**
- Create: `Assets/Scripts/Managers/PlantManager.cs`

**Step 1: Write PlantManager.cs**

```csharp
using System;
using UnityEngine;

namespace Garden
{
    public class PlantManager : MonoBehaviour
    {
        public static PlantManager Instance { get; private set; }

        public PlantState State { get; private set; } = PlantState.Empty;
        public SeedData CurrentSeed { get; private set; }
        public VariantData CurrentVariant { get; private set; }
        public float GrowthProgress { get; private set; }
        public float GrowthSpeedMultiplier { get; private set; } = 1f;
        public DateTime PlantTime { get; private set; }

        public event Action OnPlantStateChanged;
        public event Action<float> OnGrowthUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RestoreFromSave();
        }

        private void Update()
        {
            if (State != PlantState.Growing) return;

            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            GrowthProgress = Mathf.Clamp01(elapsed / totalHours);
            OnGrowthUpdated?.Invoke(GrowthProgress);

            // Check weather bonus
            if (WeatherService.Instance != null && CurrentVariant.trigger != null)
            {
                if (CurrentVariant.trigger.Evaluate(WeatherService.Instance.CurrentWeather))
                    GrowthSpeedMultiplier = 1.25f;
                else
                    GrowthSpeedMultiplier = 1f;
            }

            if (GrowthProgress >= 1f)
            {
                State = PlantState.Mature;
                OnPlantStateChanged?.Invoke();
                SaveState();
            }
        }

        public void Plant(SeedData seed)
        {
            if (State != PlantState.Empty) return;

            var weather = WeatherService.Instance.CurrentWeather;
            var result = GeneticsEngine.Resolve(seed, weather);

            CurrentSeed = seed;
            CurrentVariant = result.variant;
            GrowthSpeedMultiplier = result.growthSpeedMultiplier;
            PlantTime = DateTime.UtcNow;
            GrowthProgress = 0f;
            State = PlantState.Growing;

            // Mark discovered
            var save = SaveManager.Instance.Data;
            if (!save.discoveredVariants.Contains(result.variant.variantName))
                save.discoveredVariants.Add(result.variant.variantName);

            // Consume seed
            var entry = save.seedInventory.Find(e => e.seedName == seed.seedName);
            if (entry != null) entry.count--;

            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        public void Harvest()
        {
            if (State != PlantState.Mature) return;

            GreenhouseManager.Instance.AddPlant(CurrentSeed, CurrentVariant);
            int dewdrops = CurrencyManager.Instance.Config.GetDewdropsForRarity(CurrentVariant.rarity);
            CurrencyManager.Instance.Add(CurrencyType.Dewdrops, dewdrops);

            CurrentSeed = null;
            CurrentVariant = null;
            GrowthProgress = 0f;
            State = PlantState.Empty;

            OnPlantStateChanged?.Invoke();
            SaveState();
        }

        public float GetRemainingHours()
        {
            if (State != PlantState.Growing) return 0f;
            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            return Mathf.Max(0f, totalHours - elapsed);
        }

        private void SaveState()
        {
            var save = SaveManager.Instance.Data;
            if (State == PlantState.Empty)
            {
                save.activePlant = new ActivePlantSave { isActive = false };
            }
            else
            {
                save.activePlant = new ActivePlantSave
                {
                    isActive = true,
                    seedName = CurrentSeed.seedName,
                    variantName = CurrentVariant.variantName,
                    plantTimeUtc = PlantTime.ToString("O"),
                    growthSpeedMultiplier = GrowthSpeedMultiplier
                };
            }
            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;
            if (save.activePlant == null || !save.activePlant.isActive) return;

            var seeds = Resources.LoadAll<SeedData>("Seeds");
            foreach (var seed in seeds)
            {
                if (seed.seedName != save.activePlant.seedName) continue;
                CurrentSeed = seed;
                foreach (var v in seed.variants)
                {
                    if (v.variantName != save.activePlant.variantName) continue;
                    CurrentVariant = v;
                    break;
                }
                break;
            }

            if (CurrentSeed == null || CurrentVariant == null) return;

            PlantTime = DateTime.Parse(save.activePlant.plantTimeUtc).ToUniversalTime();
            GrowthSpeedMultiplier = save.activePlant.growthSpeedMultiplier;

            float totalHours = CurrentSeed.baseGrowthHours / GrowthSpeedMultiplier;
            float elapsed = (float)(DateTime.UtcNow - PlantTime).TotalHours;
            GrowthProgress = Mathf.Clamp01(elapsed / totalHours);

            State = GrowthProgress >= 1f ? PlantState.Mature : PlantState.Growing;
            OnPlantStateChanged?.Invoke();
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Managers/PlantManager.cs
git commit -m "feat: add PlantManager with plant/grow/harvest lifecycle and save persistence"
```

---

## Task 9: Greenhouse Manager

**Files:**
- Create: `Assets/Scripts/Managers/GreenhouseManager.cs`

**Step 1: Write GreenhouseManager.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class GreenhouseManager : MonoBehaviour
    {
        public static GreenhouseManager Instance { get; private set; }

        public List<GreenhousePlant> Plants { get; private set; } = new();
        public int MaxSlots => SaveManager.Instance.Data.greenhouseSlots;

        public event Action OnGreenhouseChanged;

        private float dustAccumulator;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RestoreFromSave();
        }

        private void Update()
        {
            if (Plants.Count == 0) return;

            dustAccumulator += Time.deltaTime;
            if (dustAccumulator >= 3600f) // every hour
            {
                dustAccumulator -= 3600f;
                int totalDust = 0;
                var config = CurrencyManager.Instance.Config;
                foreach (var p in Plants)
                    totalDust += Mathf.RoundToInt(config.GetDustPerHourForRarity(p.rarity));
                if (totalDust > 0)
                    CurrencyManager.Instance.Add(CurrencyType.AuraDust, totalDust);
            }
        }

        public bool AddPlant(SeedData seed, VariantData variant)
        {
            if (Plants.Count >= MaxSlots) return false;

            Plants.Add(new GreenhousePlant
            {
                seedName = seed.seedName,
                variantName = variant.variantName,
                rarity = variant.rarity,
                primaryColor = variant.primaryColor,
                harvestTime = DateTime.UtcNow
            });

            SaveGreenhouse();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public bool ExpandSlots()
        {
            var config = CurrencyManager.Instance.Config;
            if (!CurrencyManager.Instance.Spend(CurrencyType.SunShards, config.slotCostSunShards))
                return false;
            SaveManager.Instance.Data.greenhouseSlots++;
            SaveManager.Instance.Save();
            OnGreenhouseChanged?.Invoke();
            return true;
        }

        public float GetTotalDustPerHour()
        {
            float total = 0;
            var config = CurrencyManager.Instance.Config;
            foreach (var p in Plants)
                total += config.GetDustPerHourForRarity(p.rarity);
            return total;
        }

        private void SaveGreenhouse()
        {
            var save = SaveManager.Instance.Data;
            save.greenhousePlants.Clear();
            foreach (var p in Plants)
            {
                save.greenhousePlants.Add(new GreenhousePlantSave
                {
                    seedName = p.seedName,
                    variantName = p.variantName,
                    harvestTimeUtc = p.harvestTime.ToString("O")
                });
            }
            SaveManager.Instance.Save();
        }

        private void RestoreFromSave()
        {
            var save = SaveManager.Instance.Data;
            Plants.Clear();

            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            foreach (var ps in save.greenhousePlants)
            {
                Rarity rarity = Rarity.Common;
                Color color = Color.green;
                foreach (var seed in allSeeds)
                {
                    if (seed.seedName != ps.seedName) continue;
                    foreach (var v in seed.variants)
                    {
                        if (v.variantName != ps.variantName) continue;
                        rarity = v.rarity;
                        color = v.primaryColor;
                        break;
                    }
                    break;
                }

                Plants.Add(new GreenhousePlant
                {
                    seedName = ps.seedName,
                    variantName = ps.variantName,
                    rarity = rarity,
                    primaryColor = color,
                    harvestTime = DateTime.Parse(ps.harvestTimeUtc).ToUniversalTime()
                });
            }
        }
    }

    public class GreenhousePlant
    {
        public string seedName;
        public string variantName;
        public Rarity rarity;
        public Color primaryColor;
        public DateTime harvestTime;
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Managers/GreenhouseManager.cs
git commit -m "feat: add GreenhouseManager with passive dust generation and slot expansion"
```

---

## Task 10: Game Manager & Seed Registry

**Files:**
- Create: `Assets/Scripts/Managers/GameManager.cs`
- Create: `Assets/Scripts/Managers/SeedRegistry.cs`

**Step 1: Write SeedRegistry.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class SeedRegistry : MonoBehaviour
    {
        public static SeedRegistry Instance { get; private set; }

        private Dictionary<string, SeedData> seeds = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (var seed in Resources.LoadAll<SeedData>("Seeds"))
                seeds[seed.seedName] = seed;
        }

        public SeedData GetSeed(string name) => seeds.GetValueOrDefault(name);
        public IEnumerable<SeedData> AllSeeds => seeds.Values;

        public List<SeedData> GetOwnedSeeds()
        {
            var result = new List<SeedData>();
            var save = SaveManager.Instance.Data;
            foreach (var entry in save.seedInventory)
            {
                if (entry.count > 0 && seeds.TryGetValue(entry.seedName, out var seed))
                    result.Add(seed);
            }
            return result;
        }

        public int GetSeedCount(string seedName)
        {
            var entry = SaveManager.Instance.Data.seedInventory.Find(e => e.seedName == seedName);
            return entry?.count ?? 0;
        }

        public void AddSeed(string seedName, int count = 1)
        {
            var save = SaveManager.Instance.Data;
            var entry = save.seedInventory.Find(e => e.seedName == seedName);
            if (entry != null)
                entry.count += count;
            else
                save.seedInventory.Add(new SeedInventoryEntry { seedName = seedName, count = count });
            SaveManager.Instance.Save();
        }
    }
}
```

**Step 2: Write GameManager.cs**

```csharp
using UnityEngine;

namespace Garden
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private CurrencyConfig currencyConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Give starter seeds on first run
            if (SaveManager.Instance.Data.seedInventory.Count == 0)
            {
                SeedRegistry.Instance.AddSeed("Astra", 5);
                SaveManager.Instance.Data.sunShards = 10;
                SaveManager.Instance.Save();
            }
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/Managers/GameManager.cs Assets/Scripts/Managers/SeedRegistry.cs
git commit -m "feat: add GameManager and SeedRegistry with starter seed logic"
```

---

## Task 11: Astra Seed Data (ScriptableObject Editor Script)

**Files:**
- Create: `Assets/Scripts/Editor/AstraSeedCreator.cs`

This creates a menu item that generates all 12 Astra variant SOs and the Astra seed SO in one click, since we can't create SOs from CLI.

**Step 1: Write AstraSeedCreator.cs**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Garden.Editor
{
    public static class AstraSeedCreator
    {
        [MenuItem("Garden/Create Astra Seed Data")]
        public static void CreateAstraData()
        {
            // Ensure directories
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Seeds");
            EnsureFolder("Assets/Resources/Variants");
            EnsureFolder("Assets/Resources/Config");

            // Create currency config
            var config = ScriptableObject.CreateInstance<CurrencyConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/Config/CurrencyConfig.asset");

            // Create variants
            var bloodMoon = CreateVariant("Blood-Moon Astra", Rarity.Legendary, 1,
                new Color(0.6f, 0.05f, 0.1f), new Color(0.8f, 0.2f, 0.3f),
                "Deep crimson; petals resemble feathers.",
                "Requires the shadow of a celestial body.",
                trigger: new TriggerCondition { useCalendarEvent = true, requiredCalendarEvent = CalendarEvent.LunarEclipse });

            var equinox = CreateVariant("Equinox Astra", Rarity.Legendary, 1,
                new Color(0.5f, 0.3f, 0.8f), new Color(0.9f, 0.7f, 0.2f),
                "Perfect symmetry; dual-colored petals.",
                "Requires the balance of the cosmos.",
                trigger: new TriggerCondition { useCalendarEvent = true, requiredCalendarEvent = CalendarEvent.SpringEquinox });

            var staticV = CreateVariant("Static Astra", Rarity.Epic, 2,
                new Color(0.9f, 0.95f, 1f), new Color(0.4f, 0.6f, 1f),
                "Constant electrical arcs between leaves.",
                "Requires the wrath of the sky.",
                trigger: new TriggerCondition { useWeatherCondition = true, requiredConditions = new[] { WeatherCondition.Storm } });

            var glacial = CreateVariant("Glacial Astra", Rarity.Rare, 2,
                new Color(0.7f, 0.85f, 1f), new Color(0.9f, 0.95f, 1f),
                "Crystalline texture; icy blue tint.",
                "Requires the bite of winter's breath.",
                trigger: new TriggerCondition { useTemperature = true, minTemp = -50f, maxTemp = 5f });

            var petrified = CreateVariant("Petrified Astra", Rarity.Rare, 2,
                new Color(0.3f, 0.3f, 0.3f), new Color(0.5f, 0.35f, 0.2f),
                "Charcoal-grey; fire-resistant aesthetic.",
                "Requires the furnace of the sun itself.",
                trigger: new TriggerCondition { useTemperature = true, minTemp = 38f, maxTemp = 60f });

            var galeForce = CreateVariant("Gale-Force Astra", Rarity.Rare, 2,
                new Color(0.6f, 0.8f, 0.6f), new Color(0.8f, 0.9f, 0.7f),
                "Spiraled, corkscrew stem; vibrates.",
                "Requires the howl of an untamed gale.",
                trigger: new TriggerCondition { useWindSpeed = true, minWindSpeed = 8.9f });

            var voidV = CreateVariant("Void Astra", Rarity.Epic, 2,
                new Color(0.05f, 0.02f, 0.1f), new Color(0.2f, 0.1f, 0.3f),
                "Almost invisible; only the outline glows.",
                "Requires the deepest absence of light and moon.",
                trigger: new TriggerCondition
                {
                    useMoonPhase = true, requiredMoonPhase = MoonPhase.NewMoon,
                    useTimeOfDay = true, requiredTimeOfDay = TimeOfDay.Night
                });

            var dewDrop = CreateVariant("Dew-Drop Astra", Rarity.Rare, 3,
                new Color(0.3f, 0.6f, 0.9f), new Color(0.5f, 0.7f, 0.95f),
                "Sagging stems; holds liquid spheres.",
                "Requires the tears of the clouds.",
                trigger: new TriggerCondition
                {
                    useWeatherCondition = true, requiredConditions = new[] { WeatherCondition.Rain }
                });

            var nebula = CreateVariant("Nebula Astra", Rarity.Uncommon, 3,
                new Color(0.6f, 0.3f, 0.7f), new Color(0.9f, 0.5f, 0.6f),
                "Purple/Pink gradients; trailing dust.",
                "Requires the golden farewell of daylight.",
                trigger: new TriggerCondition { useTimeOfDay = true, requiredTimeOfDay = TimeOfDay.GoldenHour });

            var lunar = CreateVariant("Lunar Astra", Rarity.Common, 3,
                new Color(0.7f, 0.75f, 0.85f), new Color(0.9f, 0.92f, 0.95f),
                "Petals turn silver; glows in the dark.",
                "Requires the embrace of night.",
                trigger: new TriggerCondition { useTimeOfDay = true, requiredTimeOfDay = TimeOfDay.Night });

            var solar = CreateVariant("Solar Astra", Rarity.Common, 3,
                new Color(0.95f, 0.8f, 0.2f), new Color(1f, 0.9f, 0.4f),
                "Petals turn gold; leaves grow thick.",
                "Requires the full warmth of a clear sky.",
                trigger: new TriggerCondition
                {
                    useWeatherCondition = true, requiredConditions = new[] { WeatherCondition.Clear },
                    useTemperature = true, minTemp = 25f, maxTemp = 60f
                });

            var astraBase = CreateVariant("Astra Base", Rarity.Common, 4,
                new Color(0.3f, 0.7f, 0.3f), new Color(0.9f, 0.9f, 0.9f),
                "Green stems, simple white petals.",
                "The default form. Grows in any condition.",
                trigger: new TriggerCondition());

            // Create seed
            var seed = ScriptableObject.CreateInstance<SeedData>();
            seed.seedName = "Astra";
            seed.description = "A highly reactive starter seed that morphs based on environmental conditions.";
            seed.baseGrowthHours = 1f; // Short for testing; change to 24 for production
            seed.variants = new()
            {
                bloodMoon, equinox, staticV, glacial, petrified,
                galeForce, voidV, dewDrop, nebula, lunar, solar, astraBase
            };
            AssetDatabase.CreateAsset(seed, "Assets/Resources/Seeds/Astra.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Astra seed data created successfully!");
        }

        private static VariantData CreateVariant(string name, Rarity rarity, int priority,
            Color primary, Color secondary, string desc, string hint, TriggerCondition trigger)
        {
            var v = ScriptableObject.CreateInstance<VariantData>();
            v.variantName = name;
            v.rarity = rarity;
            v.priority = priority;
            v.primaryColor = primary;
            v.secondaryColor = secondary;
            v.description = desc;
            v.discoveryHint = hint;
            v.trigger = trigger;
            string safeName = name.Replace(" ", "").Replace("-", "");
            AssetDatabase.CreateAsset(v, $"Assets/Resources/Variants/{safeName}.asset");
            return v;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }
    }
}
#endif
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Editor/AstraSeedCreator.cs
git commit -m "feat: add editor script to generate all Astra seed and variant ScriptableObjects"
```

---

## Task 12: Plant Visuals

**Files:**
- Create: `Assets/Scripts/UI/PlantVisual.cs`

**Step 1: Write PlantVisual.cs**

A procedural 2D plant renderer that changes appearance based on variant data and growth progress.

```csharp
using UnityEngine;

namespace Garden
{
    public class PlantVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer stemRenderer;
        [SerializeField] private SpriteRenderer petalRenderer;
        [SerializeField] private SpriteRenderer potRenderer;
        [SerializeField] private Transform stemTransform;
        [SerializeField] private Transform petalTransform;
        [SerializeField] private ParticleSystem glowParticles;

        [Header("Growth")]
        [SerializeField] private float maxStemHeight = 2f;
        [SerializeField] private float maxPetalScale = 1f;

        private VariantData currentVariant;

        public void SetVariant(VariantData variant)
        {
            currentVariant = variant;
            if (variant == null)
            {
                stemRenderer.enabled = false;
                petalRenderer.enabled = false;
                glowParticles.Stop();
                return;
            }

            stemRenderer.enabled = true;
            petalRenderer.enabled = true;
            stemRenderer.color = variant.primaryColor;
            petalRenderer.color = variant.secondaryColor;

            if (variant.rarity >= Rarity.Rare && glowParticles != null)
            {
                var main = glowParticles.main;
                main.startColor = variant.primaryColor;
                glowParticles.Play();
            }
            else
            {
                glowParticles?.Stop();
            }
        }

        public void SetGrowth(float progress)
        {
            float p = Mathf.Clamp01(progress);

            // Stem grows upward
            if (stemTransform != null)
            {
                float h = Mathf.Lerp(0.1f, maxStemHeight, p);
                stemTransform.localScale = new Vector3(0.15f, h, 1f);
                stemTransform.localPosition = new Vector3(0, h * 0.5f, 0);
            }

            // Petals appear after 60% growth
            if (petalTransform != null)
            {
                float petalProgress = Mathf.Clamp01((p - 0.6f) / 0.4f);
                float s = Mathf.Lerp(0f, maxPetalScale, petalProgress);
                petalTransform.localScale = new Vector3(s, s, 1f);
                if (stemTransform != null)
                    petalTransform.localPosition = new Vector3(0, stemTransform.localScale.y, 0);
            }
        }

        public void Clear()
        {
            currentVariant = null;
            stemRenderer.enabled = false;
            petalRenderer.enabled = false;
            glowParticles?.Stop();
            if (stemTransform != null) stemTransform.localScale = Vector3.zero;
            if (petalTransform != null) petalTransform.localScale = Vector3.zero;
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/PlantVisual.cs
git commit -m "feat: add procedural PlantVisual with growth animation and variant coloring"
```

---

## Task 13: UI Foundation - Hortus View

**Files:**
- Create: `Assets/Scripts/UI/HortusUI.cs`
- Create: `Assets/Scripts/UI/ResonanceBar.cs`
- Create: `Assets/Scripts/UI/PulseButton.cs`

**Step 1: Write ResonanceBar.cs**

```csharp
using UnityEngine;
using TMPro;

namespace Garden
{
    public class ResonanceBar : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI weatherText;

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += UpdateDisplay;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateDisplay;
        }

        private void Start()
        {
            if (WeatherService.Instance != null)
                UpdateDisplay(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateDisplay(WeatherData w)
        {
            string temp = $"{w.temperature:F0}°C";
            string condition = w.condition.ToString();
            string moon = FormatMoonPhase(w.moonPhase);
            weatherText.text = $"{temp}  •  {condition}  •  {moon}";
        }

        private string FormatMoonPhase(MoonPhase phase) => phase switch
        {
            MoonPhase.NewMoon => "New Moon",
            MoonPhase.WaxingCrescent => "Waxing Crescent",
            MoonPhase.FirstQuarter => "First Quarter",
            MoonPhase.WaxingGibbous => "Waxing Gibbous",
            MoonPhase.FullMoon => "Full Moon",
            MoonPhase.WaningGibbous => "Waning Gibbous",
            MoonPhase.LastQuarter => "Last Quarter",
            MoonPhase.WaningCrescent => "Waning Crescent",
            _ => phase.ToString()
        };
    }
}
```

**Step 2: Write PulseButton.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class PulseButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image pulseRing;

        public event System.Action OnPulse;

        private void Start()
        {
            button.onClick.AddListener(HandleClick);
            UpdateState();
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged += UpdateState;
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
                PlantManager.Instance.OnPlantStateChanged -= UpdateState;
        }

        private void Update()
        {
            if (PlantManager.Instance?.State == PlantState.Growing)
            {
                float hours = PlantManager.Instance.GetRemainingHours();
                if (hours > 1f)
                    label.text = $"{hours:F1}h remaining";
                else
                    label.text = $"{hours * 60f:F0}m remaining";
            }
        }

        private void HandleClick()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            switch (pm.State)
            {
                case PlantState.Empty:
                    OnPulse?.Invoke();
                    break;
                case PlantState.Growing:
                    // Show ripple effect (handled by animation)
                    if (pulseRing != null)
                        pulseRing.GetComponent<Animator>()?.SetTrigger("Pulse");
                    break;
                case PlantState.Mature:
                    pm.Harvest();
                    break;
            }
        }

        private void UpdateState()
        {
            var pm = PlantManager.Instance;
            if (pm == null) return;

            label.text = pm.State switch
            {
                PlantState.Empty => "Plant a Seed",
                PlantState.Growing => "Growing...",
                PlantState.Mature => "Harvest!",
                _ => ""
            };
        }
    }
}
```

**Step 3: Write HortusUI.cs**

```csharp
using UnityEngine;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlantVisual plantVisual;
        [SerializeField] private PulseButton pulseButton;
        [SerializeField] private GameObject satchelPanel;
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private GameObject greenhousePanel;
        [SerializeField] private GameObject debugPanel;

        [Header("Nav Buttons")]
        [SerializeField] private UnityEngine.UI.Button codexButton;
        [SerializeField] private UnityEngine.UI.Button greenhouseButton;
        [SerializeField] private UnityEngine.UI.Button debugButton;

        private void Start()
        {
            pulseButton.OnPulse += OpenSatchel;
            codexButton?.onClick.AddListener(() => TogglePanel(codexPanel));
            greenhouseButton?.onClick.AddListener(() => TogglePanel(greenhousePanel));
            debugButton?.onClick.AddListener(() => TogglePanel(debugPanel));

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
            satchelPanel.SetActive(true);
        }

        private void TogglePanel(GameObject panel)
        {
            bool wasActive = panel.activeSelf;
            CloseAllPanels();
            if (!wasActive) panel.SetActive(true);
        }

        private void CloseAllPanels()
        {
            satchelPanel?.SetActive(false);
            codexPanel?.SetActive(false);
            greenhousePanel?.SetActive(false);
            debugPanel?.SetActive(false);
        }
    }
}
```

**Step 4: Commit**

```bash
git add Assets/Scripts/UI/HortusUI.cs Assets/Scripts/UI/ResonanceBar.cs Assets/Scripts/UI/PulseButton.cs
git commit -m "feat: add HortusUI main screen with ResonanceBar and PulseButton"
```

---

## Task 14: Seed Satchel UI

**Files:**
- Create: `Assets/Scripts/UI/SatchelUI.cs`
- Create: `Assets/Scripts/UI/SeedSlotUI.cs`

**Step 1: Write SeedSlotUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class SeedSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Button selectButton;

        private SeedData seed;
        private System.Action<SeedData> onSelected;

        public void Setup(SeedData data, int count, System.Action<SeedData> callback)
        {
            seed = data;
            onSelected = callback;
            nameText.text = data.seedName;
            countText.text = $"x{count}";
            if (data.icon != null) iconImage.sprite = data.icon;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(seed));
        }
    }
}
```

**Step 2: Write SatchelUI.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        [SerializeField] private Transform seedGrid;
        [SerializeField] private GameObject seedSlotPrefab;
        [SerializeField] private GameObject probabilityPanel;
        [SerializeField] private Transform probabilityGrid;
        [SerializeField] private GameObject probabilityEntryPrefab;
        [SerializeField] private Button plantButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI selectedSeedName;

        private SeedData selectedSeed;

        private void OnEnable()
        {
            RefreshGrid();
            probabilityPanel.SetActive(false);
            plantButton.interactable = false;
        }

        private void Start()
        {
            plantButton.onClick.AddListener(OnPlant);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void RefreshGrid()
        {
            foreach (Transform child in seedGrid) Destroy(child.gameObject);

            var seeds = SeedRegistry.Instance.GetOwnedSeeds();
            foreach (var seed in seeds)
            {
                var slot = Instantiate(seedSlotPrefab, seedGrid);
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                slot.GetComponent<SeedSlotUI>().Setup(seed, count, OnSeedSelected);
            }
        }

        private void OnSeedSelected(SeedData seed)
        {
            selectedSeed = seed;
            selectedSeedName.text = seed.seedName;
            plantButton.interactable = true;
            ShowProbabilities(seed);
        }

        private void ShowProbabilities(SeedData seed)
        {
            probabilityPanel.SetActive(true);
            foreach (Transform child in probabilityGrid) Destroy(child.gameObject);

            var weather = WeatherService.Instance.CurrentWeather;
            var probs = GeneticsEngine.GetProbabilities(seed, weather);

            foreach (var (variant, isHigh) in probs)
            {
                var entry = Instantiate(probabilityEntryPrefab, probabilityGrid);
                var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = variant.variantName;
                    text.color = isHigh ? Color.yellow : Color.gray;
                }
            }
        }

        private void OnPlant()
        {
            if (selectedSeed == null || PlantManager.Instance.State != PlantState.Empty) return;
            PlantManager.Instance.Plant(selectedSeed);
            gameObject.SetActive(false);
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Scripts/UI/SatchelUI.cs Assets/Scripts/UI/SeedSlotUI.cs
git commit -m "feat: add Seed Satchel UI with probability preview"
```

---

## Task 15: Flora Codex UI

**Files:**
- Create: `Assets/Scripts/UI/CodexUI.cs`

**Step 1: Write CodexUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class CodexUI : MonoBehaviour
    {
        [SerializeField] private Transform variantGrid;
        [SerializeField] private GameObject variantEntryPrefab;
        [SerializeField] private TextMeshProUGUI detailName;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private TextMeshProUGUI detailRarity;
        [SerializeField] private Image detailColorSwatch;
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshCodex();
        }

        private void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void RefreshCodex()
        {
            foreach (Transform child in variantGrid) Destroy(child.gameObject);

            var discovered = SaveManager.Instance.Data.discoveredVariants;

            foreach (var seed in SeedRegistry.Instance.AllSeeds)
            {
                foreach (var variant in seed.variants)
                {
                    var entry = Instantiate(variantEntryPrefab, variantGrid);
                    bool isDiscovered = discovered.Contains(variant.variantName);

                    var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                    var image = entry.GetComponent<Image>();
                    var button = entry.GetComponent<Button>();

                    if (isDiscovered)
                    {
                        if (text != null) text.text = variant.variantName;
                        if (image != null) image.color = variant.primaryColor;
                    }
                    else
                    {
                        if (text != null) text.text = "???";
                        if (image != null) image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    }

                    button?.onClick.AddListener(() => ShowDetail(variant, isDiscovered));
                }
            }
        }

        private void ShowDetail(VariantData variant, bool discovered)
        {
            if (discovered)
            {
                detailName.text = variant.variantName;
                detailDescription.text = variant.description;
                detailRarity.text = variant.rarity.ToString();
                detailColorSwatch.color = variant.primaryColor;
            }
            else
            {
                detailName.text = "Unknown Variant";
                detailDescription.text = variant.discoveryHint;
                detailRarity.text = "???";
                detailColorSwatch.color = Color.black;
            }
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/CodexUI.cs
git commit -m "feat: add Flora Codex UI with discovery silhouettes and cryptic hints"
```

---

## Task 16: Greenhouse UI

**Files:**
- Create: `Assets/Scripts/UI/GreenhouseUI.cs`

**Step 1: Write GreenhouseUI.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class GreenhouseUI : MonoBehaviour
    {
        [SerializeField] private Transform plantGrid;
        [SerializeField] private GameObject plantSlotPrefab;
        [SerializeField] private TextMeshProUGUI dustRateText;
        [SerializeField] private TextMeshProUGUI slotsText;
        [SerializeField] private Button expandButton;
        [SerializeField] private Button closeButton;

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void Start()
        {
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            expandButton.onClick.AddListener(OnExpand);
        }

        private void RefreshDisplay()
        {
            foreach (Transform child in plantGrid) Destroy(child.gameObject);

            var gm = GreenhouseManager.Instance;
            slotsText.text = $"{gm.Plants.Count} / {gm.MaxSlots}";
            dustRateText.text = $"+{gm.GetTotalDustPerHour():F1} Aura Dust/hr";

            foreach (var plant in gm.Plants)
            {
                var slot = Instantiate(plantSlotPrefab, plantGrid);
                var text = slot.GetComponentInChildren<TextMeshProUGUI>();
                var image = slot.GetComponent<Image>();
                if (text != null) text.text = plant.variantName;
                if (image != null) image.color = plant.primaryColor;
            }

            // Show empty slots
            for (int i = gm.Plants.Count; i < gm.MaxSlots; i++)
            {
                var slot = Instantiate(plantSlotPrefab, plantGrid);
                var text = slot.GetComponentInChildren<TextMeshProUGUI>();
                var image = slot.GetComponent<Image>();
                if (text != null) text.text = "Empty";
                if (image != null) image.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            }

            var config = CurrencyManager.Instance.Config;
            expandButton.interactable = CurrencyManager.Instance.CanAfford(
                CurrencyType.SunShards, config.slotCostSunShards);
        }

        private void OnExpand()
        {
            if (GreenhouseManager.Instance.ExpandSlots())
                RefreshDisplay();
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/GreenhouseUI.cs
git commit -m "feat: add Greenhouse UI with plant display and slot expansion"
```

---

## Task 17: Currency Display UI

**Files:**
- Create: `Assets/Scripts/UI/CurrencyDisplay.cs`

**Step 1: Write CurrencyDisplay.cs**

```csharp
using UnityEngine;
using TMPro;

namespace Garden
{
    public class CurrencyDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dewdropsText;
        [SerializeField] private TextMeshProUGUI sunShardsText;
        [SerializeField] private TextMeshProUGUI auraDustText;

        private void OnEnable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged += OnChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnCurrencyChanged -= OnChanged;
        }

        private void OnChanged(CurrencyType type, int oldVal, int newVal) => Refresh();

        private void Refresh()
        {
            var cm = CurrencyManager.Instance;
            if (cm == null) return;
            dewdropsText.text = cm.Dewdrops.ToString();
            sunShardsText.text = cm.SunShards.ToString();
            auraDustText.text = cm.AuraDust.ToString();
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/CurrencyDisplay.cs
git commit -m "feat: add CurrencyDisplay UI component"
```

---

## Task 18: Weather Overlay Effects

**Files:**
- Create: `Assets/Scripts/UI/WeatherOverlay.cs`

**Step 1: Write WeatherOverlay.cs**

```csharp
using UnityEngine;

namespace Garden
{
    public class WeatherOverlay : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rainEffect;
        [SerializeField] private ParticleSystem snowEffect;
        [SerializeField] private ParticleSystem windLines;

        private void OnEnable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated += UpdateEffects;
        }

        private void OnDisable()
        {
            if (WeatherService.Instance != null)
                WeatherService.Instance.OnWeatherUpdated -= UpdateEffects;
        }

        private void Start()
        {
            StopAll();
            if (WeatherService.Instance != null)
                UpdateEffects(WeatherService.Instance.CurrentWeather);
        }

        private void UpdateEffects(WeatherData w)
        {
            StopAll();

            switch (w.condition)
            {
                case WeatherCondition.Rain:
                case WeatherCondition.Storm:
                    rainEffect?.Play();
                    if (w.condition == WeatherCondition.Storm && windLines != null)
                        windLines.Play();
                    break;
                case WeatherCondition.Snow:
                    snowEffect?.Play();
                    break;
            }

            if (w.windSpeed > 5f && windLines != null && !windLines.isPlaying)
                windLines.Play();
        }

        private void StopAll()
        {
            rainEffect?.Stop();
            snowEffect?.Stop();
            windLines?.Stop();
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/UI/WeatherOverlay.cs
git commit -m "feat: add WeatherOverlay with rain, snow, and wind particle effects"
```

---

## Task 19: Debug Weather Panel

**Files:**
- Create: `Assets/Scripts/Debug/DebugWeatherPanel.cs`

**Step 1: Write DebugWeatherPanel.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class DebugWeatherPanel : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Toggle debugModeToggle;
        [SerializeField] private Slider tempSlider;
        [SerializeField] private Slider humiditySlider;
        [SerializeField] private Slider windSlider;
        [SerializeField] private TMP_Dropdown conditionDropdown;
        [SerializeField] private TMP_Dropdown moonPhaseDropdown;
        [SerializeField] private TMP_Dropdown timeOfDayDropdown;
        [SerializeField] private TMP_Dropdown calendarEventDropdown;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI tempLabel;
        [SerializeField] private TextMeshProUGUI humidityLabel;
        [SerializeField] private TextMeshProUGUI windLabel;

        [Header("Preset Buttons")]
        [SerializeField] private Button blizzardButton;
        [SerializeField] private Button thunderstormButton;
        [SerializeField] private Button clearNightButton;
        [SerializeField] private Button goldenHourButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button closeButton;

        private void Start()
        {
            tempSlider.minValue = -20; tempSlider.maxValue = 50;
            humiditySlider.minValue = 0; humiditySlider.maxValue = 100;
            windSlider.minValue = 0; windSlider.maxValue = 50;

            tempSlider.onValueChanged.AddListener(v => tempLabel.text = $"{v:F0}°C");
            humiditySlider.onValueChanged.AddListener(v => humidityLabel.text = $"{v:F0}%");
            windSlider.onValueChanged.AddListener(v => windLabel.text = $"{v:F1} m/s");

            applyButton.onClick.AddListener(ApplySettings);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            blizzardButton?.onClick.AddListener(() => ApplyPreset(-5, 60, 15, WeatherCondition.Snow, TimeOfDay.Day, MoonPhase.FullMoon));
            thunderstormButton?.onClick.AddListener(() => ApplyPreset(20, 90, 25, WeatherCondition.Storm, TimeOfDay.Day, MoonPhase.WaxingGibbous));
            clearNightButton?.onClick.AddListener(() => ApplyPreset(15, 40, 2, WeatherCondition.Clear, TimeOfDay.Night, MoonPhase.FullMoon));
            goldenHourButton?.onClick.AddListener(() => ApplyPreset(22, 50, 5, WeatherCondition.Clear, TimeOfDay.GoldenHour, MoonPhase.WaxingCrescent));

            // Set initial values
            tempSlider.value = 22;
            humiditySlider.value = 50;
            windSlider.value = 3;
        }

        private void ApplyPreset(float temp, float humidity, float wind, WeatherCondition cond, TimeOfDay tod, MoonPhase moon)
        {
            tempSlider.value = temp;
            humiditySlider.value = humidity;
            windSlider.value = wind;
            conditionDropdown.value = (int)cond;
            timeOfDayDropdown.value = (int)tod;
            moonPhaseDropdown.value = (int)moon;
            ApplySettings();
        }

        private void ApplySettings()
        {
            var weather = new WeatherData
            {
                temperature = tempSlider.value,
                humidity = humiditySlider.value,
                windSpeed = windSlider.value,
                condition = (WeatherCondition)conditionDropdown.value,
                timeOfDay = (TimeOfDay)timeOfDayDropdown.value,
                isNight = (TimeOfDay)timeOfDayDropdown.value == TimeOfDay.Night,
                isGoldenHour = (TimeOfDay)timeOfDayDropdown.value == TimeOfDay.GoldenHour,
                moonPhase = (MoonPhase)moonPhaseDropdown.value,
                calendarEvent = (CalendarEvent)calendarEventDropdown.value,
                cloudCover = conditionDropdown.value >= 1 ? 70f : 10f
            };

            WeatherService.Instance.SetDebugWeather(weather);
        }
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Debug/DebugWeatherPanel.cs
git commit -m "feat: add DebugWeatherPanel with sliders, dropdowns, and preset buttons"
```

---

## Task 20: Scene Setup Script

**Files:**
- Create: `Assets/Scripts/Editor/SceneSetup.cs`

This editor script creates the full MainScene hierarchy with all GameObjects, Canvas, panels, and component wiring in one click.

**Step 1: Write SceneSetup.cs**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Garden.Editor
{
    public static class SceneSetup
    {
        [MenuItem("Garden/Setup Main Scene")]
        public static void SetupScene()
        {
            // --- Managers ---
            var managers = new GameObject("--- MANAGERS ---");

            CreateManager<SaveManager>(managers.transform, "SaveManager");
            CreateManager<WeatherService>(managers.transform, "WeatherService");
            CreateManager<SeedRegistry>(managers.transform, "SeedRegistry");

            var currencyGO = CreateManager<CurrencyManager>(managers.transform, "CurrencyManager");
            // CurrencyConfig will be assigned manually after running AstraSeedCreator

            CreateManager<PlantManager>(managers.transform, "PlantManager");
            CreateManager<GreenhouseManager>(managers.transform, "GreenhouseManager");
            CreateManager<GameManager>(managers.transform, "GameManager");

            // --- Canvas ---
            var canvasGO = new GameObject("MainCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // Background
            var bg = CreateUIImage(canvasGO.transform, "Background", new Color(0.05f, 0.08f, 0.12f));
            StretchFill(bg);

            // -- Resonance Bar --
            var resonanceGO = CreateUIPanel(canvasGO.transform, "ResonanceBar", new Vector2(0, -60), new Vector2(1000, 80));
            var resonanceText = CreateTMPText(resonanceGO.transform, "WeatherText", "22°C  •  Clear  •  Waxing Crescent", 28);
            resonanceGO.AddComponent<ResonanceBar>(); // Will need manual wiring of serialized field

            // -- Currency Display --
            var currDisplayGO = CreateUIPanel(canvasGO.transform, "CurrencyDisplay", new Vector2(0, -150), new Vector2(1000, 60));
            CreateTMPText(currDisplayGO.transform, "Dewdrops", "0", 24, new Vector2(-300, 0));
            CreateTMPText(currDisplayGO.transform, "SunShards", "0", 24, new Vector2(0, 0));
            CreateTMPText(currDisplayGO.transform, "AuraDust", "0", 24, new Vector2(300, 0));
            currDisplayGO.AddComponent<CurrencyDisplay>();

            // -- Plant Visual (World Space) --
            var plantRoot = new GameObject("PlantVisual");
            plantRoot.transform.position = new Vector3(0, -1, 0);

            var pot = CreateSprite(plantRoot.transform, "Pot", new Color(0.5f, 0.3f, 0.2f));
            pot.transform.localScale = new Vector3(1.5f, 0.5f, 1f);
            pot.transform.localPosition = new Vector3(0, 0, 0);

            var stem = CreateSprite(plantRoot.transform, "Stem", Color.green);
            stem.transform.localScale = new Vector3(0.15f, 0.1f, 1f);
            stem.transform.localPosition = new Vector3(0, 0.1f, 0);

            var petal = CreateSprite(plantRoot.transform, "Petal", Color.white);
            petal.transform.localScale = Vector3.zero;

            var glowGO = new GameObject("GlowParticles");
            glowGO.transform.SetParent(plantRoot.transform);
            glowGO.transform.localPosition = new Vector3(0, 1, 0);
            var ps = glowGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.1f;
            main.startLifetime = 2f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.Stop();

            var plantVis = plantRoot.AddComponent<PlantVisual>();

            // -- Pulse Button --
            var pulseGO = CreateUIPanel(canvasGO.transform, "PulseButton", new Vector2(0, 200), new Vector2(300, 300));
            var pulseImg = pulseGO.GetComponent<Image>();
            pulseImg.color = new Color(0.2f, 0.6f, 0.4f, 0.8f);
            var pulseBtn = pulseGO.AddComponent<Button>();
            CreateTMPText(pulseGO.transform, "Label", "Plant a Seed", 32);
            var pulseComp = pulseGO.AddComponent<PulseButton>();

            // -- Nav Buttons --
            var navBar = CreateUIPanel(canvasGO.transform, "NavBar", new Vector2(0, 80), new Vector2(1000, 80));
            var codexBtn = CreateButton(navBar.transform, "CodexBtn", "Codex", new Vector2(-300, 0));
            var ghBtn = CreateButton(navBar.transform, "GreenhouseBtn", "Greenhouse", new Vector2(0, 0));
            var dbgBtn = CreateButton(navBar.transform, "DebugBtn", "Debug", new Vector2(300, 0));

            // -- Overlay Panels (inactive by default) --
            var satchelPanel = CreateFullPanel(canvasGO.transform, "SatchelPanel");
            satchelPanel.AddComponent<SatchelUI>();
            satchelPanel.SetActive(false);

            var codexPanel = CreateFullPanel(canvasGO.transform, "CodexPanel");
            codexPanel.AddComponent<CodexUI>();
            codexPanel.SetActive(false);

            var greenhousePanel = CreateFullPanel(canvasGO.transform, "GreenhousePanel");
            greenhousePanel.AddComponent<GreenhouseUI>();
            greenhousePanel.SetActive(false);

            var debugPanel = CreateFullPanel(canvasGO.transform, "DebugPanel");
            debugPanel.AddComponent<DebugWeatherPanel>();
            debugPanel.SetActive(false);

            // -- Weather Overlay --
            var weatherOverlayGO = new GameObject("WeatherOverlay");
            weatherOverlayGO.transform.position = new Vector3(0, 5, 0);
            weatherOverlayGO.AddComponent<WeatherOverlay>();
            // Particle systems will need manual setup

            // -- HortusUI --
            var hortusGO = new GameObject("HortusUI");
            hortusGO.transform.SetParent(canvasGO.transform);
            hortusGO.AddComponent<HortusUI>();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Main scene setup complete! Run Garden > Create Astra Seed Data first, then manually wire serialized references in Inspector.");
        }

        private static GameObject CreateManager<T>(Transform parent, string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<T>();
            return go;
        }

        private static GameObject CreateUIImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.2f, 0.25f, 0.6f);
            return go;
        }

        private static GameObject CreateFullPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.05f);
            rt.anchorMax = new Vector2(0.95f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);

            // Close button
            var closeGO = CreateButton(go.transform, "CloseButton", "X", new Vector2(400, 400));
            // Scroll content area
            var content = new GameObject("Content");
            content.transform.SetParent(go.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(20, 20);
            crt.offsetMax = new Vector2(-20, -60);

            // Grid layout
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200, 200);
            grid.spacing = new Vector2(20, 20);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            return go;
        }

        private static TextMeshProUGUI CreateTMPText(Transform parent, string name, string text, float size, Vector2? pos = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (pos.HasValue) rt.anchoredPosition = pos.Value;
            rt.sizeDelta = new Vector2(400, 50);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(200, 60);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.3f, 0.35f, 0.8f);
            go.AddComponent<Button>();
            CreateTMPText(go.transform, "Label", label, 22);
            return go;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color = color;
            return sr;
        }
    }
}
#endif
```

**Step 2: Commit**

```bash
git add Assets/Scripts/Editor/SceneSetup.cs
git commit -m "feat: add SceneSetup editor script to auto-create full MainScene hierarchy"
```

---

## Task 21: Integration - Wire and Test

**Step 1: Add TextMeshPro package dependency**

Add to `Packages/manifest.json`:
```json
"com.unity.textmeshpro": "4.0.0"
```

**Step 2: Open Unity Editor and run setup**

1. Open the project in Unity Editor
2. Go to menu **Garden > Create Astra Seed Data** — creates all ScriptableObjects
3. Go to menu **Garden > Setup Main Scene** — creates the scene hierarchy
4. In Inspector, wire the serialized references that need manual assignment:
   - CurrencyManager → assign CurrencyConfig from Resources/Config
   - HortusUI → assign panel references (SatchelPanel, CodexPanel, etc.)
   - ResonanceBar → assign WeatherText TMP
   - PulseButton → assign Button, Label TMP
   - SatchelUI → assign grid, prefab, close button references
   - CodexUI → assign grid, detail texts, close button references
   - GreenhouseUI → assign grid, texts, buttons
   - DebugWeatherPanel → assign all sliders, dropdowns, buttons, labels
   - PlantVisual → assign stem/petal/pot renderers and particle system
5. Save scene as `Assets/Scenes/MainScene.unity`

**Step 3: Create prefabs for grid items**

In the Unity Editor, create simple prefabs:
- `Assets/Prefabs/UI/SeedSlot.prefab` — Image + TMP text + Button
- `Assets/Prefabs/UI/VariantEntry.prefab` — Image + TMP text + Button
- `Assets/Prefabs/UI/GreenhouseSlot.prefab` — Image + TMP text
- `Assets/Prefabs/UI/ProbabilityEntry.prefab` — TMP text

**Step 4: Enter Play Mode and verify**

- Debug panel should open and allow weather simulation
- Planting a seed should resolve a variant based on debug weather
- Plant should grow over time and become harvestable
- Harvested plant should appear in greenhouse
- Currencies should update

**Step 5: Commit all remaining files**

```bash
git add -A
git commit -m "feat: complete Garden MVP scene setup and integration"
```

---

## Task 22: Add .gitignore

**Step 1: Write .gitignore for Unity**

```
# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/
asset_bundles/

# IDE
.vs/
.vscode/
*.csproj
*.sln
*.slnx
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.unityproj

# OS
.DS_Store
Thumbs.db

# Build
*.apk
*.aab
*.unitypackage
*.app

# Crashlytics
crashlytics-build.properties
```

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: add Unity .gitignore"
```
