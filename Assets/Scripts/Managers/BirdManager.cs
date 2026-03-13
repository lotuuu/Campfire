using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Garden
{
    public class BirdManager : MonoBehaviour
    {
        public static BirdManager Instance { get; private set; }

        public event Action OnBirdPlaced;
        public event Action<BirdSave> OnBirdCollected;

        private static float BaseChance => ConfigService.Instance.BirdConfig.spawn_base_chance;
        private static float HalvingFactor => ConfigService.Instance.BirdConfig.spawn_decay;

        private bool _isChecking;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return;

            // When online and not time-overridden, use server for bird spawning
            if (GameService.Instance != null && GameService.Instance.IsOnline)
            {
                CheckBirdsFromServer();
                return;
            }

            // Offline fallback: local processing
            int gridRadius = FlameManager.Instance != null
                ? FlameManager.Instance.GetGridSize(data.flameLevel)
                : 2;

            var allSeeds = ConfigService.Instance?.GetAllSeeds();
            if (allSeeds == null || allSeeds.Count == 0) return;
            bool changed = ProcessHourlyChecks(data, allSeeds, gridRadius, GameTime.UtcNow);
            if (changed)
            {
                SaveManager.Instance.Save();
                OnBirdPlaced?.Invoke();
                AudioManager.Instance?.PlaySFX("bird_arrive");
            }
        }

        private async void CheckBirdsFromServer()
        {
            if (_isChecking) return;

            // Only check hourly
            var data = SaveManager.Instance.Data;
            var now = GameTime.UtcNow;
            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

            if (!string.IsNullOrEmpty(data.lastBirdCheckHourUtc))
            {
                var lastCheck = DateTime.Parse(data.lastBirdCheckHourUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                if (lastCheck >= currentHour) return;
            }

            _isChecking = true;
            try
            {
                var newBirds = await GameService.Instance.CheckBirds();
                if (newBirds != null && newBirds.Count > 0)
                {
                    foreach (var bird in newBirds)
                    {
                        data.birds.Add(new BirdSave
                        {
                            serverId = bird.id,
                            gridX = bird.gridX,
                            gridY = bird.gridY,
                            itemKey = bird.itemKey,
                            itemCount = bird.itemCount
                        });
                    }
                    SaveManager.Instance.Save();
                    OnBirdPlaced?.Invoke();
                }
                data.lastBirdCheckHourUtc = currentHour.ToString("o");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BirdManager: Server check failed: {e.Message}");
            }
            finally
            {
                _isChecking = false;
            }
        }

        public async Task<BirdSave> CollectBirdFromServer(int birdIndex)
        {
            var data = SaveManager.Instance.Data;
            if (birdIndex < 0 || birdIndex >= data.birds.Count) return null;
            var bird = data.birds[birdIndex];

            if (bird.serverId > 0 && GameService.Instance != null && GameService.Instance.IsOnline)
            {
                var reward = await GameService.Instance.CollectBird(bird.serverId);
                if (reward == null) return null;

                data.birds.RemoveAt(birdIndex);
                ApothekeManager.Instance.AddItem(reward.itemKey, reward.itemCount);
                SaveManager.Instance.Save();
                OnBirdCollected?.Invoke(bird);
                AudioManager.Instance?.PlaySFX("bird_collect");
                return bird;
            }

            // Offline fallback
            var collected = CollectBird(data, birdIndex);
            if (collected != null)
            {
                ApothekeManager.Instance.AddItem(collected.itemKey, collected.itemCount);
                SaveManager.Instance.Save();
                NotifyBirdCollected(collected);
                AudioManager.Instance?.PlaySFX("bird_collect");
            }
            return collected;
        }

        public void NotifyBirdCollected(BirdSave bird)
        {
            OnBirdCollected?.Invoke(bird);
        }

        // --- Static helpers (testable without MonoBehaviour) ---

        public static List<(int q, int r)> GetFreeTiles(SaveData data, int gridRadius)
        {
            var occupied = new HashSet<(int, int)>();

            // Flame is always at (0, 0)
            occupied.Add((0, 0));

            // Plots
            foreach (var plot in data.plots)
                occupied.Add((plot.gridX, plot.gridY));

            // Vases
            foreach (var vase in data.vases)
                occupied.Add((vase.gridX, vase.gridY));

            // Gardens
            foreach (var garden in data.gardens)
                occupied.Add((garden.gridX, garden.gridY));

            // Mallum houses
            foreach (var house in data.mallumHouses)
                occupied.Add((house.gridX, house.gridY));

            // Apotheke
            occupied.Add((data.apothekeGridX, data.apothekeGridY));

            // Birds
            foreach (var bird in data.birds)
                occupied.Add((bird.gridX, bird.gridY));

            // Visitor
            if (data.currentVisitor != null)
                occupied.Add((data.currentVisitor.gridX, data.currentVisitor.gridY));

            var freeTiles = new List<(int q, int r)>();
            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                for (int r = -gridRadius; r <= gridRadius; r++)
                {
                    if (!HexGridUtil.IsWithinRadius(q, r, gridRadius))
                        continue;
                    if (occupied.Contains((q, r)))
                        continue;
                    freeTiles.Add((q, r));
                }
            }

            return freeTiles;
        }

        public static List<ServerSeedConfig> GetEligibleSeeds(List<ServerSeedConfig> allSeeds, int flameLevel)
        {
            var eligible = new List<ServerSeedConfig>();
            foreach (var seed in allSeeds)
            {
                if (seed.tier <= flameLevel)
                    eligible.Add(seed);
            }
            return eligible;
        }

        public static BirdSave RollSeedDrop(List<ServerSeedConfig> eligibleSeeds, int flameLevel)
        {
            if (eligibleSeeds == null || eligibleSeeds.Count == 0)
                return null;

            var seed = eligibleSeeds[UnityEngine.Random.Range(0, eligibleSeeds.Count)];
            int baseCount = Mathf.Max(1, flameLevel - seed.tier + 1);
            int quantity = UnityEngine.Random.Range(
                Mathf.Max(1, baseCount - 1),
                baseCount + 2
            );

            return new BirdSave
            {
                itemKey = seed.item_key,
                itemCount = quantity
            };
        }

        public static bool ProcessHourlyChecks(SaveData data, List<ServerSeedConfig> allSeeds, int gridRadius, DateTime utcNow)
        {
            // Truncate to hour boundary
            var currentHour = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);

            // If lastBirdCheckHourUtc is null/empty, initialize and return false
            if (string.IsNullOrEmpty(data.lastBirdCheckHourUtc))
            {
                data.lastBirdCheckHourUtc = currentHour.ToString("o");
                return false;
            }

            var lastCheck = DateTime.Parse(data.lastBirdCheckHourUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            var eligible = GetEligibleSeeds(allSeeds, data.flameLevel);
            if (eligible.Count == 0)
            {
                data.lastBirdCheckHourUtc = currentHour.ToString("o");
                return false;
            }

            bool anyPlaced = false;

            // Walk from (lastCheck + 1h) to currentHour
            var checkHour = lastCheck.AddHours(1);
            while (checkHour <= currentHour)
            {
                float effectiveChance = BaseChance * Mathf.Pow(HalvingFactor, data.birds.Count);

                if (UnityEngine.Random.value < effectiveChance)
                {
                    var freeTiles = GetFreeTiles(data, gridRadius);
                    if (freeTiles.Count > 0 && eligible.Count > 0)
                    {
                        var tile = freeTiles[UnityEngine.Random.Range(0, freeTiles.Count)];
                        var bird = RollSeedDrop(eligible, data.flameLevel);
                        bird.gridX = tile.q;
                        bird.gridY = tile.r;
                        data.birds.Add(bird);
                        anyPlaced = true;
                    }
                }

                checkHour = checkHour.AddHours(1);
            }

            data.lastBirdCheckHourUtc = currentHour.ToString("o");
            return anyPlaced;
        }

        public static BirdSave CollectBird(SaveData data, int index)
        {
            if (data == null || data.birds == null)
                return null;
            if (index < 0 || index >= data.birds.Count)
                return null;

            var bird = data.birds[index];
            data.birds.RemoveAt(index);
            return bird;
        }
    }
}
