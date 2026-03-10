using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class VaseManager : MonoBehaviour
    {
        public static VaseManager Instance { get; private set; }

        private float FillDurationMinutes => ConfigService.Instance.VaseConfig.fill_duration_minutes;
        private int BaseCapacity => ConfigService.Instance.VaseConfig.default_capacity;

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

                if (elapsed.TotalMinutes >= FillDurationMinutes)
                {
                    vase.currentWater = vase.capacity;
                    vase.state = VaseState.Full;
                    vase.fillStartTimeUtc = null;
                    changed = true;

                    // Notify server
                    if (GameService.Instance != null && GameService.Instance.IsOnline && vase.serverId > 0)
                    {
                        _ = GameService.Instance.CheckVase(vase.serverId);
                    }
                }
            }
            if (changed)
            {
                SaveManager.Instance.Save();
                OnVasesChanged?.Invoke();
                AudioManager.Instance?.PlaySFX("vase_fill_complete");
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

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline && vase.serverId > 0)
                _ = GameService.Instance.InstantFinishVase(vase.serverId);

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

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline && vase.serverId > 0)
            {
                _ = GameService.Instance.FillVase(vase.serverId);
            }

            return true;
        }

        public BuildingCost GetNextVaseCost()
        {
            return ConfigService.Instance?.GetVaseCost(SaveManager.Instance.Data.vases.Count);
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

            if (!CurrencyManager.FreeMode)
            foreach (var hc in cost.harvestCosts)
            {
                var entry = data.items.Find(i => i.itemName == hc.itemName);
                if (entry == null) continue;
                entry.count -= hc.count;
                if (entry.count <= 0) data.items.Remove(entry);
            }

            data.vases.Add(new VaseSave { capacity = BaseCapacity, state = VaseState.Empty, gridX = gridX, gridY = gridY });
            SaveManager.Instance.Save();
            int newIndex = data.vases.Count - 1;
            OnVasesChanged?.Invoke();
            AudioManager.Instance?.PlaySFX("vase_craft");

            // Notify server
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = NotifyServerCraftVase(newIndex, gridX, gridY);
            }

            return true;
        }

        private async Task NotifyServerCraftVase(int vaseIndex, int gridX, int gridY)
        {
            var result = await GameService.Instance.CraftVase(gridX, gridY);
            if (result != null)
            {
                var data = SaveManager.Instance.Data;
                if (vaseIndex < data.vases.Count)
                {
                    data.vases[vaseIndex].serverId = result.id;
                    SaveManager.Instance.Save();
                }
            }
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
            float totalSeconds = FillDurationMinutes * 60f;
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
            return Mathf.Clamp01(elapsed / FillDurationMinutes);
        }
    }
}
