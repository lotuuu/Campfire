# Multitouch Support for SwipeablePageView

**Date:** 2026-02-24
**Status:** Approved

## Problem

`SwipeablePageView` tracks a single active drag but does not guard against a second pointer starting while the first is active. A second finger mid-swipe can reset drag state or fire conflicting Move/Up events.

## Goal

Lock onto the first pointer that initiates a drag. Ignore all other pointers until that finger lifts or is cancelled.

## Approach: `activePointerId` field

Add `int activePointerId = -1` (-1 = no active gesture). Guard all four pointer handlers to only act on the active pointer.

### Changes to `SwipeablePageView.cs`

| Handler | Change |
|---|---|
| `OnPointerDown` | Return early if `activePointerId != -1`. Set `activePointerId = evt.pointerId` on first touch. |
| `OnPointerMove` | Return early if `evt.pointerId != activePointerId`. |
| `OnPointerUp` | Return early if `evt.pointerId != activePointerId`. Reset `activePointerId = -1` after finishing. |
| `OnPointerCancel` | Same as Up — guard + reset. |

The existing `isDragging` flag and pointer capture logic are unchanged.

## Edge Cases

- OS-level cancellation (e.g. incoming call) fires `OnPointerCancel` → resets `activePointerId`, next touch works normally.
- Mouse (pointerId 0) is always the only pointer, so it is unaffected.

## Scope

Single file: `Assets/Scripts/UI/SwipeablePageView.cs`
