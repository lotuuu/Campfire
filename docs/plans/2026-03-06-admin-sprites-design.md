# Admin Sprite Management

## Problem

Sprites are now server-served from `priv/static/assets/sprites/`, but there's no admin UI to view, upload, or replace them. Managing sprites requires SSH/file access.

## Solution

Two features:
1. **Inline sprite preview + upload** on existing entity edit forms (seeds, quests, skins)
2. **Dedicated `/admin/sprites` page** for browsing, uploading, replacing, and deleting all sprites

## Part B: Inline Sprite Upload on Entity Edit Forms

### Seeds (Items > Seeds > Edit)

- Show current sprite thumbnail next to the seed name: `<img src="/assets/sprites/seeds/{name}/icon.png">`
- Add "Replace icon" file upload field below the thumbnail
- On upload, write PNG to `priv/static/assets/sprites/seeds/{seed_name_lowercase}/icon.png`
- Refresh manifest via `ConfigCache.refresh()`
- Thumbnail updates after upload

### Quests (Quests > Edit)

- Same pattern: thumbnail of `quests/{quest_name_lowercase}.png` + upload field

### Skins (Items > Skins > Edit)

- Same pattern: thumbnail of `skins/{skin_name_lowercase}.png` + upload field

### Key derivation

Sprite key is derived automatically from the entity name — no manual key entry. The admin just picks a file.

## Part C: Dedicated Sprites Page (`/admin/sprites`)

### Layout

- Grid view of all sprites grouped by category
- Categories: seeds, gardens, ui, moon, portraits, buildings, skins
- Each sprite card shows: thumbnail (64x64), key, file size
- Upload button per category (enter key name + pick file)
- Replace button on each existing sprite (pick new file)
- Delete button on each sprite (with confirmation)

### After any change

- PNG written to/deleted from `priv/static/assets/sprites/`
- `CampFire.SpriteManifest.build()` called to rebuild hashes
- `ConfigCache.refresh()` to update ETS cache
- Page re-renders with updated sprites

## Server Architecture

### New context: `CampFire.Sprites`

```elixir
defmodule CampFire.Sprites do
  def list_sprites()        # returns [{key, hash, size_bytes}]
  def upload_sprite(key, binary_data)  # writes PNG, returns :ok
  def delete_sprite(key)    # removes PNG, returns :ok
  def sprites_dir()         # base path helper
end
```

All operations are filesystem-based (no database). The manifest in ConfigCache/ETS is the derived index.

### LiveView uploads

Use Phoenix's built-in `allow_upload` / `consume_uploaded_entries` for file handling. Accept `.png` only, max 512KB.

### New LiveView: `SpritesLive`

- Route: `/admin/sprites`
- Added to admin nav bar
- Groups sprites by first path segment (category)

### Modified LiveViews

- `ItemsLive`: add thumbnail + upload to seed edit form, skin edit form
- `QuestsLive`: add thumbnail + upload to quest edit form

## YAGNI

- No image resizing/cropping
- No drag-and-drop
- No versioning/history
- No bulk upload (one at a time)
