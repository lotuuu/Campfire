using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Garden
{
    public class BackyardViewUI : MonoBehaviour
    {
        [SerializeField] private BackyardIsometricView isometricView;
        private Label backyardTitle;
        private int ActiveEnv => EnvironmentManager.Instance != null
            ? EnvironmentManager.Instance.ActiveEnvironmentIndex : 0;

        public event Action<int, int> OnEmptySlotTapped;
        public event Action<int, int> OnMatureSlotTapped;

        private VisualElement terrariumPage;
        // Root-level container for slot visual overlays (labels, progress bars).
        // Position:absolute at (0,0) so panel coords map directly onto it.
        private VisualElement _slotContainer;
        private readonly List<VisualElement> slotButtons = new();
        private readonly List<Label> labels = new();
        private readonly List<VisualElement> progressFills = new();
        private readonly List<VisualElement> progressBars = new();
        private readonly List<string> _lastLabelText = new();

        private VisualElement _pickerContainer;
        private Button _pickerBtn;
        private VisualElement _iconsContainer;
        private ConsumableType? _pendingType; // only set for slot-scoped apply mode
        private ConsumableType? _pendingEnvConfirmType; // set while inline env-replace confirmation is showing

        // Overlay elements — taps are suppressed while any of these are visible.
        private VisualElement _satchelScrim;
        private VisualElement _harvestPopup;
        private VisualElement _discoveryPopup;

        private bool initialized;
        private bool pageActive;

        public void SetPageActive(bool active)
        {
            pageActive = active;
            if (_slotContainer != null)
                _slotContainer.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Initialize(VisualElement root)
        {
            terrariumPage = root.Q<VisualElement>("terrarium-page");
            backyardTitle = root.Q<Label>("backyard-title");

            _satchelScrim    = root.Q<VisualElement>("satchel-scrim");
            _harvestPopup    = root.Q<VisualElement>("harvest-popup");
            _discoveryPopup  = root.Q<VisualElement>("discovery-popup");

            // Create a root-level overlay container for slot visuals so they are never
            // clipped by SwipeablePageView / pageContainer overflow:hidden.
            _slotContainer = new VisualElement();
            _slotContainer.style.position = Position.Absolute;
            _slotContainer.style.left = 0;
            _slotContainer.style.top = 0;
            _slotContainer.style.right = 0;
            _slotContainer.style.bottom = 0;
            _slotContainer.pickingMode = PickingMode.Ignore;
            _slotContainer.style.display = DisplayStyle.None;
            // Insert after app-shell (index 1) so it sits below the overlay panels.
            root.Insert(1, _slotContainer);

            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged += OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated += OnSlotGrowthUpdated;
            }

            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked += OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged += OnActiveEnvironmentChanged;
                BuildSlotsForEnv(ActiveEnv);
            }

            BuildConsumablePicker();
            RefreshPickerIndicator();
            RestoreConsumableVisuals(ActiveEnv);

            initialized = true;
            RefreshAllSlots();
            UpdateTitle();
        }

        private void OnActiveEnvironmentChanged(int envIndex)
        {
            RebuildForEnvironment(envIndex);
        }

        private void RebuildForEnvironment(int envIndex)
        {
            // Dismiss any in-flight env replacement confirmation before rebuilding
            CancelEnvConfirm();

            // Rebuild the iso view first so new tiles exist before we spawn slot buttons
            // and consumable visuals on them. This also makes ordering deterministic —
            // BackyardIsometricView no longer subscribes to OnActiveEnvironmentChanged;
            // we drive it explicitly here instead.
            isometricView?.SetEnvironment(envIndex);

            foreach (var btn in slotButtons)
                btn.RemoveFromHierarchy();
            slotButtons.Clear();
            labels.Clear();
            progressFills.Clear();
            progressBars.Clear();
            _lastLabelText.Clear();

            BuildSlotsForEnv(envIndex);
            RestoreConsumableVisuals(envIndex);
            RefreshAllSlots();
            UpdateTitle();
            RefreshPickerIndicator();
        }

        private void BuildSlotsForEnv(int envIndex)
        {
            if (EnvironmentManager.Instance == null) return;
            int count = EnvironmentManager.Instance.GetActiveSlotCount(envIndex);
            for (int i = 0; i < count; i++)
                AddSlotButton(i);
            ReorderSlotButtonsByDepth();
        }

        private void ReorderSlotButtonsByDepth()
        {
            // Visual-only overlays have no pointer-event Z-order concern.
            // Picker still needs to sit above slot overlays.
            _pickerContainer?.BringToFront();
        }

        private void RestoreConsumableVisuals(int envIndex)
        {
            if (isometricView == null) return;

            if (PlantManager.Instance != null)
            {
                foreach (var slot in PlantManager.Instance.Slots)
                {
                    if (slot.environmentIndex != envIndex) continue;
                    foreach (var c in slot.appliedConsumables)
                        isometricView.SpawnSlotConsumableVisual(slot.slotIndex, c.type);
                }
            }

            if (ConsumableManager.Instance != null)
            {
                foreach (var c in ConsumableManager.Instance.GetEnvConsumables(envIndex))
                    isometricView.SpawnEnvConsumableVisual(c.type);
            }
        }

        private void UpdateTitle()
        {
            if (backyardTitle == null || EnvironmentManager.Instance == null) return;
            var envs = EnvironmentManager.Instance.Environments;
            int idx = ActiveEnv;
            backyardTitle.text = (idx >= 0 && idx < envs.Count) ? envs[idx].environmentName : "Backyard";
        }

        private void BuildConsumablePicker()
        {
            _pickerContainer = new VisualElement();
            _pickerContainer.AddToClassList("consumable-picker");
            _pickerContainer.pickingMode = PickingMode.Ignore;
            terrariumPage.Add(_pickerContainer);

            _pickerBtn = new Button(ToggleDropdown);
            _pickerBtn.text = "";
            _pickerBtn.AddToClassList("consumable-picker-btn");
            _pickerContainer.Add(_pickerBtn);

            _iconsContainer = new VisualElement();
            _iconsContainer.AddToClassList("consumable-picker-icons");
            _pickerContainer.Add(_iconsContainer);
        }

        private void ToggleDropdown()
        {
            if (_pendingEnvConfirmType.HasValue)
            {
                CancelEnvConfirm();
                return;
            }
            if (_pendingType.HasValue)
            {
                CancelApplyMode();
                return;
            }

            bool open = _pickerContainer.ClassListContains("consumable-picker--open");
            if (open)
            {
                _pickerContainer.RemoveFromClassList("consumable-picker--open");
                return;
            }

            RefreshIcons();
            _pickerContainer.AddToClassList("consumable-picker--open");
        }

        private void RefreshIcons()
        {
            _iconsContainer.Clear();
            if (ConsumableManager.Instance == null) return;

            foreach (var c in ConsumableManager.Instance.AllConsumables)
            {
                int count = ConsumableManager.Instance.GetCount(c.type);
                if (count <= 0) continue;

                var btn = new Button();
                btn.AddToClassList("consumable-icon-btn");
                if (c.icon != null)
                    btn.style.backgroundImage = new StyleBackground(c.icon);

                var badge = new Label($"x{count}");
                badge.AddToClassList("consumable-icon-badge");
                btn.Add(badge);

                var capturedType = c.type;
                var capturedIsEnvScoped = c.isEnvironmentScoped;
                btn.clicked += () => OnConsumableRowTapped(capturedType, capturedIsEnvScoped);

                _iconsContainer.Add(btn);
            }
        }

        private void OnConsumableRowTapped(ConsumableType type, bool isEnvironmentScoped)
        {
            _pickerContainer.RemoveFromClassList("consumable-picker--open");

            if (isEnvironmentScoped)
            {
                var existingList = ConsumableManager.Instance?.GetEnvConsumables(ActiveEnv);
                if (existingList != null && existingList.Count > 0)
                {
                    ShowEnvReplaceConfirmation(type, existingList[0]);
                    return;
                }
                if (ConsumableManager.Instance != null &&
                    ConsumableManager.Instance.ApplyToEnvironment(type, ActiveEnv))
                {
                    isometricView?.SpawnEnvConsumableVisual(type);
                    RefreshPickerIndicator();
                }
                return;
            }

            // Slot-scoped: enter apply mode so player can tap a slot
            _pendingType = type;
            foreach (var btn in slotButtons)
                btn.AddToClassList("backyard-slot-apply-mode");
        }

        private void CancelApplyMode()
        {
            _pendingType = null;
            foreach (var btn in slotButtons)
                btn.RemoveFromClassList("backyard-slot-apply-mode");
        }

        private void RefreshPickerIndicator()
        {
            if (_pickerBtn == null || ConsumableManager.Instance == null) return;
            bool occupied = ConsumableManager.Instance.GetEnvConsumables(ActiveEnv).Count > 0;
            if (occupied)
                _pickerBtn.AddToClassList("consumable-picker-btn--occupied");
            else
                _pickerBtn.RemoveFromClassList("consumable-picker-btn--occupied");
        }

        private void ShowEnvReplaceConfirmation(ConsumableType newType, ConsumableData existingData)
        {
            _pendingEnvConfirmType = newType;
            _iconsContainer.Clear();
            _pickerContainer.AddToClassList("consumable-picker--open");

            var label = new Label($"Replace {existingData.displayName}?");
            label.AddToClassList("consumable-confirm-label");
            _iconsContainer.Add(label);

            var confirmBtn = new Button(() => ConfirmEnvReplace(newType));
            confirmBtn.text = "Replace";
            confirmBtn.AddToClassList("consumable-confirm-btn");
            _iconsContainer.Add(confirmBtn);

            var cancelBtn = new Button(CancelEnvConfirm);
            cancelBtn.text = "Cancel";
            cancelBtn.AddToClassList("consumable-confirm-cancel-btn");
            _iconsContainer.Add(cancelBtn);
        }

        private void ConfirmEnvReplace(ConsumableType newType)
        {
            _pendingEnvConfirmType = null;
            _pickerContainer.RemoveFromClassList("consumable-picker--open");
            if (ConsumableManager.Instance != null &&
                ConsumableManager.Instance.ApplyToEnvironment(newType, ActiveEnv))
            {
                isometricView?.SpawnEnvConsumableVisual(newType);
            }
            RefreshPickerIndicator();
        }

        private void CancelEnvConfirm()
        {
            _pendingEnvConfirmType = null;
            _pickerContainer.RemoveFromClassList("consumable-picker--open");
        }

        private void OnDestroy()
        {
            if (PlantManager.Instance != null)
            {
                PlantManager.Instance.OnSlotStateChanged -= OnSlotStateChanged;
                PlantManager.Instance.OnSlotGrowthUpdated -= OnSlotGrowthUpdated;
            }
            if (EnvironmentManager.Instance != null)
            {
                EnvironmentManager.Instance.OnSlotUnlocked -= OnSlotUnlocked;
                EnvironmentManager.Instance.OnActiveEnvironmentChanged -= OnActiveEnvironmentChanged;
            }
        }

        private void AddSlotButton(int slotIndex)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("backyard-slot-overlay");
            overlay.style.position = Position.Absolute;
            overlay.pickingMode = PickingMode.Ignore;

            var label = new Label();
            label.AddToClassList("backyard-slot-label");
            overlay.Add(label);

            var progressBar = new VisualElement();
            progressBar.AddToClassList("backyard-progress-bar");
            var fill = new VisualElement();
            fill.AddToClassList("backyard-progress-fill");
            progressBar.Add(fill);
            overlay.Add(progressBar);

            _slotContainer.Add(overlay);
            slotButtons.Add(overlay);
            labels.Add(label);
            _lastLabelText.Add(null);
            progressFills.Add(fill);
            progressBars.Add(progressBar);
        }

        private void OnSlotUnlocked(int envIndex)
        {
            if (envIndex != ActiveEnv) return;
            AddSlotButton(slotButtons.Count);
            ReorderSlotButtonsByDepth();
            RefreshAllSlots();
        }

        private void Update()
        {
            if (!initialized || !pageActive || isometricView == null || terrariumPage == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
                PositionButton(i);

            // Direct screen-space hit test — bypasses UI Toolkit coordinate systems entirely.
            // Pointer.current unifies mouse (editor) and primary touch (mobile).
            bool tapped = false;
            Vector2 tapScreenPos = default;
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame)
            {
                tapped = true;
                tapScreenPos = pointer.position.ReadValue();
            }

            if (tapped)
            {
                bool overlayOpen =
                    _satchelScrim?.resolvedStyle.display   == DisplayStyle.Flex ||
                    _harvestPopup?.resolvedStyle.display   == DisplayStyle.Flex ||
                    _discoveryPopup?.resolvedStyle.display == DisplayStyle.Flex;

                if (!overlayOpen)
                {
                    // Test front tiles first (higher index = visually in front).
                    for (int i = slotButtons.Count - 1; i >= 0; i--)
                    {
                        if (isometricView.GetTileScreenBounds(i).Contains(tapScreenPos))
                        {
                            OnSlotClicked(i);
                            break;
                        }
                    }
                }
            }

            if (PlantManager.Instance == null) return;

            for (int i = 0; i < slotButtons.Count; i++)
            {
                var slot = PlantManager.Instance.GetSlot(ActiveEnv, i);
                if (slot == null) continue;

                if (slot.state == PlantState.Growing)
                {
                    float hours = PlantManager.Instance.GetRemainingHours(ActiveEnv, i);
                    string text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < labels.Count && labels[i] != null && text != _lastLabelText[i])
                    {
                        labels[i].text = text;
                        _lastLabelText[i] = text;
                    }
                }
                else if (slot.state == PlantState.Mature)
                {
                    float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 3f);
                    isometricView.SetPlantScale(i, pulse);
                }
            }
        }

        private void PositionButton(int i)
        {
            if (i >= slotButtons.Count || terrariumPage?.panel == null) return;

            var screenRect = isometricView.GetTileScreenBounds(i);
            var panel = terrariumPage.panel;

            var bl = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.x, screenRect.y));
            var tr = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenRect.xMax, screenRect.yMax));

            float panelLeft   = Mathf.Min(bl.x, tr.x);
            float panelTop    = Mathf.Min(bl.y, tr.y);
            float panelWidth  = Mathf.Abs(tr.x - bl.x);
            float panelHeight = Mathf.Abs(bl.y - tr.y);

            if (panelWidth <= 0) return;
            // _slotContainer is at panel origin (0,0), so panel coords map directly.
            slotButtons[i].style.left   = panelLeft;
            slotButtons[i].style.top    = panelTop;
            slotButtons[i].style.width  = panelWidth;
            slotButtons[i].style.height = panelHeight;
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < slotButtons.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int i)
        {
            if (PlantManager.Instance == null || i >= slotButtons.Count) return;

            var slot = PlantManager.Instance.GetSlot(ActiveEnv, i);
            if (slot == null) return;

            var label       = i < labels.Count        ? labels[i]        : null;
            var fill        = i < progressFills.Count ? progressFills[i] : null;
            var progressBar = i < progressBars.Count  ? progressBars[i]  : null;

            switch (slot.state)
            {
                case PlantState.Empty:
                    isometricView?.SetEmptyIndicator(i, true);
                    if (label != null)       label.style.display       = DisplayStyle.None;
                    if (progressBar != null) progressBar.style.display = DisplayStyle.None;
                    if (fill != null) fill.style.width = new Length(0, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Empty, Color.clear);
                    isometricView?.ClearSlotConsumableVisuals(i);
                    break;

                case PlantState.Growing:
                    isometricView?.SetEmptyIndicator(i, false);
                    if (label != null)       label.style.display       = DisplayStyle.Flex;
                    if (progressBar != null) progressBar.style.display = DisplayStyle.Flex;
                    float hours = PlantManager.Instance.GetRemainingHours(ActiveEnv, i);
                    if (label != null)
                        label.text = hours > 1f ? $"{hours:F1}h" : $"{hours * 60f:F0}m";
                    if (i < _lastLabelText.Count) _lastLabelText[i] = null;
                    if (fill != null)
                        fill.style.width = new Length(slot.growthProgress * 100f, LengthUnit.Percent);
                    slotButtons[i].RemoveFromClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Growing,
                        slot.variant?.primaryColor ?? Color.green);
                    break;

                case PlantState.Mature:
                    isometricView?.SetEmptyIndicator(i, false);
                    if (label != null)       label.style.display       = DisplayStyle.Flex;
                    if (progressBar != null) progressBar.style.display = DisplayStyle.Flex;
                    if (label != null) label.text = "Harvest!";
                    if (fill  != null) fill.style.width = new Length(100, LengthUnit.Percent);
                    slotButtons[i].AddToClassList("backyard-slot-mature");
                    isometricView?.SetPlantVisual(i, PlantState.Mature,
                        slot.variant?.primaryColor ?? Color.green);
                    break;
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            // Slot-scoped apply mode: apply consumable to this slot
            if (_pendingType.HasValue)
            {
                var type = _pendingType.Value;
                CancelApplyMode();
                if (PlantManager.Instance != null &&
                    PlantManager.Instance.ApplyConsumable(type, ActiveEnv, slotIndex))
                {
                    isometricView?.SpawnSlotConsumableVisual(slotIndex, type);
                }
                return;
            }

            // Normal interaction
            if (PlantManager.Instance == null) return;
            var slot = PlantManager.Instance.GetSlot(ActiveEnv, slotIndex);
            if (slot == null) return;

            switch (slot.state)
            {
                case PlantState.Empty:  OnEmptySlotTapped?.Invoke(ActiveEnv, slotIndex);  break;
                case PlantState.Mature: OnMatureSlotTapped?.Invoke(ActiveEnv, slotIndex); break;
            }
        }

        private void OnSlotStateChanged(int envIndex, int slotIndex, PlantState state)
        {
            if (envIndex != ActiveEnv) return;
            if (slotIndex >= 0 && slotIndex < slotButtons.Count)
                RefreshSlot(slotIndex);
        }

        private void OnSlotGrowthUpdated(int envIndex, int slotIndex, float progress)
        {
            if (envIndex != ActiveEnv) return;
            if (slotIndex >= 0 && slotIndex < progressFills.Count && progressFills[slotIndex] != null)
                progressFills[slotIndex].style.width = new Length(progress * 100f, LengthUnit.Percent);
        }
    }
}
