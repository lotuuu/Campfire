# Reward Reveal Screen Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-screen reward reveal overlay that shows earned items with fanfare, usable by quests, visitors, birds, and any future reward source.

**Architecture:** A new `RewardRevealUI` MonoBehaviour singleton follows the same pattern as `DialogueUI` — caches UXML element refs via `Initialize(root)`, builds reward cards dynamically in C#, and exposes `Show(title, rewards, onCollect)` / `Hide()`. Styled via a dedicated USS file. Integrated into `CampFireUI` initialization chain.

**Tech Stack:** Unity UI Toolkit (UXML + USS + C#), SpriteService for seed sprites, ConfigService for tier lookup.

**Spec:** `docs/superpowers/specs/2026-03-11-reward-reveal-design.md`

---

## Chunk 1: Core UI

### Task 1: Add UXML elements and USS stylesheet

**Files:**
- Create: `Assets/UI/Styles/RewardReveal.uss`
- Modify: `Assets/UI/Documents/CampFireRoot.uxml:1-19` (add stylesheet import)
- Modify: `Assets/UI/Documents/CampFireRoot.uxml:337-343` (add UXML elements between tutorial hint bar and dialogue overlay)

- [ ] **Step 1: Create `Assets/UI/Styles/RewardReveal.uss`**

```css
/* RewardReveal.uss — full-screen reward reveal overlay */

#reward-reveal-overlay {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.85);
    align-items: center;
    justify-content: center;
    display: none;
    opacity: 0;
    transition-property: opacity;
    transition-duration: 200ms;
}

#reward-reveal-overlay.reward-reveal--visible {
    opacity: 1;
}

#reward-reveal-title {
    font-size: var(--font-xl);
    color: var(--color-text-accent);
    -unity-font-style: bold;
    -unity-text-align: middle-center;
    margin-bottom: var(--spacing-xl);
}

#reward-reveal-cards {
    flex-direction: row;
    justify-content: center;
    align-items: center;
    flex-wrap: wrap;
    padding: 0 var(--spacing-lg);
}

.reward-card {
    width: 150px;
    height: 200px;
    margin: var(--spacing-sm);
    border-radius: var(--radius-md);
    background-color: rgba(20, 14, 8, 0.95);
    align-items: center;
    justify-content: center;
    border-width: 2px;
    border-color: rgba(255, 230, 170, 0.8);
    scale: 0;
    transition-property: scale;
    transition-duration: 300ms;
    transition-timing-function: ease-out;
}

.reward-card.reward-card--visible {
    scale: 1;
}

/* Tier border colors */
.reward-card--tier0 { border-color: rgba(255, 230, 170, 0.8); }
.reward-card--tier1 { border-color: rgba(80, 190, 100, 0.8); }
.reward-card--tier2 { border-color: rgba(80, 150, 220, 0.8); }
.reward-card--tier3 { border-color: rgba(170, 100, 220, 0.8); }
.reward-card--tier4 { border-color: rgba(255, 200, 60, 0.9); }

.reward-card-glow {
    position: absolute;
    left: -10px;
    top: -10px;
    right: -10px;
    bottom: -10px;
    border-radius: var(--radius-lg);
    border-width: 2px;
    border-color: rgba(255, 230, 170, 0.4);
    scale: 1.2;
    opacity: 0;
    transition-property: scale, opacity;
    transition-duration: 400ms;
    transition-timing-function: ease-out;
}

.reward-card--visible > .reward-card-glow {
    scale: 1;
    opacity: 1;
}

/* Tier glow colors */
.reward-card--tier0 > .reward-card-glow { border-color: rgba(255, 230, 170, 0.4); }
.reward-card--tier1 > .reward-card-glow { border-color: rgba(80, 190, 100, 0.4); }
.reward-card--tier2 > .reward-card-glow { border-color: rgba(80, 150, 220, 0.4); }
.reward-card--tier3 > .reward-card-glow { border-color: rgba(170, 100, 220, 0.4); }
.reward-card--tier4 > .reward-card-glow { border-color: rgba(255, 200, 60, 0.5); }

.reward-card-sprite {
    width: 80px;
    height: 80px;
    -unity-background-scale-mode: scale-to-fit;
    margin-bottom: var(--spacing-xs);
}

.reward-card-name {
    font-size: var(--font-sm);
    color: var(--color-text);
    -unity-text-align: middle-center;
    white-space: normal;
}

.reward-card-count {
    position: absolute;
    top: 6px;
    right: 8px;
    font-size: var(--font-sm);
    color: var(--color-text-bright);
    -unity-font-style: bold;
}

#reward-reveal-collect {
    margin-top: var(--spacing-xl);
    padding: var(--spacing-sm) var(--spacing-xl);
    background-color: var(--color-button-bg);
    border-radius: var(--radius-md);
    border-width: 2px;
    border-color: var(--color-border-accent);
    font-size: var(--font-md);
    color: var(--color-text-accent);
    -unity-font-style: bold;
}

#reward-reveal-collect:hover {
    background-color: var(--color-button-bg-hover);
}

#reward-reveal-collect:active {
    background-color: var(--color-button-bg-active);
}
```

- [ ] **Step 2: Add stylesheet import to `CampFireRoot.uxml`**

After line 19 (`Tutorial.uss` import), add:
```xml
    <Style src="project://database/Assets/UI/Styles/RewardReveal.uss" />
```

- [ ] **Step 3: Add UXML elements to `CampFireRoot.uxml`**

Between the tutorial hint bar closing tag (line ~343) and the dialogue overlay comment (line ~345), insert:
```xml
        <!-- Reward reveal overlay -->
        <ui:VisualElement name="reward-reveal-overlay" style="display: none;">
            <ui:Label name="reward-reveal-title" text="" />
            <ui:VisualElement name="reward-reveal-cards" />
            <ui:Button name="reward-reveal-collect" text="Collect" />
        </ui:VisualElement>
```

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/Styles/RewardReveal.uss Assets/UI/Documents/CampFireRoot.uxml
git commit -m "feat(ui): add reward reveal overlay UXML and USS"
```

---

### Task 2: Create RewardRevealUI controller

**Files:**
- Create: `Assets/Scripts/UI/RewardRevealUI.cs`

- [ ] **Step 1: Create `Assets/Scripts/UI/RewardRevealUI.cs`**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Garden
{
    public class RewardRevealUI : MonoBehaviour
    {
        public static RewardRevealUI Instance { get; private set; }

        private VisualElement overlay;
        private Label titleLabel;
        private VisualElement cardsContainer;
        private Button collectBtn;

        private Action onCollect;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Initialize(VisualElement root)
        {
            overlay = root.Q("reward-reveal-overlay");
            titleLabel = root.Q<Label>("reward-reveal-title");
            cardsContainer = root.Q("reward-reveal-cards");
            collectBtn = root.Q<Button>("reward-reveal-collect");

            collectBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Collect();
            });

            // Block clicks from falling through to elements behind
            overlay?.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        public void Show(string title, List<RewardEntry> rewards, Action onCollectCallback)
        {
            if (overlay == null || rewards == null || rewards.Count == 0)
            {
                onCollectCallback?.Invoke();
                return;
            }

            onCollect = onCollectCallback;
            titleLabel.text = title;
            cardsContainer.Clear();

            overlay.style.display = DisplayStyle.Flex;
            // Trigger fade-in on next frame so transition plays
            overlay.schedule.Execute(() => overlay.AddToClassList("reward-reveal--visible"));

            AudioManager.Instance?.PlaySFX("ui_panel_open");

            StartCoroutine(RevealCards(rewards));
        }

        public void Hide()
        {
            if (overlay == null) return;
            overlay.RemoveFromClassList("reward-reveal--visible");
            overlay.style.display = DisplayStyle.None;
            cardsContainer.Clear();
        }

        private void Collect()
        {
            AudioManager.Instance?.PlaySFX("ui_panel_close");
            var callback = onCollect;
            onCollect = null;
            Hide();
            callback?.Invoke();
        }

        private IEnumerator RevealCards(List<RewardEntry> rewards)
        {
            foreach (var reward in rewards)
            {
                var card = BuildCard(reward);
                cardsContainer.Add(card);

                // Stagger: wait a frame then trigger scale-in
                yield return null;
                card.AddToClassList("reward-card--visible");

                yield return new WaitForSeconds(0.1f);
            }
        }

        private VisualElement BuildCard(RewardEntry reward)
        {
            int tier = ConfigService.Instance?.GetSeed(reward.seedName)?.tier ?? 0;
            string tierClass = $"reward-card--tier{Mathf.Min(tier, 4)}";

            var card = new VisualElement();
            card.AddToClassList("reward-card");
            card.AddToClassList(tierClass);

            // Glow element behind card
            var glow = new VisualElement();
            glow.AddToClassList("reward-card-glow");
            card.Add(glow);

            // Seed sprite
            var sprite = new VisualElement();
            sprite.AddToClassList("reward-card-sprite");
            string spriteKey = $"items/{SpriteService.SeedToSpriteKey(reward.seedName)}/seed";
            var tex = SpriteService.Instance?.GetTexture(spriteKey);
            if (tex != null)
                sprite.style.backgroundImage = new StyleBackground(tex);
            card.Add(sprite);

            // Seed name
            var nameLabel = new Label(PlotManager.GetSeedDisplayName(reward.seedName));
            nameLabel.AddToClassList("reward-card-name");
            card.Add(nameLabel);

            // Count badge (only if > 1)
            if (reward.count > 1)
            {
                var countLabel = new Label($"x{reward.count}");
                countLabel.AddToClassList("reward-card-count");
                card.Add(countLabel);
            }

            return card;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/UI/RewardRevealUI.cs
git commit -m "feat(ui): add RewardRevealUI controller with card reveal animation"
```

---

### Task 3: Wire into CampFireUI initialization

**Files:**
- Modify: `Assets/Scripts/UI/CampFireUI.cs:29-30` (add field)
- Modify: `Assets/Scripts/UI/CampFireUI.cs:94-100` (add initialization)

- [ ] **Step 1: Add field to CampFireUI**

After `private TutorialUI tutorialUI;` (line ~30), add:
```csharp
        private RewardRevealUI rewardRevealUI;
```

- [ ] **Step 2: Add initialization call**

After `tutorialUI?.Initialize(root);` (line ~100), add:
```csharp
            rewardRevealUI = GetComponent<RewardRevealUI>();
            rewardRevealUI?.Initialize(root);
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/CampFireUI.cs
git commit -m "feat(ui): wire RewardRevealUI into CampFireUI initialization"
```

---

## Chunk 2: Quest Integration

### Task 4: Integrate reward reveal into QuestUI

**Files:**
- Modify: `Assets/Scripts/UI/QuestUI.cs:182-219` (speed-up and quest-complete handlers)

The goal: when the player speeds up a quest or collects rewards from a completed quest, show the reward reveal screen instead of immediately collecting. The `onCollect` callback handles the actual `CollectQuestRewards` call.

- [ ] **Step 1: Modify speed-up handler (OnQuest case, ~line 182-186)**

Replace:
```csharp
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpQuest(mallumIndex);
                            Refresh();
                        };
```

With:
```csharp
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.SpeedUpQuest(mallumIndex);
                            var mallumData = SaveManager.Instance.Data.mallums[mallumIndex];
                            var rewards = new List<RewardEntry>(mallumData.pendingRewards);
                            RewardRevealUI.Instance?.Show("Quest Complete!", rewards, () =>
                            {
                                MallumManager.Instance.CollectQuestRewards(mallumIndex);
                                Refresh();
                            });
                        };
```

- [ ] **Step 2: Modify collect-rewards handler (QuestComplete case, ~line 214-219)**

Replace:
```csharp
                        actionBtn.text = "Collect Rewards";
                        actionBtn.AddToClassList("quest-collect-btn");
                        actionBtn.clicked += () =>
                        {
                            MallumManager.Instance.CollectQuestRewards(mallumIndex);
                            Refresh();
                        };
```

With:
```csharp
                        actionBtn.text = "Collect Rewards";
                        actionBtn.AddToClassList("quest-collect-btn");
                        actionBtn.clicked += () =>
                        {
                            var rewards = new List<RewardEntry>(mallum.pendingRewards);
                            RewardRevealUI.Instance?.Show("Quest Complete!", rewards, () =>
                            {
                                MallumManager.Instance.CollectQuestRewards(mallumIndex);
                                Refresh();
                            });
                        };
```

- [ ] **Step 3: Add `using System.Collections.Generic;`** at top of QuestUI.cs if not already present (needed for `List<RewardEntry>` constructor).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/QuestUI.cs
git commit -m "feat(quest): show reward reveal screen on quest completion"
```

---

### Task 5: Add RewardRevealUI component to Unity scene

**Files:**
- Modify: Unity scene (via MCP)

- [ ] **Step 1: Add RewardRevealUI MonoBehaviour to the `--- UI ---` GameObject**

Use Unity MCP `manage_components` tool to add `Garden.RewardRevealUI` to the same GameObject that has `CampFireUI`, `DialogueUI`, `TutorialUI`, etc.

- [ ] **Step 2: Check console for compilation errors**

Use `read_console` MCP tool. Fix any issues.

- [ ] **Step 3: Enter play mode and test the quest flow**

Verify:
- Send mallum on quest → speed up → reward reveal shows with title "Quest Complete!" and seed cards
- Cards scale in with stagger
- Tapping "Collect" adds seeds to inventory and closes overlay
- Quest refreshes to show mallum as Idle

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/Garden.unity
git commit -m "feat(scene): add RewardRevealUI component to UI GameObject"
```
