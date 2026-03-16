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

        public static void Play(VisualElement root, VisualElement flameCell,
            VisualElement gridContainer, int newLevel, Action onComplete)
        {
            if (IsPlaying) return;
            IsPlaying = true;

            var state = new AnimationState();
            IVisualElementScheduledItem updater = null;
            var createdElements = new List<VisualElement>();

            // ── Stage 1: Screen Flash (0.0s–0.3s) ──
            var flash = new VisualElement();
            flash.AddToClassList("flame-flash-overlay");
            flash.pickingMode = PickingMode.Position; // blocks input
            root.Add(flash);
            createdElements.Add(flash);

            // Trigger fade on next frame so transition runs
            flash.schedule.Execute(() => flash.AddToClassList("flame-flash-overlay--fade"));

            // ── Stage 2: Flame Pulse (0.0s–0.6s) ──
            // Add pulse class (scales to 1.2 via USS transition over 600ms)
            flameCell?.AddToClassList("grid-cell--levelup-pulse");
            // Remove pulse class at 600ms — the element reverts to base scale (1.0).
            // Since the transition is defined on the pulse class itself, the snap-back
            // is instant, but at this point the shockwave and cascade are drawing
            // attention away from the flame cell, so the instant revert is fine.
            flameCell?.schedule.Execute(() =>
            {
                flameCell.RemoveFromClassList("grid-cell--levelup-pulse");
            }).StartingIn(600);

            // ── Stage 3: Shockwave Ring (0.2s) + Stage 5: Embers (0.3s) — via Painter2D overlay ──
            var overlay = new VisualElement();
            overlay.pickingMode = PickingMode.Ignore;
            overlay.StretchToParentSize();
            overlay.generateVisualContent += ctx => DrawOverlay(ctx, state);
            gridContainer.Add(overlay);
            createdElements.Add(overlay);

            float containerWidth = gridContainer.resolvedStyle.width;
            state.shockwaveMaxRadius = float.IsNaN(containerWidth) ? 400f : containerWidth / 2f;

            // Init embers at 0.3s
            gridContainer.schedule.Execute(() =>
            {
                var rng = new System.Random();
                for (int i = 0; i < 20; i++)
                {
                    state.embers.Add(new Ember
                    {
                        position = new Vector2(
                            (float)(rng.NextDouble() * 60 - 30),
                            (float)(rng.NextDouble() * 30 - 15)),
                        speed = (float)(rng.NextDouble() * 80 + 60),   // 60–140 px/s
                        drift = (float)(rng.NextDouble() * 40 - 20),   // -20 to +20 px/s
                        lifetime = (float)(rng.NextDouble() * 1.0 + 1.0), // 1–2s
                        age = 0f,
                        radius = (float)(rng.NextDouble() * 3 + 3)     // 3–6 px
                    });
                }
            }).StartingIn(300);

            // Start update loop at 0.2s (shockwave starts immediately within the loop)
            const float shockwaveDuration = 1.0f;

            gridContainer.schedule.Execute(() =>
            {
                updater = overlay.schedule.Execute(() =>
                {
                    float dt = 0.016f; // ~60fps
                    state.elapsed += dt;

                    // Shockwave — starts immediately (the 0.2s delay is the StartingIn below)
                    if (!state.shockwaveDone)
                    {
                        state.shockwaveRadius = (state.elapsed / shockwaveDuration)
                            * state.shockwaveMaxRadius;
                        if (state.elapsed >= shockwaveDuration)
                            state.shockwaveDone = true;
                    }

                    // Embers
                    bool anyAlive = false;
                    for (int i = 0; i < state.embers.Count; i++)
                    {
                        var e = state.embers[i];
                        if (e.age >= e.lifetime) continue;
                        e.age += dt;
                        e.position.y -= e.speed * dt; // rise upward (negative Y)
                        e.position.x += e.drift * dt;
                        state.embers[i] = e;
                        if (e.age < e.lifetime) anyAlive = true;
                    }
                    overlay.MarkDirtyRepaint();
                }).Every(16);
            }).StartingIn(200);

            // ── Stage 4: Hex Cascade (0.4s–1.5s) ──
            if (gridContainer.childCount > 0)
            {
                // Collect cells by ring distance from center
                var cellsByRing = new Dictionary<int, List<VisualElement>>();
                foreach (var child in gridContainer.Children())
                {
                    if (!child.ClassListContains("grid-cell")) continue;
                    // Compute ring distance using element position relative to container center
                    float childCX = child.resolvedStyle.left + child.resolvedStyle.width / 2f;
                    float childCY = child.resolvedStyle.top + child.resolvedStyle.height / 2f;
                    float contCX = gridContainer.resolvedStyle.width / 2f;
                    float contCY = gridContainer.resolvedStyle.height / 2f;
                    float dist = Vector2.Distance(
                        new Vector2(childCX, childCY),
                        new Vector2(contCX, contCY));
                    // Approximate ring number: distance / hex spacing (~190px per ring at HexSize=220)
                    int ring = Mathf.RoundToInt(dist / 190f);
                    if (!cellsByRing.ContainsKey(ring))
                        cellsByRing[ring] = new List<VisualElement>();
                    cellsByRing[ring].Add(child);
                }

                foreach (var kvp in cellsByRing)
                {
                    int ring = kvp.Key;
                    // Cap delay so glow-add + 400ms glow-remove finishes before 2500ms cleanup
                    long delayMs = Math.Min(400 + ring * 200, 1700);
                    foreach (var cell in kvp.Value)
                    {
                        var c = cell; // capture
                        gridContainer.schedule.Execute(() =>
                        {
                            c.AddToClassList("grid-cell--levelup-glow");
                            // Remove glow after 400ms
                            c.schedule.Execute(() =>
                            {
                                c.RemoveFromClassList("grid-cell--levelup-glow");
                            }).StartingIn(400);
                        }).StartingIn(delayMs);
                    }
                }
            }

            // ── Stage 6: Level Badge (1.0s–2.5s) ──
            var badge = new VisualElement();
            badge.AddToClassList("flame-levelup-badge");
            var badgeText = new Label($"Level {newLevel}");
            badgeText.AddToClassList("flame-levelup-badge__text");
            badge.Add(badgeText);
            root.Add(badge);
            createdElements.Add(badge);

            // Bounce in at 1.0s: scale 0 → 1.15 (250ms via USS transition), then 1.15 → 1.0 (200ms)
            root.schedule.Execute(() =>
            {
                badgeText.AddToClassList("flame-levelup-badge__text--visible");
                // Phase 1: USS transition animates scale from 0 → 1.15 over 250ms
                badgeText.style.scale = new Scale(new Vector2(1.15f, 1.15f));
                // Phase 2: settle to 1.0 after 250ms (USS transition animates 1.15 → 1.0)
                badgeText.schedule.Execute(() =>
                {
                    badgeText.style.scale = new Scale(new Vector2(1f, 1f));
                }).StartingIn(250);
                // Fade out after 1.5s from badge appear
                badgeText.schedule.Execute(() =>
                {
                    badgeText.RemoveFromClassList("flame-levelup-badge__text--visible");
                    badgeText.AddToClassList("flame-levelup-badge__text--fade");
                }).StartingIn(1500);
            }).StartingIn(1000);

            // ── Cleanup at ~2.5s ──
            root.schedule.Execute(() =>
            {
                updater?.Pause();
                flameCell?.RemoveFromClassList("grid-cell--levelup-pulse");
                foreach (var el in createdElements)
                    el.RemoveFromHierarchy();
                IsPlaying = false;
                onComplete?.Invoke();
            }).StartingIn(2500);
        }
    }
}
