# 📑 Game Specification: garden

**Project Name:** garden  
**Version:** 1.1  
**Status:** Design Phase  
**Lead Designer:** Senior Mobile Architect (AI Persona)

---

## 1. Executive Summary
**garden** is a "Hyper-Contextual" mobile simulation game. Unlike traditional farming sims with static timers, **garden** uses real-world GPS and weather metadata to dictate plant growth, morphology, and rarity. The player's physical environment becomes the primary game engine.

---

## 2. The Resonance Engine (Core Tech)
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

## 3. Gameplay Mechanics

### 🪴 The Planting Loop
1.  **Detection:** The app scans the "Local Resonance" (e.g., *It is currently 22°C and Overcast*).
2.  **Seed Selection:** The player chooses a base seed (e.g., *Stellaris*).
3.  **Incubation:** The plant grows over 4–72 hours. If the real-world weather matches the plant's "Preferred State," growth speed increases by **25%**.
4.  **Harvest/Transfer:** Once mature, the plant is moved to the permanent **Greenhouse** to generate passive currency.

### 🧬 The Flora Codex
Every base seed has **12 distinct variants** based on environmental "Triggers":
* **Frost-Glass Variant:** Requires planting during $T < 5$°C.
* **Biolume Variant:** Requires maturity reached during a New Moon.
* **Static Variant:** Requires a local Thunderstorm/High Wind alert.

---

## 4. Economic Model

### Currencies
| Currency       | Type    | Acquisition               | Usage                         |
| :------------- | :------ | :------------------------ | :---------------------------- |
| **Dewdrops**   | Soft    | Harvesting common plants  | Basic seeds, standard pots.   |
| **Sun-Shards** | Hard    | Achievements, IAP         | Rare seeds, Greenhouse slots. |
| **Aura Dust**  | Passive | Produced by mature plants | Cosmetic upgrades, VFX.       |

### The Weather Merchant (Monetization)
* **Climate Bottles:** Consumables that "spoof" a specific weather condition for 2 hours (e.g., *Bottled Blizzard*).
* **Chrono-Mists:** Traditional "Time Skips" to finish a growth cycle instantly.
* **Global Pollen Pass:** Monthly subscription allowing unlimited "Cross-Pollination" with players in other hemispheres.

---

## 5. Social & Connectivity
* **Pollen Exchange:** Players can generate a "Pollen QR Code" or link. A player in a **Desert** climate can send pollen to a player in a **Rainforest** to create "Hybrid Anomalies" impossible to grow alone.
* **Regional Leaderboards:** Monthly showcases for the most impressive "Storm-Grown" or "Midnight-Grown" gardens in specific cities.

---

## 6. Visual & Audio Direction
* **Art Style:** "Lofi-Bioluminescence." Soft edges, glowing veins in leaves, and fluid, wind-reactive animations.
* **Adaptive Audio:** The background music is a generative ambient track. 
    * *Rainy Day:* Adds soft piano and muffled textures.
    * *Nighttime:* Lowers the BPM and adds synth pads.
    * *High Wind:* Increases the frequency of "chime" sound effects.

---

## 7. Technical Requirements
* **API Integration:** OpenWeatherMap or Apple WeatherKit.
* **Location Services:** Required for core loop (Low-power background polling).
* **Engine:** Unity or Godot (Optimized for 2D/3D hybrid rendering).

---

## 8. UI Screen Architecture (Interface Design)

The UI uses a **Glassmorphism** aesthetic—semi-transparent panels that blur the background, making the app feel like a physical glass terrarium resting on your screen.

### A. The Primary "Hortus" View (Main Screen)
* **The Centerpiece:** A 3D/Isometric view of your active planting pot.
* **The Resonance Bar (Top):** A thin, pulsing bar showing current local metadata (e.g., *“18°C • Overcast • Waxing Crescent”*).
* **The Pulse Button:** A large, haptically-reactive button at the bottom center. 
    * *If empty:* Tapping opens the Seed Satchel.
    * *If growing:* Tapping releases a "Pulse" ripple that reveals the remaining growth time.
* **Visual Weather Overlay:** If it is raining at the player's GPS location, digital rain streaks appear on the inner "glass" of the screen.

### B. The Seed Satchel (Inventory)
* **Grid View:** Icons of seeds you’ve collected.
* **Probability Preview:** Selecting a seed highlights which variants are currently "High Probability" based on the current weather.
    * *Example:* If it's night, the "Lunar" variant icon will glow.

### C. The Flora Codex (Discovery Log)
* **Silhouettes:** Undiscovered variants appear as shadowy outlines.
* **Condition Hints:** Tapping an undiscovered variant gives a cryptic hint (e.g., *"Requires the breath of a storm"* for the Wind variant).

---

## 9. Seed Profile: The "Astra" Seed (Starter)

The **Astra Seed** is the first seed given to players. It is highly reactive and serves as the tutorial for how environment shapes biology.

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

---

## 10. Technical Logic: The "Astra" Resolution Case

To determine which variant is "born," the game runs a priority check upon the **Plant** action. The engine processes inputs in the following order:

1.  **Priority 1: Celestial/Calendar Events** * *Check:* Is it an Equinox? Is there an Eclipse? 
    * *Result:* Overrides all other weather conditions.
2.  **Priority 2: Extreme Atmospheric Triggers** * *Check:* Is there an active Storm/High Wind alert? Is the Temp < 5°C or > 38°C?
    * *Result:* Assigns rare/epic environmental variants.
3.  **Priority 3: Temporal/Light Triggers** * *Check:* Is it Night, Golden Hour, or Bright Daylight?
    * *Result:* Assigns common time-based variants.
4.  **Priority 4: Default** * *Check:* If no specific environmental thresholds are met.
    * *Result:* Result is **Astra Base**.

---

## 11. Audio & Haptic Feedback
* **Sound:** Procedural Lo-Fi. The BPM slows down at night and increases during sunny days. Rain triggers a muffled, piano-heavy track.
* **Haptics:** Gentle vibrations that mimic rain taps or wind gusts when interacting with the plants.

---

> **Designer's Note:** The "Astra" seed is designed to ensure players see a different result almost immediately based on when they download the app (Day vs. Night), instantly proving the core mechanic.

> **Design Note:** Retention is driven by "FOMO" (Fear Of Missing Out) on rare weather events. If a hurricane or rare snowstorm hits the player's area, the app should send a high-priority notification: *"A rare Atmospheric Rift has opened! Plant now to claim a Storm-Weaver Fern."*
