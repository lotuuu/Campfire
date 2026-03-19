using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    [Serializable]
    public class GrowthRecipe
    {
        [Header("Heat")]
        public bool useHeat;
        public float idealTempMin;
        public float idealTempMax;
        public float heatTolerance;
        public float heatWeight;

        [Header("Wind")]
        public bool useWind;
        public float idealWindMin;
        public float idealWindMax;
        public float windTolerance;
        public float windWeight;

        [Header("Humidity")]
        public bool useHumidity;
        public float idealHumidityMin;
        public float idealHumidityMax;
        public float humidityTolerance;
        public float humidityWeight;

        [Header("Sunlight")]
        public bool useSunlight;
        public float idealSunlightMin;
        public float idealSunlightMax;
        public float sunlightTolerance;
        public float sunlightWeight;

        [Header("Rain")]
        public bool useRain;
        public float idealRainMin;
        public float idealRainMax;
        public float rainTolerance;
        public float rainWeight;

        [Header("Moon")]
        public bool useMoon;
        public MoonPhase requiredMoonPhase;
        public float moonWeight;

        [Header("Waterings")]
        public bool useWaterings;
        public int idealWateringsMin;
        public int idealWateringsMax;
        public float wateringsTolerance;
        public float wateringsWeight;

        public float Evaluate(GrowthSnapshots snapshots, int waterCount)
        {
            float weightSum = 0f;
            float scoreSum = 0f;
            bool hasSnapshots = snapshots.snapshotCount > 0;

            // Weather axes: score normally when snapshots exist, score 0 when missing
            if (useHeat)
            {
                float score = hasSnapshots ? ScoreRange(snapshots.sumTemp / snapshots.snapshotCount, idealTempMin, idealTempMax, heatTolerance) : 0f;
                scoreSum += score * heatWeight;
                weightSum += heatWeight;
            }
            if (useWind)
            {
                float score = hasSnapshots ? ScoreRange(snapshots.sumWind / snapshots.snapshotCount, idealWindMin, idealWindMax, windTolerance) : 0f;
                scoreSum += score * windWeight;
                weightSum += windWeight;
            }
            if (useHumidity)
            {
                float score = hasSnapshots ? ScoreRange(snapshots.sumHumidity / snapshots.snapshotCount, idealHumidityMin, idealHumidityMax, humidityTolerance) : 0f;
                scoreSum += score * humidityWeight;
                weightSum += humidityWeight;
            }
            if (useSunlight)
            {
                float score = hasSnapshots ? ScoreRange(snapshots.sumSunlight / snapshots.snapshotCount, idealSunlightMin, idealSunlightMax, sunlightTolerance) : 0f;
                scoreSum += score * sunlightWeight;
                weightSum += sunlightWeight;
            }
            if (useRain)
            {
                float score = hasSnapshots ? ScoreRange((float)snapshots.rainSnapshots / snapshots.snapshotCount, idealRainMin, idealRainMax, rainTolerance) : 0f;
                scoreSum += score * rainWeight;
                weightSum += rainWeight;
            }
            if (useMoon)
            {
                float fraction = 0f;
                if (hasSnapshots && snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
                    fraction = (float)snapshots.moonPhaseSnapshots[(int)requiredMoonPhase] / snapshots.snapshotCount;
                scoreSum += fraction * moonWeight;
                weightSum += moonWeight;
            }
            if (useWaterings)
            {
                scoreSum += ScoreRange(waterCount, idealWateringsMin, idealWateringsMax, wateringsTolerance) * wateringsWeight;
                weightSum += wateringsWeight;
            }

            if (weightSum <= 0f) return 1f;
            return scoreSum / weightSum;
        }

        public List<AxisResult> EvaluatePerAxis(GrowthSnapshots snapshots, int waterCount)
        {
            var results = new List<AxisResult>();
            bool hasSnapshots = snapshots.snapshotCount > 0;

            // Weather axes: show actual values when snapshots exist, show 0 when missing
            if (useHeat)
            {
                float avg = hasSnapshots ? snapshots.sumTemp / snapshots.snapshotCount : 0f;
                results.Add(new AxisResult
                {
                    axisName = "Heat",
                    actual = avg,
                    idealMin = idealTempMin,
                    idealMax = idealTempMax,
                    unit = "\u00b0C",
                    score = hasSnapshots ? ScoreRange(avg, idealTempMin, idealTempMax, heatTolerance) : 0f
                });
            }
            if (useWind)
            {
                float avg = hasSnapshots ? snapshots.sumWind / snapshots.snapshotCount : 0f;
                results.Add(new AxisResult
                {
                    axisName = "Wind",
                    actual = avg,
                    idealMin = idealWindMin,
                    idealMax = idealWindMax,
                    unit = "m/s",
                    score = hasSnapshots ? ScoreRange(avg, idealWindMin, idealWindMax, windTolerance) : 0f
                });
            }
            if (useHumidity)
            {
                float avg = hasSnapshots ? snapshots.sumHumidity / snapshots.snapshotCount : 0f;
                results.Add(new AxisResult
                {
                    axisName = "Humidity",
                    actual = avg,
                    idealMin = idealHumidityMin,
                    idealMax = idealHumidityMax,
                    unit = "%",
                    score = hasSnapshots ? ScoreRange(avg, idealHumidityMin, idealHumidityMax, humidityTolerance) : 0f
                });
            }
            if (useSunlight)
            {
                float avg = hasSnapshots ? snapshots.sumSunlight / snapshots.snapshotCount : 0f;
                results.Add(new AxisResult
                {
                    axisName = "Sunlight",
                    actual = avg,
                    idealMin = idealSunlightMin,
                    idealMax = idealSunlightMax,
                    unit = "%",
                    score = hasSnapshots ? ScoreRange(avg, idealSunlightMin, idealSunlightMax, sunlightTolerance) : 0f
                });
            }
            if (useRain)
            {
                float fraction = hasSnapshots ? (float)snapshots.rainSnapshots / snapshots.snapshotCount : 0f;
                results.Add(new AxisResult
                {
                    axisName = "Rain",
                    actual = fraction * 100f,
                    idealMin = idealRainMin * 100f,
                    idealMax = idealRainMax * 100f,
                    unit = "%",
                    score = hasSnapshots ? ScoreRange(fraction, idealRainMin, idealRainMax, rainTolerance) : 0f
                });
            }
            if (useMoon)
            {
                float fraction = 0f;
                if (hasSnapshots && snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
                    fraction = (float)snapshots.moonPhaseSnapshots[(int)requiredMoonPhase] / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Moon",
                    actual = fraction * 100f,
                    idealMin = -1f,
                    idealMax = -1f,
                    unit = "% " + requiredMoonPhase,
                    score = fraction
                });
            }
            if (useWaterings)
            {
                results.Add(new AxisResult
                {
                    axisName = "Waterings",
                    actual = waterCount,
                    idealMin = idealWateringsMin,
                    idealMax = idealWateringsMax,
                    unit = "x",
                    score = ScoreRange(waterCount, idealWateringsMin, idealWateringsMax, wateringsTolerance)
                });
            }

            return results;
        }

        public static float ScoreRange(float actual, float min, float max, float tolerance)
        {
            if (actual >= min && actual <= max) return 1f;
            float distance = actual < min ? min - actual : actual - max;
            if (tolerance <= 0f) return 0f;
            return Mathf.Clamp01(1f - distance / tolerance);
        }

    }

    [Serializable]
    public class GrowthSnapshots
    {
        public int snapshotCount;
        public float sumTemp;
        public float sumWind;
        public float sumHumidity;
        public float sumSunlight;
        public int rainSnapshots;
        public int[] moonPhaseSnapshots = new int[8];

        public void RecordSnapshot(WeatherData weather)
        {
            snapshotCount++;
            sumTemp += weather.temperature;
            sumWind += weather.windSpeed;
            sumHumidity += weather.humidity;
            sumSunlight += weather.isNight ? 0f : (100f - weather.cloudCover);
            if (weather.condition == WeatherCondition.Rain || weather.condition == WeatherCondition.Storm)
                rainSnapshots++;
            if (moonPhaseSnapshots == null || moonPhaseSnapshots.Length < 8)
                moonPhaseSnapshots = new int[8];
            int phaseIndex = (int)weather.moonPhase;
            if (phaseIndex >= 0 && phaseIndex < moonPhaseSnapshots.Length)
                moonPhaseSnapshots[phaseIndex]++;
        }
    }

    public class AxisResult
    {
        public string axisName;
        public float actual;
        public float idealMin;
        public float idealMax;
        public string unit;
        public float score;
    }
}
