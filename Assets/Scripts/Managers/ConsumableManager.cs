using System.Collections.Generic;
using UnityEngine;

namespace Garden
{
    public class ConsumableManager : MonoBehaviour
    {
        public static ConsumableManager Instance { get; private set; }

        private readonly List<ConsumableData> _allConsumables = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _allConsumables.AddRange(Resources.LoadAll<ConsumableData>("Consumables"));
        }

        public IReadOnlyList<ConsumableData> AllConsumables => _allConsumables;

        public ConsumableData GetConsumableData(ConsumableType type)
            => _allConsumables.Find(c => c.type == type);

        // ── Inventory ──────────────────────────────────────────────────

        public int GetCount(ConsumableType type)
        {
            var entry = SaveManager.Instance.Data.consumableInventory
                .Find(e => e.consumableType == type.ToString());
            return entry?.count ?? 0;
        }

        public void Add(ConsumableType type, int count = 1)
        {
            var inv = SaveManager.Instance.Data.consumableInventory;
            var entry = inv.Find(e => e.consumableType == type.ToString());
            if (entry != null)
                entry.count += count;
            else
                inv.Add(new ConsumableInventoryEntry { consumableType = type.ToString(), count = count });
            SaveManager.Instance.Save();
        }

        public bool Spend(ConsumableType type)
        {
            var entry = SaveManager.Instance.Data.consumableInventory
                .Find(e => e.consumableType == type.ToString());
            if (entry == null || entry.count <= 0) return false;
            entry.count--;
            SaveManager.Instance.Save();
            return true;
        }

        public bool CanBuy(ConsumableData c)
            => CurrencyManager.Instance.CanAfford(c.currency, c.buyPrice);

        public bool Buy(ConsumableData c)
        {
            if (!CurrencyManager.Instance.Spend(c.currency, c.buyPrice)) return false;
            Add(c.type);
            return true;
        }

        // ── Environment-scoped consumables ─────────────────────────────

        /// <summary>
        /// Returns all ConsumableData currently applied to the given environment.
        /// </summary>
        public List<ConsumableData> GetEnvConsumables(int envIndex)
        {
            var result = new List<ConsumableData>();
            var envList = SaveManager.Instance.Data.environmentConsumables;
            foreach (var save in envList)
            {
                if (save.envIndex != envIndex) continue;
                if (System.Enum.TryParse<ConsumableType>(save.consumableType, out var ctype))
                {
                    var cd = GetConsumableData(ctype);
                    if (cd != null) result.Add(cd);
                }
            }
            return result;
        }

        /// <summary>
        /// Spends one consumable from inventory and applies it to the environment.
        /// Replaces any existing env-scoped consumable on this environment (one per env).
        /// Only env-scoped types allowed. The replaced consumable is discarded.
        /// </summary>
        public bool ApplyToEnvironment(ConsumableType type, int envIndex)
        {
            var cd = GetConsumableData(type);
            if (cd == null || !cd.isEnvironmentScoped) return false;
            if (!Spend(type)) return false;

            var envList = SaveManager.Instance.Data.environmentConsumables;
            // One consumable per environment — discard any existing (regardless of type)
            envList.RemoveAll(e => e.envIndex == envIndex);
            envList.Add(new EnvironmentConsumableSave
            {
                envIndex = envIndex,
                consumableType = type.ToString()
            });
            SaveManager.Instance.Save();

            return true;
        }
    }
}
