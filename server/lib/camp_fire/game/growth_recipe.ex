defmodule CampFire.Game.GrowthRecipe do
  @moduledoc """
  Server-side GrowthRecipe evaluation, mirroring the Unity C# GrowthRecipe logic.

  A recipe defines ideal growing conditions across multiple axes (heat, wind, humidity,
  sunlight, rain, moon phase, waterings). Each axis has an ideal range, tolerance, and weight.
  `evaluate/3` computes a weighted average quality score from 0.0 to 1.0.
  """

  @doc """
  Score a single value against an ideal range with tolerance.

  Returns 1.0 if `actual` is within [ideal_min, ideal_max].
  Linear falloff within `tolerance` beyond the ideal range.
  Returns 0.0 if beyond tolerance.
  """
  def score_range(actual, ideal_min, ideal_max, tolerance) do
    cond do
      actual >= ideal_min and actual <= ideal_max ->
        1.0

      actual < ideal_min ->
        distance = ideal_min - actual

        if tolerance > 0 and distance <= tolerance do
          1.0 - distance / tolerance
        else
          0.0
        end

      actual > ideal_max ->
        distance = actual - ideal_max

        if tolerance > 0 and distance <= tolerance do
          1.0 - distance / tolerance
        else
          0.0
        end
    end
  end

  @doc """
  Evaluate a recipe against weather snapshots and water count.

  Returns a quality score from 0.0 to 1.0.
  If no axes are enabled in the recipe, returns 1.0 (vacuous truth).
  If snapshots is empty/nil, weather-based axes score 0.0.
  """
  def evaluate(recipe, snapshots, water_count) when is_map(recipe) do
    axes = build_axes(recipe, snapshots, water_count)

    if axes == [] do
      1.0
    else
      {weighted_sum, total_weight} =
        Enum.reduce(axes, {0.0, 0.0}, fn {score, weight}, {sum, tw} ->
          {sum + score * weight, tw + weight}
        end)

      if total_weight > 0, do: weighted_sum / total_weight, else: 1.0
    end
  end

  def evaluate(_recipe, _snapshots, _water_count), do: 1.0

  @doc """
  Calculate drops from quality score and base drops.
  Minimum of 1 drop.
  """
  def calculate_drops(score, base_drops) do
    max(1, round(base_drops * score))
  end

  # --- Private helpers ---

  defp build_axes(recipe, snapshots, water_count) do
    snapshot_list = normalize_snapshots(snapshots)
    count = length(snapshot_list)

    axes = []

    axes = maybe_add_axis(axes, recipe, "heat", fn ->
      if count == 0, do: 0.0, else: avg(snapshot_list, "temperature")
    end)

    axes = maybe_add_axis(axes, recipe, "wind", fn ->
      if count == 0, do: 0.0, else: avg(snapshot_list, "wind_speed")
    end)

    axes = maybe_add_axis(axes, recipe, "humidity", fn ->
      if count == 0, do: 0.0, else: avg(snapshot_list, "humidity")
    end)

    axes = maybe_add_axis(axes, recipe, "sunlight", fn ->
      if count == 0, do: 0.0, else: 100.0 - avg(snapshot_list, "cloud_cover")
    end)

    axes = maybe_add_axis(axes, recipe, "rain", fn ->
      if count == 0 do
        0.0
      else
        rain_count = Enum.count(snapshot_list, fn s -> get_float(s, "is_raining") == 1.0 end)
        rain_count / count
      end
    end)

    axes = maybe_add_axis(axes, recipe, "moon", fn ->
      if count == 0 do
        0.0
      else
        # Use the dominant (most common) moon phase value
        phases = Enum.map(snapshot_list, fn s -> get_float(s, "moon_phase") end)
        dominant_moon(phases)
      end
    end)

    axes = maybe_add_axis(axes, recipe, "waterings", fn ->
      water_count * 1.0
    end)

    axes
  end

  defp maybe_add_axis(axes, recipe, axis_name, value_fn) do
    axis_config = Map.get(recipe, axis_name)

    if is_map(axis_config) and Map.get(axis_config, "enabled", false) do
      ideal_min = get_float(axis_config, "ideal_min")
      ideal_max = get_float(axis_config, "ideal_max")
      tolerance = get_float(axis_config, "tolerance")
      weight = get_float(axis_config, "weight", 1.0)

      actual = value_fn.()
      score = score_range(actual, ideal_min, ideal_max, tolerance)
      [{score, weight} | axes]
    else
      axes
    end
  end

  defp normalize_snapshots(nil), do: []
  defp normalize_snapshots(snapshots) when is_list(snapshots), do: snapshots

  defp normalize_snapshots(snapshots) when is_map(snapshots) do
    # Snapshots may be stored as a map with string keys like "0", "1", ...
    snapshots
    |> Enum.sort_by(fn {k, _v} -> to_string(k) end)
    |> Enum.map(fn {_k, v} -> v end)
  end

  defp normalize_snapshots(_), do: []

  defp avg([], _key), do: 0.0

  defp avg(snapshots, key) do
    sum = Enum.reduce(snapshots, 0.0, fn s, acc -> acc + get_float(s, key) end)
    sum / length(snapshots)
  end

  defp get_float(map, key, default \\ 0.0) when is_map(map) do
    case Map.get(map, key) do
      nil -> default
      val when is_number(val) -> val * 1.0
      _ -> default
    end
  end

  defp dominant_moon([]), do: 0.0

  defp dominant_moon(phases) do
    # Round to nearest integer for grouping, return the most frequent value
    phases
    |> Enum.map(&round/1)
    |> Enum.frequencies()
    |> Enum.max_by(fn {_phase, count} -> count end)
    |> elem(0)
    |> Kernel.*(1.0)
  end
end
