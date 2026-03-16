defmodule CampFire.Translations.Translation do
  use Ecto.Schema
  import Ecto.Changeset

  schema "translations" do
    field :locale, :string
    field :key, :string
    field :value, :string
    timestamps(type: :utc_datetime)
  end

  def changeset(translation, attrs) do
    translation
    |> cast(attrs, [:locale, :key, :value])
    |> validate_required([:locale, :key, :value])
    |> unique_constraint([:locale, :key])
  end
end
