defmodule CampFire.Game.MoonPhase do
  @moduledoc """
  Moon phase calculator, ported from Unity C# MoonPhaseCalculator.

  Returns a phase index 0-7:
    0 = New Moon, 1 = Waxing Crescent, 2 = First Quarter, 3 = Waxing Gibbous,
    4 = Full Moon, 5 = Waning Gibbous, 6 = Last Quarter, 7 = Waning Crescent
  """

  # Known new moon: January 6, 2000
  @known_new_moon_jd 2_451_549.5
  @synodic_month 29.53

  @doc """
  Calculate moon phase index (0-7) for the given datetime.
  """
  def calculate(datetime \\ DateTime.utc_now()) do
    jd = julian_date(datetime)
    days_since_new = jd - @known_new_moon_jd
    cycles = days_since_new / @synodic_month
    phase_fraction = cycles - Float.floor(cycles)
    rem(round(phase_fraction * 8), 8)
  end

  @doc """
  Calculate Julian Date from a DateTime.
  """
  def julian_date(%DateTime{year: y, month: m, day: d, hour: h, minute: min, second: s}) do
    {y, m} =
      if m <= 2 do
        {y - 1, m + 12}
      else
        {y, m}
      end

    a = div(y, 100)
    b = 2 - a + div(a, 4)

    day_fraction = (h + min / 60.0 + s / 3600.0) / 24.0

    Float.floor(365.25 * (y + 4716)) +
      Float.floor(30.6001 * (m + 1)) +
      d +
      day_fraction +
      b -
      1524.5
  end
end
