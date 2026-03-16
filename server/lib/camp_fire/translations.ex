defmodule CampFire.Translations do
  import Ecto.Query
  alias CampFire.Repo
  alias CampFire.Translations.{Translation, ConfigTranslation}

  def list_translations(locale) do
    from(t in Translation, where: t.locale == ^locale)
    |> Repo.all()
  end

  def get_translations_map(locale) do
    en =
      from(t in Translation, where: t.locale == "en", select: {t.key, t.value})
      |> Repo.all()
      |> Map.new()

    if locale == "en" do
      en
    else
      local =
        from(t in Translation, where: t.locale == ^locale, select: {t.key, t.value})
        |> Repo.all()
        |> Map.new()

      Map.merge(en, local)
    end
  end

  def upsert_translation(attrs) do
    %Translation{}
    |> Translation.changeset(attrs)
    |> Repo.insert(
      on_conflict: {:replace, [:value, :updated_at]},
      conflict_target: [:locale, :key]
    )
  end

  def delete_translation(id) do
    Repo.get!(Translation, id) |> Repo.delete()
  end

  def get_config_translations(locale) do
    from(ct in ConfigTranslation, where: ct.locale == ^locale)
    |> Repo.all()
    |> Enum.group_by(& &1.translatable_type)
  end

  def upsert_config_translation(attrs) do
    %ConfigTranslation{}
    |> ConfigTranslation.changeset(attrs)
    |> Repo.insert(
      on_conflict: {:replace, [:value, :updated_at]},
      conflict_target: [:locale, :translatable_type, :translatable_key, :field]
    )
  end

  def supported_locales do
    from(t in Translation, select: t.locale, distinct: true)
    |> Repo.all()
    |> Enum.sort()
  end
end
