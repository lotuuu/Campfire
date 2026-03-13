using System.Runtime.InteropServices;
using UnityEngine;

namespace Garden
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _WarmUpKeyboard();
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 120;

#if UNITY_IOS && !UNITY_EDITOR
            _WarmUpKeyboard();
#endif
        }

        private void Start()
        {
            if (SocialService.Instance != null)
                SocialService.Instance.OnSignedIn += OnSignedIn;

            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded += CheckNewPlayer;
        }

        private void CheckNewPlayer()
        {
            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded -= CheckNewPlayer;

            // Tutorial-incomplete wipe is handled in GameService.Initialize()
            // before server state is applied (prevents visible position jumps).
            // By this point, if tutorial was incomplete, save is already wiped.
            if (SaveManager.Instance.Data.vases.Count == 0)
                InitializeNewPlayer();
        }

        private void Update()
        {
        }

        private void OnSignedIn()
        {
            EconomyService.Instance?.Initialize();

            // Initialize GameService after EconomyService starts
            if (EconomyService.Instance != null)
            {
                EconomyService.Instance.OnStateSynced += OnEconomySynced;
            }
            else
            {
                // Fallback: init GameService directly if no EconomyService
                GameService.Instance?.Initialize();
            }
        }

        private void OnEconomySynced()
        {
            if (EconomyService.Instance != null)
                EconomyService.Instance.OnStateSynced -= OnEconomySynced;

            GameService.Instance?.Initialize();
        }

        private void InitializeNewPlayer()
        {
            var data = SaveManager.Instance.Data;
            var npc = ConfigService.Instance?.NewPlayerConfig;

            data.mana = npc?.mana ?? 50f;
            data.gems = npc?.gems ?? 5;

            // Pick 3 random distinct hex positions for starting elements (vase, plot, apotheke)
            int gridRadius = ConfigService.Instance != null
                ? ConfigService.Instance.FlameConfig.GetGridSize(1)
                : 2;
            var positions = HexGridUtil.GetNonCenterPositions(gridRadius);
            HexGridUtil.Shuffle(positions);

            VaseManager.InitializeNewPlayer(data, ConfigService.Instance.VaseConfig.default_capacity);
            int startingWater = npc?.startingWater ?? 1;
            data.vases[0].currentWater = startingWater;
            data.vases[0].state = startingWater > 0 ? VaseState.Full : VaseState.Empty;
            data.vases[0].gridX = positions[0].q;
            data.vases[0].gridY = positions[0].r;
            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = positions[1].q, gridY = positions[1].r });
            data.apothekeGridX = positions[2].q;
            data.apothekeGridY = positions[2].r;

            // Grant starting seeds from config (add directly to save data, not via
            // ApothekeManager.AddSeed which enqueues server economy actions — repeated
            // tutorial resets would accumulate seeds on the server)
            if (npc?.seeds != null)
            {
                foreach (var seed in npc.seeds)
                {
                    data.inventory.Add(new InventoryItem { itemName = seed.name + "_Seed", count = seed.count });
                    ApothekeManager.DiscoverSeed(data, seed.name);
                }
            }

            // Grant starting items from config
            if (npc?.items != null)
            {
                foreach (var item in npc.items)
                    data.inventory.Add(new InventoryItem { itemName = item.name, count = item.count });
            }

            // No starting Mallum House — player buys first one after growing seeds
            SaveManager.Instance.Save();
        }
    }
}
