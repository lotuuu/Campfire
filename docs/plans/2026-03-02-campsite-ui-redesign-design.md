# Campsite UI Redesign

Match the main campsite view to the pixel art sketch with a new top bar layout, 4-cell weather display, and renamed bottom navigation.

## Reference Sketch

Top-to-bottom: header bar (player name + date/time), 4-cell weather bar, campsite grid, bottom nav (SEEDS/CRAFT/MAIL).

## Top Bar

Replace the current separate weather-bar + resource-bar with a single framed "top bar" container:

```
+-------------------------------------+
|  Kaline (gear)    27 Feb  6:23 PM   |  <- header row
|                                      |
| [CLOUDY] [droplet 60] [30/28] [moon]|  <- weather row (4 cells)
|                          mana water  |  <- resources (lower-right)
+-------------------------------------+
```

### Header Row
- **Player name**: pulled from `SocialService.Instance` display name
- **Gear icon**: placeholder button (opens debug panel)
- **Date/time**: formatted from `GameTime.UtcNow` as "DD Mon h:mm tt"

### Weather Row (4 equal cells)
Each cell is a bordered box with icon + value:
1. **Condition**: weather condition text + emoji icon (e.g. "CLOUDY" + cloud)
2. **Humidity**: water droplet icon + humidity percentage
3. **Temperature**: thermometer icon + high/low temps
4. **Moon phase**: moon phase icon/texture

### Resources
Mana and water counts displayed in the lower-right corner of the top bar frame, small and unobtrusive.

### Frame Styling
Placeholder CSS: brown border, warm background with slight inner shadow. Ready for future 9-slice pixel art border image.

## Campsite Area

No structural change. Hex grid viewport stays as-is.

## Bottom Navigation

Rename and reorder:
- Left: **SEEDS** (opens Apotheke panel)
- Center: **CRAFT** (opens Build panel)
- Right: **MAIL** (opens Letters panel)

Each button shows text label above an icon placeholder (emoji for now, pixel art later).

## Files to Change

1. **CampFireRoot.uxml** - restructure top section, reorder/rename bottom nav buttons
2. **WeatherBar.uss** - 4-cell grid layout, individual cell styling
3. **CampSite.uss** - top bar frame container, header row, resource positioning
4. **BottomNav.uss** - button layout for icon + text vertical stack
5. **WeatherBarUI.cs** - populate new elements (humidity, hi/lo temp, date/time, player name)
6. **CampFireUI.cs** - wire player name from SocialService, update nav button refs
7. **BottomNavUI.cs** - update button name references
8. **ResourceDisplayUI.cs** - target new element locations in top bar
