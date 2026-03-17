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
            public float speed;
            public float drift;
            public float lifetime;
            public float age;
            public float radius;
        }

        private struct ShockwaveRing
        {
            public float startTime;   // when this ring starts expanding (relative to updater start)
            public float duration;
            public float radius;
            public float maxRadius;
            public bool done;
        }

        private class AnimationState
        {
            public float elapsed;
            public List<ShockwaveRing> rings = new();
            public List<Ember> embers = new();
            public float shakeAmount;
        }

        private static readonly Color GoldColor = new(1f, 0.71f, 0.2f, 1f);
        private static readonly Color BrightGold = new(1f, 0.85f, 0.4f, 1f);

        private static void DrawOverlay(MeshGenerationContext ctx, AnimationState state)
        {
            var el = ctx.visualElement;
            float w = el.resolvedStyle.width;
            float h = el.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            var painter = ctx.painter2D;
            float cx = w / 2f;
            float cy = h / 2f;

            // Central glow — warm radial gradient approximation via concentric circles
            float glowAlpha = Mathf.Clamp01(1f - state.elapsed / 2f) * 0.3f;
            if (glowAlpha > 0.01f)
            {
                for (int g = 3; g >= 0; g--)
                {
                    float r = 60f + g * 40f;
                    float a = glowAlpha * (1f - g * 0.25f);
                    painter.BeginPath();
                    painter.Arc(new Vector2(cx, cy), r, 0f, 360f);
                    painter.ClosePath();
                    painter.fillColor = new Color(1f, 0.71f, 0.2f, a);
                    painter.Fill();
                }
            }

            // Shockwave rings
            for (int i = 0; i < state.rings.Count; i++)
            {
                var ring = state.rings[i];
                if (ring.done || ring.radius <= 0) continue;
                float progress = Mathf.Clamp01(ring.radius / ring.maxRadius);
                float alpha = (1f - progress) * 0.9f;
                float lineWidth = Mathf.Lerp(6f, 1.5f, progress);

                painter.BeginPath();
                painter.Arc(new Vector2(cx, cy), ring.radius, 0f, 360f);
                painter.ClosePath();
                painter.strokeColor = new Color(1f, 0.71f, 0.2f, alpha);
                painter.lineWidth = lineWidth;
                painter.Stroke();

                // Inner bright ring for extra punch
                if (progress < 0.5f)
                {
                    painter.BeginPath();
                    painter.Arc(new Vector2(cx, cy), ring.radius - 2f, 0f, 360f);
                    painter.ClosePath();
                    painter.strokeColor = new Color(1f, 0.9f, 0.6f, alpha * 0.5f);
                    painter.lineWidth = 1.5f;
                    painter.Stroke();
                }
            }

            // Embers
            for (int i = 0; i < state.embers.Count; i++)
            {
                var e = state.embers[i];
                if (e.age >= e.lifetime) continue;
                float life = e.age / e.lifetime;
                float alpha = life < 0.1f ? life / 0.1f : 1f - ((life - 0.1f) / 0.9f);
                // Brighter core, dimmer outer
                float px = cx + e.position.x;
                float py = cy + e.position.y;

                // Outer glow
                painter.BeginPath();
                painter.Arc(new Vector2(px, py), e.radius * 2f, 0f, 360f);
                painter.ClosePath();
                painter.fillColor = new Color(1f, 0.6f, 0.1f, alpha * 0.2f);
                painter.Fill();

                // Core
                painter.BeginPath();
                painter.Arc(new Vector2(px, py), e.radius, 0f, 360f);
                painter.ClosePath();
                painter.fillColor = new Color(1f, 0.85f, 0.4f, alpha);
                painter.Fill();
            }
        }

        public static void Play(VisualElement root, VisualElement flameCell,
            VisualElement gridContainer, VisualElement viewport, int newLevel, Action onComplete)
        {
            if (IsPlaying) return;
            IsPlaying = true;

            var state = new AnimationState();
            IVisualElementScheduledItem updater = null;
            var createdElements = new List<VisualElement>();

            // ── Stage 1: Golden Screen Flash (0.0s–0.5s) ──
            var flash = new VisualElement();
            flash.AddToClassList("flame-flash-overlay");
            flash.pickingMode = PickingMode.Position;
            root.Add(flash);
            createdElements.Add(flash);

            // Trigger fade on next frame
            flash.schedule.Execute(() => flash.AddToClassList("flame-flash-overlay--fade"));

            // ── Stage 2: Flame Pulse (0.0s–0.8s) — bigger scale ──
            flameCell?.AddToClassList("grid-cell--levelup-pulse");
            flameCell?.schedule.Execute(() =>
            {
                flameCell.RemoveFromClassList("grid-cell--levelup-pulse");
            }).StartingIn(800);

            // ── Painter2D overlay for rings, embers, glow ──
            var overlay = new VisualElement();
            overlay.pickingMode = PickingMode.Ignore;
            overlay.StretchToParentSize();
            overlay.generateVisualContent += ctx => DrawOverlay(ctx, state);
            gridContainer.Add(overlay);
            createdElements.Add(overlay);

            float containerWidth = gridContainer.resolvedStyle.width;
            float maxRadius = float.IsNaN(containerWidth) ? 500f : containerWidth / 2f;

            // 3 staggered shockwave rings
            state.rings.Add(new ShockwaveRing { startTime = 0f, duration = 1.2f, maxRadius = maxRadius });
            state.rings.Add(new ShockwaveRing { startTime = 0.25f, duration = 1.0f, maxRadius = maxRadius * 0.85f });
            state.rings.Add(new ShockwaveRing { startTime = 0.5f, duration = 0.8f, maxRadius = maxRadius * 0.7f });

            // Init embers at 0.2s — 40 particles, bigger, faster
            gridContainer.schedule.Execute(() =>
            {
                var rng = new System.Random();
                for (int i = 0; i < 40; i++)
                {
                    state.embers.Add(new Ember
                    {
                        position = new Vector2(
                            (float)(rng.NextDouble() * 80 - 40),
                            (float)(rng.NextDouble() * 40 - 20)),
                        speed = (float)(rng.NextDouble() * 120 + 80),    // 80–200 px/s
                        drift = (float)(rng.NextDouble() * 60 - 30),     // wider drift
                        lifetime = (float)(rng.NextDouble() * 1.5 + 1.0), // 1–2.5s
                        age = 0f,
                        radius = (float)(rng.NextDouble() * 4 + 4)       // 4–8 px
                    });
                }
            }).StartingIn(200);

            // ── Viewport shake (0.0s–0.6s) — applied to viewport, not canvas ──
            state.shakeAmount = 8f;

            // Update loop at 0.1s — drives rings, embers, and shake
            gridContainer.schedule.Execute(() =>
            {
                var shakeRng = new System.Random(42);
                updater = overlay.schedule.Execute(() =>
                {
                    float dt = 0.016f;
                    state.elapsed += dt;

                    // Shockwave rings
                    for (int i = 0; i < state.rings.Count; i++)
                    {
                        var ring = state.rings[i];
                        if (ring.done) continue;
                        float ringElapsed = state.elapsed - ring.startTime;
                        if (ringElapsed < 0) continue;
                        ring.radius = (ringElapsed / ring.duration) * ring.maxRadius;
                        if (ringElapsed >= ring.duration) ring.done = true;
                        state.rings[i] = ring;
                    }

                    // Embers
                    for (int i = 0; i < state.embers.Count; i++)
                    {
                        var e = state.embers[i];
                        if (e.age >= e.lifetime) continue;
                        e.age += dt;
                        e.position.y -= e.speed * dt;
                        e.position.x += e.drift * dt;
                        state.embers[i] = e;
                    }

                    // Shake — applied to viewport so it doesn't conflict with pan controller's translate on canvas
                    if (state.shakeAmount > 0.1f)
                    {
                        state.shakeAmount *= 0.92f;
                        float sx = ((float)shakeRng.NextDouble() * 2f - 1f) * state.shakeAmount;
                        float sy = ((float)shakeRng.NextDouble() * 2f - 1f) * state.shakeAmount;
                        viewport.style.translate =
                            new Translate(new Length(sx, LengthUnit.Pixel), new Length(sy, LengthUnit.Pixel));
                    }
                    else if (state.shakeAmount > 0)
                    {
                        state.shakeAmount = 0;
                        viewport.style.translate = StyleKeyword.Null;
                    }

                    overlay.MarkDirtyRepaint();
                }).Every(16);
            }).StartingIn(100);

            // ── Stage 4: Hex Cascade (0.3s–2.0s) — brighter, staggered ──
            if (gridContainer.childCount > 0)
            {
                var cellsByRing = new Dictionary<int, List<VisualElement>>();
                foreach (var child in gridContainer.Children())
                {
                    if (!child.ClassListContains("grid-cell")) continue;
                    float childCX = child.resolvedStyle.left + child.resolvedStyle.width / 2f;
                    float childCY = child.resolvedStyle.top + child.resolvedStyle.height / 2f;
                    float contCX = gridContainer.resolvedStyle.width / 2f;
                    float contCY = gridContainer.resolvedStyle.height / 2f;
                    float dist = Vector2.Distance(
                        new Vector2(childCX, childCY),
                        new Vector2(contCX, contCY));
                    int ring = Mathf.RoundToInt(dist / 190f);
                    if (!cellsByRing.ContainsKey(ring))
                        cellsByRing[ring] = new List<VisualElement>();
                    cellsByRing[ring].Add(child);
                }

                foreach (var kvp in cellsByRing)
                {
                    int ring = kvp.Key;
                    // Start earlier, faster stagger, cap before cleanup
                    long delayMs = Math.Min(300 + ring * 150, 2200);
                    foreach (var cell in kvp.Value)
                    {
                        var c = cell;
                        gridContainer.schedule.Execute(() =>
                        {
                            c.AddToClassList("grid-cell--levelup-glow");
                            c.schedule.Execute(() =>
                            {
                                c.RemoveFromClassList("grid-cell--levelup-glow");
                            }).StartingIn(600); // longer glow
                        }).StartingIn(delayMs);
                    }
                }
            }

            // ── Stage 6: Level Badge (1.2s–3.5s) — bigger, with glow ──
            var badge = new VisualElement();
            badge.AddToClassList("flame-levelup-badge");
            var badgeText = new Label($"Level {newLevel}");
            badgeText.AddToClassList("flame-levelup-badge__text");
            badge.Add(badgeText);
            root.Add(badge);
            createdElements.Add(badge);

            root.schedule.Execute(() =>
            {
                badgeText.AddToClassList("flame-levelup-badge__text--visible");
                // Bigger bounce: 0 → 1.25 → 1.0
                badgeText.style.scale = new Scale(new Vector2(1.25f, 1.25f));
                badgeText.schedule.Execute(() =>
                {
                    badgeText.style.scale = new Scale(new Vector2(1f, 1f));
                }).StartingIn(300);
                // Fade out after 1.8s
                badgeText.schedule.Execute(() =>
                {
                    badgeText.RemoveFromClassList("flame-levelup-badge__text--visible");
                    badgeText.AddToClassList("flame-levelup-badge__text--fade");
                }).StartingIn(1800);
            }).StartingIn(1200);

            // ── Cleanup at ~3.5s ──
            root.schedule.Execute(() =>
            {
                updater?.Pause();
                flameCell?.RemoveFromClassList("grid-cell--levelup-pulse");
                viewport.style.translate = StyleKeyword.Null; // reset shake
                foreach (var el in createdElements)
                    el.RemoveFromHierarchy();
                IsPlaying = false;
                onComplete?.Invoke();
            }).StartingIn(3500);
        }

        /// <summary>
        /// Animate the new outer ring of hex cells after a grid expansion.
        /// Cells cascade in by angle, starting from the top and sweeping clockwise.
        /// </summary>
        public static void AnimateNewCells(Dictionary<(int, int), VisualElement> cellLookup, int oldRadius)
        {
            if (oldRadius <= 0) return;

            // Collect new cells with their angle from center for cascading
            var newCells = new List<(VisualElement cell, float angle)>();

            foreach (var kvp in cellLookup)
            {
                int q = kvp.Key.Item1;
                int r = kvp.Key.Item2;
                int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;
                if (dist <= oldRadius) continue;

                // Compute angle from center for cascade ordering (top = 0, clockwise)
                float px = q + r * 0.5f; // approximate x in hex space
                float py = r * 0.866f;   // approximate y in hex space
                float angle = Mathf.Atan2(px, -py); // top = 0, clockwise
                if (angle < 0) angle += Mathf.PI * 2f;

                newCells.Add((kvp.Value, angle));
            }

            if (newCells.Count == 0) return;

            // Sort by angle (top-first, clockwise)
            newCells.Sort((a, b) => a.angle.CompareTo(b.angle));

            // Hide all new cells
            foreach (var (cell, _) in newCells)
                cell.AddToClassList("grid-cell--reveal-hidden");

            // Cascade reveal with even spacing
            float delayPerCell = 60f; // ms between each cell
            for (int i = 0; i < newCells.Count; i++)
            {
                var c = newCells[i].cell;
                long delayMs = 200 + (long)(i * delayPerCell);
                c.schedule.Execute(() =>
                {
                    c.AddToClassList("grid-cell--reveal");
                    c.schedule.Execute(() =>
                    {
                        c.RemoveFromClassList("grid-cell--reveal-hidden");
                        c.RemoveFromClassList("grid-cell--reveal");
                    }).StartingIn(500);
                }).StartingIn(delayMs);
            }
        }
    }
}
