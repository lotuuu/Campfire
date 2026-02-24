using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        private List<EnvironmentData> environments = new();

        public event Action<int> OnEnvironmentUnlocked;
        public event Action<int> OnSlotUnlocked;

        public IReadOnlyList<EnvironmentData> Environments => environments;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var loaded = Resources.LoadAll<EnvironmentData>("Config/Environments");
            environments.AddRange(loaded);
            environments.Sort((a, b) => a.unlockCostDewdrops.CompareTo(b.unlockCostDewdrops));
        }

        public bool IsUnlocked(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            if (env.unlockCostDewdrops == 0) return true;
            return SaveManager.Instance.Data.unlockedEnvironments.Contains(env.environmentName);
        }

        public bool Unlock(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            if (IsUnlocked(envIndex)) return false;

            var env = environments[envIndex];
            if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, env.unlockCostDewdrops))
                return false;

            SaveManager.Instance.Data.unlockedEnvironments.Add(env.environmentName);
            SaveManager.Instance.Save();
            OnEnvironmentUnlocked?.Invoke(envIndex);
            return true;
        }

        public float GetGrowthBonus(int envIndex, WeatherData weather)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0f;
            var env = environments[envIndex];
            if (env.bonusCondition == null) return 0f;
            return env.bonusCondition.Evaluate(weather) ? env.growthSpeedBonus : 0f;
        }

        public int GetTotalUnlockedSlots()
        {
            int total = 0;
            for (int i = 0; i < environments.Count; i++)
            {
                if (IsUnlocked(i))
                    total += environments[i].slotCount;
            }
            return total;
        }

        public int GetSlotCount(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0;
            return environments[envIndex].slotCount;
        }

        public int GetActiveSlotCount(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return 0;
            var env = environments[envIndex];
            var entry = SaveManager.Instance.Data.environmentSlots
                .Find(e => e.environmentName == env.environmentName);
            return entry != null ? entry.unlockedSlots : env.slotCount;
        }

        public bool CanUnlockSlot(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            return GetActiveSlotCount(envIndex) < env.maxSlotCount
                && CurrencyManager.Instance.CanAfford(CurrencyType.Dewdrops, env.slotUnlockCostDewdrops);
        }

        public bool UnlockSlot(int envIndex)
        {
            if (envIndex < 0 || envIndex >= environments.Count) return false;
            var env = environments[envIndex];
            int current = GetActiveSlotCount(envIndex);
            if (current >= env.maxSlotCount) return false;
            if (!CurrencyManager.Instance.Spend(CurrencyType.Dewdrops, env.slotUnlockCostDewdrops))
                return false;

            var save = SaveManager.Instance.Data;
            var entry = save.environmentSlots.Find(e => e.environmentName == env.environmentName);
            if (entry == null)
            {
                entry = new EnvironmentSlotsSave { environmentName = env.environmentName, unlockedSlots = env.slotCount };
                save.environmentSlots.Add(entry);
            }
            entry.unlockedSlots++;
            SaveManager.Instance.Save();

            PlantManager.Instance.AddSlot(envIndex, current);
            OnSlotUnlocked?.Invoke(envIndex);
            return true;
        }
    }
}
