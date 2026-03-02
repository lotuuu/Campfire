# Garden — Design Critique & Recommended Improvements

Analysis using the MDA Framework, Flow Theory, Player Psychology, and Reward System design principles.

---

## MDA Framework Assessment

**Mechanics → Dynamics → Aesthetics**

| Layer | Intended | Actual Risk |
|---|---|---|
| **Mechanics** | Weather-driven genetics, quality RNG, multi-currency economy, decay timers | Well-defined. Clear rules with measurable outputs. |
| **Dynamics** | Players check weather, time planting strategically, optimize greenhouse | **Gap:** Dynamics heavily depend on external factors the player cannot control, creating a passivity problem. |
| **Aesthetics** | Discovery, collection satisfaction, connection to real world | Strong on Discovery and Sensation. Weak on Challenge, Fellowship, and Expression. |

The MDA gap is the central issue: the mechanics produce dynamics where the **optimal play is often to do nothing and wait**. When the weather doesn't cooperate, the player has no meaningful action. This inverts the engagement loop — instead of "play → reward," it becomes "wait → check → maybe play."

---

## Core Loop Quality Criteria

| Criterion | Rating | Notes |
|---|---|---|
| Fast feedback (<100ms) | **Weak** | Core feedback is hours away (growth time). The planting moment gives genetics feedback, but the payoff — harvest quality — is delayed by the full growth cycle. |
| Clear causation | **Mixed** | Weather → variant is clear conceptually, but opaque in practice. Players can't easily see *why* they got Astra Base vs. Solar Astra without consulting external weather data. The game tells you what grew, not why. |
| Rewarding outcomes | **Strong** | Quality tier reveals and new discoveries feel good. The Sell/Keep decision adds weight. |
| Compelling repetition | **Weak** | After filling all slots, the player has nothing to do until plants mature. The "check back later" pattern competes poorly against other mobile games that offer secondary activities. |

**Verdict:** The loop has a strong payoff moment (harvest) but a hollow middle (growth wait) and a passive entry point (weather dependency). The loop needs more **active verbs** between planting and harvesting.

---

## Flow Channel Analysis

```
     Anxiety
         ↑
         │              Late game:
  Hard   │              All slots full, waiting ──── BOREDOM
         │
         │     Mid game: Learning triggers,
         │     optimizing environments ──── FLOW (briefly)
         │
         │   Early game:
  Easy   │   Plant, wait, harvest ──── BOREDOM
         │
         └──────────────────────────────────→
           Low         Challenge          High
```

**Problem:** The game spends most of its time in the lower-left quadrant (low challenge, low skill demand). Weather-gating creates *waiting*, not *difficulty*. There's no skill expression — a veteran player and a brand-new player facing the same weather will get identical results. The only "skill" is knowledge of trigger thresholds, which becomes rote after a few cycles.

The game needs **player-driven challenge** that scales with mastery. Right now, progression is purely economic (buy more slots, buy more seeds), not skill-based.

---

## Player Psychology Assessment

### Bartle's Types

| Type | Served? | How |
|---|---|---|
| **Achiever** | Partially | Codex completion, quality chasing. But no achievements system, no milestones, no stats tracking. |
| **Explorer** | Strong | Variant discovery is the game's best mechanic for this type. Hints system is well-designed. |
| **Socializer** | **Not served** | The game spec mentions Pollen Exchange and leaderboards, but neither is implemented or designed in the GDD. The game is entirely solo. |
| **Killer/Competitor** | **Not served** | No competition, no leaderboards, no comparison mechanics. |

### Self-Determination Theory

| Need | Satisfied? | Notes |
|---|---|---|
| **Autonomy** | Mixed | Player chooses seeds and environments, but weather removes agency over the most interesting outcome (variant). Consumables help but are gated behind the least-earnable currency (SunShards). |
| **Competence** | **Weak** | No skill to master. Outcomes are determined by RNG and weather. A player can't "get better" at Garden. There's no learning curve that rewards mastery. |
| **Relatedness** | **Weak** | No social features. No characters. No narrative. The "connection to real world" is conceptually interesting but emotionally thin — watching a temperature number drive a lookup table isn't the same as feeling connected to your local weather. |

---

## Reward System Analysis

### Reward Types Present

| Type | Examples | Quality |
|---|---|---|
| Extrinsic (currency) | Gold, Pollen, SunShards | Functional but the three-currency system adds complexity without depth. Gold and Pollen could be one currency with different earn/spend rates. |
| Extrinsic (collection) | Codex variants | Strong. Best reward in the game. |
| Extrinsic (unlocks) | Environments, slots | Functional but linear. No branching choices. |
| Intrinsic (discovery) | First-time variant popup | Strong moment, but happens fewer times as the game progresses. |
| Intrinsic (mastery) | **Missing** | Nothing to get better at. |
| Intrinsic (expression) | **Missing** | No way to arrange, decorate, or personalize your garden. |

### Reward Scheduling

The current schedule is almost entirely **Fixed Interval** (plant, wait N hours, harvest). This is the least engaging schedule. The game would benefit from **Variable Ratio** elements (random events during growth) and **Milestone** rewards (Codex completion tiers, total harvest counts).

**Critical gap:** Rewards become sparser as the player progresses. Early game has frequent discoveries. Late game has almost none — the player has seen most variants, and is grinding currency for expensive unlocks with no new surprises along the way.

---

## Economy Balance Critique

### Faucets and Sinks

| Currency | Faucets | Sinks | Balance Issue |
|---|---|---|---|
| **Gold** | Selling harvests | Environments, slots, greenhouse expansion | **Front-loaded earning, back-loaded spending.** Early harvests feel worthwhile. But environment costs scale to 5,000+ while earning rate stays flat. The mid-game grind between Balcony (100) and Wild Patch (~1,000) is manageable, but Deep Conservatory (~5,000) requires dozens of harvests with no new mechanics to sustain interest. |
| **Pollen** | Greenhouse passive | Buying seeds | **Snowball problem.** More greenhouse plants → more Pollen → buy more seeds → more greenhouse plants. Once a player has 6+ rare plants in the greenhouse, Pollen becomes trivially abundant, removing purchasing tension. |
| **SunShards** | Achievements (unimplemented) | Consumables | **Dead currency.** With no earn mechanic, the 10 starter SunShards are spent and the system stops. Consumables become inaccessible, removing the player's only tool for weather manipulation. This makes the system feel like a trap: it teaches you about consumables, then takes them away. |

### Dominant Strategy Risk

The optimal play is: always Keep (never Sell) until Greenhouse is full → Pollen snowball → buy expensive seeds → Sell expensive harvests for Gold → unlock environments. This is a single optimal path with no meaningful trade-offs after the first few harvests.

---

## Specific Design Issues

### 1. The Passivity Problem (Most Critical)

The player's most common experience is: open app → all slots growing → nothing to do → close app. The game has **no secondary activity** during growth waits. Compare to successful idle games:
- *Stardew Valley*: Fish, mine, forage, socialize while crops grow
- *Egg Inc*: Tap to earn, prestige system, research tree
- *Neko Atsume*: Rearrange furniture, check visiting cats

**Recommendation:** Add a "tending" mechanic — small active tasks during growth (watering, turning toward light, pruning) that provide minor bonuses and give the player something to *do* each session.

### 2. Weather Creates Inequality, Not Engagement

A player in San Diego (perpetual clear skies, 20°C) will never naturally encounter Glacial Astra, Static Astra, or Dew-Drop Astra. Their Codex is permanently incomplete without consumables, which require an unearnable currency.

This isn't "hyper-contextual" — it's "hyper-exclusionary." The game's central promise (your weather makes your game unique) becomes a punishment for players in stable climates.

**Recommendation:** Add slow-cycling "micro-seasons" — artificial weather shifts that cycle through conditions over weeks, ensuring every player *eventually* encounters every trigger. Frame it as the garden having its own "resonance" that drifts. This preserves the weather-matching advantage (real storms still trigger instantly) while guaranteeing long-term completionism.

### 3. No Meaningful Decisions After Planting

Once a seed is planted, the player makes zero decisions until harvest. Growth is automatic. The only harvest decision (Sell vs. Keep) becomes formulaic quickly: keep rare/high-tier, sell common/low-tier.

**Recommendation:** Add growth-phase decisions. For example: at 50% growth, offer a "mutation window" where the player can spend a resource to re-roll the variant against current weather. Or let players choose to "rush harvest" at 80% growth for reduced quality but faster turnover.

### 4. Greenhouse Decay Is Punishing Without Counterplay

The decay system creates urgency but no agency. Plants decay on a fixed timer with nothing the player can do about it. This punishes players who can't check the app frequently (sleeping, working) and feels unfair rather than strategic.

**Recommendation:** Let players spend a resource to "preserve" a plant, pausing decay for a set time. Or add a "compost" mechanic where withered plants convert into fertilizer, so decay at least feeds back into the loop instead of being pure loss.

### 5. Progression Is Linear and Predictable

The environment unlock path is strictly sequential: Hearth → Balcony → Wild Patch → Deep Conservatory. There's no branching, no choice about which environment to unlock next, and no reason to specialize.

**Recommendation:** Let players choose between two environments at each tier (e.g., Balcony OR Rooftop Garden, each with different weather bonuses and slot layouts). This adds replayability and makes each player's garden feel personal.

### 6. The Codex Lacks Reward Tiers

Discovering all 12 variants of a seed gives no reward beyond the individual discovery moments. There's no milestone for completing a seed's full variant set, no reward for completing rarity tiers across all seeds, and no "prestige" for the Codex itself.

**Recommendation:** Add Codex milestones: "Discover all Astra variants" → unlock an exclusive cosmetic or bonus seed. "Discover all Rare variants across all seeds" → unlock a new environment theme. This transforms the Codex from a passive log into an active goal system.

### 7. No Narrative or Emotional Hook

The game has no story, no characters, no reason to care. The plant names are evocative (Blood-Moon Astra, Void Astra) but they exist in a vacuum. There's no lore explaining *why* weather creates these variants, no world to inhabit.

**Recommendation:** Add minimal worldbuilding through Codex flavor text. Each variant discovery could reveal a fragment of lore — "Herbalists in the northern valleys first cultivated the Glacial Astra during the Long Winter of..." This gives Explorer-type players a reason to chase completionism beyond the mechanical reward.

---

## Summary of Recommended Improvements

| Priority | Issue | Recommendation |
|---|---|---|
| **Critical** | Passivity during growth | Add tending/micro-interactions during growth phase |
| **Critical** | Climate inequality | Add slow-cycling micro-seasons as a safety net for stable climates |
| **High** | No skill expression | Add growth-phase decisions (mutation windows, early harvest trade-offs) |
| **High** | SunShards dead currency | Add a reliable earn path (daily tasks, Codex milestones, streak rewards) |
| **High** | Pollen snowball | Add Pollen sinks (cosmetics, plant upgrades, re-roll costs) or cap greenhouse output |
| **Medium** | Linear progression | Offer branching environment choices |
| **Medium** | Codex lacks milestones | Add tiered completion rewards |
| **Medium** | Greenhouse decay has no counterplay | Add preservation mechanic or compost recycling |
| **Low** | No social features | Design Pollen Exchange or async garden visiting |
| **Low** | No narrative | Add lore fragments to Codex discoveries |

---

## Conclusion

The game's **unique selling point** (real-world weather integration) is genuinely distinctive. But the design currently treats weather as the *only* interesting system, leaving the player with too little agency, too few decisions, and too much waiting. The improvements above aim to make weather the *context* for interesting choices, rather than a substitute for them.
