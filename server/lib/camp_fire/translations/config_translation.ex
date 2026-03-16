defmodule CampFire.Translations.ConfigTranslation do
  use Ecto.Schema
  import Ecto.Changeset

  schema "config_translations" do
    field :locale, :string
    field :translatable_type, :string
    field :translatable_key, :string
    field :field, :string
    field :value, :string
    timestamps(type: :utc_datetime)
  end

  def changeset(ct, attrs) do
    ct
    |> cast(attrs, [:locale, :translatable_type, :translatable_key, :field, :value])
    |> validate_required([:locale, :translatable_type, :translatable_key, :field, :value])
    |> validate_inclusion(:translatable_type, ["item", "quest", "garden"])
    |> unique_constraint([:locale, :translatable_type, :translatable_key, :field])
  end
end
