using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class CampsitePanController
    {
        private readonly VisualElement viewport;
        private readonly VisualElement canvas;

        private bool isDragging;
        private Vector2 dragStart;
        private Vector2 panOffset;
        private const float DragThreshold = 20f;
        private float dragDistance;

        // Stored from CenterOnPoint — used to recompute bounds dynamically
        private float focusPtX;
        private float focusPtY;
        private float storedCanvasW;
        private float storedCanvasH;

        public bool WasDragged { get; private set; }
        public bool SuppressMove { get; set; }

        public CampsitePanController(VisualElement viewport, VisualElement canvas)
        {
            this.viewport = viewport;
            this.canvas = canvas;

            viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        public void CenterOnPoint(float focusX, float focusY, float canvasWidth, float canvasHeight)
        {
            focusPtX = focusX;
            focusPtY = focusY;
            storedCanvasW = canvasWidth;
            storedCanvasH = canvasHeight;

            var (vpW, vpH) = GetViewportSize();

            panOffset = new Vector2(
                vpW / 2f - focusX,
                vpH / 2f - focusY
            );
            ClampPan();
            ApplyPan();
        }

        private int activePointerId = -1;

        private (float w, float h) GetViewportSize()
        {
            float w = viewport.resolvedStyle.width;
            float h = viewport.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0) { w = 400f; h = 800f; }
            return (w, h);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var (vpW, vpH) = GetViewportSize();
            bool canPanX = storedCanvasW > vpW;
            bool canPanY = storedCanvasH > vpH;
            if (!canPanX && !canPanY) return;

            isDragging = true;
            WasDragged = false;
            dragDistance = 0f;
            dragStart = evt.position;
            activePointerId = evt.pointerId;
            // Don't capture yet — let clicks reach child elements.
            // Capture only once drag threshold is exceeded.
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (SuppressMove) return;
            if (!isDragging || evt.pointerId != activePointerId) return;

            Vector2 delta = (Vector2)evt.position - dragStart;
            dragStart = evt.position;
            dragDistance += delta.magnitude;

            var (vpW, vpH) = GetViewportSize();
            if (storedCanvasW > vpW) panOffset.x += delta.x;
            if (storedCanvasH > vpH) panOffset.y += delta.y;

            ClampPan();
            ApplyPan();

            if (dragDistance > DragThreshold)
            {
                WasDragged = true;
                // Now capture to prevent stray clicks during drag
                if (!viewport.HasPointerCapture(evt.pointerId))
                    viewport.CapturePointer(evt.pointerId);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging || evt.pointerId != activePointerId) return;
            isDragging = false;
            activePointerId = -1;
            if (viewport.HasPointerCapture(evt.pointerId))
                viewport.ReleasePointer(evt.pointerId);
            // Reset so stale WasDragged doesn't block the next tap's ClickEvent
            WasDragged = false;
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!isDragging || evt.pointerId != activePointerId) return;
            isDragging = false;
            activePointerId = -1;
            if (viewport.HasPointerCapture(evt.pointerId))
                viewport.ReleasePointer(evt.pointerId);
        }

        private void ClampPan()
        {
            var (vpW, vpH) = GetViewportSize();
            if (storedCanvasW <= 0) return;

            // On each axis: if canvas fits, lock to centered. Otherwise clamp so edges stay visible.
            if (storedCanvasW <= vpW)
            {
                panOffset.x = vpW / 2f - focusPtX;
            }
            else
            {
                float minX = vpW - storedCanvasW;
                float maxX = 0f;
                panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);
            }

            if (storedCanvasH <= vpH)
            {
                panOffset.y = vpH / 2f - focusPtY;
            }
            else
            {
                float minY = vpH - storedCanvasH;
                float maxY = 0f;
                panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
            }
        }

        private void ApplyPan()
        {
            canvas.style.translate = new Translate(panOffset.x, panOffset.y, 0);
        }

        /// <summary>
        /// Smoothly animate the pan to center on a point over the given duration.
        /// </summary>
        public void AnimateCenterOnPoint(float focusX, float focusY, float canvasWidth, float canvasHeight, float durationMs = 600f)
        {
            focusPtX = focusX;
            focusPtY = focusY;
            storedCanvasW = canvasWidth;
            storedCanvasH = canvasHeight;

            var (vpW, vpH) = GetViewportSize();
            var targetOffset = new Vector2(
                vpW / 2f - focusX,
                vpH / 2f - focusY
            );
            // Clamp target
            if (storedCanvasW <= vpW)
                targetOffset.x = vpW / 2f - focusPtX;
            else
                targetOffset.x = Mathf.Clamp(targetOffset.x, vpW - storedCanvasW, 0f);
            if (storedCanvasH <= vpH)
                targetOffset.y = vpH / 2f - focusPtY;
            else
                targetOffset.y = Mathf.Clamp(targetOffset.y, vpH - storedCanvasH, 0f);

            var startOffset = panOffset;
            float elapsed = 0f;

            canvas.schedule.Execute(() =>
            {
                elapsed += 16f;
                float t = Mathf.Clamp01(elapsed / durationMs);
                // Ease-out cubic
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                panOffset = Vector2.Lerp(startOffset, targetOffset, ease);
                ApplyPan();
            }).Every(16).Until(() => elapsed >= durationMs);
        }
    }
}
