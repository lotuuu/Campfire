# Architecture Fixes Design

## Overview

Address 10 architectural issues identified during codebase audit, covering data integrity, performance, UI correctness, dead code, server hardening, package management, testability, and asset pipeline.

## Group A — Save System Hardening

### Auto-Save (fix 1)
- Add `_autoSaveTimer` in `SaveManager.Update()` that calls `Save()` every 30s
- Hook `OnApplicationPause(true)` and `OnApplicationFocus(false)` to force immediate `Flush()`
- Catches mobile backgrounding/force-quit that loses accumulated mana

### Atomic Writes (fix 2)
- `Flush()` writes JSON to `save.tmp` first
- If `save.json` exists, rename to `save.bak`
- Rename `save.tmp` to `save.json`
- `Load()` tries `save.bak` if `save.json` fails to parse before falling back to `new SaveData()`
- Same pattern for `social.json`

## Group B — Performance (fix 3)

- Add `private static Dictionary<string, SeedData>` in `PlotManager`, populated once in `Awake()`
- Replace `LoadSeed(seedName)` with dictionary lookup
- Same pattern in `GardenManager` for `GardenPlantData`
- Cache `MerchantData[]` in `MerchantUI.Initialize()` instead of loading on every `Refresh()`

## Group C — UI Staleness (fix 4)

- In `CampFireUI.OpenOverlay()`, call `Refresh()` on the panel being opened
- Affects `BuildUI`, `ApothekeUI`, `MerchantUI`
- Minimal change — add calls in existing overlay-open method

## Group D — Dead Code Removal (fix 5)

- Delete `TriggerCondition.cs` and its `.meta`
- Remove `[SerializeField] MallumConfig config` from `MallumManager`
- Remove `SaveData.lastManaCollectTime` field
- Remove `MallumManager.OnFlameUpgraded()` if it's a no-op
- Remove dead CSS classes from `Common.uss` (`.page-content`, `.bottom-sheet`, etc.)

## Group E — Server Hardening (fixes 6 & 7)

- Add `express-rate-limit` package
- Global limiter: 100 req/min per IP
- Stricter limiter on `/auth/register`: 5 req/min per IP
- `PUT /village` payload size cap at 100KB via Content-Length check
- Validate `snapshot` is an object

## Group F — Package Pinning (fix 8)

- Pin `com.coplaydev.unity-mcp` to specific commit SHA
- Pin `com.yasirkula.nativeshare` to specific commit SHA

## Group G — Testability (fix 9)

Extract static helpers:
- `FlameManager.AccumulateMana(SaveData, float deltaTime, FlameConfig)` — pure mana calc
- `GardenManager.GetGrowthProgress(GardenSave, GardenPlantData)` — pure progress calc
- `GardenManager.CheckYieldReady(GardenSave, GardenPlantData)` — pure yield check
- `GardenManager.CollectYield(GardenSave, GardenPlantData)` — pure yield collection
- `VisitorSystem.RollVisitorGift(SaveData, WeatherData)` — pure gift rolling

Add EditMode tests for each extracted method.

## Group H — Git LFS (fix 10)

- Add `.gitattributes` for `*.png *.jpg *.psd *.tga *.wav *.mp3 *.ogg *.fbx *.obj`
- Run `git lfs migrate import` to retroactively move binaries to LFS
- **Destructive to git history** — requires explicit user approval before executing
