using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public static class FlameLevelUpAnimator
    {
        public static bool IsPlaying { get; private set; }

        private struct Ember
        {
            public Vector2 position;
            public float speed;      // pixels per second upward
            public float drift;      // pixels per second horizontal
            public float lifetime;   // total seconds
            public float age;        // seconds elapsed
            public float radius;     // circle radius in px
        }

        private class AnimationState
        {
            public float elapsed;
            public float shockwaveRadius;
            public float shockwaveMaxRadius;
            public bool shockwaveDone;
            public List<Ember> embers = new();
            public bool embersDone;
        }

        private static void DrawOverlay(MeshGenerationContext ctx, AnimationState state)
        {
            var el = ctx.visualElement;
            float w = el.resolvedStyle.width;
            float h = el.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            var painter = ctx.painter2D;
            float cx = w / 2f;
            float cy = h / 2f;

            // Shockwave ring
            if (!state.shockwaveDone && state.shockwaveRadius > 0)
            {
                float progress = Mathf.Clamp01(state.shockwaveRadius / state.shockwaveMaxRadius);
                float alpha = 1f - progress;
                float lineWidth = Mathf.Lerp(4f, 1f, progress);

                painter.BeginPath();
                painter.Arc(new Vector2(cx, cy), state.shockwaveRadius, 0f, 360f);
                painter.ClosePath();
                painter.strokeColor = new Color(1f, 0.71f, 0.2f, alpha);
                painter.lineWidth = lineWidth;
                painter.Stroke();
            }

            // Embers
            for (int i = 0; i < state.embers.Count; i++)
            {
                var e = state.embers[i];
                if (e.age >= e.lifetime) continue;
                float alpha = 1f - (e.age / e.lifetime);
                painter.BeginPath();
                painter.Arc(new Vector2(cx + e.position.x, cy + e.position.y),
                    e.radius, 0f, 360f);
                painter.ClosePath();
                painter.fillColor = new Color(1f, 0.71f, 0.2f, alpha);
                painter.Fill();
            }
        }
    }
}
