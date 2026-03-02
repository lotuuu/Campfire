using UnityEngine;

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

        private void InitializeNewPlayer()
        {
            var data = SaveManager.Instance.Data;
            data.mana = 50f;
            VaseManager.InitializeNewPlayer(data, VaseManager.Instance.Config.BaseCapacity);
            data.plots.Add(new PlotSave { state = PlotState.Empty });
            ApothekeManager.Instance.AddSeed("Fern", 3);
            SaveManager.Instance.Save();
        }
    }
}
