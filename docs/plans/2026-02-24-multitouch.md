# Multitouch First-Touch Lock Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Lock `SwipeablePageView` onto the first pointer that initiates a drag, ignoring all other pointers until that finger lifts.

**Architecture:** Add an `activePointerId` field (int, default -1). Guard all four pointer event handlers to early-return when `evt.pointerId` doesn't match. Reset on Up/Cancel.

**Tech Stack:** Unity 6, UIElements (`VisualElement`, pointer events), C#, NUnit EditMode tests

> **Testing note:** UIElements pointer-event simulation requires a live panel (runtime). The existing test suite is pure-logic EditMode tests. The one unit-testable piece is the pointer-ID guard logic extracted as a helper; the rest is verified manually in the editor.

---

### Task 1: Add `activePointerId` field and guard `OnPointerDown`

**Files:**
- Modify: `Assets/Scripts/UI/SwipeablePageView.cs`

**Step 1: Add the field after the existing `bool` fields (~line 22)**

```csharp
private int activePointerId = -1;
```

Place it with the other drag-state fields:
```csharp
private bool isDragging;
private bool pointerCaptured;
private float dragStartX;
private float dragStartY;
private float dragCurrentX;
private bool dragDirectionLocked;
private bool dragIsHorizontal;
private float pageWidth;
private int activePointerId = -1;   // ← add this
```

**Step 2: Guard `OnPointerDown` to reject a second finger**

Current start of `OnPointerDown`:
```csharp
private void OnPointerDown(PointerDownEvent evt)
{
    if (evt.button != 0) return;
    isDragging = true;
    ...
```

Replace with:
```csharp
private void OnPointerDown(PointerDownEvent evt)
{
    if (evt.button != 0) return;
    if (activePointerId != -1) return;   // ← ignore second finger
    activePointerId = evt.pointerId;      // ← lock to this finger
    isDragging = true;
    ...
```

**Step 3: Verify the file compiles — check Unity console for errors**

---

### Task 2: Guard `OnPointerMove`, `OnPointerUp`, `OnPointerCancel`

**Files:**
- Modify: `Assets/Scripts/UI/SwipeablePageView.cs`

**Step 1: Guard `OnPointerMove`**

Current:
```csharp
private void OnPointerMove(PointerMoveEvent evt)
{
    if (!isDragging) return;
    ...
```

Replace with:
```csharp
private void OnPointerMove(PointerMoveEvent evt)
{
    if (!isDragging) return;
    if (evt.pointerId != activePointerId) return;   // ← add this line
    ...
```

**Step 2: Guard `OnPointerUp` and reset `activePointerId`**

Current:
```csharp
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
```

Replace with:
```csharp
private void OnPointerUp(PointerUpEvent evt)
{
    if (!isDragging) return;
    if (evt.pointerId != activePointerId) return;   // ← ignore other fingers
    activePointerId = -1;                            // ← unlock
    FinishDrag();
    if (pointerCaptured)
    {
        this.ReleasePointer(evt.pointerId);
        pointerCaptured = false;
    }
}
```

**Step 3: Guard `OnPointerCancel` and reset `activePointerId`**

Current:
```csharp
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
```

Replace with:
```csharp
private void OnPointerCancel(PointerCancelEvent evt)
{
    if (!isDragging) return;
    if (evt.pointerId != activePointerId) return;   // ← ignore other fingers
    activePointerId = -1;                            // ← unlock
    FinishDrag();
    if (pointerCaptured)
    {
        this.ReleasePointer(evt.pointerId);
        pointerCaptured = false;
    }
}
```

**Step 4: Check Unity console — zero compile errors**

---

### Task 3: Manual verification in Unity Editor

**Steps:**

1. Enter Play mode in the Unity Editor
2. Navigate to the greenhouse or codex page (swipe works with single finger/mouse click-drag)
3. On device or with a multi-touch simulator: start a swipe with one finger, place a second finger down mid-swipe — the page should complete the swipe driven by the first finger only
4. Lift both fingers — next swipe should work normally

**Expected:** No jitter, no snap-back, second finger has zero effect.

---

### Task 4: Commit

```bash
git add Assets/Scripts/UI/SwipeablePageView.cs
git commit -m "feat: ignore second touch while swipe gesture is active"
```
