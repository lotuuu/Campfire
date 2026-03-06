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
        private enum CampsiteMode { Normal, Placing, Watering, Visiting, Moving }
        private CampsiteMode mode;
        private CampBuildingType pendingBuildingType;
        private int wateringVaseIndex = -1;
        private Button modeCancelBtn;

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
        private bool needsRecenter = true;

        // Events
        public event Action OnApothekeTapped;
        public event Action OnVisitorTapped;

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

            // Viewport pointer handlers for drag-move
            viewport.RegisterCallback<PointerMoveEvent>(OnViewportPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnViewportPointerUp);

            // Subscribe to manager events
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded += RebuildGrid;
            if (PlotManager.Instance != null)
                PlotManager.Instance.OnPlotChanged += _ => RebuildGrid();
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged += RebuildGrid;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged += RebuildGrid;
            if (GardenManager.Instance != null)
                GardenManager.Instance.OnGardenChanged += _ => RebuildGrid();
            if (BirdManager.Instance != null)
            {
                BirdManager.Instance.OnBirdPlaced += RebuildGrid;
                BirdManager.Instance.OnBirdCollected += _ => RebuildGrid();
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

        private void OnDestroy()
        {
            if (FlameManager.Instance != null)
                FlameManager.Instance.OnFlameUpgraded -= RebuildGrid;
            if (VaseManager.Instance != null)
                VaseManager.Instance.OnVasesChanged -= RebuildGrid;
            if (MallumManager.Instance != null)
                MallumManager.Instance.OnMallumsChanged -= RebuildGrid;
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
        }

        // ── Grid Building ──

        public void RebuildGrid()
        {
            if (suppressRebuild) return;
            if (mode == CampsiteMode.Moving) return;
            if (canvas == null || FlameManager.Instance == null) return;

            canvas.Clear();
            growingPlots.Clear();
            fillingVases.Clear();
            cooldownPlots.Clear();
            cellLookup.Clear();
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
                viewport.Add(modeCancelBtn);
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
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedName) ? "Plot" : PlotManager.GetSeedDisplayName(plot.seedName);
                    if (status != null) status.text = plot.state.ToString();

                    if (plot.state == PlotState.Empty)
                    {
                        if (!TrySetHexSprite(cell, "hex/plot/empty", plotSkin))
                            ApplySkinColors(cell, plotSkin);
                    }
                    else if (plot.state == PlotState.Growing)
                    {
                        string seed = SeedToSpriteKey(plot.seedName);
                        string spritePrefix = $"hex/plot/{seed}";
                        float growthPct = PlotManager.Instance != null ? PlotManager.Instance.GetGrowthProgress(index) : 0f;
                        if (!TrySetHexSpriteByPercent(cell, spritePrefix, growthPct, plotSkin))
                            ApplySkinColors(cell, plotSkin);
                        if (progress != null && progressFill != null)
                        {
                            progress.AddToClassList("cell-progress--visible");
                            growingPlots.Add((progressFill, cell, spritePrefix, plotSkin, index));
                        }
                    }
                    else if (plot.state == PlotState.Mature)
                    {
                        string seed = SeedToSpriteKey(plot.seedName);
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
                        int mallumCount = MallumManager.Instance != null
                            ? MallumManager.Instance.HouseConfig.MallumsPerHouse
                            : 1;
                        status.text = $"+{mallumCount} Mallums";
                    }
                    break;

                case CampBuildingType.Bird:
                    cell.AddToClassList("grid-cell--bird");
                    TrySetHexSprite(cell, "hex/bird");
                    var bird = SaveManager.Instance.Data.birds[index];
                    if (label != null) label.text = "Bird";
                    if (status != null) status.text = $"{bird.seedCount}x {PlotManager.GetSeedDisplayName(bird.seedName)}";
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

        /// <summary>
        /// Converts a seedName like "Sprouts Seed" to a sprite-friendly slug "sprouts".
        /// Strips trailing " Seed"/" seed", lowercases, replaces spaces with hyphens.
        /// </summary>
        private static string SeedToSpriteKey(string seedName)
        {
            if (string.IsNullOrEmpty(seedName)) return seedName;
            var s = seedName.Trim();
            if (s.EndsWith(" Seed", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 5);
            return s.ToLower().Replace(' ', '-');
        }

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
            ClearBellIcon();

            bool canPlace = FlameManager.Instance.CanPlaceEntity;
            int current = FlameManager.Instance.CurrentEntityCount;
            int max = FlameManager.Instance.MaxEntities;
            interactionTitle.text = $"Build ({current}/{max})";

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("build-card-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;

            string capText = $"{current}/{max}";

            // Plot
            if (PlotManager.Instance != null)
            {
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                if (plotCost != null)
                {
                    bool canAffordPlot = canPlace
                        && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, plotCost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "Plot", "Grow seeds", "ui/buildings/plot", null,
                        BuildCardHelper.FromBuildingCost(plotCost), capText,
                        canAffordPlot, canPlace, () =>
                        {
                            if (PlotManager.Instance.CraftPlot(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            // Vase
            if (VaseManager.Instance != null)
            {
                var vaseCost = VaseManager.Instance.GetNextVaseCost();
                if (vaseCost != null)
                {
                    bool canAffordVase = canPlace
                        && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, vaseCost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "Vase", "Stores water", "ui/buildings/vase", null,
                        BuildCardHelper.FromBuildingCost(vaseCost), capText,
                        canAffordVase, canPlace, () =>
                        {
                            if (VaseManager.Instance.CraftVase(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            // House
            if (MallumManager.Instance != null)
            {
                var cost = MallumManager.Instance.GetNextHouseCost();
                if (cost != null)
                {
                    bool canAffordHouse = canPlace
                        && CurrencyManager.Instance.CanAffordMana(cost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, cost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(cost), capText,
                        canAffordHouse, canPlace, () =>
                        {
                            if (MallumManager.Instance.CraftMallumHouse(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            // Garden
            if (GardenManager.Instance != null)
            {
                var gardenCost = GardenManager.Instance.GetNextGardenCost();
                if (gardenCost != null)
                {
                    bool canAffordGarden = canPlace
                        && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, gardenCost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                        BuildCardHelper.FromBuildingCost(gardenCost), capText,
                        canAffordGarden, canPlace, () =>
                        {
                            if (GardenManager.Instance.CraftEmptyGarden(gridX, gridY))
                                CloseInteractionPanel();
                        }));
                }
            }

            interactionBody.Add(scroll);

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

            // Fixed buildings always take priority (visitor snapshot doesn't store apotheke pos)
            occupied[(1, 0)] = (CampBuildingType.Apotheke, 0);

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
                    string seed = SeedToSpriteKey(plot.seedName);
                    if (string.IsNullOrEmpty(plot.seedName) || plot.state == "empty")
                        TrySetHexSprite(cell, "hex/plot/empty");
                    else if (plot.state == "mature")
                        TrySetHexSpriteByPercent(cell, $"hex/plot/{seed}", 1f);
                    else
                        TrySetHexSpriteByPercent(cell, $"hex/plot/{seed}", 0f);
                    if (label != null) label.text = string.IsNullOrEmpty(plot.seedName) ? "Plot" : PlotManager.GetSeedDisplayName(plot.seedName);
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
            var existing = interactionPanel?.Q(className: "water-subscribe-bell");
            existing?.RemoveFromHierarchy();
        }

        private void ShowInteraction(CampBuildingType type, int index)
        {
            if (interactionPanel == null) return;

            interactionBody.Clear();
            interactionActions.Clear();
            interactionTitle.style.display = DisplayStyle.Flex;
            interactionPanel.RemoveFromClassList("skin-panel");
            ClearBellIcon();

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
                var recipe = FlameManager.Instance.Config.GetUpgradeRecipe(FlameManager.Instance.Level);
                if (recipe != null && recipe.ingredients.Count > 0)
                {
                    var costList = new VisualElement();
                    costList.AddToClassList("upgrade-cost-list");

                    var costHeader = new Label("REQUIRED");
                    costHeader.AddToClassList("upgrade-cost-header");
                    costList.Add(costHeader);

                    var items = SaveManager.Instance.Data.items;
                    foreach (var ing in recipe.ingredients)
                    {
                        string displayName = ing.itemName.Replace("_harvest", "");
                        var item = items.Find(i => i.itemName == ing.itemName);
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

                bool canAfford = FlameManager.Instance.CanUpgrade();
                var upgradeBtn = new Button(() =>
                {
                    FlameManager.Instance.UpgradeFlame();
                    CloseInteractionPanel();
                }) { text = "Level Up" };
                upgradeBtn.SetEnabled(canAfford);
                upgradeBtn.AddToClassList("upgrade-btn");
                interactionActions.Add(upgradeBtn);
            }

            // ── Craft / Build section ──
            AddFlameCraftItems();

            AddCloseButton();
        }

        private void AddFlameCraftItems()
        {
            var buildHeader = new Label("BUILD");
            buildHeader.AddToClassList("upgrade-cost-header");
            buildHeader.style.marginTop = 16;
            interactionBody.Add(buildHeader);

            bool canPlaceEntity = FlameManager.Instance != null && FlameManager.Instance.CanPlaceEntity;
            string capText = FlameManager.Instance != null
                ? $"{FlameManager.Instance.CurrentEntityCount}/{FlameManager.Instance.MaxEntities}"
                : "";

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.AddToClassList("build-card-scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;

            // Plot
            if (PlotManager.Instance != null && FlameManager.Instance != null)
            {
                var plotCost = PlotManager.Instance.GetNextPlotCost();
                bool canAfford = canPlaceEntity && plotCost != null
                    && CurrencyManager.Instance.CanAffordMana(plotCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, plotCost.harvestCosts);
                scroll.Add(BuildCardHelper.CreateBuildCard(
                    "Plot", "Grow seeds", "ui/buildings/plot", null,
                    BuildCardHelper.FromBuildingCost(plotCost), capText,
                    canAfford, canPlaceEntity, () =>
                    {
                        CloseInteractionPanel();
                        EnterPlacementMode(CampBuildingType.Plot);
                    }));
            }

            // Vase
            if (VaseManager.Instance != null)
            {
                var vaseCost = VaseManager.Instance.GetNextVaseCost();
                bool canAfford = canPlaceEntity && vaseCost != null
                    && CurrencyManager.Instance.CanAffordMana(vaseCost.manaCost)
                    && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, vaseCost.harvestCosts);
                scroll.Add(BuildCardHelper.CreateBuildCard(
                    "Vase", "Stores water", "ui/buildings/vase", null,
                    BuildCardHelper.FromBuildingCost(vaseCost), capText,
                    canAfford, canPlaceEntity, () =>
                    {
                        CloseInteractionPanel();
                        EnterPlacementMode(CampBuildingType.Vase);
                    }));
            }

            // House
            if (MallumManager.Instance != null)
            {
                var nextCost = MallumManager.Instance.GetNextHouseCost();
                if (nextCost != null)
                {
                    bool canAfford = canPlaceEntity
                        && CurrencyManager.Instance.CanAffordMana(nextCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, nextCost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "House", "Houses 1 Mallum", "ui/buildings/house", null,
                        BuildCardHelper.FromBuildingCost(nextCost), capText,
                        canAfford, canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.MallumHouse);
                        }));
                }
            }

            // Garden
            if (GardenManager.Instance != null)
            {
                var gardenCost = GardenManager.Instance.GetNextGardenCost();
                if (gardenCost != null)
                {
                    bool canAfford = canPlaceEntity
                        && CurrencyManager.Instance.CanAffordMana(gardenCost.manaCost)
                        && MallumManager.CanAffordHarvests(SaveManager.Instance.Data.items, gardenCost.harvestCosts);
                    scroll.Add(BuildCardHelper.CreateBuildCard(
                        "Garden", "Grow fruit trees", "ui/buildings/garden", null,
                        BuildCardHelper.FromBuildingCost(gardenCost), capText,
                        canAfford, canPlaceEntity, () =>
                        {
                            CloseInteractionPanel();
                            EnterPlacementMode(CampBuildingType.Garden);
                        }));
                }
            }

            interactionBody.Add(scroll);
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
                    interactionTitle.text = PlotManager.GetSeedDisplayName(plot.seedName);
                    float remaining = PlotManager.Instance.GetRemainingSeconds(index);
                    var progressLabel = new Label($"Growing... {FormatTimeRemaining(remaining)} left");
                    progressLabel.AddToClassList("interaction-info");
                    interactionBody.Add(progressLabel);

                    var wateringsLabel = new Label($"Waterings: {plot.waterCount}");
                    wateringsLabel.AddToClassList("interaction-info");
                    interactionBody.Add(wateringsLabel);

                    AddWaterSubscribeToggle(index, plot);

                    AddGrowthRecipeSection(plot.seedName);

                    int plotPotionCount = MallumManager.Instance != null ? MallumManager.Instance.GetSpeedPotionCount() : 0;
                    var finishBtn = new Button(() =>
                    {
                        if (MallumManager.Instance != null && MallumManager.Instance.ConsumeSpeedPotion())
                        {
                            PlotManager.Instance.InstantFinish(index);
                            suppressRebuild = true;
                            var result = PlotManager.Instance.Harvest(index);
                            suppressRebuild = false;
                            if (result != null)
                            {
                                RebuildGrid();
                                ShowHarvestResult(result);
                                ShowInteractionPanel();
                            }
                            else
                                CloseInteractionPanel();
                        }
                    }) { text = $"Finish Now ({plotPotionCount} potions)" };
                    finishBtn.SetEnabled(plotPotionCount > 0 || CurrencyManager.FreeMode);
                    finishBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(finishBtn);
                    break;

                case PlotState.Mature:
                    interactionTitle.text = $"{PlotManager.GetSeedDisplayName(plot.seedName)} - Ready!";
                    AddGrowthRecipeSection(plot.seedName);
                    var harvestBtn = new Button(() =>
                    {
                        suppressRebuild = true;
                        var result = PlotManager.Instance.Harvest(index);
                        suppressRebuild = false;
                        if (result != null)
                        {
                            RebuildGrid();
                            ShowHarvestResult(result);
                            ShowInteractionPanel();
                        }
                        else
                            CloseInteractionPanel();
                    }) { text = "Harvest" };
                    harvestBtn.AddToClassList("interaction-btn-primary");
                    interactionActions.Add(harvestBtn);
                    break;
            }

            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.Plot, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }

            AddCloseButton();
        }

        private void ShowHarvestResult(HarvestResult result)
        {
            interactionBody.Clear();
            interactionActions.Clear();
            ClearBellIcon();

            interactionTitle.text = "Harvested!";

            // Seed icon + yield row
            var yieldRow = new VisualElement();
            yieldRow.AddToClassList("harvest-yield-row");
            var seedSprite = SpriteService.Instance?.GetSprite($"seeds/{result.seedName.ToLower()}/icon");
            if (seedSprite != null)
            {
                var iconEl = new VisualElement();
                iconEl.AddToClassList("harvest-seed-icon");
                iconEl.style.backgroundImage = new StyleBackground(seedSprite);
                yieldRow.Add(iconEl);
            }
            var yieldLabel = new Label($"{PlotManager.GetSeedDisplayName(result.seedName)} x{result.drops}");
            yieldLabel.AddToClassList("harvest-yield-label");
            yieldRow.Add(yieldLabel);
            interactionBody.Add(yieldRow);

            // Recipe match tier
            string matchText = result.recipeScore >= 0.8f ? "Perfect Match"
                : result.recipeScore >= 0.5f ? "Good Match"
                : "Weak Match";
            string matchClass = result.recipeScore >= 0.8f ? "harvest-match--perfect"
                : result.recipeScore >= 0.5f ? "harvest-match--good"
                : "harvest-match--weak";
            int pct = Mathf.RoundToInt(result.recipeScore * 100f);
            var matchLabel = new Label($"{matchText} ({pct}%)");
            matchLabel.AddToClassList("harvest-match-badge");
            matchLabel.AddToClassList(matchClass);
            interactionBody.Add(matchLabel);

            // Per-axis breakdown
            if (result.recipe != null)
            {
                var axisResults = result.recipe.EvaluatePerAxis(result.snapshots, result.waterCount);
                if (axisResults.Count > 0)
                {
                    var header = new Label("Recipe Breakdown");
                    header.AddToClassList("interaction-section-header");
                    interactionBody.Add(header);

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

                        var statusEl = new Label(axis.score >= 0.5f ? "+" : "-");
                        statusEl.AddToClassList(axis.score >= 0.5f ? "harvest-axis-pass" : "harvest-axis-fail");
                        row.Add(statusEl);

                        interactionBody.Add(row);
                    }
                }
            }

            AddCloseButton();
        }

        private void BuildSeedPicker(int plotIndex)
        {
            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            var inventory = SaveManager.Instance.Data.seedInventory;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("seed-picker-scroll");

            var list = new VisualElement();
            list.AddToClassList("seed-picker-list");

            foreach (var entry in inventory)
            {
                if (entry.count <= 0) continue;

                SeedData seedData = null;
                foreach (var s in allSeeds)
                    if (s.name == entry.seedName) { seedData = s; break; }

                var card = new VisualElement();
                card.AddToClassList("seed-card");

                // Header row: name + count
                var header = new VisualElement();
                header.AddToClassList("seed-card--header");
                var nameLabel = new Label(seedData != null ? seedData.seedName : entry.seedName);
                nameLabel.AddToClassList("seed-card--name");
                header.Add(nameLabel);
                var countLabel = new Label($"x{entry.count}");
                countLabel.AddToClassList("seed-card--count");
                header.Add(countLabel);
                card.Add(header);

                // Stats row: growth time + drops
                if (seedData != null)
                {
                    var stats = new VisualElement();
                    stats.AddToClassList("seed-card--stats");

                    AddSeedStat(stats, "Growth", TimeUtils.FormatDurationHours(seedData.growthDurationHours));
                    AddSeedStat(stats, "Drops", $"{seedData.minDrops}-{seedData.maxDrops}");

                    card.Add(stats);

                    // Recipe tags (compact weather preferences)
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
                            card.Add(tags);
                    }
                }

                // Plant button
                string sName = entry.seedName;
                var plantBtn = new Button(() =>
                {
                    PlotManager.Instance.Plant(plotIndex, sName);
                    CloseInteractionPanel();
                }) { text = "Plant" };
                plantBtn.AddToClassList("seed-card--plant-btn");
                card.Add(plantBtn);

                list.Add(card);
            }

            scroll.Add(list);
            interactionBody.Add(scroll);
        }

        private static void AddSeedStat(VisualElement container, string label, string value)
        {
            var stat = new VisualElement();
            stat.AddToClassList("seed-card--stat");
            var l = new Label(label);
            l.AddToClassList("seed-card--stat-label");
            stat.Add(l);
            var v = new Label(value);
            v.AddToClassList("seed-card--stat-value");
            stat.Add(v);
            container.Add(stat);
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

            interactionPanel.Add(bellIcon);
        }

        private static void UpdateBellIcon(VisualElement icon, bool active)
        {
            string path = active ? "UI/Icons/bell-on" : "UI/Icons/bell-off";
            var vi = Resources.Load<VectorImage>(path);
            if (vi != null)
                icon.style.backgroundImage = new StyleBackground(Background.FromVectorImage(vi));
        }

        private void AddGrowthRecipeSection(string seedName)
        {
            var allSeeds = Resources.LoadAll<SeedData>("Seeds");
            SeedData seed = null;
            foreach (var s in allSeeds)
            {
                if (s.name == seedName) { seed = s; break; }
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
                    var emptyLabel = new Label($"Empty - capacity {vase.capacity}");
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

                    int vasePotionCount = MallumManager.Instance != null ? MallumManager.Instance.GetSpeedPotionCount() : 0;
                    var finishVaseBtn = new Button(() =>
                    {
                        if (MallumManager.Instance != null && MallumManager.Instance.ConsumeSpeedPotion())
                        {
                            VaseManager.Instance.InstantFinish(index);
                            RebuildGrid();
                            ShowVaseInteraction(index);
                            ShowInteractionPanel();
                        }
                    }) { text = $"Finish Now ({vasePotionCount} potions)" };
                    finishVaseBtn.SetEnabled(vasePotionCount > 0 || CurrencyManager.FreeMode);
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

            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.Vase, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }

            AddCloseButton();
        }

        private void ShowGardenInteraction(int index)
        {
            var garden = SaveManager.Instance.Data.gardens[index];

            if (string.IsNullOrEmpty(garden.plantName))
            {
                interactionTitle.text = "Garden";

                var hint = new Label("Choose a plant to grow:");
                hint.AddToClassList("interaction-info");
                interactionBody.Add(hint);

                foreach (var plantData in Resources.LoadAll<GardenPlantData>("GardenPlants"))
                {
                    string pName = plantData.plantName;
                    string desc = $"Water: {plantData.waterRequired}";
                    bool canAfford = CurrencyManager.Instance != null
                        && CurrencyManager.Instance.CanAffordWater(plantData.waterRequired);

                    var btn = new Button(() =>
                    {
                        if (GardenManager.Instance.Plant(index, pName))
                        {
                            CloseInteractionPanel();
                            RebuildGrid();
                        }
                    }) { text = $"{pName} ({desc})" };
                    btn.AddToClassList("interaction-btn-primary");
                    btn.SetEnabled(canAfford);
                    interactionActions.Add(btn);
                }

                AddCloseButton();
                return;
            }

            interactionTitle.text = garden.plantName;

            var stateLabel = new Label(garden.mature ? "Mature - yielding fruit" : "Growing...");
            stateLabel.AddToClassList("interaction-info");
            interactionBody.Add(stateLabel);

            AddCloseButton();
        }

        private void ShowMallumHouseInteraction(int index)
        {
            if (MallumManager.Instance == null) return;
            var config = MallumManager.Instance.HouseConfig;
            interactionTitle.text = "House";

            var infoLabel = new Label($"Houses {config.MallumsPerHouse} {(config.MallumsPerHouse == 1 ? "Mallum" : "Mallums")}");
            infoLabel.AddToClassList("interaction-info");
            interactionBody.Add(infoLabel);

            if (SkinManager.Instance != null)
            {
                var paintBtn = new Button(() => ShowSkinSelector(CampBuildingType.MallumHouse, index)) { text = "Paint" };
                interactionActions.Add(paintBtn);
            }

            AddCloseButton();
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

            var backArrow = new Button(() => ShowInteraction(type, index)) { text = "<" };
            backArrow.AddToClassList("skin-back-arrow");
            headerRow.Add(backArrow);

            var titleLabel = new Label($"Paint {typeName}");
            titleLabel.AddToClassList("skin-title");
            headerRow.Add(titleLabel);

            // Hide default title, use our custom header
            interactionTitle.style.display = DisplayStyle.None;
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
                }) { text = "Remove Skin" };
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
                var equippedLabel = new Label("Currently applied");
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
                }) { text = "Paint" };
                paintBtn.AddToClassList("skin-action-btn");
                detailArea.Add(paintBtn);
            }
            else
            {
                // Cost row with pigment icon + count
                var items = SaveManager.Instance.Data.items;
                var pigmentItem = items.Find(i => i.itemName == skin.costItemName);
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
                }) { text = "Unlock" };
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
            interactionTitle.text = "Bird";

            var info = new Label($"A bird has brought you {bird.seedCount}x {PlotManager.GetSeedDisplayName(bird.seedName)}!");
            info.AddToClassList("interaction-info");
            interactionBody.Add(info);

            var collectBtn = new Button(() =>
            {
                var drop = BirdManager.CollectBird(data, index);
                if (drop != null)
                {
                    ApothekeManager.Instance?.AddSeed(drop.seedName, drop.seedCount);
                    SaveManager.Instance.Save();
                    BirdManager.Instance?.NotifyBirdCollected(drop);
                }
                CloseInteractionPanel();
            }) { text = "Collect Seeds" };
            collectBtn.AddToClassList("interaction-btn-primary");
            interactionActions.Add(collectBtn);

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
            {
                interactionPanel.style.display = DisplayStyle.None;
                interactionPanel.RemoveFromClassList("skin-panel");
            }
            if (interactionTitle != null)
                interactionTitle.style.display = DisplayStyle.Flex;
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
            switch (type)
            {
                case CampBuildingType.Plot:
                    data.plots[index].gridX = newQ;
                    data.plots[index].gridY = newR;
                    break;
                case CampBuildingType.Vase:
                    data.vases[index].gridX = newQ;
                    data.vases[index].gridY = newR;
                    break;
                case CampBuildingType.Garden:
                    data.gardens[index].gridX = newQ;
                    data.gardens[index].gridY = newR;
                    break;
                case CampBuildingType.Apotheke:
                    data.apothekeGridX = newQ;
                    data.apothekeGridY = newR;
                    break;
                case CampBuildingType.MallumHouse:
                    data.mallumHouses[index].gridX = newQ;
                    data.mallumHouses[index].gridY = newR;
                    break;
            }
            SaveManager.Instance.Save();
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
