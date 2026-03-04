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
        public float idealTempMax = 30f;
        public float heatTolerance = 10f;
        public float heatWeight = 1f;

        [Header("Wind")]
        public bool useWind;
        public float idealWindMin;
        public float idealWindMax = 10f;
        public float windTolerance = 5f;
        public float windWeight = 1f;

        [Header("Humidity")]
        public bool useHumidity;
        public float idealHumidityMin;
        public float idealHumidityMax = 80f;
        public float humidityTolerance = 20f;
        public float humidityWeight = 1f;

        [Header("Sunlight")]
        public bool useSunlight;
        public float idealSunlightMin;
        public float idealSunlightMax = 100f;
        public float sunlightTolerance = 20f;
        public float sunlightWeight = 1f;

        [Header("Rain")]
        public bool useRain;
        public float idealRainMin;
        public float idealRainMax = 1f;
        public float rainTolerance = 0.3f;
        public float rainWeight = 1f;

        [Header("Moon")]
        public bool useMoon;
        public MoonPhase requiredMoonPhase;
        public float moonWeight = 1f;

        [Header("Waterings")]
        public bool useWaterings;
        public int idealWateringsMin;
        public int idealWateringsMax;
        public float wateringsTolerance = 2f;
        public float wateringsWeight = 1f;

        public float Evaluate(GrowthSnapshots snapshots, int waterCount)
        {
            if (snapshots.snapshotCount <= 0) return 1f;

            float weightSum = 0f;
            float scoreSum = 0f;

            if (useHeat)
            {
                float avg = snapshots.sumTemp / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealTempMin, idealTempMax, heatTolerance) * heatWeight;
                weightSum += heatWeight;
            }
            if (useWind)
            {
                float avg = snapshots.sumWind / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealWindMin, idealWindMax, windTolerance) * windWeight;
                weightSum += windWeight;
            }
            if (useHumidity)
            {
                float avg = snapshots.sumHumidity / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealHumidityMin, idealHumidityMax, humidityTolerance) * humidityWeight;
                weightSum += humidityWeight;
            }
            if (useSunlight)
            {
                float avg = snapshots.sumSunlight / snapshots.snapshotCount;
                scoreSum += ScoreRange(avg, idealSunlightMin, idealSunlightMax, sunlightTolerance) * sunlightWeight;
                weightSum += sunlightWeight;
            }
            if (useRain)
            {
                float fraction = (float)snapshots.rainSnapshots / snapshots.snapshotCount;
                scoreSum += ScoreRange(fraction, idealRainMin, idealRainMax, rainTolerance) * rainWeight;
                weightSum += rainWeight;
            }
            if (useMoon)
            {
                float fraction = 0f;
                if (snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
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
            if (snapshots.snapshotCount <= 0) return results;

            if (useHeat)
            {
                float avg = snapshots.sumTemp / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Heat",
                    actual = avg,
                    idealMin = idealTempMin,
                    idealMax = idealTempMax,
                    unit = "\u00b0C",
                    score = ScoreRange(avg, idealTempMin, idealTempMax, heatTolerance)
                });
            }
            if (useWind)
            {
                float avg = snapshots.sumWind / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Wind",
                    actual = avg,
                    idealMin = idealWindMin,
                    idealMax = idealWindMax,
                    unit = "m/s",
                    score = ScoreRange(avg, idealWindMin, idealWindMax, windTolerance)
                });
            }
            if (useHumidity)
            {
                float avg = snapshots.sumHumidity / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Humidity",
                    actual = avg,
                    idealMin = idealHumidityMin,
                    idealMax = idealHumidityMax,
                    unit = "%",
                    score = ScoreRange(avg, idealHumidityMin, idealHumidityMax, humidityTolerance)
                });
            }
            if (useSunlight)
            {
                float avg = snapshots.sumSunlight / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Sunlight",
                    actual = avg,
                    idealMin = idealSunlightMin,
                    idealMax = idealSunlightMax,
                    unit = "%",
                    score = ScoreRange(avg, idealSunlightMin, idealSunlightMax, sunlightTolerance)
                });
            }
            if (useRain)
            {
                float fraction = (float)snapshots.rainSnapshots / snapshots.snapshotCount;
                results.Add(new AxisResult
                {
                    axisName = "Rain",
                    actual = fraction * 100f,
                    idealMin = idealRainMin * 100f,
                    idealMax = idealRainMax * 100f,
                    unit = "%",
                    score = ScoreRange(fraction, idealRainMin, idealRainMax, rainTolerance)
                });
            }
            if (useMoon)
            {
                float fraction = 0f;
                if (snapshots.moonPhaseSnapshots != null && snapshots.moonPhaseSnapshots.Length > (int)requiredMoonPhase)
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
            sumSunlight += 100f - weather.cloudCover;
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
