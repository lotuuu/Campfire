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

            foreach (var seed in Resources.LoadAll<SeedData>("Seeds"))
                seeds[seed.seedName] = seed;
        }

        public SeedData GetSeed(string name) => seeds.GetValueOrDefault(name);
        public IEnumerable<SeedData> AllSeeds => seeds.Values;

        public List<SeedData> GetOwnedSeeds()
        {
            var result = new List<SeedData>();
            var added = new System.Collections.Generic.HashSet<string>();
            var save = SaveManager.Instance.Data;

            foreach (var seed in seeds.Values)
            {
                if (seed.infinite)
                {
                    result.Add(seed);
                    added.Add(seed.seedName);
                }
            }

            foreach (var entry in save.seedInventory)
            {
                if (entry.count > 0 && seeds.TryGetValue(entry.seedName, out var seed) && !added.Contains(entry.seedName))
                    result.Add(seed);
            }
            return result;
        }

        public int GetSeedCount(string seedName)
        {
            if (seeds.TryGetValue(seedName, out var seed) && seed.infinite)
                return -1;
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