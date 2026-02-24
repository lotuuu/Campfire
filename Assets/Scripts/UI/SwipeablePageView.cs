using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class SwipeablePageView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SwipeablePageView> { }

        private readonly VisualElement pageContainer;

        private int currentPageIndex;
        private int pageCount;
        private bool isDragging;
        private bool pointerCaptured;
        private float dragStartX;
        private float dragStartY;
        private float dragCurrentX;
        private bool dragDirectionLocked;
        private bool dragIsHorizontal;
        private float pageWidth;

        private const float SwipeThreshold = 50f;
        private const float DirectionLockThreshold = 10f;
        private const int AnimationDurationMs = 300;

        public int CurrentPageIndex => currentPageIndex;
        public int PageCount => pageCount;
        /// <summary>Current animated X translate of the page container, in panel points.</summary>
        public float CurrentPageContainerX => pageContainer.resolvedStyle.translate.x;
        public float PageWidth => pageWidth;
        public event Action<int> OnPageChanged;

        public SwipeablePageView()
        {
            style.overflow = Overflow.Hidden;
            style.flexGrow = 1;

            pageContainer = new VisualElement();
            pageContainer.style.flexDirection = FlexDirection.Row;
            pageContainer.style.height = new Length(100, LengthUnit.Percent);
            pageContainer.style.position = Position.Absolute;
            pageContainer.style.left = 0;
            pageContainer.style.top = 0;
            pageContainer.style.bottom = 0;
            hierarchy.Add(pageContainer);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void AddPage(VisualElement page)
        {
            page.style.height = new Length(100, LengthUnit.Percent);
            page.style.flexShrink = 0;
            // Width set in absolute pixels by UpdatePageWidths (not percentage,
            // since the container is N*viewport wide and 100% would resolve too large)
            if (pageWidth > 0)
                page.style.width = pageWidth;
            pageContainer.Add(page);
            pageCount = pageContainer.childCount;
            UpdateContainerWidth();
        }

        public void GoToPage(int index, bool animated = true)
        {
            index = Mathf.Clamp(index, 0, pageCount - 1);
            if (index == currentPageIndex && !isDragging) return;
            currentPageIndex = index;
            AnimateToCurrentPage(animated);
            OnPageChanged?.Invoke(currentPageIndex);
        }

        private void AnimateToCurrentPage(bool animated)
        {
            float target = -currentPageIndex * pageWidth;
            if (animated)
            {
                pageContainer.style.transitionProperty = new List<StylePropertyName> { new("translate") };
                pageContainer.style.transitionDuration = new List<TimeValue> { new(AnimationDurationMs, TimeUnit.Millisecond) };
                pageContainer.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut) };
            }
            else
            {
                pageContainer.style.transitionDuration = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            }
            pageContainer.style.translate = new Translate(target, 0);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            isDragging = true;
            pointerCaptured = false;
            dragStartX = evt.position.x;
            dragStartY = evt.position.y;
            dragCurrentX = evt.position.x;
            dragDirectionLocked = false;
            dragIsHorizontal = false;

            pageContainer.style.transitionDuration = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            // Don't capture pointer yet — wait until drag direction is confirmed horizontal.
            // Capturing immediately would steal events from child ScrollViews.
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging) return;

            float dx = evt.position.x - dragStartX;
            float dy = evt.position.y - dragStartY;

            if (!dragDirectionLocked)
            {
                if (Mathf.Abs(dx) > DirectionLockThreshold || Mathf.Abs(dy) > DirectionLockThreshold)
                {
                    dragDirectionLocked = true;
                    dragIsHorizontal = Mathf.Abs(dx) > Mathf.Abs(dy);
                    if (dragIsHorizontal)
                    {
                        this.CapturePointer(evt.pointerId);
                        pointerCaptured = true;
                    }
                    else
                    {
                        // Vertical drag — abort tracking so ScrollView keeps control.
                        isDragging = false;
                        return;
                    }
                }
            }

            if (!dragDirectionLocked || !dragIsHorizontal) return;

            dragCurrentX = evt.position.x;
            float offset = dragCurrentX - dragStartX;
            float baseX = -currentPageIndex * pageWidth;

            if ((currentPageIndex == 0 && offset > 0) || (currentPageIndex == pageCount - 1 && offset < 0))
                offset *= 0.3f;

            pageContainer.style.translate = new Translate(baseX + offset, 0);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging) return;
            FinishDrag();
            if (pointerCaptured)
            {
                this.ReleasePointer(evt.pointerId);
                pointerCaptured = false;
            }
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!isDragging) return;
            FinishDrag();
            if (pointerCaptured)
            {
                this.ReleasePointer(evt.pointerId);
                pointerCaptured = false;
            }
        }

        private void FinishDrag()
        {
            isDragging = false;
            if (!dragIsHorizontal)
            {
                AnimateToCurrentPage(false);
                return;
            }

            float dx = dragCurrentX - dragStartX;
            int targetPage = currentPageIndex;

            if (Mathf.Abs(dx) > SwipeThreshold)
            {
                if (dx < 0 && currentPageIndex < pageCount - 1)
                    targetPage = currentPageIndex + 1;
                else if (dx > 0 && currentPageIndex > 0)
                    targetPage = currentPageIndex - 1;
            }

            bool pageChanged = targetPage != currentPageIndex;
            currentPageIndex = targetPage;
            AnimateToCurrentPage(true);
            if (pageChanged)
                OnPageChanged?.Invoke(currentPageIndex);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            pageWidth = resolvedStyle.width;
            UpdatePageWidths();
            UpdateContainerWidth();
            AnimateToCurrentPage(false);
        }

        private void UpdatePageWidths()
        {
            if (pageWidth <= 0) return;
            foreach (var child in pageContainer.Children())
                child.style.width = pageWidth;
        }

        private void UpdateContainerWidth()
        {
            if (pageWidth > 0)
                pageContainer.style.width = pageCount * pageWidth;
        }
    }
}
