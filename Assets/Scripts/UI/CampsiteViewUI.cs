using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CampsiteViewUI : MonoBehaviour
    {
        private VisualElement viewport;
        private VisualElement canvas;
        private VisualElement interactionBackdrop;
        private VisualElement interactionPanel;
        private Label interactionTitle;
        private VisualElement interactionBody;
        private VisualElement interactionActions;

        private VisualTreeAsset cellTemplate;
        private CampsitePanController panController;

        // Curtain transition
        private VisualElement visitTransition;

        private const float HexSize = 220f;     // hex outer radius (pixel spacing)
        private const float CellWidth = 380f;   // cell element width
        private const float CellHeight = 380f;  // cell element height
        private const float GridPadding = 40f;
        private const int ExtraRows = 0;

        // USS custom properties for hex drawing
        private static readonly CustomStyleProperty<Color> s_HexFill = new("--hex-fill");
        private static readonly CustomStyleProperty<Color> s_HexBorder = new("--hex-border");

        // Mode state machine
        private enum CampsiteMode { Normal, Placing, Watering, Visiting }
        private CampsiteMode mode;
        private CampBuildingType pendingBuildingType;
        private int wateringVaseIndex = -1;
        private Button modeCancelBtn;

        // Visit mode
        private VillageSnapshot visitSnapshot;
        private Button visitBackBtn;

        // Grid cell tracking for Update loop
        private readonly List<(VisualElement fill, int plotIndex)> growingPlots = new();
        private readonly List<(VisualElement fill, int vaseIndex)> fillingVases = new();

        // Current grid state
        private int currentGridSize;
        private bool suppressRebuild;

        // Fixed building positions (always present, not saved)
        private static readonly (int q, int r) ApothekeHex = (1, 0);

        // Events
        public event Action OnApothekeTapped;

        public void Initialize(VisualElement root)
        {
            viewport = root.Q("campsite-viewport");
            canvas = root.Q("campsite-canvas");
            interactionBackdrop = root.Q("interaction-backdrop");
            interactionPanel = root.Q("interaction-panel");
            interactionTitle = root.Q<Label>("interaction-title");
            interactionBody = root.Q("interaction-body");
            interactionActions = root.Q("interaction-actions");

            cellTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GridCell");
            visitTransition = root.Q("visit-transition");

            panController = new CampsitePanController(viewport, canvas);

            // Subscribe to manager events
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += RebuildGrid;
            if (PlotManager.Instance != null)
                PlotManager.Instance.OnPlotChanged += _ => RebuildGrid();
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged += RebuildGrid;
            if (GardenManager.Instance != null)
                GardenManager.Instance.OnGardenChanged += _ => RebuildGrid();

            // Tap backdrop to close interaction panel (consumes the tap)
            interactionBackdrop?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                CloseInteractionPanel();
            });

            // Prevent clicks on interaction panel from falling through
            interactionPanel?.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // Defer initial build so viewport has resolved dimensions
            viewport.RegisterCallback<GeometryChangedEvent>(_ => RebuildGrid());
        }

        private void OnDestroy()
        {
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= RebuildGrid;
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged -= RebuildGrid;
        }

        private void Update()
        {
            if (PlotManager.Instance != null)
            {
                foreach (var (fill, plotIndex) in growingPlots)
                {
                    float progress = PlotManager.Instance.GetGrowthProgress(plotIndex);
                    fill.style.width = new Length(progress * 100f, LengthUnit.Percent);
                }
            }

            if (VaseManager.Instance != null)
            {
                foreach (var (fill, vaseIndex) in fillingVases)
                {
                    float progress = VaseManager.Instance.GetFillProgress(vaseIndex);
                    fill.style.width = new Length(progress * 100f, LengthUnit.Percent);
                }
            }
        }

        // ── Grid Building ──

        public void RebuildGrid()
        {
            if (suppressRebuild) return;
            if (canvas == null || FlameManager.Instance == null) return;

            canvas.Clear();
            growingPlots.Clear();
            fillingVases.Clear();
            CloseInteractionPanel();

            // Remove previous cancel button if it exists
            if (modeCancelBtn != null)
            {
                modeCancelBtn.RemoveFromHierarchy();
                modeCancelBtn = null;
            }

            if (mode == CampsiteMode.Visiting && visitSnapshot != null)
            {
                RebuildVisitGrid();
                return;
            }

            int radius = FlameManager.Instance.Config.GetGridSize(FlameManager.Instance.Level);
            currentGridSize = radius;

            // Compute bounding box of all hex cell centers (extended vertically)
            int rExtent = radius + ExtraRows;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-rExtent, -q - rExtent);
                int rMax = Mathf.Min(rExtent, -q + rExtent);
                for (int r = rMin; r <= rMax; r++)
                {
                    var center = HexGridUtil.HexToPixel(q, r, HexSize);
                    if (center.x < minX) minX = center.x;
                    if (center.x > maxX) maxX = center.x;
                    if (center.y < minY) minY = center.y;
                    if (center.y > maxY) maxY = center.y;
                }
            }

            float canvasWidth = (maxX - minX) + CellWidth + GridPadding * 2;
            float canvasHeight = (maxY - minY) + CellHeight + GridPadding * 2;
            canvas.style.width = canvasWidth;
            canvas.style.height = canvasHeight;

            // Offset so that top-left hex center maps to (GridPadding + CellWidth/2, GridPadding + CellHeight/2)
            float offsetX = -minX + GridPadding + CellWidth / 2f;
            float offsetY = -minY + GridPadding + CellHeight / 2f;

            // Build lookup from save data
            var occupied = new Dictionary<(int, int), (CampBuildingType type, int index)>();
            occupied[(0, 0)] = (CampBuildingType.Flame, 0);

            var data = SaveManager.Instance.Data;
            for (int i = 0; i < data.plots.Count; i++)
                occupied[(data.plots[i].gridX, data.plots[i].gridY)] = (CampBuildingType.Plot, i);
            for (int i = 0; i < data.vases.Count; i++)
                occupied[(data.vases[i].gridX, data.vases[i].gridY)] = (CampBuildingType.Vase, i);
            for (int i = 0; i < data.gardens.Count; i++)
                occupied[(data.gardens[i].gridX, data.gardens[i].gridY)] = (CampBuildingType.Garden, i);

            // Fixed buildings always take priority
            occupied[ApothekeHex] = (CampBuildingType.Apotheke, 0);

            // Create hex cells (extended vertically by ExtraRows)
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-rExtent, -q - rExtent);
                int rMax = Mathf.Min(rExtent, -q + rExtent);
                for (int r = rMin; r <= rMax; r++)
                {
                    var el = cellTemplate.CloneTree();
                    var cell = el.Q(className: "grid-cell");
                    if (cell == null) continue;

                    var center = HexGridUtil.HexToPixel(q, r, HexSize);
                    float x = center.x + offsetX - CellWidth / 2f;
                    float y = center.y + offsetY - CellHeight / 2f;
                    cell.style.left = x;
                    cell.style.top = y;

                    var label = cell.Q<Label>(className: "cell-label");
                    var status = cell.Q<Label>(className: "cell-status");
                    var progress = cell.Q(className: "cell-progress");
                    var progressFill = cell.Q(className: "cell-progress-fill");

                    int gx = q;
                    int gy = r;

                    if (occupied.TryGetValue((q, r), out var info))
                    {
                        PopulateOccupiedCell(cell, label, status, progress, progressFill, info.type, info.index);

                        // In watering mode, highlight planted plots as targets
                        if (mode == CampsiteMode.Watering && info.type == CampBuildingType.Plot)
                        {
                            var plot = data.plots[info.index];
                            if (plot.state == PlotState.Growing)
                                cell.AddToClassList("grid-cell--water-target");
                        }

                        int idx = info.index;
                        CampBuildingType cellType = info.type;
                        cell.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (panController.WasDragged) return;
                            evt.StopPropagation();
                            OnCellTapped(gx, gy, cellType, idx);
                        });
                    }
                    else
                    {
                        cell.AddToClassList("grid-cell--empty");
                        if (label != null) label.text = "";
                        if (status != null) status.text = "";

                        if (mode == CampsiteMode.Placing)
                            cell.AddToClassList("grid-cell--placeable");

                        cell.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (panController.WasDragged) return;
                            evt.StopPropagation();
                            OnEmptyCellTapped(gx, gy);
                        });
                    }

                    // Draw hex shape via Painter2D
                    cell.generateVisualContent += DrawHexCell;
                    cell.RegisterCallback<CustomStyleResolvedEvent>(_ => cell.MarkDirtyRepaint());

                    canvas.Add(cell);
                }
            }

            // Cancel button for placing/watering modes
            if (mode == CampsiteMode.Placing || mode == CampsiteMode.Watering)
            {
                string label2 = mode == CampsiteMode.Watering ? "Cancel Watering" : "Cancel";
                modeCancelBtn = new Button(ExitMode) { text = label2 };
                modeCancelBtn.name = "placement-cancel";
                modeCancelBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                viewport.Add(modeCancelBtn);
            }

            var flameCenter = HexGridUtil.HexToPixel(0, 0, HexSize);
            float flameCenterX = flameCenter.x + offsetX;
            float flameCenterY = flameCenter.y + offsetY;
            panController.CenterOnPoint(flameCenterX, flameCenterY, canvasWidth, canvasHeight);
        }

        private void PopulateOccupiedCell(VisualElement cell, Label label, Label status,
            VisualElement progress, VisualElement progressFill,
            CampBuildingType type, int index)
        {
            switch (type)
            {
                case CampBuildingType.Flame:
                    cell.AddToClassList("grid-cell--flame");
                    if (label != null) label.text = $"Lv.{FlameManager.Instance.Level}";
                    if (status != null) status.text = "Spark of Ara";
                    break;

                case CampBuildingType.Plot:
                    cell.AddToClassList("grid-cell--plot");
                    var plot = SaveManager.Instance.Data.plots[index];
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedName) ? "Plot" : plot.seedName;
                    if (status != null) status.text = plot.state.ToString();
                    if (plot.state == PlotState.Mature)
                        cell.AddToClassList("grid-cell--plot-mature");
                    if (plot.state == PlotState.Growing && progress != null && progressFill != null)
                    {
                        progress.AddToClassList("cell-progress--visible");
                        growingPlots.Add((progressFill, index));
                    }
                    break;

                case CampBuildingType.Vase:
                    cell.AddToClassList("grid-cell--vase");
                    var vase = SaveManager.Instance.Data.vases[index];
                    if (label != null) label.text = $"{vase.currentWater}/{vase.capacity}";
                    if (status != null) status.text = vase.state.ToString();
                    if (vase.state == VaseState.Filling && progress != null && progressFill != null)
                    {
                        progress.AddToClassList("cell-progress--visible");
                        fillingVases.Add((progressFill, index));
                    }
                    break;

                case CampBuildingType.Garden:
                    cell.AddToClassList("grid-cell--garden");
                    var garden = SaveManager.Instance.Data.gardens[index];
                    if (label != null) label.text = garden.plantName ?? "Garden";
                    if (status != null) status.text = garden.mature ? "Mature" : "Growing";
                    break;

                case CampBuildingType.Apotheke:
                    cell.AddToClassList("grid-cell--apotheke");
                    if (label != null) label.text = "Apotheke";
                    if (status != null) status.text = "Mixing";
                    break;
            }
        }

        // ── Cell Tap Handlers ──

        private void OnCellTapped(int gridX, int gridY, CampBuildingType type, int index)
        {
            // Watering mode: tap a planted plot to water it
            if (mode == CampsiteMode.Watering)
            {
                if (type == CampBuildingType.Plot)
                {
                    var plot = SaveManager.Instance.Data.plots[index];
                    if (plot.state == PlotState.Growing)
                    {
                        PlotManager.Instance.Water(index);
                        ExitMode();
                    }
                }
                return;
            }

            if (mode == CampsiteMode.Placing) return;

            if (type == CampBuildingType.Apotheke)
            {
                OnApothekeTapped?.Invoke();
                return;
            }

            ShowInteraction(type, index);
        }

        private void OnEmptyCellTapped(int gridX, int gridY)
        {
            if (mode == CampsiteMode.Placing)
            {
                bool success = false;
                switch (pendingBuildingType)
                {
                    case CampBuildingType.Plot:
                        success = PlotManager.Instance.CraftPlot(gridX, gridY);
                        break;
                    case CampBuildingType.Vase:
                        success = VaseManager.Instance.CraftVase(gridX, gridY);
                        break;
                }
                if (success) ExitMode();
                return;
            }

            if (mode != CampsiteMode.Normal) return;
            ShowBuildMenu(gridX, gridY);
        }

        private void ShowBuildMenu(int gridX, int gridY)
        {
            if (interactionPanel == null) return;

            interactionBody.Clear();
            interactionActions.Clear();

            bool canPlace = FlameManager.Instance.CanPlaceEntity;
            int current = FlameManager.Instance.CurrentEntityCount;
            int max = FlameManager.Instance.MaxEntities;
            interactionTitle.text = $"Build ({current}/{max})";

            // Plot option
            if (PlotManager.Instance != null)
            {
                var plotBtn = new Button(() =>
                {
                    if (PlotManager.Instance.CraftPlot(gridX, gridY))
                        CloseInteractionPanel();
                }) { text = "New Plot" };
                plotBtn.AddToClassList("interaction-btn-primary");
                plotBtn.SetEnabled(canPlace);
                interactionActions.Add(plotBtn);
            }

            // Vase option
            if (VaseManager.Instance != null)
            {
                float cost = VaseManager.Instance.Config.CraftCostMana;
                bool canAfford = canPlace && CurrencyManager.Instance.CanAffordMana(cost);
                var vaseBtn = new Button(() =>
                {
                    if (VaseManager.Instance.CraftVase(gridX, gridY))
                        CloseInteractionPanel();
                }) { text = $"New Vase ({cost:F0} Mana)" };
                vaseBtn.AddToClassList("interaction-btn-primary");
                vaseBtn.SetEnabled(canAfford);
                interactionActions.Add(vaseBtn);
            }

            if (!canPlace)
            {
                var hint = new Label("Upgrade flame for more slots");
                hint.AddToClassList("interaction-info");
                interactionBody.Add(hint);
            }

            AddCloseButton();
            ShowInteractionPanel();
        }

        // ── Modes ──

        public void EnterPlacementMode(CampBuildingType type)
        {
            mode = CampsiteMode.Placing;
            pendingBuildingType = type;
            CloseInteractionPanel();
            RebuildGrid();
        }

        private void EnterWateringMode(int vaseIndex)
        {
            mode = CampsiteMode.Watering;
            wateringVaseIndex = vaseIndex;
            CloseInteractionPanel();
            RebuildGrid();
        }

        private void ExitMode()
        {
            mode = CampsiteMode.Normal;
            pendingBuildingType = CampBuildingType.None;
            wateringVaseIndex = -1;
            if (modeCancelBtn != null)
            {
                modeCancelBtn.RemoveFromHierarchy();
                modeCancelBtn = null;
            }
            RebuildGrid();
        }

        // ── Visit Mode ──

        private const float CurtainDurationMs = 400f;

        public void EnterVisitMode(VillageSnapshot snapshot, string friendName = null)
        {
            visitSnapshot = snapshot;
            CloseInteractionPanel();

            // Set curtain label
            if (visitTransition != null)
            {
                var label = visitTransition.Q<Label>("curtain-label");
                if (label != null)
                    label.text = string.IsNullOrEmpty(friendName) ? "Visiting..." : $"Visiting {friendName}...";
            }

            StartCoroutine(VisitTransitionCoroutine(toVisit: true));
        }

        public void ExitVisitMode()
        {
            if (visitTransition != null)
            {
                var label = visitTransition.Q<Label>("curtain-label");
                if (label != null) label.text = "Returning...";
            }
            StartCoroutine(VisitTransitionCoroutine(toVisit: false));
        }

        private IEnumerator VisitTransitionCoroutine(bool toVisit)
        {
            if (visitTransition == null)
            {
                // No transition element — just switch immediately
                ApplyVisitState(toVisit);
                yield break;
            }

            // Show container and close curtains
            visitTransition.style.display = DisplayStyle.Flex;
            // Force a frame so display:flex is applied before adding the class
            yield return null;
            visitTransition.AddToClassList("curtain-closed");

            // Wait for curtains to fully close
            yield return new WaitForSeconds(CurtainDurationMs / 1000f + 0.05f);

            // Swap the content behind the curtains
            ApplyVisitState(toVisit);

            // Let one frame render the new grid
            yield return null;

            // Open curtains
            visitTransition.RemoveFromClassList("curtain-closed");

            // Wait for curtains to fully open, then hide
            yield return new WaitForSeconds(CurtainDurationMs / 1000f + 0.05f);
            visitTransition.style.display = DisplayStyle.None;
        }

        private void ApplyVisitState(bool toVisit)
        {
            if (toVisit)
            {
                mode = CampsiteMode.Visiting;
                RebuildGrid();
            }
            else
            {
                mode = CampsiteMode.Normal;
                visitSnapshot = null;
                if (visitBackBtn != null)
                {
                    visitBackBtn.RemoveFromHierarchy();
                    visitBackBtn = null;
                }
                RebuildGrid();
            }
        }

        private void RebuildVisitGrid()
        {
            // Determine grid radius from the visitor's flame level using the same config
            int radius = FlameManager.Instance != null
                ? FlameManager.Instance.Config.GetGridSize(visitSnapshot.flameLevel)
                : visitSnapshot.flameLevel + 1;
            currentGridSize = radius;

            int rExtent = radius + ExtraRows;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-rExtent, -q - rExtent);
                int rMax = Mathf.Min(rExtent, -q + rExtent);
                for (int r = rMin; r <= rMax; r++)
                {
                    var center = HexGridUtil.HexToPixel(q, r, HexSize);
                    if (center.x < minX) minX = center.x;
                    if (center.x > maxX) maxX = center.x;
                    if (center.y < minY) minY = center.y;
                    if (center.y > maxY) maxY = center.y;
                }
            }

            float canvasWidth = (maxX - minX) + CellWidth + GridPadding * 2;
            float canvasHeight = (maxY - minY) + CellHeight + GridPadding * 2;
            canvas.style.width = canvasWidth;
            canvas.style.height = canvasHeight;

            float offsetX = -minX + GridPadding + CellWidth / 2f;
            float offsetY = -minY + GridPadding + CellHeight / 2f;

            // Build occupied lookup from snapshot data
            var occupied = new Dictionary<(int, int), (CampBuildingType type, int index)>();
            occupied[(0, 0)] = (CampBuildingType.Flame, 0);

            for (int i = 0; i < visitSnapshot.plots.Count; i++)
                occupied[(visitSnapshot.plots[i].gridX, visitSnapshot.plots[i].gridY)] = (CampBuildingType.Plot, i);
            for (int i = 0; i < visitSnapshot.vases.Count; i++)
                occupied[(visitSnapshot.vases[i].gridX, visitSnapshot.vases[i].gridY)] = (CampBuildingType.Vase, i);
            for (int i = 0; i < visitSnapshot.gardens.Count; i++)
                occupied[(visitSnapshot.gardens[i].gridX, visitSnapshot.gardens[i].gridY)] = (CampBuildingType.Garden, i);

            // Fixed buildings always take priority
            occupied[ApothekeHex] = (CampBuildingType.Apotheke, 0);

            // Create hex cells — read-only, no interaction handlers
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-rExtent, -q - rExtent);
                int rMax = Mathf.Min(rExtent, -q + rExtent);
                for (int r = rMin; r <= rMax; r++)
                {
                    var el = cellTemplate.CloneTree();
                    var cell = el.Q(className: "grid-cell");
                    if (cell == null) continue;

                    var center = HexGridUtil.HexToPixel(q, r, HexSize);
                    float x = center.x + offsetX - CellWidth / 2f;
                    float y = center.y + offsetY - CellHeight / 2f;
                    cell.style.left = x;
                    cell.style.top = y;

                    var label = cell.Q<Label>(className: "cell-label");
                    var status = cell.Q<Label>(className: "cell-status");

                    if (occupied.TryGetValue((q, r), out var info))
                    {
                        PopulateVisitCell(cell, label, status, info.type, info.index);
                    }
                    else
                    {
                        cell.AddToClassList("grid-cell--empty");
                        if (label != null) label.text = "";
                        if (status != null) status.text = "";
                    }

                    cell.generateVisualContent += DrawHexCell;
                    cell.RegisterCallback<CustomStyleResolvedEvent>(_ => cell.MarkDirtyRepaint());
                    canvas.Add(cell);
                }
            }

            // "Back to My Camp" button
            visitBackBtn = new Button(ExitVisitMode) { text = "Back to My Camp" };
            visitBackBtn.name = "visit-back-btn";
            visitBackBtn.AddToClassList("interaction-btn-primary");
            visitBackBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            viewport.Add(visitBackBtn);

            var flameCenter = HexGridUtil.HexToPixel(0, 0, HexSize);
            float flameCenterX = flameCenter.x + offsetX;
            float flameCenterY = flameCenter.y + offsetY;
            panController.CenterOnPoint(flameCenterX, flameCenterY, canvasWidth, canvasHeight);
        }

        private void PopulateVisitCell(VisualElement cell, Label label, Label status,
            CampBuildingType type, int index)
        {
            switch (type)
            {
                case CampBuildingType.Flame:
                    cell.AddToClassList("grid-cell--flame");
                    if (label != null) label.text = $"Lv.{visitSnapshot.flameLevel}";
                    if (status != null) status.text = "Spark of Ara";
                    break;

                case CampBuildingType.Plot:
                    cell.AddToClassList("grid-cell--plot");
                    var plot = visitSnapshot.plots[index];
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedName) ? "Plot" : plot.seedName;
                    if (status != null) status.text = plot.state ?? "";
                    break;

                case CampBuildingType.Vase:
                    cell.AddToClassList("grid-cell--vase");
                    var vase = visitSnapshot.vases[index];
                    if (label != null) label.text = $"{vase.currentWater}/{vase.capacity}";
                    if (status != null) status.text = vase.state ?? "";
                    break;

                case CampBuildingType.Garden:
                    cell.AddToClassList("grid-cell--garden");
                    var garden = visitSnapshot.gardens[index];
                    if (label != null) label.text = garden.plantName ?? "Garden";
                    if (status != null) status.text = garden.mature ? "Mature" : "Growing";
                    break;

                case CampBuildingType.Apotheke:
                    cell.AddToClassList("grid-cell--apotheke");
                    if (label != null) label.text = "Apotheke";
                    if (status != null) status.text = "Mixing";
                    break;
            }
        }

        // ── Hex Cell Drawing ──

        private static void DrawHexCell(MeshGenerationContext ctx)
        {
            var el = ctx.visualElement;
            float w = el.resolvedStyle.width;
            float h = el.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            Color fillColor = new Color(0.16f, 0.1f, 0.05f, 0.3f);
            Color borderColor = new Color(0.55f, 0.39f, 0.2f, 0.15f);
            el.customStyle.TryGetValue(s_HexFill, out fillColor);
            el.customStyle.TryGetValue(s_HexBorder, out borderColor);

            float cx = w / 2f;
            float cy = h / 2f;
            // Largest pointy-top regular hex fitting in w x h
            float hexR = Mathf.Min(h / 2f, w / Mathf.Sqrt(3f));

            var painter = ctx.painter2D;

            // Fill
            painter.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i - 90f);
                float vx = cx + hexR * Mathf.Cos(angle);
                float vy = cy + hexR * Mathf.Sin(angle);
                if (i == 0) painter.MoveTo(new Vector2(vx, vy));
                else painter.LineTo(new Vector2(vx, vy));
            }
            painter.ClosePath();
            painter.fillColor = fillColor;
            painter.Fill();

            // Border
            painter.BeginPath();
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i - 90f);
                float vx = cx + hexR * Mathf.Cos(angle);
                float vy = cy + hexR * Mathf.Sin(angle);
                if (i == 0) painter.MoveTo(new Vector2(vx, vy));
                else painter.LineTo(new Vector2(vx, vy));
            }
            painter.ClosePath();
            painter.strokeColor = borderColor;
            painter.lineWidth = el.ClassListContains("grid-cell--flame") ? 3f : 2f;
            painter.Stroke();
        }

        // ── Interaction Panel ──

        private void ShowInteraction(CampBuildingType type, int index)
        {
            if (interactionPanel == null) return;

            interactionBody.Clear();
            interactionActions.Clear();

            switch (type)
            {
                case CampBuildingType.Flame:
                    ShowFlameInteraction();
                    break;
                case CampBuildingType.Plot:
                    ShowPlotInteraction(index);
                    break;
                case CampBuildingType.Vase:
                    ShowVaseInteraction(index);
                    break;
                case CampBuildingType.Garden:
                    ShowGardenInteraction(index);
                    break;
            }

            ShowInteractionPanel();
        }

        private void ShowInteractionPanel()
        {
            if (interactionBackdrop != null)
                interactionBackdrop.style.display = DisplayStyle.Flex;
            interactionPanel.style.display = DisplayStyle.Flex;
        }

        private void ShowFlameInteraction()
        {
            interactionTitle.text = $"Spark of Ara";

            var levelLabel = new Label($"Level {FlameManager.Instance.Level}");
            levelLabel.AddToClassList("interaction-info");
            interactionBody.Add(levelLabel);

            var manaLabel = new Label($"{FlameManager.Instance.ManaPerSecond:F1} Mana per second");
            manaLabel.AddToClassList("interaction-info");
            interactionBody.Add(manaLabel);

            if (FlameManager.Instance.Level >= FlameManager.Instance.Config.MaxLevel)
            {
                var maxLabel = new Label("Max level reached");
                maxLabel.AddToClassList("interaction-info");
                interactionBody.Add(maxLabel);
            }
            else
            {
                var cost = FlameManager.Instance.Config.GetUpgradeCost(FlameManager.Instance.Level);
                var btn = new Button(() =>
                {
                    FlameManager.Instance.UpgradeFlame();
                    CloseInteractionPanel();
                }) { text = $"Level Up ({cost:F0} Mana)" };
                btn.AddToClassList("interaction-btn-primary");
                interactionActions.Add(btn);
            }

            AddCloseButton();
        }

        private void ShowPlotInteraction(int index)
        {
            var plot = SaveManager.Instance.Data.plots[index];

            switch (plot.state)
            {
                case PlotState.Empty:
                    interactionTitle.text = "Empty Plot";
                    var hint = new Label("Choose a seed to plant");
                    hint.AddToClassList("interaction-info");
                    interactionBody.Add(hint);

                    var seeds = SaveManager.Instance.Data.seedInventory;
                    foreach (var seed in seeds)
                    {
                        if (seed.count <= 0) continue;
                        string seedName = seed.seedName;
                        var btn = new Button(() =>
                        {
                            PlotManager.Instance.Plant(index, seedName);
                            CloseInteractionPanel();
                        }) { text = $"{seedName} ({seed.count})" };
                        interactionActions.Add(btn);
                    }
                    break;

                case PlotState.Growing:
                    interactionTitle.text = plot.seedName;
                    float remaining = PlotManager.Instance.GetRemainingSeconds(index);
                    var progressLabel = new Label($"Growing... {FormatTimeRemaining(remaining)} left");
                    progressLabel.AddToClassList("interaction-info");
                    interactionBody.Add(progressLabel);

                    var wateringsLabel = new Label($"Waterings: {plot.waterCount}");
                    wateringsLabel.AddToClassList("interaction-info");
                    interactionBody.Add(wateringsLabel);

                    AddGrowthRecipeSection(plot.seedName);

                    var finishBtn = new Button(() =>
                    {
                        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendGems(1))
                        {
                            PlotManager.Instance.InstantFinish(index);
                            CloseInteractionPanel();
                        }
                    }) { text = "Finish Now (1 Gem)" };
                    finishBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(finishBtn);
                    break;

                case PlotState.Mature:
                    interactionTitle.text = $"{plot.seedName} — Ready!";
                    AddGrowthRecipeSection(plot.seedName);
                    var harvestBtn = new Button(() =>
                    {
                        suppressRebuild = true;
                        var result = PlotManager.Instance.Harvest(index);
                        suppressRebuild = false;
                        if (result != null)
                        {
                            ShowHarvestResult(result);
                            RebuildGrid();
                        }
                        else
                            CloseInteractionPanel();
                    }) { text = "Harvest" };
                    harvestBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(harvestBtn);
                    break;
            }

            AddCloseButton();
        }

        private void ShowHarvestResult(HarvestResult result)
        {
            interactionBody.Clear();
            interactionActions.Clear();

            interactionTitle.text = "Harvested!";

            var yieldLabel = new Label($"{result.seedName} x{result.drops}");
            yieldLabel.AddToClassList("interaction-info");
            interactionBody.Add(yieldLabel);

            string qualityText = result.recipeScore >= 0.8f ? "Excellent"
                : result.recipeScore >= 0.5f ? "Good"
                : "Poor";
            int pct = Mathf.RoundToInt(result.recipeScore * 100f);
            var qualityLabel = new Label($"Recipe match: {qualityText} ({pct}%)");
            qualityLabel.AddToClassList("interaction-info");
            interactionBody.Add(qualityLabel);

            AddCloseButton();
        }

        private void AddGrowthRecipeSection(string seedName)
        {
            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            SeedData seed = null;
            foreach (var s in allSeeds)
            {
                if (s.seedName == seedName) { seed = s; break; }
            }
            if (seed == null || seed.recipe == null) return;

            var recipe = seed.recipe;
            bool hasAny = recipe.useHeat || recipe.useWind || recipe.useHumidity
                || recipe.useSunlight || recipe.useRain || recipe.useMoon || recipe.useWaterings;
            if (!hasAny) return;

            var header = new Label("Growth Recipe");
            header.AddToClassList("interaction-section-header");
            interactionBody.Add(header);

            ApothekeUI.AddRecipeDimensions(interactionBody, recipe);
        }

        private void ShowVaseInteraction(int index)
        {
            var vase = SaveManager.Instance.Data.vases[index];

            switch (vase.state)
            {
                case VaseState.Empty:
                    interactionTitle.text = "Water Vase";
                    var emptyLabel = new Label($"Empty — capacity {vase.capacity}");
                    emptyLabel.AddToClassList("interaction-info");
                    interactionBody.Add(emptyLabel);

                    int available = MallumManager.Instance != null ? MallumManager.Instance.GetAvailableMallumCount() : 1;
                    int total = MallumManager.Instance != null ? MallumManager.Instance.GetTotalMallumCount() : 1;
                    var collectBtn = new Button(() =>
                    {
                        if (MallumManager.Instance != null)
                        {
                            MallumManager.Instance.SendToFetchWater(index);
                        }
                        else
                        {
                            VaseManager.Instance.SendToCollect(index);
                        }
                        CloseInteractionPanel();
                    }) { text = $"Send Mallum ({available}/{total})" };
                    collectBtn.SetEnabled(available > 0);
                    collectBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(collectBtn);
                    break;

                case VaseState.Filling:
                    interactionTitle.text = "Water Vase";
                    float fillRemaining = VaseManager.Instance.GetRemainingSeconds(index);
                    var fillingLabel = new Label($"Mallum is collecting... {FormatTimeRemaining(fillRemaining)} left");
                    fillingLabel.AddToClassList("interaction-info");
                    interactionBody.Add(fillingLabel);

                    var finishVaseBtn = new Button(() =>
                    {
                        if (CurrencyManager.Instance != null && CurrencyManager.Instance.SpendGems(1))
                        {
                            VaseManager.Instance.InstantFinish(index);
                            CloseInteractionPanel();
                        }
                    }) { text = "Finish Now (1 Gem)" };
                    finishVaseBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(finishVaseBtn);
                    break;

                case VaseState.Full:
                    interactionTitle.text = "Water Vase";
                    var fullLabel = new Label($"Water: {vase.currentWater}/{vase.capacity}");
                    fullLabel.AddToClassList("interaction-info");
                    interactionBody.Add(fullLabel);

                    var waterBtn = new Button(() =>
                    {
                        EnterWateringMode(index);
                    }) { text = "Water a plot" };
                    waterBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(waterBtn);
                    break;
            }

            AddCloseButton();
        }

        private void ShowGardenInteraction(int index)
        {
            var garden = SaveManager.Instance.Data.gardens[index];
            interactionTitle.text = garden.plantName ?? "Garden";

            var stateLabel = new Label(garden.mature ? "Mature — yielding fruit" : "Growing...");
            stateLabel.AddToClassList("interaction-info");
            interactionBody.Add(stateLabel);

            AddCloseButton();
        }

        private void AddCloseButton()
        {
            var closeBtn = new Button(CloseInteractionPanel) { text = "Close" };
            interactionActions.Add(closeBtn);
        }

        private static string FormatTimeRemaining(float seconds)
        {
            if (seconds <= 0f) return "0s";
            int totalMinutes = Mathf.CeilToInt(seconds / 60f);
            if (totalMinutes < 60) return $"{totalMinutes}m";
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            return mins > 0 ? $"{hours}h {mins}m" : $"{hours}h";
        }

        private void CloseInteractionPanel()
        {
            if (interactionBackdrop != null)
                interactionBackdrop.style.display = DisplayStyle.None;
            if (interactionPanel != null)
                interactionPanel.style.display = DisplayStyle.None;
        }
    }
}
