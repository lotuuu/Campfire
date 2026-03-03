using UnityEngine;
using UnityEngine.InputSystem;

namespace Garden
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 120;
        }

        private void Start()
        {
            if (SaveManager.Instance.Data.vases.Count == 0)
            {
                InitializeNewPlayer();
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current != null &&
                Keyboard.current.gKey.wasPressedThisFrame &&
                Keyboard.current.leftShiftKey.isPressed)
                CurrencyManager.Instance?.GrantInfiniteGems();
#endif
        }

        private void InitializeNewPlayer()
        {
            var data = SaveManager.Instance.Data;
            data.mana = 50f;
            data.gems = 5;
            VaseManager.InitializeNewPlayer(data, VaseManager.Instance.Config.BaseCapacity);
            data.vases[0].currentWater = data.vases[0].capacity;
            data.vases[0].state = VaseState.Full;
            data.vases[0].gridX = 1;
            data.vases[0].gridY = 0;
            data.vases[1].gridX = 0;
            data.vases[1].gridY = 1;
            data.plots.Add(new PlotSave { state = PlotState.Empty, gridX = -1, gridY = 0 });
            ApothekeManager.Instance.AddSeed("Basil", 3);
            // Start with 1 Mallum House
            data.mallumHouses.Add(new MallumHouseSave { gridX = 1, gridY = -1 });
            if (MallumManager.Instance != null)
            {
                int maxMallums = MallumManager.Instance.HouseConfig.GetMaxMallums(data.mallumHouses.Count);
                MallumManager.EnsureMallumCount(data.mallums, maxMallums);
            }
            SaveManager.Instance.Save();
        }
    }
}
