using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CampsiteViewUI : MonoBehaviour
    {
        private VisualElement plotsContainer;
        private VisualElement vasesContainer;
        private VisualElement gardensContainer;
        private VisualElement flameContainer;
        private Label flameLevel;

        private VisualTreeAsset plotTemplate;
        private VisualTreeAsset vaseTemplate;
        private VisualTreeAsset gardenTemplate;

        public void Initialize(VisualElement root)
        {
            plotsContainer = root.Q("plots-container");
            vasesContainer = root.Q("vases-container");
            gardensContainer = root.Q("gardens-container");
            flameContainer = root.Q("flame-container");
            flameLevel = root.Q<Label>("flame-level");

            plotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/PlotItem");
            vaseTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/VaseItem");
            gardenTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GardenItem");

            // Wire flame tap
            var flame = root.Q("flame");
            flame?.RegisterCallback<ClickEvent>(_ => OnFlameTapped());

            // Subscribe to manager events
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += RefreshAll;
            if (PlotManager.Instance != null)
                PlotManager.Instance.OnPlotChanged += _ => RefreshPlots();
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged += RefreshVases;
            if (GardenManager.Instance != null)
                GardenManager.Instance.OnGardenChanged += _ => RefreshGardens();

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= RefreshAll;
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged -= RefreshVases;
        }

        private void Update()
        {
            // Update progress bars for growing plots
            if (PlotManager.Instance == null) return;
            var plots = PlotManager.Instance.Plots;
            for (int i = 0; i < plotsContainer.childCount && i < plots.Count; i++)
            {
                var plot = plots[i];
                if (plot.state == PlotState.Growing)
                {
                    var fill = plotsContainer[i].Q(className: "plot-progress-fill");
                    if (fill != null)
                    {
                        float progress = PlotManager.Instance.GetGrowthProgress(i);
                        fill.style.width = new Length(progress * 100f, LengthUnit.Percent);
                    }
                }
            }
        }

        public void RefreshAll()
        {
            RefreshFlame();
            RefreshPlots();
            RefreshVases();
            RefreshGardens();
        }

        private void RefreshFlame()
        {
            if (flameLevel != null && FlameManager.Instance != null)
                flameLevel.text = $"Lv.{FlameManager.Instance.Level}";
        }

        private void RefreshPlots()
        {
            if (plotsContainer == null || PlotManager.Instance == null) return;
            plotsContainer.Clear();
            var plots = PlotManager.Instance.Plots;
            for (int i = 0; i < plots.Count; i++)
            {
                var el = plotTemplate.CloneTree();
                var plot = plots[i];
                int idx = i;

                var stateLabel = el.Q<Label>(className: "plot-state");
                var actionBtn = el.Q<Button>(className: "plot-action");
                var progressContainer = el.Q(className: "plot-progress");

                if (stateLabel != null) stateLabel.text = plot.state.ToString();

                if (progressContainer != null)
                    progressContainer.style.display = plot.state == PlotState.Growing
                        ? DisplayStyle.Flex : DisplayStyle.None;

                if (actionBtn != null)
                {
                    switch (plot.state)
                    {
                        case PlotState.Empty:
                            actionBtn.text = "Plant";
                            actionBtn.clicked += () => OpenPlotInteraction(idx);
                            break;
                        case PlotState.Planted:
                            actionBtn.text = "Water";
                            actionBtn.clicked += () => PlotManager.Instance.Water(idx);
                            break;
                        case PlotState.Mature:
                            actionBtn.text = "Harvest";
                            actionBtn.clicked += () => PlotManager.Instance.Harvest(idx);
                            break;
                        default:
                            actionBtn.text = plot.state.ToString();
                            actionBtn.SetEnabled(false);
                            break;
                    }
                }

                plotsContainer.Add(el);
            }
        }

        private void RefreshVases()
        {
            if (vasesContainer == null || SaveManager.Instance == null) return;
            vasesContainer.Clear();
            var vases = SaveManager.Instance.Data.vases;
            for (int i = 0; i < vases.Count; i++)
            {
                var el = vaseTemplate.CloneTree();
                var vase = vases[i];
                int idx = i;

                var waterLabel = el.Q<Label>(className: "vase-water");
                var stateLabel = el.Q<Label>(className: "vase-state");
                var actionBtn = el.Q<Button>(className: "vase-action");

                if (waterLabel != null) waterLabel.text = $"{vase.currentWater}/{vase.capacity}";
                if (stateLabel != null) stateLabel.text = vase.state.ToString();

                if (actionBtn != null)
                {
                    if (vase.state == VaseState.Filling)
                    {
                        actionBtn.text = "Collecting...";
                        actionBtn.SetEnabled(false);
                    }
                    else
                    {
                        actionBtn.text = "Send Mallum";
                        actionBtn.clicked += () =>
                        {
                            VaseManager.Instance.SendToCollect(idx);
                            RefreshVases();
                        };
                    }
                }

                vasesContainer.Add(el);
            }
        }

        private void RefreshGardens()
        {
            if (gardensContainer == null || GardenManager.Instance == null) return;
            gardensContainer.Clear();
            var gardens = GardenManager.Instance.Gardens;
            for (int i = 0; i < gardens.Count; i++)
            {
                var el = gardenTemplate.CloneTree();
                var garden = gardens[i];

                var nameLabel = el.Q<Label>(className: "garden-name");
                var stateLabel = el.Q<Label>(className: "garden-state");

                if (nameLabel != null) nameLabel.text = garden.plantName ?? "Empty";
                if (stateLabel != null) stateLabel.text = garden.mature ? "Mature" : "Growing";

                gardensContainer.Add(el);
            }
        }

        private void OpenPlotInteraction(int plotIndex)
        {
            // Open seed selection -- for now plant first available seed
            var data = SaveManager.Instance.Data;
            if (data.seedInventory.Count > 0)
            {
                PlotManager.Instance.Plant(plotIndex, data.seedInventory[0].seedName);
            }
        }

        private void OnFlameTapped()
        {
            if (FlameManager.Instance != null && FlameManager.Instance.CanUpgrade())
            {
                FlameManager.Instance.UpgradeFlame();
            }
        }
    }
}
