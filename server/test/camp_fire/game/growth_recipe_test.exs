defmodule CampFire.Game.GrowthRecipeTest do
  use ExUnit.Case, async: true

  alias CampFire.Game.GrowthRecipe

  describe "score_range/4" do
    test "returns 1.0 when actual is within ideal range" do
      assert GrowthRecipe.score_range(25.0, 20.0, 30.0, 5.0) == 1.0
    end

    test "returns 1.0 at ideal boundary" do
      assert GrowthRecipe.score_range(20.0, 20.0, 30.0, 5.0) == 1.0
      assert GrowthRecipe.score_range(30.0, 20.0, 30.0, 5.0) == 1.0
    end

    test "returns 0.0 beyond tolerance" do
      assert GrowthRecipe.score_range(10.0, 20.0, 30.0, 5.0) == 0.0
      assert GrowthRecipe.score_range(40.0, 20.0, 30.0, 5.0) == 0.0
    end

    test "linear falloff below ideal within tolerance" do
      # 2.5 below ideal_min of 20, tolerance of 5 -> 1.0 - 2.5/5 = 0.5
      assert GrowthRecipe.score_range(17.5, 20.0, 30.0, 5.0) == 0.5
    end

    test "linear falloff above ideal within tolerance" do
      # 2.5 above ideal_max of 30, tolerance of 5 -> 1.0 - 2.5/5 = 0.5
      assert GrowthRecipe.score_range(32.5, 20.0, 30.0, 5.0) == 0.5
    end

    test "returns 0.0 at exact tolerance boundary" do
      assert GrowthRecipe.score_range(15.0, 20.0, 30.0, 5.0) == 0.0
      assert GrowthRecipe.score_range(35.0, 20.0, 30.0, 5.0) == 0.0
    end

    test "returns 0.0 when tolerance is zero and outside ideal" do
      assert GrowthRecipe.score_range(19.0, 20.0, 30.0, 0.0) == 0.0
    end
  end

  describe "evaluate/3" do
    test "returns 1.0 when no axes are enabled" do
      recipe = %{}
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 0) == 1.0
    end

    test "returns 1.0 when recipe has disabled axes" do
      recipe = %{
        "heat" => %{"enabled" => false, "ideal_min" => 20, "ideal_max" => 30, "tolerance" => 5, "weight" => 1}
      }

      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 0) == 1.0
    end

    test "single axis with perfect score" do
      recipe = %{
        "heat" => %{
          "enabled" => true,
          "ideal_min" => 20,
          "ideal_max" => 30,
          "tolerance" => 10,
          "weight" => 1
        }
      }

      # Column-oriented snapshots (as stored by Plots.record_snapshot)
      snapshots = %{
        "temperatures" => [25.0, 25.0],
        "snapshot_count" => 2
      }

      assert GrowthRecipe.evaluate(recipe, snapshots, 0) == 1.0
    end

    test "waterings axis scores correctly" do
      recipe = %{
        "waterings" => %{
          "enabled" => true,
          "ideal_min" => 2,
          "ideal_max" => 5,
          "tolerance" => 2,
          "weight" => 1
        }
      }

      # 3 waterings is within [2,5] -> 1.0
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 3) == 1.0

      # 0 waterings: distance below ideal_min=2 is 2, tolerance=2 -> 1.0 - 2/2 = 0.0
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 0) == 0.0

      # 1 watering: distance below ideal_min=2 is 1, tolerance=2 -> 1.0 - 1/2 = 0.5
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 1) == 0.5
    end

    test "multi-axis weighted average" do
      recipe = %{
        "heat" => %{
          "enabled" => true,
          "ideal_min" => 20,
          "ideal_max" => 30,
          "tolerance" => 10,
          "weight" => 2.0
        },
        "waterings" => %{
          "enabled" => true,
          "ideal_min" => 2,
          "ideal_max" => 5,
          "tolerance" => 2,
          "weight" => 1.0
        }
      }

      # Heat: avg temp 25 -> score 1.0, weight 2.0
      # Waterings: 3 -> score 1.0, weight 1.0
      # Weighted avg: (1.0*2.0 + 1.0*1.0) / (2.0 + 1.0) = 1.0
      snapshots = %{"temperatures" => [25.0], "snapshot_count" => 1}
      assert GrowthRecipe.evaluate(recipe, snapshots, 3) == 1.0

      # Heat: avg temp 25 -> score 1.0, weight 2.0
      # Waterings: 0 -> score 0.0, weight 1.0
      # Weighted avg: (1.0*2.0 + 0.0*1.0) / (2.0 + 1.0) = 2/3
      result = GrowthRecipe.evaluate(recipe, snapshots, 0)
      assert_in_delta result, 2.0 / 3.0, 0.001
    end

    test "zero snapshots returns 0.0 for weather axes" do
      recipe = %{
        "heat" => %{
          "enabled" => true,
          "ideal_min" => 20,
          "ideal_max" => 30,
          "tolerance" => 10,
          "weight" => 1
        }
      }

      # With zero snapshots, heat actual = 0.0, ideal_min=20, tolerance=10
      # distance = 20, > tolerance -> 0.0
      assert GrowthRecipe.evaluate(recipe, %{"snapshot_count" => 0}, 0) == 0.0
      assert GrowthRecipe.evaluate(recipe, nil, 0) == 0.0
    end

    test "sunlight axis uses 100 minus cloud_cover" do
      recipe = %{
        "sunlight" => %{
          "enabled" => true,
          "ideal_min" => 50,
          "ideal_max" => 80,
          "tolerance" => 20,
          "weight" => 1
        }
      }

      # cloud_cover 30 -> sunlight = 70, within [50,80] -> 1.0
      snapshots = %{"cloud_covers" => [30.0], "snapshot_count" => 1}
      assert GrowthRecipe.evaluate(recipe, snapshots, 0) == 1.0
    end

    test "rain axis uses ratio of raining snapshots" do
      recipe = %{
        "rain" => %{
          "enabled" => true,
          "ideal_min" => 0.4,
          "ideal_max" => 0.6,
          "tolerance" => 0.3,
          "weight" => 1
        }
      }

      # 2 rain snapshots out of 4 total -> ratio 0.5, within [0.4, 0.6] -> 1.0
      snapshots = %{
        "rain_snapshots" => [true, true],
        "snapshot_count" => 4
      }

      assert GrowthRecipe.evaluate(recipe, snapshots, 0) == 1.0
    end
  end

  describe "calculate_drops/2" do
    test "perfect score returns base drops" do
      assert GrowthRecipe.calculate_drops(1.0, 4) == 4
    end

    test "half score returns half base drops" do
      assert GrowthRecipe.calculate_drops(0.5, 4) == 2
    end

    test "zero score returns minimum of 1" do
      assert GrowthRecipe.calculate_drops(0.0, 4) == 1
    end

    test "low score still returns at least 1" do
      assert GrowthRecipe.calculate_drops(0.1, 2) == 1
    end
  end
end
