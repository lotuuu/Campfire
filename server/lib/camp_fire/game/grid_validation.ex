defmodule CampFire.Game.GridValidation do
  @moduledoc """
  Validates hex grid placement and entity caps for the campsite.

  The campsite uses a flat-top axial hex grid centered at (0,0) where the flame sits.
  Grid radius and entity caps scale with flame level via flame_config.
  """

  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Game.{PlayerPlot, PlayerVase, PlayerGarden, PlayerMallumHouse, PlayerBird, PlayerState}
  alias CampFire.Economy.PlayerEconomy

  @doc """
  Axial hex distance from origin (0,0).

  For flat-top hex grids with axial coordinates (q, r):
    distance = max(|q|, |r|, |q + r|)
  """
  def hex_distance(q, r) do
    Enum.max([abs(q), abs(r), abs(q + r)])
  end

  @doc """
  Checks whether the player can place another entity (plot, vase, or garden).

  Counts all existing entities + 1 for the apotheke against the entity cap
  for the player's current flame level.

  Returns `:ok` or `{:error, :entity_cap_reached}`.
  """
  def check_entity_cap(player_uid) do
    flame_config = CampFire.ConfigCache.get("flame_config")

    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        {:error, :not_found}

      economy ->
        flame_level = economy.flame_level
        entity_caps = flame_config["entity_caps"]
        cap = Enum.at(entity_caps, flame_level - 1)
        entity_count = count_entities(player_uid)

        if entity_count < cap do
          :ok
        else
          {:error, :entity_cap_reached}
        end
    end
  end

  @doc """
  Validates that a hex coordinate is a legal placement for a new entity.

  Checks:
  1. The hex is within the grid radius for the player's flame level
  2. No existing entity occupies that hex (plots, vases, gardens, flame at 0,0, apotheke)

  Returns `:ok`, `{:error, :out_of_bounds}`, or `{:error, :hex_occupied}`.
  """
  def validate_grid_placement(player_uid, grid_x, grid_y) do
    flame_config = CampFire.ConfigCache.get("flame_config")

    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        {:error, :not_found}

      economy ->
        flame_level = economy.flame_level
        grid_sizes = flame_config["grid_sizes"]
        grid_radius = Enum.at(grid_sizes, flame_level - 1)
        distance = hex_distance(grid_x, grid_y)

        cond do
          distance > grid_radius ->
            {:error, :out_of_bounds}

          hex_occupied?(player_uid, grid_x, grid_y) ->
            {:error, :hex_occupied}

          true ->
            :ok
        end
    end
  end

  @doc """
  Returns a list of `{q, r}` tuples for all unoccupied hexes within the player's grid radius.

  Useful for the birds system to find available landing spots.
  """
  def get_free_tiles(player_uid) do
    flame_config = CampFire.ConfigCache.get("flame_config")

    case Repo.get(PlayerEconomy, player_uid) do
      nil ->
        []

      economy ->
        flame_level = economy.flame_level
        grid_sizes = flame_config["grid_sizes"]
        grid_radius = Enum.at(grid_sizes, flame_level - 1)
        occupied = get_occupied_hexes(player_uid)

        for q <- -grid_radius..grid_radius,
            r <- -grid_radius..grid_radius,
            hex_distance(q, r) <= grid_radius,
            not MapSet.member?(occupied, {q, r}) do
          {q, r}
        end
    end
  end

  # --- Private Helpers ---

  defp count_entities(player_uid) do
    plot_count =
      from(p in PlayerPlot, where: p.player_uid == ^player_uid, select: count(p.id))
      |> Repo.one()

    vase_count =
      from(v in PlayerVase, where: v.player_uid == ^player_uid, select: count(v.id))
      |> Repo.one()

    garden_count =
      from(g in PlayerGarden, where: g.player_uid == ^player_uid, select: count(g.id))
      |> Repo.one()

    house_count =
      from(h in PlayerMallumHouse, where: h.player_uid == ^player_uid, select: count(h.id))
      |> Repo.one()

    # +1 for apotheke (always present)
    plot_count + vase_count + garden_count + house_count + 1
  end

  defp hex_occupied?(player_uid, grid_x, grid_y) do
    # Flame is always at (0, 0)
    if grid_x == 0 and grid_y == 0 do
      true
    else
      # Check apotheke position
      apotheke_pos = get_apotheke_position(player_uid)

      if {grid_x, grid_y} == apotheke_pos do
        true
      else
        # Check plots, vases, gardens, mallum houses
        Repo.exists?(
          from(p in PlayerPlot,
            where: p.player_uid == ^player_uid and p.grid_x == ^grid_x and p.grid_y == ^grid_y
          )
        ) or
          Repo.exists?(
            from(v in PlayerVase,
              where:
                v.player_uid == ^player_uid and v.grid_x == ^grid_x and v.grid_y == ^grid_y
            )
          ) or
          Repo.exists?(
            from(g in PlayerGarden,
              where:
                g.player_uid == ^player_uid and g.grid_x == ^grid_x and g.grid_y == ^grid_y
            )
          ) or
          Repo.exists?(
            from(h in PlayerMallumHouse,
              where:
                h.player_uid == ^player_uid and h.grid_x == ^grid_x and h.grid_y == ^grid_y
            )
          ) or
          Repo.exists?(
            from(b in PlayerBird,
              where:
                b.player_uid == ^player_uid and b.grid_x == ^grid_x and b.grid_y == ^grid_y
            )
          )
      end
    end
  end

  defp get_occupied_hexes(player_uid) do
    plots =
      from(p in PlayerPlot,
        where: p.player_uid == ^player_uid,
        select: {p.grid_x, p.grid_y}
      )
      |> Repo.all()

    vases =
      from(v in PlayerVase,
        where: v.player_uid == ^player_uid,
        select: {v.grid_x, v.grid_y}
      )
      |> Repo.all()

    gardens =
      from(g in PlayerGarden,
        where: g.player_uid == ^player_uid,
        select: {g.grid_x, g.grid_y}
      )
      |> Repo.all()

    houses =
      from(h in PlayerMallumHouse,
        where: h.player_uid == ^player_uid,
        select: {h.grid_x, h.grid_y}
      )
      |> Repo.all()

    birds =
      from(b in PlayerBird,
        where: b.player_uid == ^player_uid,
        select: {b.grid_x, b.grid_y}
      )
      |> Repo.all()

    apotheke_pos = get_apotheke_position(player_uid)

    # Flame at origin + apotheke + all entity positions
    [{0, 0}, apotheke_pos | plots ++ vases ++ gardens ++ houses ++ birds]
    |> MapSet.new()
  end

  defp get_apotheke_position(player_uid) do
    case Repo.get(PlayerState, player_uid) do
      nil ->
        {1, 0}

      state ->
        q = Map.get(state.data, "apothekeGridX", 1)
        r = Map.get(state.data, "apothekeGridY", 0)
        {q, r}
    end
  end
end
