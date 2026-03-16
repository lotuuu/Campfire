using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CampsiteViewUI : MonoBehaviour
    {
        private VisualElement campRoot;
        private VisualElement viewport;
        private VisualElement canvas;
        private VisualElement interactionBackdrop;
        private VisualElement interactionPanel;
        private Label interactionTitle;
        private VisualElement interactionTitleRow;
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
        private enum CampsiteMode { Normal, Placing, Watering, Visiting, Moving, Tutorial }
        private CampsiteMode mode;
        private CampBuildingType pendingBuildingType;
        private int wateringVaseIndex = -1;
        private Button modeCancelBtn;
        private int tutorialTargetQ = int.MinValue;
        private int tutorialTargetR = int.MinValue;

        // Drag-move state
        private IVisualElementScheduledItem longPressTimer;
        private Vector2 longPressStart;
        private bool longPressPending;
        private CampBuildingType dragBuildingType;
        private int dragBuildingIndex;
        private int dragOriginQ, dragOriginR;
        private VisualElement dragGhost;
        private float gridOffsetX, gridOffsetY;
        private readonly Dictionary<(int, int), VisualElement> cellLookup = new();

        public VisualElement GetCellElement(int q, int r)
        {
            return cellLookup.TryGetValue((q, r), out var cell) ? cell : null;
        }

        private int dragPointerId = -1;
        private const float LongPressMs = 400f;
        private const float LongPressMoveThreshold = 10f;

        // Visit mode
        private VillageSnapshot visitSnapshot;
        private Button visitBackBtn;

        // Grid cell tracking for Update loop
        private readonly List<(VisualElement fill, VisualElement cell, string spritePrefix, string skin, int plotIndex)> growingPlots = new();
        private readonly List<(VisualElement fill, int vaseIndex)> fillingVases = new();
        private readonly List<(VisualElement fill, int plotIndex)> cooldownPlots = new();

        // Current grid state
        private int currentGridSize;
        private bool suppressRebuild;
        private bool pendingRebuild;
        private bool needsRecenter = true;

        // Interaction panel live-refresh
        private CampBuildingType? openInteractionType;
        private int openInteractionIndex;
        private VisualElement flameBuildGrid;
        private float lastAffordCheckTime;

        // Live-updating progress bar refs (plot/garden popups)
        private Label progressPctLabel;
        private VisualElement progressBarFill;
        private Label progressTimeLabel;

        // Events
        public event Action OnApothekeTapped;
        public event Action OnVisitorTapped;

        public void Initialize(VisualElement root)
        {
            campRoot = root;
            viewport = root.Q("campsite-viewport");
            canvas = root.Q("campsite-canvas");
            interactionBackdrop = root.Q("interaction-backdrop");
            interactionPanel = root.Q("interaction-panel");
            interactionTitle = root.Q<Label>("interaction-title");

            // Wrap title in a row so bell icon can sit beside it
            interactionTitleRow = new VisualElement();
            interactionTitleRow.AddToClassList("interaction-title-row");
            interactionTitle.parent.Insert(interactionTitle.parent.IndexOf(interactionTitle), interactionTitleRow);
            interactionTitleRow.Add(interactionTitle);

            interactionBody = root.Q("interaction-body");
            interactionActions = root.Q("interaction-actions");

            // Wire interaction close button (X icon)
            var interactionCloseBtn = root.Q<Button>("interaction-close");
            interactionCloseBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                CloseInteractionPanel();
            });

            cellTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/GridCell");
            visitTransition = root.Q("visit-transition");

            panController = new CampsitePanController(viewport, canvas);

            // Viewport pointer handlers for drag-move
            viewport.RegisterCallback<PointerMoveEvent>(OnViewportPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnViewportPointerUp);

            // Subscribe to manager events
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += () =>
                {
                    if (!FlameLevelUpAnimator.IsPlaying) RebuildGrid();
                };
            if (PlotManager.Instance != null)
                PlotManager.Instance.OnPlotChanged += OnPlotChangedRebuild;
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged += RebuildGrid;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged += RebuildGrid;
            if (GardenManager.Instance != null)
                GardenManager.Instance.OnGardenChanged += OnGardenChangedRebuild;
            if (BirdManager.Instance != null)
            {
                BirdManager.Instance.OnBirdPlaced += RebuildGrid;
                BirdManager.Instance.OnBirdCollected += OnBirdCollectedRebuild;
            }
            if (VisitorManager.Instance != null)
            {
                VisitorManager.Instance.OnVisitorArrived += RebuildGrid;
                VisitorManager.Instance.OnVisitorDeparted += RebuildGrid;
            }
            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded += RebuildGrid;

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

        private void OnPlotChangedRebuild(int _) => RebuildGrid();
        private void OnGardenChangedRebuild(int _) => RebuildGrid();
        private void OnBirdCollectedRebuild(BirdSave _) => RebuildGrid();

        private void OnDestroy()
        {
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= RebuildGrid;
            if (PlotManager.Instance != null)
                PlotManager.Instance.OnPlotChanged -= OnPlotChangedRebuild;
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged -= RebuildGrid;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged -= RebuildGrid;
            if (GardenManager.Instance != null)
                GardenManager.Instance.OnGardenChanged -= OnGardenChangedRebuild;
            if (BirdManager.Instance != null)
            {
                BirdManager.Instance.OnBirdPlaced -= RebuildGrid;
                BirdManager.Instance.OnBirdCollected -= OnBirdCollectedRebuild;
            }
            if (VisitorManager.Instance != null)
            {
                VisitorManager.Instance.OnVisitorArrived -= RebuildGrid;
                VisitorManager.Instance.OnVisitorDeparted -= RebuildGrid;
            }
            if (GameService.Instance != null)
                GameService.Instance.OnStateLoaded -= RebuildGrid;
        }

        private void Update()
        {
            if (PlotManager.Instance != null)
            {
                foreach (var (fill, cell, spritePrefix, skin, plotIndex) in growingPlots)
                {
                    float progress = PlotManager.Instance.GetGrowthProgress(plotIndex);
                    fill.style.width = new Length(progress * 100f, LengthUnit.Percent);
                    TrySetHexSpriteByPercent(cell, spritePrefix, progress, skin);
                    cell.EnableInClassList("grid-cell--ready", progress >= 1.0f);
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

            if (cooldownPlots.Count > 0)
            {
                var data = SaveManager.Instance?.Data;
                if (data != null)
                {
                    double totalSeconds = PlotManager.ManualWaterCooldownHours * 3600.0;
                    foreach (var (fill, plotIndex) in cooldownPlots)
                    {
                        double remaining = PlotManager.GetWaterCooldownRemaining(data.plots[plotIndex]);
                        float progress = (float)(1.0 - remaining / totalSeconds);
                        fill.style.width = new Length(progress * 100f, LengthUnit.Percent);
                    }
                }
            }

            // Live-refresh flame build cards so affordability updates as mana ticks
            if (openInteractionType == CampBuildingType.Flame && flameBuildGrid != null
                && Time.time - lastAffordCheckTime >= 0.5f)
            {
                lastAffordCheckTime = Time.time;
                RefreshFlameBuildGrid();
            }

            // Live-refresh growth progress bar in plot/garden popups
            if (progressPctLabel != null && progressBarFill != null)
            {
                float fraction = 0f;
                float remaining = 0f;
                if (openInteractionType == CampBuildingType.Plot && PlotManager.Instance != null)
                {
                    fraction = PlotManager.Instance.GetGrowthProgress(openInteractionIndex);
                    remaining = PlotManager.Instance.GetRemainingSeconds(openInteractionIndex);
                }
                else if (openInteractionType == CampBuildingType.Garden)
                {
                    var data = SaveManager.Instance?.Data;
                    if (data != null && openInteractionIndex < data.gardens.Count)
                    {
                        var g = data.gardens[openInteractionIndex];
                        var gc = ConfigService.Instance?.GetGarden(g.plantName);
                        if (gc != null && !string.IsNullOrEmpty(g.lastYieldTimeUtc))
                        {
                            var lastYield = System.DateTime.Parse(g.lastYieldTimeUtc, null,
                                System.Globalization.DateTimeStyles.RoundtripKind);
                            float elapsed = (float)(GameTime.UtcNow - lastYield).TotalHours;
                            fraction = Mathf.Clamp01(elapsed / gc.yieldIntervalHours);
                            remaining = Mathf.Max(0f, (gc.yieldIntervalHours - elapsed) * 3600f);
                        }
                    }
                }
                progressPctLabel.text = $"{Mathf.RoundToInt(fraction * 100)}%";
                progressBarFill.style.width = new Length(fraction * 100f, LengthUnit.Percent);
                if (progressTimeLabel != null)
                    progressTimeLabel.text = remaining > 0f ? FormatTimeRemaining(remaining) + " remaining" : "";
            }
        }

        // ── Grid Building ──

        public void RebuildGrid()
        {
            if (suppressRebuild) { pendingRebuild = true; return; }
            if (mode == CampsiteMode.Moving) return;
            if (canvas == null || FlameManager.Instance == null) return;

            canvas.Clear();
            growingPlots.Clear();
            fillingVases.Clear();
            cooldownPlots.Clear();
            cellLookup.Clear();

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

            int radius = FlameManager.Instance.GetGridSize();
            if (radius != currentGridSize)
                needsRecenter = true;
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
            gridOffsetX = offsetX;
            gridOffsetY = offsetY;

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
            for (int i = 0; i < data.mallumHouses.Count; i++)
                occupied[(data.mallumHouses[i].gridX, data.mallumHouses[i].gridY)] = (CampBuildingType.MallumHouse, i);
            for (int i = 0; i < data.birds.Count; i++)
                occupied[(data.birds[i].gridX, data.birds[i].gridY)] = (CampBuildingType.Bird, i);
            if (data.currentVisitor != null)
                occupied[(data.currentVisitor.gridX, data.currentVisitor.gridY)] = (CampBuildingType.Visitor, 0);

            // Fixed buildings always take priority
            occupied[(data.apothekeGridX, data.apothekeGridY)] = (CampBuildingType.Apotheke, 0);

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

                    cellLookup[(q, r)] = cell;

                    if (occupied.TryGetValue((q, r), out var info))
                    {
                        PopulateOccupiedCell(cell, label, status, progress, progressFill, info.type, info.index);

                        if (mode == CampsiteMode.Placing)
                            cell.AddToClassList("grid-cell--dimmed");

                        // In watering mode, highlight planted plots as targets or dim non-waterable cells
                        if (mode == CampsiteMode.Watering)
                        {
                            bool isWaterTarget = false;
                            if (info.type == CampBuildingType.Plot)
                            {
                                var plot = data.plots[info.index];
                                if (plot.state == PlotState.Growing)
                                {
                                    double cooldownRemaining = PlotManager.GetWaterCooldownRemaining(plot);
                                    if (cooldownRemaining <= 0)
                                    {
                                        cell.AddToClassList("grid-cell--water-target");
                                        isWaterTarget = true;
                                    }
                                    else
                                    {
                                        cell.AddToClassList("grid-cell--water-cooldown");
                                        if (progress != null && progressFill != null)
                                        {
                                            progress.AddToClassList("cell-progress--visible");
                                            progressFill.AddToClassList("cell-progress-fill--cooldown");
                                            double totalSeconds = PlotManager.ManualWaterCooldownHours * 3600.0;
                                            double remaining = PlotManager.GetWaterCooldownRemaining(data.plots[info.index]);
                                            float cooldownPct = (float)(1.0 - remaining / totalSeconds);
                                            progressFill.style.width = new Length(cooldownPct * 100f, LengthUnit.Percent);
                                            cooldownPlots.Add((progressFill, info.index));
                                        }
                                    }
                                }
                            }
                            if (!isWaterTarget)
                            {
                                cell.AddToClassList("grid-cell--dimmed");
                            }
                        }

                        // Tutorial mode: highlight target hex, dim everything else
                        if (mode == CampsiteMode.Tutorial)
                        {
                            if (q == tutorialTargetQ && r == tutorialTargetR)
                                cell.AddToClassList("grid-cell--tutorial-target");
                            else
                                cell.AddToClassList("grid-cell--dimmed");
                        }

                        int idx = info.index;
                        CampBuildingType cellType = info.type;

                        // Long-press detection on movable buildings
                        bool isMovable = cellType != CampBuildingType.Flame && cellType != CampBuildingType.Bird && cellType != CampBuildingType.Visitor;
                        if (isMovable && mode == CampsiteMode.Normal)
                        {
                            int cq = q, cr = r;
                            cell.RegisterCallback<PointerDownEvent>(evt =>
                            {
                                BeginLongPressDetection(evt, cellType, idx, cq, cr);
                            });
                        }

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
                        TrySetHexSprite(cell, "hex/terrain");
                        if (label != null) label.text = "";
                        if (status != null) status.text = "";

                        if (mode == CampsiteMode.Placing)
                            cell.AddToClassList("grid-cell--placeable");
                        else if (mode == CampsiteMode.Watering)
                            cell.AddToClassList("grid-cell--dimmed");
                        else if (mode == CampsiteMode.Tutorial)
                            cell.AddToClassList("grid-cell--dimmed");

                        cell.RegisterCallback<ClickEvent>(evt =>
                        {
                            if (panController.WasDragged) return;
                            evt.StopPropagation();
                            OnEmptyCellTapped(gx, gy);
                        });
                    }

                    // Draw hex shape via Painter2D (skip for sprite cells)
                    if (!cell.ClassListContains("grid-cell--sprite"))
                    {
                        cell.generateVisualContent += DrawHexCell;
                        cell.RegisterCallback<CustomStyleResolvedEvent>(_ => cell.MarkDirtyRepaint());
                    }

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
                campRoot.Add(modeCancelBtn);
            }

            if (needsRecenter)
            {
                var flameCenter = HexGridUtil.HexToPixel(0, 0, HexSize);
                float flameCenterX = flameCenter.x + offsetX;
                float flameCenterY = flameCenter.y + offsetY;
                panController.CenterOnPoint(flameCenterX, flameCenterY, canvasWidth, canvasHeight);
                needsRecenter = false;
            }
        }

        private void PopulateOccupiedCell(VisualElement cell, Label label, Label status,
            VisualElement progress, VisualElement progressFill,
            CampBuildingType type, int index)
        {
            switch (type)
            {
                case CampBuildingType.Flame:
                    cell.AddToClassList("grid-cell--flame");
                    TrySetHexSprite(cell, "hex/flame");
                    if (label != null) label.text = $"Lv.{FlameManager.Instance.Level}";
                    if (status != null) status.text = "Spark of Ara";
                    break;

                case CampBuildingType.Plot:
                    cell.AddToClassList("grid-cell--plot");
                    var plot = SaveManager.Instance.Data.plots[index];
                    string plotSkin = plot.skinName;
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedItemKey) ? "Plot" : PlotManager.GetSeedDisplayName(plot.seedItemKey);
                    if (status != null) status.text = plot.state.ToString();

                    if (plot.state == PlotState.Empty)
                    {
                        if (!TrySetHexSprite(cell, "hex/plot/empty", plotSkin))
                            ApplySkinColors(cell, plotSkin);
                    }
                    else if (plot.state == PlotState.Growing)
                    {
                        string seed = SeedToSpriteKey(plot.seedItemKey);
                        string spritePrefix = $"hex/plot/{seed}";
                        float growthPct = PlotManager.Instance != null ? PlotManager.Instance.GetGrowthProgress(index) : 0f;
                        if (!TrySetHexSpriteByPercent(cell, spritePrefix, growthPct, plotSkin))
                            ApplySkinColors(cell, plotSkin);
                        if (progress != null && progressFill != null)
                        {
                            progress.AddToClassList("cell-progress--visible");
                            progressFill.style.width = new Length(growthPct * 100f, LengthUnit.Percent);
                            growingPlots.Add((progressFill, cell, spritePrefix, plotSkin, index));
                        }
                    }
                    else if (plot.state == PlotState.Mature)
                    {
                        string seed = SeedToSpriteKey(plot.seedItemKey);
                        if (!TrySetHexSpriteByPercent(cell, $"hex/plot/{seed}", 1f, plotSkin))
                        {
                            ApplySkinColors(cell, plotSkin);
                            cell.AddToClassList("grid-cell--plot-mature");
                        }
                    }
                    break;

                case CampBuildingType.Vase:
                    cell.AddToClassList("grid-cell--vase");
                    var vase = SaveManager.Instance.Data.vases[index];
                    string vaseSkin = vase.skinName;
                    if (label != null) label.text = vase.currentWater >= vase.capacity ? "Full Vase" : vase.currentWater > 0 ? "Vase" : "Empty Vase";
                    if (status != null) status.text = $"{vase.currentWater}/{vase.capacity}";

                    float vasePct = vase.capacity > 0 ? (float)vase.currentWater / vase.capacity : 0f;
                    if (!TrySetHexSpriteByPercent(cell, "hex/vase", vasePct, vaseSkin))
                        ApplySkinColors(cell, vaseSkin);

                    if (vase.state == VaseState.Filling && progress != null && progressFill != null)
                    {
                        progress.AddToClassList("cell-progress--visible");
                        float fillPct = VaseManager.Instance != null ? VaseManager.Instance.GetFillProgress(index) : 0f;
                        progressFill.style.width = new Length(fillPct * 100f, LengthUnit.Percent);
                        fillingVases.Add((progressFill, index));
                    }
                    break;

                case CampBuildingType.Garden:
                    cell.AddToClassList("grid-cell--garden");
                    var garden = SaveManager.Instance.Data.gardens[index];
                    string plant = garden.plantName?.ToLower();
                    if (label != null) label.text = string.IsNullOrEmpty(garden.plantName) ? "Garden" : garden.plantName;
                    if (string.IsNullOrEmpty(garden.plantName))
                    {
                        TrySetHexSprite(cell, "hex/garden/empty");
                        if (status != null) status.text = "Empty";
                    }
                    else if (garden.mature)
                    {
                        TrySetHexSprite(cell, $"hex/garden/{plant}/mature");
                        if (status != null) status.text = "Mature";
                    }
                    else
                    {
                        TrySetHexSprite(cell, $"hex/garden/{plant}/growing");
                        if (status != null) status.text = "Growing";
                    }
                    break;

                case CampBuildingType.Apotheke:
                    cell.AddToClassList("grid-cell--apotheke");
                    TrySetHexSprite(cell, "hex/apotheke");
                    if (label != null) label.text = "Apotheke";
                    if (status != null) status.text = "Mixing";
                    break;

                case CampBuildingType.MallumHouse:
                    cell.AddToClassList("grid-cell--mallum-house");
                    string houseSkin = SaveManager.Instance.Data.mallumHouses[index].skinName;
                    if (!TrySetHexSprite(cell, "hex/house", houseSkin))
                        ApplySkinColors(cell, houseSkin);
                    if (label != null) label.text = "House";
                    if (status != null)
                    {
                        int mallumCount = ConfigService.Instance?.MallumHouseConfig != null
                            ? ConfigService.Instance.MallumHouseConfig.MallumsPerHouse
                            : 1;
                        status.text = $"+{mallumCount} Mallums";
                    }
                    break;

                case CampBuildingType.Bird:
                    cell.AddToClassList("grid-cell--bird");
                    TrySetHexSprite(cell, "hex/bird");
                    var bird = SaveManager.Instance.Data.birds[index];
                    if (label != null) label.text = "Bird";
                    if (status != null) status.text = $"{bird.itemCount}x {ConfigService.Instance.GetItemDisplayName(bird.itemKey)}";
                    break;

                case CampBuildingType.Visitor:
                    cell.AddToClassList("grid-cell--visitor");
                    TrySetHexSprite(cell, "hex/visitor");
                    var visitor = SaveManager.Instance.Data.currentVisitor;
                    if (label != null) label.text = visitor?.visitorName ?? "Visitor";
                    if (status != null)
                    {
                        status.text = visitor?.type switch
                        {
                            VisitorType.Merchant => $"{visitor.offers?.Count ?? 0} trades",
                            VisitorType.Gifter => "Has a gift",
                            VisitorType.Quester => "Has a quest",
                            _ => ""
                        };
                    }
                    break;
            }
        }

        /// <summary>
        /// Tries to set a hex sprite from SpriteService. With skin, tries skin key first.
        /// Returns true if a sprite was set (cell gets grid-cell--sprite class).
        /// Returns false → caller should fall back to colored hex drawing.
        /// </summary>
        private static bool TrySetHexSprite(VisualElement cell, string key, string skinName = null)
        {
            if (SpriteService.Instance == null) return false;

            Texture2D tex = null;

            // Try skin-specific sprite first
            if (!string.IsNullOrEmpty(skinName))
            {
                // key = "hex/plot/empty" → skin key = "hex/plot/skin-{name}/empty"
                int firstSlash = key.IndexOf('/');
                int secondSlash = firstSlash >= 0 ? key.IndexOf('/', firstSlash + 1) : -1;
                if (secondSlash >= 0)
                {
                    string prefix = key[..secondSlash];
                    string suffix = key[secondSlash..];
                    tex = SpriteService.Instance.GetTexture($"{prefix}/skin-{skinName}{suffix}");
                }
            }

            // Fall back to base key
            if (tex == null)
                tex = SpriteService.Instance.GetTexture(key);

            if (tex == null) return false;

            cell.style.backgroundImage = tex;
            cell.AddToClassList("grid-cell--sprite");
            return true;
        }

        private static string SeedToSpriteKey(string seedName) => SpriteService.SeedToSpriteKey(seedName);

        /// <summary>
        /// Picks the best sprite by scanning keys like {prefix}/0, {prefix}/50, {prefix}/100
        /// and choosing the highest numeric threshold ≤ the given percentage.
        /// </summary>
        private static bool TrySetHexSpriteByPercent(VisualElement cell, string prefix, float percent01, string skinName = null)
        {
            if (SpriteService.Instance == null) return false;

            Texture2D tex = null;

            // Try skin-specific sprites first
            if (!string.IsNullOrEmpty(skinName))
            {
                int firstSlash = prefix.IndexOf('/');
                int secondSlash = firstSlash >= 0 ? prefix.IndexOf('/', firstSlash + 1) : -1;
                if (secondSlash >= 0)
                {
                    string skinPrefix = $"{prefix[..secondSlash]}/skin-{skinName}{prefix[secondSlash..]}";
                    tex = SpriteService.Instance.GetTextureByPercentage(skinPrefix, percent01);
                }
            }

            if (tex == null)
                tex = SpriteService.Instance.GetTextureByPercentage(prefix, percent01);

            if (tex == null) return false;

            cell.style.backgroundImage = tex;
            cell.AddToClassList("grid-cell--sprite");
            return true;
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

            // Tutorial mode: only allow tapping the highlighted target cell
            if (mode == CampsiteMode.Tutorial)
            {
                if (gridX != tutorialTargetQ || gridY != tutorialTargetR) return;
                // Let the tap through to normal handling below
                mode = CampsiteMode.Normal;
            }

            if (type == CampBuildingType.Apotheke)
            {
                OnApothekeTapped?.Invoke();
                return;
            }

            if (type == CampBuildingType.Visitor)
            {
                OnVisitorTapped?.Invoke();
                return;
            }

            // Instant harvest: tap a mature plot to harvest immediately
            if (type == CampBuildingType.Plot)
            {
                var plot = SaveManager.Instance.Data.plots[index];
                if (plot.state == PlotState.Mature)
                {
                    _ = HarvestAndShow(index);
                    return;
                }
            }

            ShowInteraction(type, index);
        }

        private void OnEmptyCellTapped(int gridX, int gridY)
        {
            if (mode == CampsiteMode.Tutorial) return;

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
                    case CampBuildingType.MallumHouse:
                        success = MallumManager.Instance.CraftMallumHouse(gridX, gridY);
                        break;
                    case CampBuildingType.Garden:
                        success = GardenManager.Instance.CraftEmptyGarden(gridX, gridY);
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
            interactionTitleRow.style.display = DisplayStyle.Flex;
            interactionActions.style.display = DisplayStyle.Flex;
            ClearBellIcon();
            ClearPaintIcon();

            bool canPlace = FlameManager.Instance.CanPlaceEntity;
            int current = FlameManager.Instance.CurrentEntityCount;
            int max = FlameManager.Instance.MaxEntities;
            interactionTitle.text = $"Build ({current}/{max})";

            var grid = new VisualElement();
            grid.AddToClassList("build-grid");

            // Tutorial filtering: only show allowed building types
            var allowed = TutorialManager.Instance?.GetAllowedBuildings();

            // Plot
            if ((allowed == null || allowed.Contains(CampBuildingType.Plot))
                && PlotManager.Instance != null)
            {
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                if (plotCost != null)
                {
                    bool canAffordPlot = canPlace
                        && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, plotCost.harvestCosts);
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Plot", "Grow seeds", "ui/buildings/plot", null,
                        BuildCardHelper.FromBuildingCost(plotCost), null,
                        canAffordPlot, canPlace, () =>
                        {
                            if (PlotManager.Instance.CraftPlot(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            // Vase (unlocked at flame level 2)
            if ((allowed == null || allowed.Contains(CampBuildingType.Vase))
                && VaseManager.Instance != null)
            {
                bool vaseUnlocked = FlameManager.Instance.Level >= VaseManager.VaseUnlockLevel;
                if (vaseUnlocked)
                {
                    var vaseCost = VaseManager.Instance.GetNextVaseCost();
                    if (vaseCost != null)
                    {
                        bool canAffordVase = canPlace
                            && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                            && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, vaseCost.harvestCosts);
                        grid.Add(BuildCardHelper.CreateBuildCard(
                            "Vase", "Stores water", "ui/buildings/vase", null,
                            BuildCardHelper.FromBuildingCost(vaseCost), null,
                            canAffordVase, canPlace, () =>
                            {
                                if (VaseManager.Instance.CraftVase(gridX, gridY))
                                    CloseInteractionPanel();
                            }));
                    }
                }
                else
                {
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water",
                        "ui/buildings/vase", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{VaseManager.VaseUnlockLevel}"));
                }
            }

            // House
            if ((allowed == null || allowed.Contains(CampBuildingType.MallumHouse))
                && MallumManager.Instance != null)
            {
                var cost = MallumManager.Instance.GetNextHouseCost();
                if (cost != null)
                {
                    bool canAffordHouse = canPlace
                        && CurrencyManager.Instance.CanAffordMana(cost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, cost.harvestCosts);
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(cost), null,
                        canAffordHouse, canPlace, () =>
                        {
                            if (MallumManager.Instance.CraftMallumHouse(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            // Garden
            if ((allowed == null || allowed.Contains(CampBuildingType.Garden))
                && GardenManager.Instance != null)
            {
                bool gardenUnlocked = FlameManager.Instance.Level >= GardenManager.GardenUnlockLevel;
                if (gardenUnlocked)
                {
                    var gardenCost = GardenManager.Instance.GetNextGardenCost();
                    if (gardenCost != null)
                    {
                        bool canAffordGarden = canPlace
                            && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                            && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, gardenCost.harvestCosts);
                        grid.Add(BuildCardHelper.CreateBuildCard(
                            "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                            BuildCardHelper.FromBuildingCost(gardenCost), null,
                            canAffordGarden, canPlace, () =>
                            {
                                if (GardenManager.Instance.CraftEmptyGarden(gridX, gridY))
                                    CloseInteractionPanel();
                            }));
                    }
                }
                else
                {
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees",
                        "ui/buildings/garden", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{GardenManager.GardenUnlockLevel}"));
                }
            }

            interactionBody.Add(grid);

            if (!canPlace)
            {
                var hint = new Label("Upgrade flame for more slots");
                hint.AddToClassList("interaction-info");
                interactionBody.Add(hint);
            }

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
            tutorialTargetQ = int.MinValue;
            tutorialTargetR = int.MinValue;
            if (modeCancelBtn != null)
            {
                modeCancelBtn.RemoveFromHierarchy();
                modeCancelBtn = null;
            }
            RebuildGrid();
        }

        // ── Tutorial Highlight Mode ──

        public void EnterTutorialHighlight(int q, int r)
        {
            tutorialTargetQ = q;
            tutorialTargetR = r;
            mode = CampsiteMode.Tutorial;
            CloseInteractionPanel();
            RebuildGrid();
        }

        public void ExitTutorialHighlight()
        {
            bool wasHighlighting = tutorialTargetQ != int.MinValue || tutorialTargetR != int.MinValue;
            tutorialTargetQ = int.MinValue;
            tutorialTargetR = int.MinValue;
            if (mode == CampsiteMode.Tutorial)
                mode = CampsiteMode.Normal;
            if (wasHighlighting)
                RebuildGrid();
        }

        // ── Visit Mode ──

        private const float CurtainDurationMs = 480f;

        private string visitFriendName;

        public void EnterVisitMode(VillageSnapshot snapshot, string friendName = null)
        {
            visitSnapshot = snapshot;
            visitFriendName = friendName;
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
            var weatherBar = GetComponent<WeatherBarUI>();
            if (toVisit)
            {
                mode = CampsiteMode.Visiting;
                weatherBar?.SetVisitingName(visitFriendName);
                RebuildGrid();
            }
            else
            {
                mode = CampsiteMode.Normal;
                visitSnapshot = null;
                visitFriendName = null;
                weatherBar?.SetVisitingName(null);
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
                ? ConfigService.Instance.FlameConfig.GetGridSize(visitSnapshot.flameLevel)
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

            // Apotheke position from snapshot — (0,0) means unset (old snapshot), fall back to default (1,0)
            bool apoUnset = visitSnapshot.apothekeGridX == 0 && visitSnapshot.apothekeGridY == 0;
            occupied[(apoUnset ? 1 : visitSnapshot.apothekeGridX, apoUnset ? 0 : visitSnapshot.apothekeGridY)] = (CampBuildingType.Apotheke, 0);

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
                        TrySetHexSprite(cell, "hex/terrain");
                        if (label != null) label.text = "";
                        if (status != null) status.text = "";
                    }

                    if (!cell.ClassListContains("grid-cell--sprite"))
                    {
                        cell.generateVisualContent += DrawHexCell;
                        cell.RegisterCallback<CustomStyleResolvedEvent>(_ => cell.MarkDirtyRepaint());
                    }
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
                    TrySetHexSprite(cell, "hex/flame");
                    if (label != null) label.text = $"Lv.{visitSnapshot.flameLevel}";
                    if (status != null) status.text = "Spark of Ara";
                    break;

                case CampBuildingType.Plot:
                    cell.AddToClassList("grid-cell--plot");
                    var plot = visitSnapshot.plots[index];
                    string seed = SeedToSpriteKey(plot.seedItemKey);
                    if (string.IsNullOrEmpty(plot.seedItemKey) || plot.state == "empty")
                        TrySetHexSprite(cell, "hex/plot/empty");
                    else if (plot.state == "mature")
                        TrySetHexSpriteByPercent(cell, $"hex/plot/{seed}", 1f);
                    else
                        TrySetHexSpriteByPercent(cell, $"hex/plot/{seed}", 0f);
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedItemKey) ? "Plot" : PlotManager.GetSeedDisplayName(plot.seedItemKey);
                    if (status != null) status.text = plot.state ?? "";
                    break;

                case CampBuildingType.Vase:
                    cell.AddToClassList("grid-cell--vase");
                    var vase = visitSnapshot.vases[index];
                    float visitVasePct = vase.capacity > 0 ? (float)vase.currentWater / vase.capacity : 0f;
                    TrySetHexSpriteByPercent(cell, "hex/vase", visitVasePct);
                    if (label != null) label.text = $"{vase.currentWater}/{vase.capacity}";
                    if (status != null) status.text = vase.state ?? "";
                    break;

                case CampBuildingType.Garden:
                    cell.AddToClassList("grid-cell--garden");
                    var garden = visitSnapshot.gardens[index];
                    string plant = garden.plantName?.ToLower();
                    if (string.IsNullOrEmpty(garden.plantName))
                        TrySetHexSprite(cell, "hex/garden/empty");
                    else if (garden.mature)
                        TrySetHexSprite(cell, $"hex/garden/{plant}/mature");
                    else
                        TrySetHexSprite(cell, $"hex/garden/{plant}/growing");
                    if (label != null) label.text = string.IsNullOrEmpty(garden.plantName) ? "Garden" : garden.plantName;
                    if (status != null) status.text = string.IsNullOrEmpty(garden.plantName) ? "Empty" : (garden.mature ? "Mature" : "Growing");
                    break;

                case CampBuildingType.Apotheke:
                    cell.AddToClassList("grid-cell--apotheke");
                    TrySetHexSprite(cell, "hex/apotheke");
                    if (label != null) label.text = "Apotheke";
                    if (status != null) status.text = "Mixing";
                    break;
            }
        }

        // ── Hex Cell Drawing ──

        private static void DrawHexCell(MeshGenerationContext ctx)
        {
            var el = ctx.visualElement;
            if (el.ClassListContains("grid-cell--sprite")) return;
            float w = el.resolvedStyle.width;
            float h = el.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            Color fillColor = new Color(0.16f, 0.1f, 0.05f, 0.3f);
            Color borderColor = new Color(0.55f, 0.39f, 0.2f, 0.15f);
            bool hasStateOverride = el.ClassListContains("grid-cell--water-target")
                || el.ClassListContains("grid-cell--water-cooldown")
                || el.ClassListContains("grid-cell--drop-target")
                || el.ClassListContains("grid-cell--drop-hover")
                || el.ClassListContains("grid-cell--placeable");
            if (!hasStateOverride && el.userData is (Color skinFill, Color skinBorder))
            {
                fillColor = skinFill;
                borderColor = skinBorder;
            }
            else
            {
                el.customStyle.TryGetValue(s_HexFill, out fillColor);
                el.customStyle.TryGetValue(s_HexBorder, out borderColor);
            }

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

        private static void ApplySkinColors(VisualElement cell, string skinName)
        {
            if (string.IsNullOrEmpty(skinName) || SkinManager.Instance == null) return;
            var skin = SkinManager.Instance.GetSkin(skinName);
            if (skin != null)
                cell.userData = (skin.hexFillColor, skin.hexBorderColor);
        }

        // ── Interaction Panel ──

        private void ClearBellIcon()
        {
            var existing = interactionTitleRow?.Q(className: "water-subscribe-bell");
            existing?.RemoveFromHierarchy();
        }

        private void ClearPaintIcon()
        {
            var existing = interactionPanel?.Q(className: "interaction-paint");
            existing?.RemoveFromHierarchy();
        }

        private void AddPaintIcon(CampBuildingType type, int index)
        {
            if (SkinManager.Instance == null) return;
            var paintBtn = new Button(() => ShowSkinSelector(type, index));
            paintBtn.AddToClassList("interaction-paint");
            TrySetSprite(paintBtn, "ui/icon-paint-brush");
            interactionPanel.Add(paintBtn);
        }

        private void ShowInteraction(CampBuildingType type, int index)
        {
            if (interactionPanel == null) return;

            interactionBody.Clear();
            interactionActions.Clear();
            interactionTitleRow.style.display = DisplayStyle.Flex;
            interactionActions.style.display = DisplayStyle.Flex;
            interactionPanel.RemoveFromClassList("skin-panel");
            ClearBellIcon();
            ClearPaintIcon();

            openInteractionType = type;
            openInteractionIndex = index;
            flameBuildGrid = null;
            progressPctLabel = null;
            progressBarFill = null;
            progressTimeLabel = null;

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
                case CampBuildingType.MallumHouse:
                    ShowMallumHouseInteraction(index);
                    break;
                case CampBuildingType.Bird:
                    ShowBirdInteraction(index);
                    break;
            }

            ShowInteractionPanel();
        }

        private void ShowInteractionPanel()
        {
            AudioManager.Instance?.PlaySFX("ui_tap");
            if (interactionBackdrop != null)
                interactionBackdrop.style.display = DisplayStyle.Flex;
            interactionPanel.style.display = DisplayStyle.Flex;
        }

        private void ShowFlameInteraction()
        {
            TutorialManager.Instance?.OnFlameMenuOpened();
            interactionTitle.text = $"Spark of Ara";

            var levelLabel = new Label($"Level {FlameManager.Instance.Level}");
            levelLabel.AddToClassList("flame-level-badge");
            interactionBody.Add(levelLabel);

            var manaLabel = new Label($"{FlameManager.Instance.ManaPerSecond:F1} Mana / sec");
            manaLabel.AddToClassList("flame-mana-rate");
            interactionBody.Add(manaLabel);

            // Tutorial: disable upgrade when Flame not in allowed set
            var flameAllowed = TutorialManager.Instance?.GetAllowedBuildings();
            bool upgradeAllowed = flameAllowed == null || flameAllowed.Contains(CampBuildingType.Flame);

            if (FlameManager.Instance.Level >= ConfigService.Instance.FlameConfig.MaxLevel)
            {
                var maxLabel = new Label("Max Level");
                maxLabel.AddToClassList("plot-ready-badge");
                interactionBody.Add(maxLabel);
            }
            else
            {
                var recipe = FlameManager.Instance.GetUpgradeRecipe();
                if (recipe != null && recipe.ingredients.Count > 0)
                {
                    var costList = new VisualElement();
                    costList.AddToClassList("upgrade-cost-list");

                    var costHeader = new Label("UPGRADE COST");
                    costHeader.AddToClassList("upgrade-cost-header");
                    costList.Add(costHeader);

                    var items = SaveManager.Instance.Data.inventory;
                    foreach (var ing in recipe.ingredients)
                    {
                        string displayName = ConfigService.Instance.GetItemDisplayName(ing.itemKey);
                        var item = items.Find(i => i.itemKey == ing.itemKey);
                        int have = item != null ? item.count : 0;
                        bool enough = have >= ing.count;

                        var row = new VisualElement();
                        row.AddToClassList("upgrade-cost-row");

                        var nameLabel = new Label(displayName);
                        nameLabel.AddToClassList("upgrade-cost-name");
                        row.Add(nameLabel);

                        var amountLabel = new Label($"{have}/{ing.count}");
                        amountLabel.AddToClassList("upgrade-cost-amount");
                        amountLabel.AddToClassList(enough ? "upgrade-cost-amount--have" : "upgrade-cost-amount--need");
                        row.Add(amountLabel);

                        costList.Add(row);
                    }
                    interactionBody.Add(costList);
                }

                // Show what next level unlocks
                int nextLevel = FlameManager.Instance.Level + 1;
                var fc = ConfigService.Instance.FlameConfig;
                float nextMana = fc.GetManaPerSecond(nextLevel);
                float currMana = FlameManager.Instance.ManaPerSecond;
                if (nextMana > currMana)
                {
                    var benefitLabel = new Label($"+{(nextMana - currMana):F1} mana/sec at Lv {nextLevel}");
                    benefitLabel.AddToClassList("interaction-info-highlight");
                    interactionBody.Add(benefitLabel);
                }
                int nextEntities = fc.GetMaxEntities(nextLevel);
                int currEntities = fc.GetMaxEntities(FlameManager.Instance.Level);
                if (nextEntities > currEntities)
                {
                    var capLabel = new Label($"+{nextEntities - currEntities} build slots");
                    capLabel.AddToClassList("interaction-info");
                    interactionBody.Add(capLabel);
                }
                if (nextLevel == GardenManager.GardenUnlockLevel)
                {
                    var unlockLabel = new Label("Unlocks Gardens!");
                    unlockLabel.AddToClassList("interaction-info-highlight");
                    interactionBody.Add(unlockLabel);
                }

                bool canAfford = upgradeAllowed && FlameManager.Instance.CanUpgrade();
                var upgradeBtn = new Button(() =>
                {
                    if (FlameLevelUpAnimator.IsPlaying) return;
                    var flameCellEl = canvas?.Q(className: "grid-cell--flame");
                    FlameManager.Instance.UpgradeFlame();
                    CloseInteractionPanel();
                    FlameLevelUpAnimator.Play(campRoot, flameCellEl, canvas, FlameManager.Instance.Level, RebuildGrid);
                })
                { text = "Level Up" };
                upgradeBtn.SetEnabled(canAfford);
                upgradeBtn.AddToClassList("upgrade-btn");
                interactionBody.Add(upgradeBtn);
            }

            // ── Craft / Build section ──
            AddFlameCraftItems();
        }

        private void AddFlameCraftItems()
        {
            // ── Section divider ──
            var divider = new VisualElement();
            divider.AddToClassList("flame-section-divider");
            interactionBody.Add(divider);

            // ── Build header with entity cap ──
            var headerRow = new VisualElement();
            headerRow.AddToClassList("flame-build-header");

            var buildLabel = new Label("BUILD");
            buildLabel.AddToClassList("upgrade-cost-header");
            headerRow.Add(buildLabel);

            bool canPlaceEntity = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;

            if (FlameManager.Instance != null)
            {
                int current = FlameManager.Instance.CurrentEntityCount;
                int max = FlameManager.Instance.MaxEntities;

                var capBadge = new VisualElement();
                capBadge.AddToClassList("flame-cap-badge");
                if (!canPlaceEntity) capBadge.AddToClassList("flame-cap-badge--full");

                var capLabel = new Label($"{current}/{max}");
                capLabel.AddToClassList("flame-cap-label");
                capBadge.Add(capLabel);

                headerRow.Add(capBadge);
            }

            interactionBody.Add(headerRow);

            var grid = new VisualElement();
            grid.AddToClassList("build-grid");
            flameBuildGrid = grid;

            // Tutorial: disable cards for building types not in allowed set
            var allowed = TutorialManager.Instance?.GetAllowedBuildings();

            // Plot
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool plotAllowed = allowed == null || allowed.Contains(CampBuildingType.Plot);
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = plotAllowed && canPlaceEntity && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, plotCost.harvestCosts);
                grid.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "ui/buildings/plot", null,
                    BuildCardHelper.FromBuildingCost(plotCost), null,
                    canAfford, plotAllowed && canPlaceEntity, () =>
                    {
                        CloseInteractionPanel();
                        EnterPlacementMode(CampBuildingType.Plot);
                    }));
            }

            // Vase (unlocked at flame level 2)
            if (VaseManager.Instance != null)
            {
                bool vaseAllowed = allowed == null || allowed.Contains(CampBuildingType.Vase);
                bool vaseUnlocked = FlameManager.Instance != null
                    && FlameManager.Instance.Level >= VaseManager.VaseUnlockLevel;
                if (vaseUnlocked)
                {
                    var vaseCost = VaseManager.Instance.GetNextVaseCost();
                    bool canAfford = vaseAllowed && canPlaceEntity && vaseCost != null
                        && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, vaseCost.harvestCosts);
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water", "ui/buildings/vase", null,
                        BuildCardHelper.FromBuildingCost(vaseCost), null,
                        canAfford, vaseAllowed && canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.Vase);
                        }));
                }
                else
                {
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water",
                        "ui/buildings/vase", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{VaseManager.VaseUnlockLevel}"));
                }
            }

            // House
            if (MallumManager.Instance != null)
            {
                bool houseAllowed = allowed == null || allowed.Contains(CampBuildingType.MallumHouse);
                var nextCost = MallumManager.Instance.GetNextHouseCost();
                if (nextCost != null)
                {
                    bool canAfford = houseAllowed && canPlaceEntity
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, nextCost.harvestCosts);
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), null,
                        canAfford, houseAllowed && canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.MallumHouse);
                        }));
                }
            }

            // Garden
            if (GardenManager.Instance != null && FlameManager.Instance != null)
            {
                bool gardenAllowed = allowed == null || allowed.Contains(CampBuildingType.Garden);
                bool gardenUnlocked = FlameManager.Instance.Level >= GardenManager.GardenUnlockLevel;
                if (gardenUnlocked)
                {
                    var gardenCost = GardenManager.Instance.GetNextGardenCost();
                    if (gardenCost != null)
                    {
                        bool canAfford = gardenAllowed && canPlaceEntity
                            && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                            && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, gardenCost.harvestCosts);
                        grid.Add(BuildCardHelper.CreateBuildCard(
                            "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                            BuildCardHelper.FromBuildingCost(gardenCost), null,
                            canAfford, gardenAllowed && canPlaceEntity, () =>
                            {
                                CloseInteractionPanel();
                                EnterPlacementMode(CampBuildingType.Garden);
                            }));
                    }
                }
                else
                {
                    grid.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees",
                        "ui/buildings/garden", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{GardenManager.GardenUnlockLevel}"));
                }
            }

            interactionBody.Add(grid);
        }

        private void RefreshFlameBuildGrid()
        {
            if (flameBuildGrid == null) return;
            flameBuildGrid.Clear();

            bool canPlaceEntity = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;

            // Tutorial: disable cards for building types not in allowed set
            var allowed = TutorialManager.Instance?.GetAllowedBuildings();

            // Plot
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                bool plotAllowed = allowed == null || allowed.Contains(CampBuildingType.Plot);
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = plotAllowed && canPlaceEntity && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, plotCost.harvestCosts);
                flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "ui/buildings/plot", null,
                    BuildCardHelper.FromBuildingCost(plotCost), null,
                    canAfford, plotAllowed && canPlaceEntity, () =>
                    {
                        CloseInteractionPanel();
                        EnterPlacementMode(CampBuildingType.Plot);
                    },
                    null));
            }

            // Vase (unlocked at flame level 2)
            if (VaseManager.Instance != null)
            {
                bool vaseAllowed = allowed == null || allowed.Contains(CampBuildingType.Vase);
                bool vaseUnlocked = FlameManager.Instance != null
                    && FlameManager.Instance.Level >= VaseManager.VaseUnlockLevel;
                if (vaseUnlocked)
                {
                    var vaseCost = VaseManager.Instance.GetNextVaseCost();
                    bool canAfford = vaseAllowed && canPlaceEntity && vaseCost != null
                        && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, vaseCost.harvestCosts);
                    flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water", "ui/buildings/vase", null,
                        BuildCardHelper.FromBuildingCost(vaseCost), null,
                        canAfford, vaseAllowed && canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.Vase);
                        },
                        null));
                }
                else
                {
                    flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water",
                        "ui/buildings/vase", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{VaseManager.VaseUnlockLevel}"));
                }
            }

            // House
            if (MallumManager.Instance != null)
            {
                bool houseAllowed = allowed == null || allowed.Contains(CampBuildingType.MallumHouse);
                var nextCost = MallumManager.Instance.GetNextHouseCost();
                if (nextCost != null)
                {
                    bool canAfford = houseAllowed && canPlaceEntity
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, nextCost.harvestCosts);
                    flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), null,
                        canAfford, houseAllowed && canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.MallumHouse);
                        },
                        null));
                }
            }

            // Garden
            if (GardenManager.Instance != null && FlameManager.Instance != null)
            {
                bool gardenAllowed = allowed == null || allowed.Contains(CampBuildingType.Garden);
                bool gardenUnlocked = FlameManager.Instance.Level >= GardenManager.GardenUnlockLevel;
                if (gardenUnlocked)
                {
                    var gardenCost = GardenManager.Instance.GetNextGardenCost();
                    if (gardenCost != null)
                    {
                        bool canAfford = gardenAllowed && canPlaceEntity
                            && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                            && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.inventory, gardenCost.harvestCosts);
                        flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                            "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                            BuildCardHelper.FromBuildingCost(gardenCost), null,
                            canAfford, gardenAllowed && canPlaceEntity, () =>
                            {
                                CloseInteractionPanel();
                                EnterPlacementMode(CampBuildingType.Garden);
                            },
                            null));
                    }
                }
                else
                {
                    flameBuildGrid.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees",
                        "ui/buildings/garden", null,
                        null, null, false, false, null,
                        $"Unlocks at Fire Lv.{GardenManager.GardenUnlockLevel}"));
                }
            }
        }

        private void ShowPlotInteraction(int index)
        {
            var plot = SaveManager.Instance.Data.plots[index];

            switch (plot.state)
            {
                case PlotState.Empty:
                    interactionTitle.text = "Choose a Seed";
                    BuildSeedPicker(index);
                    break;

                case PlotState.Growing:
                    interactionTitle.text = PlotManager.GetSeedDisplayName(plot.seedItemKey);

                    // Growth progress bar
                    float growthFraction = PlotManager.Instance.GetGrowthProgress(index);
                    float remaining = PlotManager.Instance.GetRemainingSeconds(index);
                    AddGrowthProgressBar(growthFraction, remaining);

                    // Waterings count
                    var wateringsLabel = new Label($"Waterings: {plot.waterCount}");
                    wateringsLabel.AddToClassList("interaction-info");
                    interactionBody.Add(wateringsLabel);

                    // Fertilized status
                    if (plot.fertilized)
                    {
                        var fertLabel = new Label("Fertilized - +50% yield");
                        fertLabel.AddToClassList("interaction-info-highlight");
                        interactionBody.Add(fertLabel);
                    }

                    // Applied potions
                    if (plot.potions != null && plot.potions.Count > 0)
                    {
                        var appliedNames = string.Join(", ",
                            plot.potions.Select(p => ConfigService.Instance?.GetItemDisplayName(p) ?? p));
                        var appliedLabel = new Label($"Potions: {appliedNames}");
                        appliedLabel.AddToClassList("interaction-info");
                        interactionBody.Add(appliedLabel);
                    }

                    AddWaterSubscribeToggle(index, plot);
                    AddGrowthRecipeSection(plot.seedItemKey);

                    // Fertilize button (only when not yet fertilized)
                    if (!plot.fertilized)
                    {
                        int fertCount = PlotManager.Instance != null ? PlotManager.Instance.GetFertilizerCount() : 0;
                        var fertBtn = new Button(() =>
                        {
                            _ = FertilizePlotAndRefresh(index);
                        })
                        { text = $"Fertilize ({fertCount})" };
                        fertBtn.SetEnabled(fertCount > 0 || CurrencyManager.FreeMode);
                        fertBtn.AddToClassList("interaction-btn-primary");
                        interactionActions.Add(fertBtn);
                    }

                    // Speed-up button (only when player has items)
                    int plotPotionCount = PlotManager.Instance != null ? PlotManager.Instance.GetSpeedItemCount() : 0;
                    if (plotPotionCount > 0 || CurrencyManager.FreeMode)
                    {
                        var finishBtn = new Button(() => _ = SpeedUpAndHarvest(index))
                        { text = $"Speed Up ({plotPotionCount})" };
                        finishBtn.SetEnabled(true);
                        finishBtn.AddToClassList("interaction-btn-primary");
                        interactionActions.Add(finishBtn);
                    }

                    // Weather potion buttons
                    AddWeatherPotionButtons(index, plot);

                    break;

                case PlotState.Mature:
                    interactionTitle.text = PlotManager.GetSeedDisplayName(plot.seedItemKey);

                    // Yield preview
                    var seed = ConfigService.Instance?.GetSeed(plot.seedItemKey);
                    if (seed != null)
                    {
                        string dropName = ConfigService.Instance.GetItemDisplayName(seed.harvest_item_key) ?? seed.harvest_item_key;
                        var yieldPreview = new Label($"{dropName} x{seed.minDrops}-{seed.maxDrops}");
                        yieldPreview.AddToClassList("plot-yield-preview");
                        interactionBody.Add(yieldPreview);
                    }

                    // Ready badge
                    var readyBadge = new Label("Ready to Harvest!");
                    readyBadge.AddToClassList("plot-ready-badge");
                    interactionBody.Add(readyBadge);

                    if (plot.fertilized)
                    {
                        var fertNote = new Label("Fertilized - +50% yield");
                        fertNote.AddToClassList("interaction-info-highlight");
                        interactionBody.Add(fertNote);
                    }

                    AddGrowthRecipeSection(plot.seedItemKey);

                    var harvestBtn = new Button(() =>
                    {
                        _ = HarvestAndShow(index);
                    })
                    { text = "Harvest" };
                    harvestBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(harvestBtn);
                    break;
            }

            AddPaintIcon(CampBuildingType.Plot, index);

        }

        private async Task SpeedUpAndHarvest(int plotIndex)
        {
            if (PlotManager.Instance == null) return;

            // Show loading — speed-up + harvest both need server
            interactionBody.Clear();
            interactionActions.Clear();
            interactionTitle.text = "Finishing...";
            ShowInteractionPanel();

            bool success = await PlotManager.Instance.SpeedUpGrowth(plotIndex);
            if (!success)
            {
                CloseInteractionPanel();
                return;
            }

            await HarvestAndShow(plotIndex);
        }

        private async Task FertilizePlotAndRefresh(int plotIndex)
        {
            if (PlotManager.Instance == null) return;
            bool success = await PlotManager.Instance.Fertilize(plotIndex);
            if (success)
            {
                RebuildGrid();
                ShowInteraction(CampBuildingType.Plot, plotIndex);
            }
        }

        private static readonly HashSet<string> NonWeatherPotionKeys = new()
        {
            "fertilizer", "speed_potion", "energy_drink"
        };

        private void AddWeatherPotionButtons(int plotIndex, PlotSave plot)
        {
            var inventory = SaveManager.Instance.Data.inventory;
            string speedItem = ConfigService.Instance?.PlotConfig?.speed_item;
            var appliedSet = new HashSet<string>(plot.potions ?? new List<string>());

            foreach (var item in inventory)
            {
                if (item.count <= 0) continue;
                if (!item.itemKey.EndsWith("_potion")) continue;
                if (NonWeatherPotionKeys.Contains(item.itemKey)) continue;
                if (speedItem != null && item.itemKey == speedItem) continue;
                if (appliedSet.Contains(item.itemKey)) continue;

                string potionKey = item.itemKey;
                string displayName = ConfigService.Instance?.GetItemDisplayName(potionKey) ?? potionKey;
                var btn = new Button(() =>
                {
                    PlotManager.Instance?.ApplyPotion(plotIndex, potionKey);
                    ShowInteraction(CampBuildingType.Plot, plotIndex);
                })
                { text = $"{displayName} ({item.count})" };
                btn.AddToClassList("interaction-btn-primary");
                interactionActions.Add(btn);
            }
        }

        private async Task HarvestAndShow(int plotIndex)
        {
            // Show loading state if no cached preview (server call will block)
            var plot = SaveManager.Instance.Data.plots[plotIndex];
            bool hasCachedPreview = plot.cachedHarvestPreview != null;
            if (!hasCachedPreview)
            {
                interactionBody.Clear();
                interactionActions.Clear();
                interactionTitle.text = "Harvesting...";
                ShowInteractionPanel();
            }

            suppressRebuild = true;
            var result = await PlotManager.Instance.Harvest(plotIndex);
            suppressRebuild = false;

            if (result != null)
            {
                RebuildGrid();
                ShowHarvestResult(result);
                ShowInteractionPanel();
            }
            else
            {
                // Server unreachable — show waiting state
                interactionBody.Clear();
                interactionActions.Clear();
                interactionTitle.text = "Waiting for server...";
                ShowInteractionPanel();

                // Block until we can resync
                if (GameService.Instance != null)
                    await GameService.Instance.ResyncFullState();
                RebuildGrid();
                CloseInteractionPanel();
            }
        }

        private void ShowHarvestResult(HarvestResult result)
        {
            // Suppress grid rebuilds while harvest results are displayed so that
            // another crop maturing doesn't auto-close this panel.
            suppressRebuild = true;

            interactionBody.Clear();
            interactionActions.Clear();
            ClearBellIcon();
            ClearPaintIcon();

            interactionTitleRow.style.display = DisplayStyle.None;
            interactionActions.style.display = DisplayStyle.None;
            AudioManager.Instance?.PlaySFX("harvest_reveal");

            // ── Hero: large item icon with tier-colored glow ring ──
            var heroContainer = new VisualElement();
            heroContainer.AddToClassList("harvest-hero");

            int tier = 0;
            string plantSlug = SpriteService.SeedToSpriteKey(result.seedItemKey ?? "");
            var seedData = ConfigService.Instance?.GetSeed(plantSlug);
            if (seedData != null) tier = seedData.tier;

            var glowRing = new VisualElement();
            glowRing.AddToClassList("harvest-glow-ring");
            glowRing.AddToClassList($"harvest-glow--tier{Mathf.Min(tier, 4)}");
            heroContainer.Add(glowRing);

            string harvestSpriteKey = SpriteService.ItemToSpriteKey(result.harvestItemKey);
            var harvestSprite = harvestSpriteKey != null ? SpriteService.Instance?.GetSprite(harvestSpriteKey) : null;
            var heroIcon = new VisualElement();
            heroIcon.AddToClassList("harvest-hero-icon");
            if (harvestSprite != null)
                heroIcon.style.backgroundImage = new StyleBackground(harvestSprite);
            heroContainer.Add(heroIcon);
            interactionBody.Add(heroContainer);

            // ── Yield: item name + count ──
            int baseDrops = result.drops - result.bonusDrops;
            string itemName = ConfigService.Instance.GetItemDisplayName(result.harvestItemKey);

            var yieldName = new Label(itemName);
            yieldName.AddToClassList("harvest-result-name");
            interactionBody.Add(yieldName);

            var yieldRow = new VisualElement();
            yieldRow.AddToClassList("harvest-yield-row");
            var yieldCount = new Label($"x{baseDrops}");
            yieldCount.AddToClassList("harvest-yield-count");
            yieldRow.Add(yieldCount);
            if (result.bonusDrops > 0)
            {
                var bonusLabel = new Label($"+{result.bonusDrops}");
                bonusLabel.AddToClassList("harvest-bonus-label");
                yieldRow.Add(bonusLabel);
            }
            interactionBody.Add(yieldRow);

            // ── Quality badge + percentage ──
            string matchText = result.recipeScore >= 0.8f ? "Perfect Match"
                : result.recipeScore >= 0.5f ? "Good Match"
                : "Weak Match";
            string matchClass = result.recipeScore >= 0.8f ? "harvest-match--perfect"
                : result.recipeScore >= 0.5f ? "harvest-match--good"
                : "harvest-match--weak";
            int pct = Mathf.RoundToInt(result.recipeScore * 100f);

            var matchContainer = new VisualElement();
            matchContainer.AddToClassList("harvest-match-container");
            var matchLabel = new Label(matchText);
            matchLabel.AddToClassList("harvest-match-badge");
            matchLabel.AddToClassList(matchClass);
            matchContainer.Add(matchLabel);
            var matchPct = new Label($"{pct}%");
            matchPct.AddToClassList("harvest-match-pct");
            matchPct.AddToClassList(matchClass);
            matchContainer.Add(matchPct);
            interactionBody.Add(matchContainer);

            // ── Animated quality bar ──
            var qualityTrack = new VisualElement();
            qualityTrack.AddToClassList("harvest-quality-track");
            var qualityFill = new VisualElement();
            qualityFill.AddToClassList("harvest-quality-fill");
            qualityFill.AddToClassList(matchClass);
            qualityTrack.Add(qualityFill);
            interactionBody.Add(qualityTrack);

            // ── Per-axis breakdown (staggered cascade) ──
            var axisContainer = new VisualElement();
            axisContainer.AddToClassList("harvest-axis-container");

            if (result.recipe != null)
            {
                var axisResults = result.recipe.EvaluatePerAxis(result.snapshots, result.waterCount);
                if (axisResults.Count > 0)
                {
                    var header = new Label("Recipe Breakdown");
                    header.AddToClassList("harvest-axis-header");
                    axisContainer.Add(header);

                    foreach (var axis in axisResults)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("harvest-axis-row");

                        var nameEl = new Label(axis.axisName);
                        nameEl.AddToClassList("harvest-axis-name");
                        row.Add(nameEl);

                        string actualStr;
                        string idealStr;
                        if (axis.axisName == "Moon")
                        {
                            actualStr = $"{Mathf.RoundToInt(axis.actual)}%";
                            idealStr = axis.unit.Replace("% ", "");
                        }
                        else if (axis.axisName == "Waterings")
                        {
                            actualStr = $"{Mathf.RoundToInt(axis.actual)}{axis.unit}";
                            idealStr = axis.idealMin == axis.idealMax
                                ? $"{Mathf.RoundToInt(axis.idealMin)}{axis.unit}"
                                : $"{Mathf.RoundToInt(axis.idealMin)}-{Mathf.RoundToInt(axis.idealMax)}{axis.unit}";
                        }
                        else
                        {
                            actualStr = $"{axis.actual:F0}{axis.unit}";
                            idealStr = $"{axis.idealMin:F0}-{axis.idealMax:F0}{axis.unit}";
                        }

                        var actualEl = new Label(actualStr);
                        actualEl.AddToClassList("harvest-axis-actual");
                        row.Add(actualEl);

                        var idealEl = new Label($"({idealStr})");
                        idealEl.AddToClassList("harvest-axis-ideal");
                        row.Add(idealEl);

                        bool passed = axis.score >= 0.5f;
                        var statusEl = new VisualElement();
                        statusEl.AddToClassList(passed ? "harvest-axis-pass-icon" : "harvest-axis-fail-icon");
                        var statusTex = SpriteService.Instance?.GetTexture(passed ? "ui/icon-check" : "ui/icon-cross");
                        if (statusTex != null)
                            statusEl.style.backgroundImage = statusTex;
                        row.Add(statusEl);

                        axisContainer.Add(row);
                    }
                }
            }
            interactionBody.Add(axisContainer);

            // ── Staggered animation sequence ──
            heroContainer.schedule.Execute(() =>
                heroContainer.AddToClassList("harvest-hero--visible"));
            yieldName.schedule.Execute(() =>
                yieldName.AddToClassList("harvest-reveal--visible")).StartingIn(200);
            yieldRow.schedule.Execute(() =>
                yieldRow.AddToClassList("harvest-reveal--visible")).StartingIn(250);
            matchContainer.schedule.Execute(() =>
                matchContainer.AddToClassList("harvest-reveal--visible")).StartingIn(400);
            qualityFill.schedule.Execute(() =>
                qualityFill.style.width = new Length(pct, LengthUnit.Percent)).StartingIn(500);
            // Axis rows cascade in one by one
            int axisDelay = 600;
            for (int i = 0; i < axisContainer.childCount; i++)
            {
                var child = axisContainer[i];
                int delay = axisDelay + i * 80;
                child.schedule.Execute(() =>
                    child.AddToClassList("harvest-reveal--visible")).StartingIn(delay);
            }

            // "tap to close" hint at bottom + make entire panel close on tap
            var tapHint = new Label("tap anywhere to close");
            tapHint.AddToClassList("harvest-tap-hint");
            interactionBody.Add(tapHint);
            int hintDelay = axisDelay + axisContainer.childCount * 80 + 200;
            tapHint.schedule.Execute(() =>
                tapHint.AddToClassList("harvest-reveal--visible")).StartingIn(hintDelay);

            interactionPanel.RegisterCallback<ClickEvent>(OnHarvestTapToClose);
        }

        private void OnHarvestTapToClose(ClickEvent evt)
        {
            interactionPanel.UnregisterCallback<ClickEvent>(OnHarvestTapToClose);
            CloseInteractionPanel();
        }

        private void BuildSeedPicker(int plotIndex)
        {
            var seedItems = SaveManager.Instance.Data.inventory.FindAll(i =>
                ConfigService.Instance?.GetItem(i.itemKey)?.category == "seed");
            seedItems.Sort((a, b) =>
            {
                var sa = ConfigService.Instance?.GetSeed(SpriteService.SeedToSpriteKey(a.itemKey));
                var sb = ConfigService.Instance?.GetSeed(SpriteService.SeedToSpriteKey(b.itemKey));
                int tierCmp = (sa?.tier ?? 99).CompareTo(sb?.tier ?? 99);
                if (tierCmp != 0) return tierCmp;
                return (sa?.growthDurationHours ?? 99f).CompareTo(sb?.growthDurationHours ?? 99f);
            });

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("seed-picker-scroll");

            var list = new VisualElement();
            list.AddToClassList("seed-picker-list");

            foreach (var entry in seedItems)
            {
                if (entry.count <= 0) continue;

                var plantSlug = SpriteService.SeedToSpriteKey(entry.itemKey); // "basil_seed" → "basil"
                var seedData = ConfigService.Instance?.GetSeed(plantSlug);

                string capturedItemKey = entry.itemKey;
                var card = new Button(() =>
                {
                    PlotManager.Instance.Plant(plotIndex, capturedItemKey);
                    CloseInteractionPanel();
                });
                card.AddToClassList("seed-card");

                // Tier accent — left border color
                card.style.borderLeftColor = GetTierColor(seedData?.tier ?? 1);

                // Hero seed icon
                string spriteKey = seedData != null
                    ? SpriteService.SeedToSpriteKey(seedData.item_key)
                    : plantSlug;

                var seedIcon = new VisualElement();
                seedIcon.AddToClassList("seed-card--icon");
                var seedSprite = SpriteService.Instance?.GetSprite($"items/{spriteKey}/seed");
                if (seedSprite != null)
                    seedIcon.style.backgroundImage = new StyleBackground(seedSprite);
                card.Add(seedIcon);

                // Info column
                var info = new VisualElement();
                info.AddToClassList("seed-card--info");

                // Title row: name + stats + count
                var titleRow = new VisualElement();
                titleRow.AddToClassList("seed-card--title-row");
                var nameLabel = new Label(seedData != null ? ConfigService.Instance.GetItemDisplayName(seedData.item_key) : plantSlug);
                nameLabel.AddToClassList("seed-card--name");
                titleRow.Add(nameLabel);

                var rightGroup = new VisualElement();
                rightGroup.AddToClassList("seed-card--right-group");

                if (seedData != null)
                {
                    string growthStr = TimeUtils.FormatDurationHours(seedData.growthDurationHours);
                    var statsLabel = new Label($"{growthStr} | {seedData.minDrops}-{seedData.maxDrops} drops");
                    statsLabel.AddToClassList("seed-card--stats-line");
                    rightGroup.Add(statsLabel);
                }

                var countLabel = new Label($"x{entry.count}");
                countLabel.AddToClassList("seed-card--count");
                rightGroup.Add(countLabel);

                titleRow.Add(rightGroup);
                info.Add(titleRow);

                if (seedData != null)
                {

                    // Recipe tags (no header)
                    if (seedData.recipe != null)
                    {
                        var tags = new VisualElement();
                        tags.AddToClassList("seed-card--recipe-tags");

                        if (seedData.recipe.useHeat)
                            AddRecipeTag(tags, $"Heat {seedData.recipe.idealTempMin}-{seedData.recipe.idealTempMax}\u00b0C");
                        if (seedData.recipe.useWind)
                            AddRecipeTag(tags, $"Wind {seedData.recipe.idealWindMin}-{seedData.recipe.idealWindMax}m/s");
                        if (seedData.recipe.useHumidity)
                            AddRecipeTag(tags, $"Humid {seedData.recipe.idealHumidityMin}-{seedData.recipe.idealHumidityMax}%");
                        if (seedData.recipe.useSunlight)
                            AddRecipeTag(tags, $"Sun {seedData.recipe.idealSunlightMin}-{seedData.recipe.idealSunlightMax}%");
                        if (seedData.recipe.useRain)
                        {
                            int minPct = Mathf.RoundToInt(seedData.recipe.idealRainMin * 100f);
                            int maxPct = Mathf.RoundToInt(seedData.recipe.idealRainMax * 100f);
                            AddRecipeTag(tags, $"Rain {minPct}-{maxPct}%");
                        }
                        if (seedData.recipe.useMoon)
                            AddRecipeTag(tags, seedData.recipe.requiredMoonPhase.ToString());
                        if (seedData.recipe.useWaterings)
                        {
                            string waterTag = seedData.recipe.idealWateringsMin == seedData.recipe.idealWateringsMax
                                ? $"Water x{seedData.recipe.idealWateringsMin}"
                                : $"Water x{seedData.recipe.idealWateringsMin}-{seedData.recipe.idealWateringsMax}";
                            AddRecipeTag(tags, waterTag);
                        }

                        if (tags.childCount > 0)
                            info.Add(tags);
                    }

                    // Weather match indicator (only for perfect match on seeds with weather axes)
                    if (seedData.recipe != null && HasWeatherAxes(seedData.recipe)
                        && WeatherService.Instance != null && WeatherService.Instance.HasWeather)
                    {
                        float match = GetCurrentWeatherMatch(seedData.recipe);
                        if (match >= 1f)
                        {
                            var matchTag = new Label("Weather Match!");
                            matchTag.AddToClassList("seed-card--weather-match");
                            info.Add(matchTag);
                        }
                    }
                }

                card.Add(info);
                list.Add(card);
            }

            scroll.Add(list);
            interactionBody.Add(scroll);
        }

        private void BuildGardenPlantPicker(int gardenIndex)
        {
            var inventory = SaveManager.Instance.Data.inventory;

            // Only show garden plants the player has seeds for
            var availableGardens = new List<(ServerGardenConfig config, InventoryItem seedEntry)>();
            foreach (var plantData in ConfigService.Instance.GetAllGardens())
            {
                string seedKey = GardenManager.GetSeedItemKey(plantData.plantName);
                var seedEntry = inventory.Find(i => i.itemKey == seedKey && i.count > 0);
                if (seedEntry != null || CurrencyManager.FreeMode)
                    availableGardens.Add((plantData, seedEntry));
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("seed-picker-scroll");

            var list = new VisualElement();
            list.AddToClassList("seed-picker-list");

            foreach (var (plantData, seedEntry) in availableGardens)
            {
                string pName = plantData.plantName;
                bool canAfford = CurrencyManager.Instance != null
                    && CurrencyManager.Instance.CanAffordWater(plantData.waterRequired);

                var card = new Button(() =>
                {
                    if (GardenManager.Instance.Plant(gardenIndex, pName))
                    {
                        CloseInteractionPanel();
                        RebuildGrid();
                    }
                });
                card.AddToClassList("seed-card");
                card.SetEnabled(canAfford);

                // Green accent for gardens
                card.style.borderLeftColor = new Color(0.3f, 0.55f, 0.35f, 0.8f);

                // Plant icon
                string plantSlug = pName.ToLower();
                var plantIcon = new VisualElement();
                plantIcon.AddToClassList("seed-card--icon");
                var sprite = SpriteService.Instance?.GetSprite($"hex/garden/{plantSlug}/mature");
                if (sprite != null)
                    plantIcon.style.backgroundImage = new StyleBackground(sprite);
                card.Add(plantIcon);

                // Info column
                var info = new VisualElement();
                info.AddToClassList("seed-card--info");

                // Title row: name + stats
                var titleRow = new VisualElement();
                titleRow.AddToClassList("seed-card--title-row");
                var nameLabel = new Label(pName);
                nameLabel.AddToClassList("seed-card--name");
                titleRow.Add(nameLabel);

                var rightGroup = new VisualElement();
                rightGroup.AddToClassList("seed-card--right-group");

                string growthStr = TimeUtils.FormatDurationHours(plantData.growthDurationHours);
                string yieldStr = $"Yields {plantData.yieldAmount} every {TimeUtils.FormatDurationHours(plantData.yieldIntervalHours)}";
                var statsLabel = new Label($"{growthStr} | {yieldStr}");
                statsLabel.AddToClassList("seed-card--stats-line");
                rightGroup.Add(statsLabel);

                var countLabel = new Label($"x{seedEntry?.count ?? 0}");
                countLabel.AddToClassList("seed-card--count");
                rightGroup.Add(countLabel);

                titleRow.Add(rightGroup);
                info.Add(titleRow);

                // Cost tags
                var tags = new VisualElement();
                tags.AddToClassList("seed-card--recipe-tags");
                AddRecipeTag(tags, $"Water x{plantData.waterRequired}");
                if (plantData.manaCost > 0)
                    AddRecipeTag(tags, $"Mana {plantData.manaCost:0}");
                info.Add(tags);

                card.Add(info);
                list.Add(card);
            }

            scroll.Add(list);
            interactionBody.Add(scroll);
        }

        private static Color GetTierColor(int tier)
        {
            return tier switch
            {
                1 => new Color(0.45f, 0.6f, 0.3f, 0.8f),   // green — common
                2 => new Color(0.3f, 0.5f, 0.65f, 0.8f),   // blue — uncommon
                3 => new Color(0.6f, 0.4f, 0.7f, 0.8f),    // purple — rare
                4 => new Color(0.8f, 0.65f, 0.2f, 0.8f),   // gold — legendary
                _ => new Color(0.45f, 0.6f, 0.3f, 0.8f),
            };
        }

        private static void AddRecipeTag(VisualElement container, string text)
        {
            var tag = new VisualElement();
            tag.AddToClassList("seed-card--tag");
            tag.Add(new Label(text));
            container.Add(tag);
        }

        private void AddWaterSubscribeToggle(int plotIndex, PlotSave plot)
        {
            var bellIcon = new VisualElement();
            bellIcon.AddToClassList("water-subscribe-bell");
            UpdateBellIcon(bellIcon, plot.subscribeWater);

            bellIcon.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                bool newValue = !plot.subscribeWater;
                PlotManager.Instance.SetWaterSubscription(plotIndex, newValue);
                UpdateBellIcon(bellIcon, newValue);
            });

            interactionTitleRow.Add(bellIcon);
        }

        private static void UpdateBellIcon(VisualElement icon, bool active)
        {
            string path = active ? "UI/Icons/bell-on" : "UI/Icons/bell-off";
            var vi = Resources.Load<VectorImage>(path);
            if (vi != null)
                icon.style.backgroundImage = new StyleBackground(Background.FromVectorImage(vi));
        }

        private void AddGrowthRecipeSection(string seedItemKey)
        {
            var seed = ConfigService.Instance?.GetSeed(SpriteService.SeedToSpriteKey(seedItemKey));
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
            interactionTitle.text = "Water Vase";

            // Water level bar (shared across all states)
            // Use actual water level, not just state — state can lag behind after watering
            float waterFraction = vase.state == VaseState.Filling
                ? VaseManager.Instance.GetFillProgress(index)
                : vase.capacity > 0 ? (float)vase.currentWater / vase.capacity : 0f;
            AddWaterLevelBar(waterFraction, vase.currentWater, vase.capacity, vase.state);

            switch (vase.state)
            {
                case VaseState.Empty:
                    var emptyLabel = new Label("Empty");
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
                        RebuildGrid();
                        ShowInteraction(CampBuildingType.Vase, index);
                    })
                    { text = available > 0 ? $"Send Mallum to Fill" : "No Mallums Available" };
                    collectBtn.SetEnabled(available > 0);
                    collectBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(collectBtn);

                    if (available > 0)
                    {
                        var mallumInfo = new Label($"{available} of {total} Mallums idle");
                        mallumInfo.AddToClassList("interaction-info");
                        interactionBody.Add(mallumInfo);
                    }
                    break;

                case VaseState.Filling:
                    float fillRemaining = VaseManager.Instance.GetRemainingSeconds(index);
                    var fillingLabel = new Label($"Mallum fetching water - {FormatTimeRemaining(fillRemaining)}");
                    fillingLabel.AddToClassList("interaction-info-highlight");
                    interactionBody.Add(fillingLabel);

                    int vaseDrinkCount = MallumManager.Instance != null ? MallumManager.Instance.GetVaseSpeedItemCount() : 0;
                    int fetchingMallumIndex = -1;
                    if (MallumManager.Instance != null)
                    {
                        var mallums = SaveManager.Instance.Data.mallums;
                        for (int mi = 0; mi < mallums.Count; mi++)
                        {
                            if (mallums[mi].state == MallumState.FetchingWater && mallums[mi].assignedVaseIndex == index)
                            { fetchingMallumIndex = mi; break; }
                        }
                    }
                    int capturedMallumIdx = fetchingMallumIndex;
                    if (vaseDrinkCount > 0 || CurrencyManager.FreeMode)
                    {
                        var finishVaseBtn = new Button(() =>
                        {
                            if (MallumManager.Instance != null && capturedMallumIdx >= 0 && MallumManager.Instance.SpeedUpWaterFetch(capturedMallumIdx))
                            {
                                RebuildGrid();
                                ShowInteraction(CampBuildingType.Vase, index);
                            }
                        })
                        { text = $"Speed Up ({vaseDrinkCount})" };
                        finishVaseBtn.SetEnabled(fetchingMallumIndex >= 0 || CurrencyManager.FreeMode);
                        finishVaseBtn.AddToClassList("interaction-btn-primary");
                        interactionActions.Add(finishVaseBtn);
                    }
                    break;

                case VaseState.HasWater:
                    bool actuallyFull = vase.currentWater >= vase.capacity;
                    if (actuallyFull)
                    {
                        var fullLabel = new Label("Full! Ready to water your plants.");
                        fullLabel.AddToClassList("interaction-info-highlight");
                        interactionBody.Add(fullLabel);
                    }
                    else if (vase.currentWater > 0)
                    {
                        var partialLabel = new Label($"{vase.currentWater} water remaining");
                        partialLabel.AddToClassList("interaction-info");
                        interactionBody.Add(partialLabel);
                    }
                    else
                    {
                        var depletedLabel = new Label("Empty - send a Mallum to refill");
                        depletedLabel.AddToClassList("interaction-info");
                        interactionBody.Add(depletedLabel);
                    }

                    if (vase.currentWater > 0)
                    {
                        var waterBtn = new Button(() =>
                        {
                            EnterWateringMode(index);
                        })
                        { text = "Water a Plant" };
                        waterBtn.AddToClassList("interaction-btn-primary");
                        interactionActions.Add(waterBtn);
                    }

                    // Offer refill if not full
                    if (!actuallyFull)
                    {
                        int refillAvailable = MallumManager.Instance != null ? MallumManager.Instance.GetAvailableMallumCount() : 0;
                        var refillBtn = new Button(() =>
                        {
                            if (MallumManager.Instance != null)
                                MallumManager.Instance.SendToFetchWater(index);
                            else
                                VaseManager.Instance.SendToCollect(index);
                            RebuildGrid();
                            ShowInteraction(CampBuildingType.Vase, index);
                        })
                        { text = "Send Mallum to Refill" };
                        refillBtn.SetEnabled(refillAvailable > 0);
                        interactionActions.Add(refillBtn);
                    }
                    break;
            }

            AddPaintIcon(CampBuildingType.Vase, index);
        }

        private void AddWaterLevelBar(float fraction, int current, int capacity, VaseState state)
        {
            // Water counter
            var waterCount = new Label($"{current} / {capacity}");
            waterCount.AddToClassList("vase-water-count");
            interactionBody.Add(waterCount);

            // Level bar track
            var barTrack = new VisualElement();
            barTrack.AddToClassList("vase-level-track");
            var barFill = new VisualElement();
            barFill.AddToClassList("vase-level-fill");
            if (state == VaseState.Filling)
                barFill.AddToClassList("vase-level-fill--filling");
            else if (state == VaseState.HasWater)
                barFill.AddToClassList("vase-level-fill--full");
            barFill.style.width = new Length(fraction * 100f, LengthUnit.Percent);
            barTrack.Add(barFill);
            interactionBody.Add(barTrack);
        }

        private void AddGrowthProgressBar(float fraction, float remainingSeconds)
        {
            // Progress percentage
            var pctLabel = new Label($"{Mathf.RoundToInt(fraction * 100)}%");
            pctLabel.AddToClassList("growth-progress-pct");
            interactionBody.Add(pctLabel);

            // Progress bar
            var barTrack = new VisualElement();
            barTrack.AddToClassList("growth-progress-track");
            var barFill = new VisualElement();
            barFill.AddToClassList("growth-progress-fill");
            barFill.style.width = new Length(fraction * 100f, LengthUnit.Percent);
            barTrack.Add(barFill);
            interactionBody.Add(barTrack);

            // Time remaining
            var timeLabel = new Label(remainingSeconds > 0f
                ? FormatTimeRemaining(remainingSeconds) + " remaining"
                : "");
            timeLabel.AddToClassList("interaction-info");
            interactionBody.Add(timeLabel);

            // Store refs for live updates
            progressPctLabel = pctLabel;
            progressBarFill = barFill;
            progressTimeLabel = timeLabel;
        }

        private static void TrySetSprite(VisualElement el, string spriteKey)
        {
            if (el == null || SpriteService.Instance == null) return;
            var tex = SpriteService.Instance.GetTexture(spriteKey);
            if (tex != null)
                el.style.backgroundImage = tex;
        }

        private static bool HasWeatherAxes(GrowthRecipe recipe)
        {
            return recipe.useHeat || recipe.useWind || recipe.useHumidity;
        }

        private static float GetCurrentWeatherMatch(GrowthRecipe recipe)
        {
            if (WeatherService.Instance == null || !WeatherService.Instance.HasWeather) return 0f;
            var w = WeatherService.Instance.CurrentWeather;

            float weightSum = 0f;
            float scoreSum = 0f;

            if (recipe.useHeat)
            {
                scoreSum += GrowthRecipe.ScoreRange(w.temperature, recipe.idealTempMin, recipe.idealTempMax, recipe.heatTolerance) * recipe.heatWeight;
                weightSum += recipe.heatWeight;
            }
            if (recipe.useWind)
            {
                scoreSum += GrowthRecipe.ScoreRange(w.windSpeed, recipe.idealWindMin, recipe.idealWindMax, recipe.windTolerance) * recipe.windWeight;
                weightSum += recipe.windWeight;
            }
            if (recipe.useHumidity)
            {
                scoreSum += GrowthRecipe.ScoreRange(w.humidity, recipe.idealHumidityMin, recipe.idealHumidityMax, recipe.humidityTolerance) * recipe.humidityWeight;
                weightSum += recipe.humidityWeight;
            }

            return weightSum > 0f ? scoreSum / weightSum : 1f;
        }

        private void ShowGardenInteraction(int index)
        {
            var garden = SaveManager.Instance.Data.gardens[index];

            if (string.IsNullOrEmpty(garden.plantName))
            {
                interactionTitle.text = "Garden";
                BuildGardenPlantPicker(index);
                return;
            }

            interactionTitle.text = garden.plantName;

            var gardenConfig = ConfigService.Instance?.GetGarden(garden.plantName);

            if (garden.mature)
            {
                // Yield info
                if (gardenConfig != null)
                {
                    string yieldName = ConfigService.Instance.GetItemDisplayName(gardenConfig.yieldItem) ?? gardenConfig.yieldItem;
                    var yieldLabel = new Label($"Yields {yieldName} x{gardenConfig.yieldAmount}");
                    yieldLabel.AddToClassList("plot-yield-preview");
                    interactionBody.Add(yieldLabel);
                }

                // Next yield timer
                if (gardenConfig != null && !string.IsNullOrEmpty(garden.lastYieldTimeUtc))
                {
                    var lastYield = System.DateTime.Parse(garden.lastYieldTimeUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                    float elapsedHours = (float)(GameTime.UtcNow - lastYield).TotalHours;
                    float yieldProgress = Mathf.Clamp01(elapsedHours / gardenConfig.yieldIntervalHours);
                    float remainingSec = Mathf.Max(0f, (gardenConfig.yieldIntervalHours - elapsedHours) * 3600f);

                    if (yieldProgress >= 1f)
                    {
                        var readyLabel = new Label("Fruit ready to collect!");
                        readyLabel.AddToClassList("plot-ready-badge");
                        interactionBody.Add(readyLabel);
                    }
                    else
                    {
                        AddGrowthProgressBar(yieldProgress, remainingSec);
                        var yieldTimerLabel = new Label($"Next fruit in {FormatTimeRemaining(remainingSec)}");
                        yieldTimerLabel.AddToClassList("interaction-info");
                        interactionBody.Add(yieldTimerLabel);
                    }
                }

                // Fertilize
                if (!garden.fertilized)
                {
                    int fertCount = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == "fertilizer")?.count ?? 0;
                    var fertBtn = new Button(() =>
                    {
                        _ = FertilizeGardenAndRefresh(index);
                    })
                    { text = $"Fertilize ({fertCount})" };
                    fertBtn.SetEnabled(fertCount > 0 || CurrencyManager.FreeMode);
                    fertBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(fertBtn);
                }
                else
                {
                    var fertLabel = new Label("Fertilized - +50% next yield");
                    fertLabel.AddToClassList("interaction-info-highlight");
                    interactionBody.Add(fertLabel);
                }
            }
            else
            {
                // Growing state with progress bar
                float progress = GardenManager.Instance.GetGrowthProgress(index);
                float growthDuration = gardenConfig != null ? gardenConfig.growthDurationHours : 1f;
                float remainingSec = Mathf.Max(0f, (1f - progress) * growthDuration * 3600f);
                AddGrowthProgressBar(progress, remainingSec);
            }

        }

        private async Task FertilizeGardenAndRefresh(int gardenIndex)
        {
            if (GardenManager.Instance == null) return;
            bool success = await GardenManager.Instance.Fertilize(gardenIndex);
            if (success)
            {
                RebuildGrid();
                ShowInteraction(CampBuildingType.Garden, gardenIndex);
            }
        }

        private void ShowMallumHouseInteraction(int index)
        {
            if (MallumManager.Instance == null) return;
            var houseConfig = ConfigService.Instance.MallumHouseConfig;
            interactionTitle.text = "Mallum House";

            // Mallum count per house
            int perHouse = houseConfig.MallumsPerHouse;
            var capacityLabel = new Label($"{perHouse} {(perHouse == 1 ? "Mallum" : "Mallums")} per house");
            capacityLabel.AddToClassList("interaction-info");
            interactionBody.Add(capacityLabel);

            // Total mallum overview
            int totalMallums = MallumManager.Instance.GetTotalMallumCount();
            int idleMallums = MallumManager.Instance.GetAvailableMallumCount();
            int houseCount = SaveManager.Instance.Data.mallumHouses.Count;
            int maxMallums = houseConfig.GetMaxMallums(houseCount);

            var totalLabel = new Label($"{totalMallums} / {maxMallums} Mallums");
            totalLabel.AddToClassList("plot-yield-preview");
            interactionBody.Add(totalLabel);

            // Status breakdown
            int busy = totalMallums - idleMallums;
            if (busy > 0)
            {
                var statusLabel = new Label($"{idleMallums} idle / {busy} on task");
                statusLabel.AddToClassList("interaction-info");
                interactionBody.Add(statusLabel);
            }
            else
            {
                var statusLabel = new Label("All Mallums idle");
                statusLabel.AddToClassList("interaction-info");
                interactionBody.Add(statusLabel);
            }

            AddPaintIcon(CampBuildingType.MallumHouse, index);

        }

        private VisualElement BuildSwatchPreview(SkinData skin, string extraClass = null, bool locked = false)
        {
            var swatch = new VisualElement();
            swatch.AddToClassList("skin-swatch");
            if (extraClass != null) swatch.AddToClassList(extraClass);

            var preview = new VisualElement();
            preview.AddToClassList("skin-swatch-preview");
            preview.style.backgroundColor = skin.hexFillColor;
            preview.style.borderTopColor = skin.hexBorderColor;
            preview.style.borderBottomColor = skin.hexBorderColor;
            preview.style.borderLeftColor = skin.hexBorderColor;
            preview.style.borderRightColor = skin.hexBorderColor;
            swatch.Add(preview);

            if (locked)
            {
                var lockIcon = new VisualElement();
                lockIcon.AddToClassList("skin-swatch-lock");
                var lockTex = SpriteService.Instance?.GetTexture("ui/lorc-padlock");
                if (lockTex != null)
                    lockIcon.style.backgroundImage = lockTex;
                swatch.Add(lockIcon);
            }

            return swatch;
        }

        private void ShowSkinSelector(CampBuildingType type, int index)
        {
            if (SkinManager.Instance == null) return;
            interactionBody.Clear();
            interactionActions.Clear();
            ClearPaintIcon();

            string typeName = type switch
            {
                CampBuildingType.Plot => "Plot",
                CampBuildingType.Vase => "Vase",
                CampBuildingType.MallumHouse => "House",
                _ => "Building"
            };

            // Pin panel to top for consistent positioning
            interactionPanel.AddToClassList("skin-panel");

            // Back arrow in top-left, replacing title area
            var headerRow = new VisualElement();
            headerRow.AddToClassList("skin-header");

            var backArrow = new Button(() => ShowInteraction(type, index));
            backArrow.AddToClassList("skin-back-arrow");
            var backIcon = SpriteService.Instance?.GetTexture("ui/icon-arrow-left");
            if (backIcon != null)
                backArrow.style.backgroundImage = backIcon;
            else
                backArrow.text = "<";
            headerRow.Add(backArrow);

            var titleLabel = new Label($"Paint {typeName}");
            titleLabel.AddToClassList("skin-title");
            headerRow.Add(titleLabel);

            // Hide default title, use our custom header
            interactionTitleRow.style.display = DisplayStyle.None;
            interactionBody.Add(headerRow);

            var skins = SkinManager.Instance.GetSkinsForBuilding(type);
            if (skins.Count == 0)
            {
                var noSkins = new Label("No skins available");
                noSkins.AddToClassList("interaction-info");
                interactionBody.Add(noSkins);
                return;
            }

            string currentSkin = type switch
            {
                CampBuildingType.Plot => SaveManager.Instance.Data.plots[index].skinName,
                CampBuildingType.Vase => SaveManager.Instance.Data.vases[index].skinName,
                CampBuildingType.MallumHouse => SaveManager.Instance.Data.mallumHouses[index].skinName,
                _ => null
            };

            // Find starting index: current equipped skin, or first skin
            int currentIndex = 0;
            if (!string.IsNullOrEmpty(currentSkin))
            {
                for (int i = 0; i < skins.Count; i++)
                {
                    if (skins[i].skinName == currentSkin) { currentIndex = i; break; }
                }
            }

            var detailArea = new VisualElement();
            detailArea.AddToClassList("skin-detail");

            // Skin name label ABOVE the carousel
            var skinNameLabel = new Label();
            skinNameLabel.AddToClassList("skin-carousel-name");

            // Carousel: [ghost-left] [center-swatch] [ghost-right]
            var carouselRow = new VisualElement();
            carouselRow.AddToClassList("skin-carousel-row");

            var ghostLeft = new VisualElement();
            ghostLeft.AddToClassList("skin-carousel-ghost");
            ghostLeft.AddToClassList("skin-carousel-ghost--left");

            var centerSwatch = new VisualElement();
            centerSwatch.AddToClassList("skin-carousel-center");

            var ghostRight = new VisualElement();
            ghostRight.AddToClassList("skin-carousel-ghost");
            ghostRight.AddToClassList("skin-carousel-ghost--right");

            carouselRow.Add(ghostLeft);
            carouselRow.Add(centerSwatch);
            carouselRow.Add(ghostRight);

            // Pip dots for position
            var pipRow = new VisualElement();
            pipRow.AddToClassList("skin-carousel-pips");
            var pips = new List<VisualElement>();
            for (int i = 0; i < skins.Count; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("skin-carousel-pip");
                pipRow.Add(pip);
                pips.Add(pip);
            }

            // Separator
            var separator = new VisualElement();
            separator.AddToClassList("skin-carousel-separator");

            void ShowCarouselItem(int idx)
            {
                currentIndex = ((idx % skins.Count) + skins.Count) % skins.Count;
                var skin = skins[currentIndex];
                bool centerLocked = !SkinManager.Instance.IsSkinUnlocked(type, index, skin.skinName);

                // Center swatch
                centerSwatch.Clear();
                var center = BuildSwatchPreview(skin, "skin-swatch--center", locked: centerLocked);
                if (skin.skinName == currentSkin)
                    center.AddToClassList("skin-swatch--equipped");
                centerSwatch.Add(center);

                // Ghost left neighbor (tappable)
                ghostLeft.Clear();
                int leftIdx = ((currentIndex - 1) % skins.Count + skins.Count) % skins.Count;
                bool leftLocked = !SkinManager.Instance.IsSkinUnlocked(type, index, skins[leftIdx].skinName);
                ghostLeft.Add(BuildSwatchPreview(skins[leftIdx], "skin-swatch--ghost", locked: leftLocked));

                // Ghost right neighbor (tappable)
                ghostRight.Clear();
                int rightIdx = (currentIndex + 1) % skins.Count;
                bool rightLocked = !SkinManager.Instance.IsSkinUnlocked(type, index, skins[rightIdx].skinName);
                ghostRight.Add(BuildSwatchPreview(skins[rightIdx], "skin-swatch--ghost", locked: rightLocked));

                // Name
                skinNameLabel.text = skin.skinName.Replace("_", " ");

                // Pips
                for (int i = 0; i < pips.Count; i++)
                {
                    pips[i].EnableInClassList("skin-carousel-pip--active", i == currentIndex);
                    pips[i].EnableInClassList("skin-carousel-pip--equipped", i != currentIndex
                        && skins[i].skinName == currentSkin);
                }

                UpdateSkinDetail(detailArea, skin, type, index, currentSkin);
            }

            // Tap ghosts to navigate
            ghostLeft.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); ShowCarouselItem(currentIndex - 1); });
            ghostRight.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); ShowCarouselItem(currentIndex + 1); });

            interactionBody.Add(skinNameLabel);
            interactionBody.Add(carouselRow);
            interactionBody.Add(pipRow);
            interactionBody.Add(separator);
            interactionBody.Add(detailArea);

            if (!string.IsNullOrEmpty(currentSkin))
            {
                var removeBtn = new Button(() =>
                {
                    SkinManager.Instance.RemoveSkin(type, index);
                    CloseInteractionPanel();
                    RebuildGrid();
                })
                { text = "Remove Skin" };
                interactionActions.Add(removeBtn);
            }

            ShowCarouselItem(currentIndex);
        }

        private void UpdateSkinDetail(VisualElement detailArea, SkinData skin,
            CampBuildingType type, int index, string currentSkin)
        {
            detailArea.Clear();

            bool isUnlocked = SkinManager.Instance.IsSkinUnlocked(type, index, skin.skinName);
            bool isEquipped = skin.skinName == currentSkin;

            if (isEquipped)
            {
                var equippedLabel = new Label("Equipped");
                equippedLabel.AddToClassList("skin-detail-equipped");
                detailArea.Add(equippedLabel);
            }
            else if (isUnlocked)
            {
                var paintBtn = new Button(() =>
                {
                    if (SkinManager.Instance.ApplySkin(type, index, skin))
                    {
                        CloseInteractionPanel();
                        RebuildGrid();
                    }
                })
                { text = "Paint" };
                paintBtn.AddToClassList("skin-action-btn");
                detailArea.Add(paintBtn);
            }
            else
            {
                // Cost row with pigment icon + count
                var items = SaveManager.Instance.Data.inventory;
                var pigmentItem = items.Find(i => i.itemKey == skin.costItemName);
                int have = pigmentItem?.count ?? 0;
                bool canAfford = SkinManager.Instance.CanAffordSkin(skin);

                var costRow = new VisualElement();
                costRow.AddToClassList("skin-cost-row");

                var pigmentIcon = new VisualElement();
                pigmentIcon.AddToClassList("skin-cost-icon");
                var iconTex = SpriteService.Instance?.GetTexture($"ui/items/{skin.costItemName.ToLower()}");
                if (iconTex != null)
                    pigmentIcon.style.backgroundImage = iconTex;
                costRow.Add(pigmentIcon);

                var costText = new Label($"{have} / {skin.costQuantity}");
                costText.AddToClassList("skin-cost-text");
                costText.AddToClassList(canAfford ? "skin-cost-text--have" : "skin-cost-text--need");
                costRow.Add(costText);

                detailArea.Add(costRow);

                var unlockBtn = new Button(() =>
                {
                    if (SkinManager.Instance.ApplySkin(type, index, skin))
                    {
                        CloseInteractionPanel();
                        RebuildGrid();
                    }
                })
                { text = "Unlock" };
                unlockBtn.AddToClassList("skin-action-btn");
                unlockBtn.SetEnabled(canAfford);
                detailArea.Add(unlockBtn);
            }
        }

        private void ShowBirdInteraction(int index)
        {
            var data = SaveManager.Instance.Data;
            if (index < 0 || index >= data.birds.Count) return;

            var bird = data.birds[index];
            interactionTitle.text = "Bird Visit";

            // Gift display
            string itemName = ConfigService.Instance.GetItemDisplayName(bird.itemKey) ?? bird.itemKey;

            var giftRow = new VisualElement();
            giftRow.AddToClassList("harvest-yield-row");

            // Item icon
            string spriteKey = SeedToSpriteKey(bird.itemKey);
            var iconEl = new VisualElement();
            iconEl.AddToClassList("harvest-seed-icon");
            TrySetSprite(iconEl, $"items/{spriteKey}/seed");
            giftRow.Add(iconEl);

            var giftLabel = new Label($"{itemName} x{bird.itemCount}");
            giftLabel.AddToClassList("harvest-yield-label");
            giftRow.Add(giftLabel);

            interactionBody.Add(giftRow);

            var flavorLabel = new Label("A bird dropped this off for you!");
            flavorLabel.AddToClassList("interaction-info");
            interactionBody.Add(flavorLabel);

            var collectBtn = new Button(() =>
            {
                var drop = BirdManager.CollectBird(data, index);
                if (drop != null)
                {
                    ApothekeManager.Instance?.AddItem(drop.itemKey, drop.itemCount);
                    SaveManager.Instance.Save();
                    BirdManager.Instance?.NotifyBirdCollected(drop);
                }
                CloseInteractionPanel();
            })
            { text = "Collect" };
            collectBtn.AddToClassList("interaction-btn-primary");
            interactionActions.Add(collectBtn);

        }

        private static string FormatTimeRemaining(float seconds)
        {
            if (seconds <= 0f) return "0:00";
            int total = Mathf.CeilToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m}:{s:D2}";
        }

        private void CloseInteractionPanel()
        {
            AudioManager.Instance?.PlaySFX("ui_panel_close");
            if (interactionBackdrop != null)
                interactionBackdrop.style.display = DisplayStyle.None;
            if (interactionPanel != null)
            {
                interactionPanel.style.display = DisplayStyle.None;
                interactionPanel.RemoveFromClassList("skin-panel");
            }
            if (interactionTitle != null)
                interactionTitleRow.style.display = DisplayStyle.Flex;
            openInteractionType = null;
            flameBuildGrid = null;

            // If rebuilds were suppressed (e.g. harvest result popup was open),
            // do a deferred rebuild now that the panel is closed.
            if (suppressRebuild)
            {
                suppressRebuild = false;
                if (pendingRebuild)
                {
                    pendingRebuild = false;
                    RebuildGrid();
                }
            }

        }

        // ── Drag-Move ──

        private void BeginLongPressDetection(PointerDownEvent evt, CampBuildingType type, int index, int q, int r)
        {
            if (mode != CampsiteMode.Normal) return;

            longPressStart = evt.position;
            longPressPending = true;
            dragBuildingType = type;
            dragBuildingIndex = index;
            dragOriginQ = q;
            dragOriginR = r;
            dragPointerId = evt.pointerId;
            panController.SuppressMove = true;

            longPressTimer = viewport.schedule.Execute(() =>
            {
                if (longPressPending)
                    EnterDragMoveMode(longPressStart);
            }).StartingIn((long)LongPressMs);
        }

        private void CancelLongPress()
        {
            if (!longPressPending) return;
            longPressPending = false;
            longPressTimer?.Pause();
            longPressTimer = null;
            panController.SuppressMove = false;
            dragPointerId = -1;
        }

        private void EnterDragMoveMode(Vector2 startPos)
        {
            longPressPending = false;
            longPressTimer = null;
            mode = CampsiteMode.Moving;

            // Capture pointer on viewport so all move/up events come here
            viewport.CapturePointer(dragPointerId);

            // Dim the origin cell
            if (cellLookup.TryGetValue((dragOriginQ, dragOriginR), out var originCell))
                originCell.AddToClassList("grid-cell--drag-origin");

            // Highlight empty cells as drop targets
            foreach (var kvp in cellLookup)
            {
                if (kvp.Value.ClassListContains("grid-cell--empty"))
                    kvp.Value.AddToClassList("grid-cell--drop-target");
            }

            // Create drag ghost
            dragGhost = new VisualElement();
            dragGhost.AddToClassList("grid-cell");
            dragGhost.AddToClassList("grid-cell--drop-hover");
            dragGhost.style.width = CellWidth;
            dragGhost.style.height = CellHeight;
            dragGhost.style.position = Position.Absolute;
            dragGhost.pickingMode = PickingMode.Ignore;
            dragGhost.generateVisualContent += DrawHexCell;
            dragGhost.RegisterCallback<CustomStyleResolvedEvent>(_ => dragGhost.MarkDirtyRepaint());
            viewport.Add(dragGhost);
            PositionGhost(startPos);
        }

        private void PositionGhost(Vector2 panelPos)
        {
            if (dragGhost == null) return;
            var local = viewport.WorldToLocal(panelPos);
            dragGhost.style.left = local.x - CellWidth / 2f;
            dragGhost.style.top = local.y - CellHeight / 2f;
        }

        private (int q, int r) PointerToHex(Vector2 panelPos)
        {
            var viewportLocal = viewport.WorldToLocal(panelPos);
            var translate = canvas.resolvedStyle.translate;
            float canvasX = viewportLocal.x - translate.x;
            float canvasY = viewportLocal.y - translate.y;
            float hexX = canvasX - gridOffsetX;
            float hexY = canvasY - gridOffsetY;
            return HexGridUtil.PixelToHex(hexX, hexY, HexSize);
        }

        private (int q, int r) lastHoverHex = (int.MinValue, int.MinValue);

        private void OnViewportPointerMove(PointerMoveEvent evt)
        {
            // During long-press detection: check movement threshold
            if (longPressPending)
            {
                float dist = Vector2.Distance(evt.position, longPressStart);
                if (dist > LongPressMoveThreshold)
                    CancelLongPress();
                return;
            }

            if (mode != CampsiteMode.Moving) return;

            PositionGhost(evt.position);

            // Update drop hover highlight
            var hex = PointerToHex(evt.position);
            if (hex != lastHoverHex)
            {
                // Remove previous hover
                if (cellLookup.TryGetValue(lastHoverHex, out var prevCell))
                    prevCell.RemoveFromClassList("grid-cell--drop-hover");

                // Add hover on valid empty targets
                if (cellLookup.TryGetValue(hex, out var hoverCell)
                    && hoverCell.ClassListContains("grid-cell--drop-target"))
                {
                    hoverCell.AddToClassList("grid-cell--drop-hover");
                }

                lastHoverHex = hex;
            }
        }

        private void OnViewportPointerUp(PointerUpEvent evt)
        {
            if (longPressPending)
            {
                CancelLongPress();
                return;
            }

            if (mode != CampsiteMode.Moving) return;

            var hex = PointerToHex(evt.position);
            bool validDrop = cellLookup.TryGetValue(hex, out var dropCell)
                && dropCell.ClassListContains("grid-cell--drop-target");

            if (validDrop)
                MoveBuilding(dragBuildingType, dragBuildingIndex, hex.q, hex.r);

            ExitDragMoveMode(evt.pointerId);
        }

        private void MoveBuilding(CampBuildingType type, int index, int newQ, int newR)
        {
            var data = SaveManager.Instance.Data;
            int serverId = 0;
            string serverType = null;

            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].gridX = newQ;
                    data.plots[index].gridY = newR;
                    serverId = data.plots[index].serverId;
                    serverType = "plot";
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].gridX = newQ;
                    data.vases[index].gridY = newR;
                    serverId = data.vases[index].serverId;
                    serverType = "vase";
                    break;
                case CampBuildingType.Garden:
                    data.gardens[index].gridX = newQ;
                    data.gardens[index].gridY = newR;
                    serverId = data.gardens[index].serverId;
                    serverType = "garden";
                    break;
                case CampBuildingType.Apotheke:
                    data.apothekeGridX = newQ;
                    data.apothekeGridY = newR;
                    serverId = data.apothekeServerId;
                    serverType = "apotheke";
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].gridX = newQ;
                    data.mallumHouses[index].gridY = newR;
                    serverId = data.mallumHouses[index].serverId;
                    serverType = "mallum_house";
                    break;
            }
            SaveManager.Instance.Save();

            if (serverType != null && serverId > 0 && GameService.Instance != null && GameService.Instance.IsOnline)
            {
                _ = GameService.Instance.MoveBuilding(serverType, serverId, newQ, newR);
            }
        }

        private void ExitDragMoveMode(int pointerId)
        {
            mode = CampsiteMode.Normal;
            panController.SuppressMove = false;
            dragPointerId = -1;
            lastHoverHex = (int.MinValue, int.MinValue);

            if (viewport.HasPointerCapture(pointerId))
                viewport.ReleasePointer(pointerId);

            if (dragGhost != null)
            {
                dragGhost.RemoveFromHierarchy();
                dragGhost = null;
            }

            RebuildGrid();
        }
    }
}
