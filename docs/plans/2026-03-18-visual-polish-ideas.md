# Visual Polish Ideas

Ideas for procedural animation and visual feedback improvements across the game. Many can reuse the `GlowAlpha` dictionary pattern from the level-up hex glow.

## Hex Grid Interactions

### Plot/Vase/Garden Crafting Animation
When a new hex entity is crafted and placed, animate it in with a scale-up bounce and a subtle glow ripple to neighboring cells. A mini version of the level-up cascade — makes placement feel satisfying rather than just appearing.

### Hex Drag-and-Move Trail
While dragging a hex entity to reposition it, the ghost cell could pulse gently and leave a fading gold tint on cells it passes over. Gives spatial feedback during the move.

## Growth & Harvest

### Sprite Stage Transitions
When a plot crosses a growth threshold (0% -> 50% -> 100%) and swaps sprites, add a brief cross-fade or scale pop to smooth the visual jump. Currently the sprite just snaps to the next stage.

### Harvest Quality Reveal
The quality score (0-1 from `GrowthRecipe.Evaluate()`) could drive a glow intensity on the harvest result card. High-quality harvests get a bright golden glow ring; low-quality gets a dim or absent glow. Makes the weather-matching system feel more rewarding.

### Watering Ripple
When a plot is watered, trigger a blue tint ripple spreading outward from the watered cell to its neighbors. Uses the same ring-delay pattern as the level-up glow but with a blue color and shorter duration.

## Resource Feedback

### Mana Generation Pulse
The flame hex could pulse subtly on each mana tick — a brief warm glow that scales with the current mana rate. Higher flame levels = more visible pulse. Makes the flame feel alive and productive even when idle.

### Vase Fill Shimmer
When a vase's water sprite swaps between fill levels (0% -> 50% -> 100%), add a brief upward shimmer or blue wave to sell the "filling up" feeling. Similar to the growth stage transition but with water-themed color.

## Mallum System

### Quest Departure/Return Wave
When a mallum departs on a quest, trigger a small directional ripple wave across the grid radiating outward from the mallum house. On return, reverse the direction (inward). Gives a sense of journey starting/ending.

### Reward Collection Flash
When quest rewards are collected and seeds appear in inventory, flash the apotheke nav button with a brief golden pulse. Connects the action to where the seeds went.

## Environment

### Weather Transition Cross-Fade
When `WeatherService` updates weather data, the top bar weather display could smoothly cross-fade between states (icons, temperature, description) rather than snapping instantly. A 300-500ms opacity transition on the weather elements.

### Ambient Hex Tint
A subtle global tint shift across all hex cells based on time of day or current weather. Warmer tones during sunny weather, cooler blue tint during rain, muted during overcast. Very low alpha (0.03-0.05) so it's felt more than seen.

## Priority & Effort Estimate

| Idea | Impact | Effort | Notes |
|------|--------|--------|-------|
| Mana generation pulse | High | Low | Reuses GlowAlpha pattern directly |
| Watering ripple | High | Low | Reuses ring-delay + GlowAlpha pattern |
| Crafting bounce + ripple | High | Medium | Needs scale animation + neighbor lookup |
| Harvest quality glow | Medium | Low | USS class with variable glow intensity |
| Weather cross-fade | Medium | Low | USS opacity transitions on weather elements |
| Sprite stage transitions | Medium | Medium | Needs cross-fade logic in sprite swap code |
| Reward collection flash | Low | Low | Single element pulse, similar to bar fade |
| Quest departure wave | Low | Medium | Directional ripple is new pattern |
| Vase fill shimmer | Low | Medium | Custom painter effect |
| Drag trail | Low | Low | Extend existing ghost cell logic |
| Ambient hex tint | Low | High | Needs continuous update loop for all cells |
