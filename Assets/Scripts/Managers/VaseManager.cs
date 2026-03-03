using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class VaseManager : MonoBehaviour
    {
        public static VaseManager Instance { get; private set; }

        [SerializeField] private VaseConfig config;

        public VaseConfig Config => config;

        public event Action OnVasesChanged;

        public void NotifyChanged() => OnVasesChanged?.Invoke();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            CheckFillCompletion();
        }

        public static void InitializeNewPlayer(SaveData data, int baseCapacity, int count = 1)
        {
            for (int i = 0; i < count; i++)
                data.vases.Add(new VaseSave { capacity = baseCapacity, state = VaseState.Empty });
        }

        public static void RainFillAllVases(List<VaseSave> vases)
        {
            foreach (var vase in vases)
            {
                vase.currentWater = vase.capacity;
                vase.state = VaseState.Full;
                vase.fillStartTimeUtc = null;
            }
        }

        private void CheckFillCompletion()
        {
            var data = SaveManager.Instance.Data;
            bool changed = false;
            foreach (var vase in data.vases)
            {
                if (vase.state != VaseState.Filling) continue;
                if (string.IsNullOrEmpty(vase.fillStartTimeUtc)) continue;

                var startTime = DateTime.Parse(vase.fillStartTimeUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var elapsed = GameTime.UtcNow - startTime;

                if (elapsed.TotalMinutes >= config.FillDurationMinutes)
                {
                    vase.currentWater = vase.capacity;
                    vase.state = VaseState.Full;
                    vase.fillStartTimeUtc = null;
                    changed = true;
                }
            }
            if (changed)
            {
                SaveManager.Instance.Save();
                OnVasesChanged?.Invoke();
            }
        }

        public bool InstantFinish(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return false;
            var vase = data.vases[vaseIndex];
            if (vase.state != VaseState.Filling) return false;
            vase.currentWater = vase.capacity;
            vase.state = VaseState.Full;
            vase.fillStartTimeUtc = null;
            SaveManager.Instance.Save();
            OnVasesChanged?.Invoke();
            return true;
        }

        public bool SendToCollect(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return false;
            var vase = data.vases[vaseIndex];
            if (vase.state == VaseState.Filling) return false;

            vase.state = VaseState.Filling;
            vase.fillStartTimeUtc = GameTime.UtcNow.ToString("o");
            SaveManager.Instance.Save();
            OnVasesChanged?.Invoke();
            return true;
        }

        private BuildingCostConfig buildingCostConfig;

        private BuildingCostConfig LoadBuildingCostConfig()
        {
            if (buildingCostConfig == null)
                buildingCostConfig = Resources.Load<BuildingCostConfig>("Config/BuildingCostConfig");
            return buildingCostConfig;
        }

        public BuildingCost GetNextVaseCost()
        {
            return LoadBuildingCostConfig()?.GetVaseCost(SaveManager.Instance.Data.vases.Count);
        }

        public bool CraftVase(int gridX, int gridY)
        {
            if (!FlameManager.Instance.CanPlaceEntity) return false;

            var data = SaveManager.Instance.Data;
            var cost = GetNextVaseCost();
            if (cost == null) return false;

            if (!CurrencyManager.Instance.CanAffordMana(cost.manaCost)) return false;
            if (!MallumManager.CanAffordHarvests(data.items, cost.harvestCosts)) return false;

            CurrencyManager.Instance.SpendMana(cost.manaCost);

            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.items.Find(i => i.itemName == hc.itemName);
                entry.count -= hc.count;
                if (entry.count <= 0) data.items.Remove(entry);
            }

            data.vases.Add(new VaseSave { capacity = config.BaseCapacity, state = VaseState.Empty, gridX = gridX, gridY = gridY });
            SaveManager.Instance.Save();
            OnVasesChanged?.Invoke();
            return true;
        }

        public float GetRemainingSeconds(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return 0f;
            var vase = data.vases[vaseIndex];
            if (vase.state != VaseState.Filling || string.IsNullOrEmpty(vase.fillStartTimeUtc))
                return 0f;

            var startTime = DateTime.Parse(vase.fillStartTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            float elapsedSeconds = (float)(GameTime.UtcNow - startTime).TotalSeconds;
            float totalSeconds = config.FillDurationMinutes * 60f;
            return Mathf.Max(0f, totalSeconds - elapsedSeconds);
        }

        public float GetFillProgress(int vaseIndex)
        {
            var data = SaveManager.Instance.Data;
            if (vaseIndex < 0 || vaseIndex >= data.vases.Count) return 0f;
            var vase = data.vases[vaseIndex];
            if (vase.state != VaseState.Filling || string.IsNullOrEmpty(vase.fillStartTimeUtc))
                return 0f;

            var startTime = DateTime.Parse(vase.fillStartTimeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            var elapsed = (float)(GameTime.UtcNow - startTime).TotalMinutes;
            return Mathf.Clamp01(elapsed / config.FillDurationMinutes);
        }
    }
}
