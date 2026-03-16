# Fertilizer Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement fertilizer as a consumable item that can be applied to growing plots or mature gardens, boosting the next harvest yield by 50%.

**Architecture:** Server-authoritative `fertilized` boolean on both `player_plots` and `player_gardens` DB tables. Two new endpoints (`/game/plot/fertilize`, `/game/garden/fertilize`) consume 1 fertilizer item and set the flag. Harvest/collect logic checks the flag and applies a 1.5x multiplier (rounded up) to drops/yield. Client mirrors the flag on `PlotSave`/`GardenSave` and adds a "Fertilize" button in the interaction panels for growing plots and mature gardens.

**Tech Stack:** Elixir/Phoenix (server), C# Unity 6 (client), Ecto migrations (DB)

---

## Chunk 1: Server — DB Migration + Schema + Endpoints

### Task 1: Add `fertilized` column to `player_plots` and `player_gardens`

**Files:**
- Create: `server/priv/repo/migrations/20260316120000_add_fertilized_to_plots_and_gardens.exs`
- Modify: `server/lib/camp_fire/game/player_plot.ex`
- Modify: `server/lib/camp_fire/game/player_garden.ex`

- [ ] **Step 1: Create the migration**

```elixir
defmodule CampFire.Repo.Migrations.AddFertilizedToPlotsAndGardens do
  use Ecto.Migration

  def change do
    alter table(:player_plots) do
      add :fertilized, :boolean, default: false, null: false
    end

    alter table(:player_gardens) do
      add :fertilized, :boolean, default: false, null: false
    end
  end
end
```

- [ ] **Step 2: Add `fertilized` field to PlayerPlot schema**

In `server/lib/camp_fire/game/player_plot.ex`, add to the schema block:
```elixir
field :fertilized, :boolean, default: false
```

Add `:fertilized` to the cast list in `changeset/2`.

- [ ] **Step 3: Add `fertilized` field to PlayerGarden schema**

In `server/lib/camp_fire/game/player_garden.ex`, add to the schema block:
```elixir
field :fertilized, :boolean, default: false
```

Add `:fertilized` to the cast list in `changeset/2`.

- [ ] **Step 4: Run migration**

```bash
cd server && mix ecto.migrate
```

- [ ] **Step 5: Commit**

```bash
git add server/priv/repo/migrations/20260316120000_add_fertilized_to_plots_and_gardens.exs \
  server/lib/camp_fire/game/player_plot.ex \
  server/lib/camp_fire/game/player_garden.ex
git commit -m "feat: add fertilized column to player_plots and player_gardens"
```

### Task 2: Add fertilize endpoint for plots

**Files:**
- Modify: `server/lib/camp_fire/game/plots.ex`
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

- [ ] **Step 1: Add `fertilize/2` to Plots context**

In `server/lib/camp_fire/game/plots.ex`, add after the water function:

```elixir
def fertilize(player_uid, plot_id) do
  with %PlayerPlot{} = plot <- Repo.get(PlayerPlot, plot_id),
       true <- plot.player_uid == player_uid || {:error, :not_owned},
       true <- plot.state == "growing" || {:error, :not_growing},
       true <- not plot.fertilized || {:error, :already_fertilized} do
    case Economy.spend_item(player_uid, "fertilizer", 1) do
      {:ok, _} ->
        plot
        |> PlayerPlot.changeset(%{fertilized: true})
        |> Repo.update()

      {:error, reason} ->
        {:error, reason}
    end
  else
    nil -> {:error, :not_found}
    {:error, _} = err -> err
  end
end
```

- [ ] **Step 2: Add `fertilize_plot` action to GameController**

In `server/lib/camp_fire_web/controllers/game_controller.ex`, add in the Plots section:

```elixir
def fertilize_plot(conn, %{"plotId" => plot_id}) do
  uid = conn.assigns.current_player.uid

  case Plots.fertilize(uid, plot_id) do
    {:ok, plot} ->
      conn |> put_status(200) |> json(serialize_plot(plot))

    {:error, reason} ->
      conn |> put_status(422) |> json(%{error: format_error(reason)})
  end
end

def fertilize_plot(conn, _params) do
  conn |> put_status(400) |> json(%{error: "Missing 'plotId'"})
end
```

- [ ] **Step 3: Add `fertilized` to `serialize_plot`**

In the `serialize_plot/1` function in `game_controller.ex`, add to the returned map:
```elixir
fertilized: plot.fertilized
```

- [ ] **Step 4: Add route**

In `server/lib/camp_fire_web/router.ex`, in the `/game` scope, add:
```elixir
post "/plot/fertilize", GameController, :fertilize_plot
```

- [ ] **Step 5: Apply fertilizer boost in `Plots.harvest`**

In `server/lib/camp_fire/game/plots.ex`, in the `harvest/2` function, after `drops = GrowthRecipe.calculate_drops(...)`, add:

```elixir
drops = if plot.fertilized, do: ceil(drops * 1.5), else: drops
```

**Required**: Add `fertilized: false` to the plot reset changeset (the one that sets `state: "empty"`, `seed_item_id: nil`, etc.). Ecto defaults only apply on insert, not update, so this must be explicit:
```elixir
plot
|> PlayerPlot.changeset(%{
  state: "empty",
  seed_item_id: nil,
  plant_time_utc: nil,
  water_count: 0,
  last_watered_utc: nil,
  snapshots: %{},
  fertilized: false
})
|> Repo.update!()
```

Also apply the same `1.5x` boost in `harvest_preview/2` so the preview matches the actual harvest. In `harvest_preview`, after computing `drops`, add the same line:
```elixir
drops = if plot.fertilized, do: ceil(drops * 1.5), else: drops
```

- [ ] **Step 6: Commit**

```bash
git add server/lib/camp_fire/game/plots.ex \
  server/lib/camp_fire_web/controllers/game_controller.ex \
  server/lib/camp_fire_web/router.ex
git commit -m "feat: add plot fertilize endpoint with 1.5x harvest boost"
```

### Task 3: Add fertilize endpoint for gardens

**Files:**
- Modify: `server/lib/camp_fire/game/gardens.ex`
- Modify: `server/lib/camp_fire_web/controllers/game_controller.ex`
- Modify: `server/lib/camp_fire_web/router.ex`

- [ ] **Step 1: Add `fertilize/2` to Gardens context**

In `server/lib/camp_fire/game/gardens.ex`, add after `check_and_collect`:

```elixir
def fertilize(player_uid, garden_id) do
  with %PlayerGarden{} = garden <- Repo.get(PlayerGarden, garden_id),
       true <- garden.player_uid == player_uid || {:error, :not_owned},
       true <- garden.mature || {:error, :not_mature},
       true <- not garden.fertilized || {:error, :already_fertilized} do
    case Economy.spend_item(player_uid, "fertilizer", 1) do
      {:ok, _} ->
        garden
        |> PlayerGarden.changeset(%{fertilized: true})
        |> Repo.update()

      {:error, reason} ->
        {:error, reason}
    end
  else
    nil -> {:error, :not_found}
    {:error, _} = err -> err
  end
end
```

- [ ] **Step 2: Add `fertilize_garden` action to GameController**

In `server/lib/camp_fire_web/controllers/game_controller.ex`, add in the Gardens section:

```elixir
def fertilize_garden(conn, %{"gardenId" => garden_id}) do
  uid = conn.assigns.current_player.uid

  case Gardens.fertilize(uid, garden_id) do
    {:ok, garden} ->
      conn |> put_status(200) |> json(serialize_garden(garden))

    {:error, reason} ->
      conn |> put_status(422) |> json(%{error: format_error(reason)})
  end
end

def fertilize_garden(conn, _params) do
  conn |> put_status(400) |> json(%{error: "Missing 'gardenId'"})
end
```

- [ ] **Step 3: Add `fertilized` to `serialize_garden`**

In the `serialize_garden/1` function in `game_controller.ex`, add to the returned map:
```elixir
fertilized: garden.fertilized
```

- [ ] **Step 4: Add route**

In `server/lib/camp_fire_web/router.ex`, in the `/game` scope, add:
```elixir
post "/garden/fertilize", GameController, :fertilize_garden
```

- [ ] **Step 5: Apply fertilizer boost in garden collection**

In `server/lib/camp_fire/game/gardens.ex`, in `check_and_collect/2`, where it calls `Economy.upsert_item(player_uid, config.yield_item, config.yield_amount)`, replace with:

Compute `boosted_amount` **before** the `Repo.transaction` block so it's accessible in the return value outside the transaction. Replace the entire `Repo.transaction` block and its `case do` handler (lines 118-137 of `gardens.ex`) with:

```elixir
boosted_amount = if garden.fertilized, do: ceil(config.yield_amount * 1.5), else: config.yield_amount

Repo.transaction(fn ->
  Economy.upsert_item(player_uid, config.yield_item, boosted_amount)

  garden
  |> PlayerGarden.changeset(%{last_yield_time_utc: now, fertilized: false})
  |> Repo.update!()
end)
|> case do
  {:ok, updated_garden} ->
    {:ok,
     %{
       status: :collected,
       garden: updated_garden,
       item: config.yield_item,
       amount: boosted_amount
     }}

  {:error, reason} ->
    {:error, reason}
end
```

This replaces the existing transaction + case block. Key changes: `boosted_amount` computed before transaction (accessible in return), `fertilized: false` added to the changeset, `boosted_amount` used in both `upsert_item` and the return map.

- [ ] **Step 6: Commit**

```bash
git add server/lib/camp_fire/game/gardens.ex \
  server/lib/camp_fire_web/controllers/game_controller.ex \
  server/lib/camp_fire_web/router.ex
git commit -m "feat: add garden fertilize endpoint with 1.5x yield boost"
```

## Chunk 2: Client — Data Model + Manager Logic + GameService

### Task 4: Add `fertilized` to client data model

**Files:**
- Modify: `Assets/Scripts/Data/SaveData.cs` — add `fertilized` to `PlotSave` and `GardenSave`
- Modify: `Assets/Scripts/Data/GameStateResponse.cs` — add `fertilized` to `ServerPlot`, `ServerGarden`, and add request DTOs

- [ ] **Step 1: Add `fertilized` to `PlotSave`**

In `Assets/Scripts/Data/SaveData.cs`, add to the `PlotSave` class (after `subscribeWater`):
```csharp
public bool fertilized;
```

- [ ] **Step 2: Add `fertilized` to `GardenSave`**

In `Assets/Scripts/Data/SaveData.cs`, add to the `GardenSave` class (after `gridY`):
```csharp
public bool fertilized;
```

- [ ] **Step 3: Add `fertilized` to `ServerPlot` and `ServerGarden`**

In `Assets/Scripts/Data/GameStateResponse.cs`:

Add to `ServerPlot` class (after `unlockedSkins`):
```csharp
public bool fertilized;
```

Add to `ServerGarden` class (after `gridY`):
```csharp
public bool fertilized;
```

- [ ] **Step 4: Add request DTOs**

In `Assets/Scripts/Data/GameStateResponse.cs`, add:
```csharp
[Serializable] public class FertilizePlotRequest { public int plotId; }
[Serializable] public class FertilizeGardenRequest { public int gardenId; }
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/SaveData.cs Assets/Scripts/Data/GameStateResponse.cs
git commit -m "feat: add fertilized field to plot and garden data models"
```

### Task 5: Add fertilize to GameService

**Files:**
- Modify: `Assets/Scripts/Services/GameService.cs` — add `FertilizePlot` and `FertilizeGarden` endpoints

- [ ] **Step 1: Add `FertilizePlot` endpoint**

In `Assets/Scripts/Services/GameService.cs`, add in the Plot Endpoints section (after `SetPlotSkin`):

```csharp
public async Task<ServerPlot> FertilizePlot(int plotId)
{
    if (!IsOnline) return null;
    try
    {
        var body = JsonUtility.ToJson(new FertilizePlotRequest { plotId = plotId });
        using var req = PostJson("/game/plot/fertilize", body);
        await SendAsync(req);

        if (req.responseCode >= 200 && req.responseCode < 300)
            return JsonUtility.FromJson<ServerPlot>(req.downloadHandler.text);

        Debug.LogWarning($"GameService: FertilizePlot failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
    }
    catch (Exception e) { Debug.LogWarning($"GameService: FertilizePlot failed: {e.Message}"); }
    return null;
}
```

- [ ] **Step 2: Add `FertilizeGarden` endpoint**

In `Assets/Scripts/Services/GameService.cs`, add in the Garden Endpoints section (after `CollectGarden`):

```csharp
public async Task<ServerGarden> FertilizeGarden(int gardenId)
{
    if (!IsOnline) return null;
    try
    {
        var body = JsonUtility.ToJson(new FertilizeGardenRequest { gardenId = gardenId });
        using var req = PostJson("/game/garden/fertilize", body);
        await SendAsync(req);

        if (req.responseCode >= 200 && req.responseCode < 300)
            return JsonUtility.FromJson<ServerGarden>(req.downloadHandler.text);

        Debug.LogWarning($"GameService: FertilizeGarden failed (HTTP {req.responseCode}): {req.downloadHandler.text}");
    }
    catch (Exception e) { Debug.LogWarning($"GameService: FertilizeGarden failed: {e.Message}"); }
    return null;
}
```

- [ ] **Step 3: Add `fertilized` to state sync**

In `Assets/Scripts/Services/GameService.cs`, in `ApplyServerState`, where plots are synced (around line 200-211), add `fertilized = sp.fertilized` to the `PlotSave` construction.

Where gardens are synced (around line 239-248), add `fertilized = sg.fertilized` to the `GardenSave` construction.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Services/GameService.cs
git commit -m "feat: add FertilizePlot and FertilizeGarden to GameService"
```

### Task 6: Add fertilize methods to PlotManager and GardenManager

**Files:**
- Modify: `Assets/Scripts/Managers/PlotManager.cs` — add `Fertilize` method and fertilizer count helper
- Modify: `Assets/Scripts/Managers/GardenManager.cs` — add `Fertilize` method

- [ ] **Step 1: Add `GetFertilizerCount` and `Fertilize` to PlotManager**

In `Assets/Scripts/Managers/PlotManager.cs`, add a public helper and the fertilize method:

```csharp
public int GetFertilizerCount()
{
    var entry = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == "fertilizer");
    return entry?.count ?? 0;
}

public async Task<bool> Fertilize(int plotIndex)
{
    var data = SaveManager.Instance.Data;
    if (plotIndex < 0 || plotIndex >= data.plots.Count) return false;
    var plot = data.plots[plotIndex];
    if (plot.state != PlotState.Growing) return false;
    if (plot.fertilized) return false;

    // Check inventory
    if (!CurrencyManager.FreeMode && GetFertilizerCount() <= 0) return false;

    // Consume locally
    if (!CurrencyManager.FreeMode)
    {
        var entry = data.inventory.Find(i => i.itemKey == "fertilizer");
        entry.count--;
        if (entry.count <= 0) data.inventory.Remove(entry);
    }

    plot.fertilized = true;
    SaveManager.Instance.Save();
    OnPlotChanged?.Invoke(plotIndex);
    AudioManager.Instance?.PlaySFX("fertilize");

    // Notify server
    if (GameService.Instance != null && GameService.Instance.IsOnline && plot.serverId > 0)
    {
        var result = await GameService.Instance.FertilizePlot(plot.serverId);
        if (result == null)
            await GameService.Instance.ResyncFullState();
    }

    return true;
}
```

- [ ] **Step 2: Add `Fertilize` to GardenManager**

In `Assets/Scripts/Managers/GardenManager.cs`, add:

```csharp
public async Task<bool> Fertilize(int gardenIndex)
{
    var data = SaveManager.Instance.Data;
    if (gardenIndex < 0 || gardenIndex >= data.gardens.Count) return false;
    var garden = data.gardens[gardenIndex];
    if (!garden.mature) return false;
    if (garden.fertilized) return false;

    // Check inventory
    int fertCount = data.inventory.Find(i => i.itemKey == "fertilizer")?.count ?? 0;
    if (!CurrencyManager.FreeMode && fertCount <= 0) return false;

    // Consume locally
    if (!CurrencyManager.FreeMode)
    {
        var entry = data.inventory.Find(i => i.itemKey == "fertilizer");
        entry.count--;
        if (entry.count <= 0) data.inventory.Remove(entry);
    }

    garden.fertilized = true;
    SaveManager.Instance.Save();
    OnGardenChanged?.Invoke(gardenIndex);
    AudioManager.Instance?.PlaySFX("fertilize");

    // Notify server
    if (GameService.Instance != null && GameService.Instance.IsOnline && garden.serverId > 0)
    {
        var result = await GameService.Instance.FertilizeGarden(garden.serverId);
        if (result == null)
            await GameService.Instance.ResyncFullState();
    }

    return true;
}
```

- [ ] **Step 3: Clear fertilized on garden yield collection**

In `Assets/Scripts/Managers/GardenManager.cs`, in `CheckGrowthAndYields()`, where yields are collected (line 175-188), after collecting the yield, apply the boost and clear the flag:

Replace the yield collection block (lines 175-188 of `GardenManager.cs`):
```csharp
if (CheckYieldReady(garden, plantData.yieldIntervalHours, now))
{
    AddItem(data, plantData.yieldItem, plantData.yieldAmount);
    garden.lastYieldTimeUtc = now.ToString("o");
    changed = true;
    OnYieldCollected?.Invoke(i, plantData.yieldItem, plantData.yieldAmount);
```

With:
```csharp
if (CheckYieldReady(garden, plantData.yieldIntervalHours, now))
{
    int amount = plantData.yieldAmount;
    if (garden.fertilized)
    {
        amount = Mathf.CeilToInt(amount * 1.5f);
        garden.fertilized = false;
    }
    AddItem(data, plantData.yieldItem, amount);
    garden.lastYieldTimeUtc = now.ToString("o");
    changed = true;
    OnYieldCollected?.Invoke(i, plantData.yieldItem, amount);
```

Note: both `AddItem` and `OnYieldCollected` use the boosted `amount` variable.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Managers/PlotManager.cs Assets/Scripts/Managers/GardenManager.cs
git commit -m "feat: add Fertilize methods to PlotManager and GardenManager"
```

## Chunk 3: Client — UI

### Task 7: Add Fertilize button to plot and garden interaction panels

**Files:**
- Modify: `Assets/Scripts/UI/CampsiteViewUI.cs` — add fertilize buttons in `ShowPlotInteraction` and `ShowGardenInteraction`

- [ ] **Step 1: Add Fertilize button to growing plot interaction**

In `Assets/Scripts/UI/CampsiteViewUI.cs`, in `ShowPlotInteraction`, inside the `case PlotState.Growing:` block, before the "Finish Now" button (around line 1695), add:

```csharp
// Fertilize button
if (!plot.fertilized)
{
    int fertCount = PlotManager.Instance != null ? PlotManager.Instance.GetFertilizerCount() : 0;
    var fertBtn = new Button(() =>
    {
        _ = FertilizePlotAndRefresh(index);
    })
    { text = $"Fertilize ({fertCount})" };
    fertBtn.SetEnabled(fertCount > 0 || CurrencyManager.FreeMode);
    fertBtn.AddToClassList("interaction-btn-primary");
    interactionActions.Add(fertBtn);
}
else
{
    var fertLabel = new Label("Fertilized! +50% yield");
    fertLabel.AddToClassList("interaction-info");
    interactionBody.Add(fertLabel);
}
```

- [ ] **Step 2: Add `FertilizePlotAndRefresh` helper method**

In `CampsiteViewUI.cs`, add near the other plot helper methods (after `SpeedUpAndHarvest`):

```csharp
private async Task FertilizePlotAndRefresh(int plotIndex)
{
    if (PlotManager.Instance == null) return;
    bool success = await PlotManager.Instance.Fertilize(plotIndex);
    if (success)
    {
        RebuildGrid();
        ShowInteraction(CampBuildingType.Plot, plotIndex);
    }
}
```

- [ ] **Step 3: Add Fertilize button to mature garden interaction**

In `Assets/Scripts/UI/CampsiteViewUI.cs`, in `ShowGardenInteraction`, after the state label (around line 2266), add:

```csharp
if (garden.mature)
{
    if (!garden.fertilized)
    {
        int fertCount = SaveManager.Instance.Data.inventory.Find(i => i.itemKey == "fertilizer")?.count ?? 0;
        var fertBtn = new Button(() =>
        {
            _ = FertilizeGardenAndRefresh(index);
        })
        { text = $"Fertilize ({fertCount})" };
        fertBtn.SetEnabled(fertCount > 0 || CurrencyManager.FreeMode);
        fertBtn.AddToClassList("interaction-btn-primary");
        interactionActions.Add(fertBtn);
    }
    else
    {
        var fertLabel = new Label("Fertilized! +50% next yield");
        fertLabel.AddToClassList("interaction-info");
        interactionBody.Add(fertLabel);
    }
}
```

- [ ] **Step 4: Add `FertilizeGardenAndRefresh` helper method**

In `CampsiteViewUI.cs`, add near the garden helper methods:

```csharp
private async Task FertilizeGardenAndRefresh(int gardenIndex)
{
    if (GardenManager.Instance == null) return;
    bool success = await GardenManager.Instance.Fertilize(gardenIndex);
    if (success)
    {
        RebuildGrid();
        ShowInteraction(CampBuildingType.Garden, gardenIndex);
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/CampsiteViewUI.cs
git commit -m "feat: add Fertilize button to plot and garden interaction panels"
```

### Task 8: Ensure fertilizer item exists in server seeds

**Files:**
- Modify: `server/priv/repo/seeds.exs` — verify "fertilizer" item exists

- [ ] **Step 1: Check and add fertilizer item to seeds**

In `server/priv/repo/seeds.exs`, in the items list, ensure there is an entry:
```elixir
%{item_key: "fertilizer", display_name: "Fertilizer", category: "material"}
```

If it already exists (check the materials section), no change needed. If not, add it to the materials section of the items list.

- [ ] **Step 2: Commit if changed**

```bash
git add server/priv/repo/seeds.exs
git commit -m "feat: ensure fertilizer item exists in seed data"
```

### Task 9: Verify and test

- [ ] **Step 1: Run server tests**

```bash
cd server && mix test
```

- [ ] **Step 2: Check Unity compilation**

Use Unity MCP `read_console` to verify no compilation errors after all C# changes.

- [ ] **Step 3: Manual smoke test**

1. Start server with `make dev`
2. In Unity, plant a seed in a plot
3. While growing, tap the plot — verify "Fertilize" button appears
4. Apply fertilizer — verify button changes to "Fertilized! +50% yield" label
5. Harvest — verify drops are 1.5x normal
6. Plant a garden, wait for maturity
7. Tap mature garden — verify "Fertilize" button appears
8. Apply fertilizer — verify label shows
9. Wait for yield collection — verify yield is 1.5x and fertilized flag clears
