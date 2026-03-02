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
        private Vector2 centeredOffset;
        private bool panEnabledX;
        private bool panEnabledY;
        private const float DragThreshold = 10f;
        private float dragDistance;

        public bool WasDragged { get; private set; }

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
            float vpWidth = viewport.resolvedStyle.width;
            float vpHeight = viewport.resolvedStyle.height;

            if (float.IsNaN(vpWidth) || vpWidth <= 0)
            {
                vpWidth = 400f;
                vpHeight = 800f;
            }

            // Pan so that (focusX, focusY) on the canvas is at the viewport center
            centeredOffset = new Vector2(
                vpWidth / 2f - focusX,
                vpHeight / 2f - focusY
            );

            // Only enable panning on axes where canvas exceeds viewport
            panEnabledX = canvasWidth > vpWidth;
            panEnabledY = canvasHeight > vpHeight;

            panOffset = centeredOffset;
            ApplyPan();
        }

        private int activePointerId = -1;

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!panEnabledX && !panEnabledY) return;

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
            if (!isDragging || evt.pointerId != activePointerId) return;

            Vector2 delta = (Vector2)evt.position - dragStart;
            dragStart = evt.position;
            dragDistance += delta.magnitude;

            if (panEnabledX) panOffset.x += delta.x;
            if (panEnabledY) panOffset.y += delta.y;

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
            float canvasW = canvas.resolvedStyle.width;
            float canvasH = canvas.resolvedStyle.height;
            float vpW = viewport.resolvedStyle.width;
            float vpH = viewport.resolvedStyle.height;

            if (float.IsNaN(canvasW) || float.IsNaN(vpW)) return;

            // On each axis: if canvas fits, lock to centered. Otherwise clamp so edges stay visible.
            if (!panEnabledX)
            {
                panOffset.x = centeredOffset.x;
            }
            else
            {
                float minX = vpW - canvasW;
                float maxX = 0f;
                panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);
            }

            if (!panEnabledY)
            {
                panOffset.y = centeredOffset.y;
            }
            else
            {
                float minY = vpH - canvasH;
                float maxY = 0f;
                panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
            }
        }

        private void ApplyPan()
        {
            canvas.style.translate = new Translate(panOffset.x, panOffset.y, 0);
        }
    }
}
