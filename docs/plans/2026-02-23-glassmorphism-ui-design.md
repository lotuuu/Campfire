# Glassmorphism UI Design

**Date:** 2026-02-23
**Status:** Approved

## Summary

Replace the current opaque dark-green UI theme with a faux glassmorphism aesthetic using cool teal/blue-green tinted semi-transparent panels. Pure USS changes — no shaders, no C# changes, no UXML restructuring.

## Decisions

- **Approach:** Faux glassmorphism (no real backdrop-blur). Semi-transparent panels with subtle glow borders and tinted backgrounds simulate frosted glass. Zero GPU cost.
- **Color tone:** Cool teal/blue-green glass. Shifts away from the current forest-green toward a terrarium glass feel.
- **Scope:** All UI elements — HUD chips, nav bar, pulse button, and full-screen panels (Satchel, Codex, Greenhouse, Debug).

## Color Palette

### Backgrounds
| Token | Old Value | New Value |
|-------|-----------|-----------|
| `--color-bg-dark` | `rgb(15,20,15)` | `rgb(8,15,22)` |
| `--color-bg-panel` | `rgba(20,30,20,0.95)` | `rgba(15,25,35,0.55)` |
| `--color-bg-slot` | `rgba(40,55,40,0.7)` | `rgba(20,35,50,0.40)` |
| `--color-bg-slot-hover` | `rgba(55,75,55,0.85)` | `rgba(30,50,65,0.55)` |

### Borders
| Token | Old Value | New Value |
|-------|-----------|-----------|
| `--color-border` | `rgba(80,120,80,0.4)` | `rgba(120,200,220,0.15)` |
| `--color-border-accent` | `rgba(120,180,100,0.6)` | `rgba(140,230,240,0.35)` |

### Text
| Token | Old Value | New Value |
|-------|-----------|-----------|
| `--color-text` | `rgb(210,225,200)` | `rgb(200,220,230)` |
| `--color-text-dim` | `rgb(140,160,130)` | `rgb(120,150,170)` |
| `--color-text-bright` | `rgb(240,255,230)` | `rgb(230,245,255)` |
| `--color-text-accent` | `rgb(170,220,130)` | `rgb(100,220,220)` |

### Buttons
| Token | Old Value | New Value |
|-------|-----------|-----------|
| `--color-button-bg` | `rgba(50,80,50,0.85)` | `rgba(20,40,55,0.50)` |
| `--color-button-bg-hover` | `rgba(65,100,65,0.95)` | `rgba(30,55,70,0.65)` |
| `--color-button-bg-active` | `rgba(80,130,80,1)` | `rgba(40,70,85,0.75)` |
| `--color-button-bg-disabled` | `rgba(40,50,40,0.5)` | `rgba(15,25,35,0.35)` |

### Accents
| Token | Old Value | New Value |
|-------|-----------|-----------|
| `--color-highlight` | `rgb(255,220,80)` | `rgb(255,220,80)` (unchanged) |
| `--color-empty` | `rgba(80,80,80,0.3)` | `rgba(30,45,60,0.25)` |
| `--color-unknown` | `rgba(50,50,50,0.8)` | `rgba(15,25,35,0.60)` |

## Glass Surface Effects

All achieved via USS properties (no custom rendering):

1. **Thin borders (1px)** — Down from 2px. Cyan-tinted at low opacity for "light catching glass edge"
2. **Top-edge highlight** — `border-top-color` slightly brighter than other borders for light refraction simulation
3. **Glass-on-glass** — Grid items use different opacity/tint than their parent panel
4. **Hover glow** — Border opacity increases on hover (0.15 -> 0.35) for interactive feedback
5. **Smooth transitions** — 0.2s transitions on background-color and border-color

## Component Styling

| Element | Background | Border |
|---------|-----------|--------|
| Weather bar | `rgba(10,20,30,0.45)` | 1px cyan 0.20 |
| Currency panel | `rgba(10,20,30,0.45)` | 1px cyan 0.20 |
| Pulse button | `rgba(20,60,70,0.60)` | 2px bright cyan 0.40 |
| Nav bar | `rgba(10,20,30,0.50)` | top edge brighter |
| Nav buttons | `rgba(20,40,55,0.50)` | 1px cyan 0.20 |
| Full panels | `rgba(15,25,35,0.55)` | 1px cyan 0.15 |
| Grid items | `rgba(20,35,50,0.40)` | 1px cyan 0.15 |
| Detail subpanels | `rgba(15,30,40,0.50)` | 1px cyan 0.20 |
| Close buttons | `rgba(20,40,55,0.50)` | 1px cyan 0.25 |

## Files Changed

- `Assets/UI/Styles/Variables.uss` — Full palette replacement
- `Assets/UI/Styles/Common.uss` — Panel, button, grid-item glass styles + border width reduction
- `Assets/UI/Styles/HUD.uss` — Weather, currency, pulse, nav restyled
- `Assets/UI/Styles/Satchel.uss` — Probability panel colors
- `Assets/UI/Styles/Codex.uss` — Detail panel colors
- `Assets/UI/Styles/Greenhouse.uss` — Header/stat colors
- `Assets/UI/Styles/Debug.uss` — Slider/dropdown styling consistency

## Files NOT Changed

- No UXML changes
- No C# script changes
- No new files created
