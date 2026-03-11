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
            data.mana = 50f;
            data.gems = 5;

            // Pick 3 random distinct hex positions for starting elements
            int gridRadius = ConfigService.Instance != null
                ? ConfigService.Instance.FlameConfig.GetGridSize(1)
                : 2;
            var positions = HexGridUtil.GetNonCenterPositions(gridRadius);
            HexGridUtil.Shuffle(positions);

            VaseManager.InitializeNewPlayer(data, ConfigService.Instance.VaseConfig.default_capacity);
            data.vases[0].currentWater = data.vases[0].capacity;
            data.vases[0].state = VaseState.Full;
            data.vases[0].gridX = positions[0].q;
            data.vases[0].gridY = positions[0].r;
            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = positions[1].q, gridY = positions[1].r });
            data.apothekeGridX = positions[2].q;
            data.apothekeGridY = positions[2].r;
            ApothekeManager.Instance.AddSeed("Sprouts", 5);
            ApothekeManager.Instance.AddSeed("Cress", 3);
            data.items.Add(new InventoryItem { itemName = "Speed_Potion", count = 3 });
            SaveManager.Instance.Save();
        }
    }
}
