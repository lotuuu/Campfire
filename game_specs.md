# 📑 Game Specification: garden

**Project Name:** garden  
**Version:** 2.0 (Master Spec)  
**Genre:** Hyper-Contextual Simulation / Idle Collector  
**Platform:** iOS / Android (Requires GPS & Weather API)  
**Lead Designer:** Senior Mobile Architect (AI Persona)

---

## 1. Executive Summary
**garden** is a "Hyper-Contextual" mobile simulation game. Unlike traditional farming sims with static timers, **garden** uses real-world GPS and weather metadata to dictate plant growth, morphology, and harvest rarity. The player's physical environment becomes the primary game engine. 

---

## 2. Core Tech: The Resonance Engine
The game interfaces with global weather APIs to pull live data based on the player's coordinates. This data is fed into the **Genetics Matrix** at the moment of planting.

### Key Variables Tracked:
* **Thermal:** Actual local temperature (Celsius/Fahrenheit).
* **Luminosity:** Day/Night cycles, Golden Hour, and Cloud Cover.
* **Atmospheric:** Humidity, Precipitation (Rain/Snow), and Wind Speed.
* **Celestial:** Current Moon Phase and Zodiac Season.

### The Growth Formula:
The resulting plant variant $V$ is determined by:
$$V = \int(T, L, H) + S$$
*Where $T$=Temperature, $L$=Light, $H$=Humidity, and $S$=Base Seed Genetics.*

---

## 3. The Gameplay Loop
1.  **Detection:** The app scans the "Local Resonance" (e.g., *It is currently 22°C and Overcast*).
2.  **Seed Selection:** The player chooses a base seed (e.g., *Stellaris*).
3.  **Incubation:** The plant grows over 4–72 hours. If the real-world weather matches the plant's "Preferred State," growth speed increases by **25%**.
4.  **The Bloom Roll (Harvest):** The player harvests the plant to reveal its resulting "Quality Tier" and Variant. A harvest popup presents the result and offers two choices: **Sell** (receive Dewdrops = baseSellPrice × qualityMultiplier) or **Keep** (add to the Greenhouse for passive Aura Dust generation).

---

## 4. Harvest Quality & Economy (The "Gacha" System)
When a plant reaches 100% growth, harvesting triggers the **Bloom Roll**. Quality is determined randomly but is heavily influenced by real-world weather.

### A. The Quality Tiers (Letter-Based)
| Tier  | Label   | Base Probability | Value Multiplier | Visual State              |
| :---- | :------ | :--------------- | :--------------- | :------------------------ |
| **D** | Faded   | 15%              | 0.8x             | Small, desaturated.       |
| **C** | Stable  | 55%              | 1.0x             | Healthy, standard colors. |
| **B** | Vibrant | 20%              | 1.5x             | Bright, subtle glow.      |
| **A** | Radiant | 8%               | 2.2x             | Floating light particles. |
| **S** | Eternal | 2%               | 3.5x             | Pulsing bioluminescence.  |

### B. Weather Nudging (The "Sync Shield")
If the real-world weather matches the plant's preferred condition at harvest, the **Sync Shield** activates. Tier D (15%) is eliminated (0%). The lost 15% is redistributed to the top tiers, significantly boosting the chances of an A or S-Tier payout.

### C. Currencies & Monetization
* **Dewdrops (Soft):** Earned by harvesting common plants. Used for basic seeds, standard pots.
* **Sun-Shards (Hard):** Earned via achievements or IAP. Used for rare seeds, Greenhouse slots.
* **Aura Dust (Passive):** Produced by mature plants. Used for cosmetic upgrades, VFX.

**The Weather Merchant (Shop Items):**
* **Climate Bottles:** Consumables that "spoof" a specific weather condition for 2 hours (e.g., *Bottled Blizzard*).
* **Chrono-Mists:** Traditional "Time Skips" to finish a growth cycle instantly.
* **Global Pollen Pass:** Monthly subscription allowing unlimited "Cross-Pollination" with players in other hemispheres.

---

## 5. Progression & Social

### A. The Multi-Slot Terrarium
Players manage up to **12 slots** across different "Environments." 
* **The Hearth (2 Slots):** Default (Start). +10% Growth speed in Temperate weather.
* **The Balcony (2 Slots):** 5,000 Dewdrops. Boosts Wind/Rain sync chances.
* **The Wild Patch (4 Slots):** 15,000 Dewdrops. Higher rare variant spawn rates.
* **Deep Conservatory (4 Slots):** 25,000 Dewdrops. Allows cross-pollination.

### B. Social Connectivity
* **Pollen Exchange:** Players can generate a "Pollen QR Code" or link. A player in a **Desert** climate can send pollen to a player in a **Rainforest** to create "Hybrid Anomalies" impossible to grow alone.
* **Regional Leaderboards:** Monthly showcases for the most impressive "Storm-Grown" or "Midnight-Grown" gardens in specific cities.

---

## 6. Meta-Systems: Codex & Greenhouse

### 📖 The Flora Codex (Discovery Log)
* Every base seed has **12 distinct variants** based on environmental "Triggers".
* Undiscovered variants appear as shadowy silhouettes.
* Tapping an undiscovered variant gives a cryptic hint (e.g., *"Requires the breath of a storm"* for the Wind variant).

### 🏛️ The Greenhouse
Once mature, plants can be moved to the permanent **Greenhouse** to generate passive currency instead of selling them.

---

## 7. Seed Catalog & Variants

### Initial Shop Inventory
| Seed Name       | Buy Price | Base Sell (C-Tier) | Special Condition                    |
| :-------------- | :-------- | :----------------- | :----------------------------------- |
| **Astra**       | 100       | 120                | None (Starter)                       |
| **Cinder-Fern** | 450       | 550                | +10% S-Tier chance if Temp > 25°C    |
| **Mist-Vine**   | 800       | 1,000              | +10% S-Tier chance if Humidity > 70% |
| **Luna-Petal**  | 1,500     | 1,900              | Only grows during Nighttime          |
| **Storm-Root**  | 3,000     | 4,000              | +20% S-Tier chance during Wind/Rain  |

### Starter Profile: The "Astra" Seed
| Variant Name         | Trigger Condition    | Visual Appearance                         | Rarity    |
| :------------------- | :------------------- | :---------------------------------------- | :-------- |
| **Astra Base**       | Standard / Room Temp | Green stems, simple white petals.         | Common    |
| **Solar Astra**      | Clear Skies + >25°C  | Petals turn gold; leaves grow thick.      | Common    |
| **Lunar Astra**      | Nighttime (Any)      | Petals turn silver; glows in the dark.    | Common    |
| **Glacial Astra**    | Temp < 5°C           | Crystalline texture; icy blue tint.       | Rare      |
| **Dew-Drop Astra**   | Rain / High Humidity | Sagging stems; holds liquid spheres.      | Rare      |
| **Gale-Force Astra** | Wind > 20mph         | Spiraled, corkscrew stem; vibrates.       | Rare      |
| **Blood-Moon Astra** | Total Lunar Eclipse  | Deep crimson; petals resemble feathers.   | Legendary |
| **Equinox Astra**    | Spring/Fall Equinox  | Perfect symmetry; dual-colored petals.    | Legendary |
| **Petrified Astra**  | Extreme Heat (>38°C) | Charcoal-grey; fire-resistant aesthetic.  | Rare      |
| **Nebula Astra**     | Golden Hour (Sunset) | Purple/Pink gradients; trailing dust.     | Uncommon  |
| **Static Astra**     | Thunderstorm         | Constant electrical arcs between leaves.  | Epic      |
| **Void Astra**       | New Moon + Midnight  | Almost invisible; only the outline glows. | Epic      |
*(Table Data Sourced from Design Phase v1.1)*

### The "Astra" Resolution Logic
To determine which variant is "born," the game runs a priority check upon the **Plant** action. The engine processes inputs in the following order:
1.  **Priority 1: Celestial/Calendar Events:** Is it an Equinox? Is there an Eclipse? Overrides all other weather conditions.
2.  **Priority 2: Extreme Atmospheric Triggers:** Is there an active Storm/High Wind alert? Is the Temp < 5°C or > 38°C? Assigns rare/epic environmental variants.
3.  **Priority 3: Temporal/Light Triggers:** Is it Night, Golden Hour, or Bright Daylight? Assigns common time-based variants.
4.  **Priority 4: Default:** If no specific environmental thresholds are met, the result is **Astra Base**.

---

## 8. UI/UX & Aesthetic Architecture

The UI uses a **Glassmorphism** aesthetic—semi-transparent panels that blur the background, making the app feel like a physical glass terrarium resting on your screen. Art style revolves around "Lofi-Bioluminescence," featuring soft edges, glowing veins in leaves, and fluid, wind-reactive animations.

### A. Primary "Hortus" View (Main Screen)
* **The Centerpiece:** A 3D/Isometric view of your active planting pot.
* **The Resonance Bar (Top):** A thin, pulsing bar showing current local metadata (e.g., *“18°C • Overcast • Waxing Crescent”*).
* **The Pulse Button:** A large, haptically-reactive button at the bottom center. If empty, tapping opens the Seed Satchel. If growing, tapping releases a "Pulse" ripple that reveals the remaining growth time.
* **Visual Weather Overlay:** If it is raining at the player's GPS location, digital rain streaks appear on the inner "glass" of the screen.

### B. The Seed Satchel (Inventory)
* **Grid View:** Icons of seeds you’ve collected.
* **Probability Preview:** Selecting a seed highlights which variants are currently "High Probability" based on the current weather. Example: If it's night, the "Lunar" variant icon will glow.

### C. Audio & Haptic Feedback
* **Adaptive Audio:** The background music is a generative ambient track. Rainy Days add soft piano and muffled textures. Nighttime lowers the BPM and adds synth pads. High Wind increases the frequency of "chime" sound effects.
* **Haptics:** Gentle vibrations that mimic rain taps or wind gusts when interacting with the plants.

### D. The Dynamic Background (Atmospheric Shader)
A procedural gradient background that acts as a "Living Canvas," reflecting the time of day and weather. Features drifting mist/particles whose speed correlates to real-world wind APIs.
* **Clear Day:** Baby Blue to Alice Blue.
* **Golden Hour:** Warm Pink to Soft Orange with long light rays.
* **Midnight:** Deep Navy to Slate Blue with a subtle star-field.
* **Stormy:** Dark Gray to Charcoal with occasional soft lightning flashes.

---

## 9. Technical Requirements
* **API Integration:** OpenWeatherMap or Apple WeatherKit.
* **Location Services:** Required for core loop (Low-power background polling).
* **Engine:** Unity or Godot (Optimized for 2D/3D hybrid rendering).
* **Backend System:** Firebase Firestore (for syncing garden state across devices and calculating secure, server-side bloom rolls).

---

## Appendix: Developer Generation Prompts (For Claude/AI)

### 1. The Background Shader Prompt
> "Write a highly optimized Unity HLSL URP shader (or React/Tailwind equivalent) for a dynamic background. It needs a vertical linear gradient (`topColor`, `bottomColor`) with a 5-second smoothing cross-fade when colors update. Include a slow-moving noise layer (Mist) and drifting soft particles (Wind). Include an inner vignette. Provide a dictionary of `WeatherPresets` with Hex codes for Golden Hour, Midnight, Stormy, and Clear Day."

### 2. The Harvest Logic Prompt
> "Write a Javascript function `getHarvestResult(seedType, currentConditions)`. The base probabilities are D: 0.15, C: 0.55, B: 0.20, A: 0.08, S: 0.02. If `seedType.preferredWeather == currentConditions`, trigger a 'Sync Shield'. The Shield sets Tier D to 0, leaving C at 0.50, and redistributes the 0.15 to the upper tiers (B: 0.30, A: 0.15, S: 0.05). Return the Tier, a Value Multiplier (D=0.8, C=1.0, B=1.5, A=2.2, S=3.5), and a boolean `isWeatherMatched`."

### 3. The UI Mockup Prompt (For Image Generators)
> "A high-fidelity mobile game UI wireframe for an app called 'garden'. Vertical 9:16 aspect ratio. Dark, lofi-aesthetic background. Top: a sleek 'Resonance Bar' with minimalist weather icons. Middle: A 3x4 grid of 12 translucent frosted-glass squares (plant slots). One slot is active with a glowing bioluminescent sprout. Bottom: A circular 'Pulse' button. The entire UI uses Glassmorphism with soft blurred edges and thin-line iconography."

---

> **Designer's Note:** The "Astra" seed is designed to ensure players see a different result almost immediately based on when they download the app (Day vs. Night), instantly proving the core mechanic.

> **Design Note:** Retention is driven by "FOMO" (Fear Of Missing Out) on rare weather events. If a hurricane or rare snowstorm hits the player's area, the app should send a high-priority notification: *"A rare Atmospheric Rift has opened! Plant now to claim a Storm-Weaver Fern."*
