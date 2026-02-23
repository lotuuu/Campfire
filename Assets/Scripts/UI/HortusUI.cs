using UnityEngine;

namespace Garden
{
    public class HortusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlantVisual plantVisual;
        [SerializeField] private PulseButton pulseButton;
        [SerializeField] private GameObject satchelPanel;
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private GameObject greenhousePanel;
        [SerializeField] private GameObject debugPanel;

        [Header("Nav Buttons")]
        [SerializeField] private UnityEngine.UI.Button codexButton;
        [SerializeField] private UnityEngine.UI.Button greenhouseButton;
        [SerializeField] private UnityEngine.UI.Button debugButton;

        private void Start()
        {
            pulseButton.OnPulse += OpenSatchel;
            codexButton?.onClick.AddListener(() => TogglePanel(codexPanel));
            greenhouseButton?.onClick.AddListener(() => TogglePanel(greenhousePanel));
            debugButton?.onClick.AddListener(() => TogglePanel(debugPanel));

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged += RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated += OnGrowth;
                RefreshPlantVisual();
            }

            CloseAllPanels();
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnPlantStateChanged -= RefreshPlantVisual;
                PlantManager.Instance.OnGrowthUpdated -= OnGrowth;
            }
        }

        private void RefreshPlantVisual()
        {
            var pm = PlantManager.Instance;
            if (pm.State == PlantState.Empty)
            {
                plantVisual.Clear();
            }
            else
            {
                plantVisual.SetVariant(pm.CurrentVariant);
                plantVisual.SetGrowth(pm.GrowthProgress);
            }
        }

        private void OnGrowth(float progress)
        {
            plantVisual.SetGrowth(progress);
        }

        private void OpenSatchel()
        {
            CloseAllPanels();
            satchelPanel.SetActive(true);
        }

        private void TogglePanel(GameObject panel)
        {
            bool wasActive = panel.activeSelf;
            CloseAllPanels();
            if (!wasActive) panel.SetActive(true);
        }

        private void CloseAllPanels()
        {
            satchelPanel?.SetActive(false);
            codexPanel?.SetActive(false);
            greenhousePanel?.SetActive(false);
            debugPanel?.SetActive(false);
        }
    }
}
