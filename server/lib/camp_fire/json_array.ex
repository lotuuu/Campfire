defmodule CampFire.JsonArray do
  @moduledoc """
  Custom Ecto type for JSONB columns that store arrays (not maps).
  Ecto's built-in :map type rejects top-level arrays.
  """
  use Ecto.Type

  def type, do: :map

  def cast(data) when is_list(data), do: {:ok, data}
  def cast(_), do: :error

  def load(data) when is_list(data), do: {:ok, data}
  def load(_), do: :error

  def dump(data) when is_list(data), do: {:ok, data}
  def dump(_), do: :error
end
