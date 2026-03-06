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
  Calculate drops from quality score using min/max range with randomness.
  Score interpolates within the range; +-30% spread adds noise.
  """
  def calculate_drops(score, min_drops, max_drops) do
    center = min_drops + score * (max_drops - min_drops)
    spread = (max_drops - min_drops) * 0.3
    low = max(min_drops, round(center - spread))
    high = min(max_drops, round(center + spread))
    Enum.random(low..high)
  end

  # --- Private helpers ---
  # Snapshots are stored column-oriented by Plots.record_snapshot:
  # %{"temperatures" => [25.0, 26.0], "humidities" => [60, 65], "snapshot_count" => 2, ...}

  defp build_axes(recipe, snapshots, water_count) do
    snapshots = snapshots || %{}
    count = Map.get(snapshots, "snapshot_count", 0)

    axes = []

    axes = maybe_add_axis(axes, recipe, "heat", fn ->
      avg_from_list(snapshots, "temperatures", count)
    end)

    axes = maybe_add_axis(axes, recipe, "wind", fn ->
      avg_from_list(snapshots, "wind_speeds", count)
    end)

    axes = maybe_add_axis(axes, recipe, "humidity", fn ->
      avg_from_list(snapshots, "humidities", count)
    end)

    axes = maybe_add_axis(axes, recipe, "sunlight", fn ->
      case avg_from_list(snapshots, "cloud_covers", count) do
        +0.0 when count == 0 -> 0.0
        avg -> 100.0 - avg
      end
    end)

    axes = maybe_add_axis(axes, recipe, "rain", fn ->
      if count == 0 do
        0.0
      else
        rain_count = length(Map.get(snapshots, "rain_snapshots", []))
        rain_count / count
      end
    end)

    axes = maybe_add_axis(axes, recipe, "moon", fn ->
      phases = Map.get(snapshots, "moon_phase_snapshots", [])
      if phases == [], do: 0.0, else: dominant_moon(phases)
    end)

    axes = maybe_add_axis(axes, recipe, "waterings", fn ->
      water_count * 1.0
    end)

    axes
  end

  defp maybe_add_axis(axes, recipe, axis_name, value_fn) do
    axis_config = Map.get(recipe, axis_name)

    if is_map(axis_config) and Map.get(axis_config, "enabled", false) do
      ideal_min = to_float(axis_config["ideal_min"])
      ideal_max = to_float(axis_config["ideal_max"])
      tolerance = to_float(axis_config["tolerance"])
      weight = to_float(axis_config["weight"] || 1.0)

      actual = value_fn.()
      score = score_range(actual, ideal_min, ideal_max, tolerance)
      [{score, weight} | axes]
    else
      axes
    end
  end

  defp avg_from_list(snapshots, key, count) do
    values = Map.get(snapshots, key, [])
    if count > 0 and values != [] do
      Enum.sum(values) / count
    else
      0.0
    end
  end

  defp to_float(nil), do: 0.0
  defp to_float(val) when is_number(val), do: val * 1.0
  defp to_float(_), do: 0.0

  defp dominant_moon([]), do: 0.0

  defp dominant_moon(phases) do
    phases
    |> Enum.map(&round/1)
    |> Enum.frequencies()
    |> Enum.max_by(fn {_phase, count} -> count end)
    |> elem(0)
    |> Kernel.*(1.0)
  end
end
