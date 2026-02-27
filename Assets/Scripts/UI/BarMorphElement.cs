using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class BarMorphElement : VisualElement
    {
        private float _progress;
        private Color _color = new Color(0.549f, 0.902f, 0.941f, 0.18f);

        private const float ArmLength = 36f;
        private const float AngleDeg = 25f;
        private const float LineWidth = 3f;

        public float Progress
        {
            get => _progress;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_progress, value)) return;
                _progress = value;
                MarkDirtyRepaint();
            }
        }

        public Color StrokeColor
        {
            get => _color;
            set
            {
                if (_color == value) return;
                _color = value;
                MarkDirtyRepaint();
            }
        }

        public BarMorphElement()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            float w = contentRect.width;
            float h = contentRect.height;
            if (w <= 0 || h <= 0) return;

            float centerX = w / 2f;
            float barY = h - LineWidth / 2f;

            float fullHalfWidth = w / 2f;
            float halfWidth = Mathf.Lerp(fullHalfWidth, ArmLength, _progress);
            float angle = Mathf.Lerp(0f, AngleDeg * Mathf.Deg2Rad, _progress);

            float rise = halfWidth * Mathf.Sin(angle);
            float cosX = halfWidth * Mathf.Cos(angle);

            painter.strokeColor = _color;
            painter.lineWidth = LineWidth;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX - cosX, barY));
            painter.LineTo(new Vector2(centerX, barY - rise));
            painter.LineTo(new Vector2(centerX + cosX, barY));
            painter.Stroke();
        }
    }
}
