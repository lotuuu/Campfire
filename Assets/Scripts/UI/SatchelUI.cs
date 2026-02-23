using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        [SerializeField] private Transform seedGrid;
        [SerializeField] private GameObject seedSlotPrefab;
        [SerializeField] private GameObject probabilityPanel;
        [SerializeField] private Transform probabilityGrid;
        [SerializeField] private GameObject probabilityEntryPrefab;
        [SerializeField] private Button plantButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI selectedSeedName;

        private SeedData selectedSeed;

        private void OnEnable()
        {
            RefreshGrid();
            probabilityPanel.SetActive(false);
            plantButton.interactable = false;
        }

        private void Start()
        {
            plantButton.onClick.AddListener(OnPlant);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void RefreshGrid()
        {
            foreach (Transform child in seedGrid) Destroy(child.gameObject);

            var seeds = SeedRegistry.Instance.GetOwnedSeeds();
            foreach (var seed in seeds)
            {
                var slot = Instantiate(seedSlotPrefab, seedGrid);
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                slot.GetComponent<SeedSlotUI>().Setup(seed, count, OnSeedSelected);
            }
        }

        private void OnSeedSelected(SeedData seed)
        {
            selectedSeed = seed;
            selectedSeedName.text = seed.seedName;
            plantButton.interactable = true;
            ShowProbabilities(seed);
        }

        private void ShowProbabilities(SeedData seed)
        {
            probabilityPanel.SetActive(true);
            foreach (Transform child in probabilityGrid) Destroy(child.gameObject);

            var weather = WeatherService.Instance.CurrentWeather;
            var probs = GeneticsEngine.GetProbabilities(seed, weather);

            foreach (var (variant, isHigh) in probs)
            {
                var entry = Instantiate(probabilityEntryPrefab, probabilityGrid);
                var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = variant.variantName;
                    text.color = isHigh ? Color.yellow : Color.gray;
                }
            }
        }

        private void OnPlant()
        {
            if (selectedSeed == null || PlantManager.Instance.State != PlantState.Empty) return;
            PlantManager.Instance.Plant(selectedSeed);
            gameObject.SetActive(false);
        }
    }
}
