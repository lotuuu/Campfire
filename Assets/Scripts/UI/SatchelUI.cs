using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SatchelUI : MonoBehaviour
    {
        private VisualTreeAsset seedSlotTemplate;

        private VisualElement panel;
        private VisualElement scrim;
        private ScrollView seedList;

        private int targetEnvIndex = -1;
        private int targetSlotIndex = -1;

        private bool _isDragging;
        private float _dragStartY;

        public void Initialize(VisualElement root)
        {
            seedSlotTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/SeedSlot");

            panel    = root.Q<VisualElement>("satchel-panel");
            scrim    = root.Q<VisualElement>("satchel-scrim");
            seedList = root.Q<ScrollView>("seed-list");

            scrim.RegisterCallback<ClickEvent>(_ => Hide());

            var dragZone = root.Q<VisualElement>("satchel-drag-zone");
            dragZone.RegisterCallback<PointerDownEvent>(OnHandlePointerDown);
            panel.RegisterCallback<PointerMoveEvent>(OnPanelPointerMove);
            panel.RegisterCallback<PointerUpEvent>(OnPanelPointerUp);
            panel.RegisterCallback<PointerCancelEvent>(OnPanelPointerCancel);
        }

        public void Show() => Show(-1, -1);

        public void Show(int envIndex, int slotIndex)
        {
            targetEnvIndex  = envIndex;
            targetSlotIndex = slotIndex;

            scrim.style.display = DisplayStyle.Flex;
            panel.style.translate = new StyleTranslate(new Translate(0, 0));

            RefreshList();
        }

        public void Hide()
        {
            if (_isDragging) return;
            panel.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            panel.RemoveFromClassList("no-transition");
            panel.style.translate = new StyleTranslate(new Translate(0, Length.Percent(100)));
            panel.RegisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
        }

        private void OnHideTransitionEnd(TransitionEndEvent evt)
        {
            panel.UnregisterCallback<TransitionEndEvent>(OnHideTransitionEnd);
            scrim.style.display = DisplayStyle.None;
        }

        private void RefreshList()
        {
            seedList.Clear();
            var seeds = SeedRegistry.Instance.GetOwnedSeeds();

            if (seeds.Count == 0)
            {
                var hint = new Label("No seeds in your Satchel.\nVisit the Shop to buy some.");
                hint.AddToClassList("satchel-empty-hint");
                seedList.Add(hint);
                return;
            }

            foreach (var seed in seeds)
            {
                int count = SeedRegistry.Instance.GetSeedCount(seed.seedName);
                var slot  = SeedSlotUI.Create(seedSlotTemplate, seed, count, OnSeedTapped);
                slot.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
                seedList.Add(slot);
            }
        }

        private void OnSeedTapped(SeedData seed)
        {
            if (targetEnvIndex >= 0 && targetSlotIndex >= 0)
                PlantManager.Instance.Plant(seed, targetEnvIndex, targetSlotIndex);
            else
                PlantManager.Instance.Plant(seed);
            Hide();
        }

        private void OnHandlePointerDown(PointerDownEvent evt)
        {
            _isDragging = true;
            _dragStartY = evt.position.y;
            panel.AddToClassList("no-transition");
            panel.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPanelPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging) return;
            float delta = Mathf.Max(0f, evt.position.y - _dragStartY);
            panel.style.translate = new StyleTranslate(
                new Translate(0, new Length(delta, LengthUnit.Pixel)));
        }

        private void OnPanelPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            panel.ReleasePointer(evt.pointerId);
            panel.RemoveFromClassList("no-transition");

            float delta = evt.position.y - _dragStartY;
            if (delta > 80f)
                Hide();
            else
                panel.style.translate = new StyleTranslate(new Translate(0, 0));
        }

        private void OnPanelPointerCancel(PointerCancelEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            panel.ReleasePointer(evt.pointerId);
            panel.RemoveFromClassList("no-transition");
            panel.style.translate = new StyleTranslate(new Translate(0, 0));
        }
    }
}
