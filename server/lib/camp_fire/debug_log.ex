defmodule CampFire.DebugLog do
  @moduledoc """
  In-memory ring buffer for debug log entries, backed by ETS.
  Keeps the most recent 1000 entries. Broadcasts new entries via PubSub.
  """

  use GenServer

  @table :debug_log_buffer
  @max_entries 1000
  @pubsub_topic "debug_log"

  defmodule Entry do
    @moduledoc "A single debug log entry."

    @type level :: :error | :warning | :info
    @type source :: :server | :client

    @enforce_keys [:id, :timestamp, :level, :source, :category, :message]
    defstruct [
      :id,
      :timestamp,
      :level,
      :source,
      :category,
      :message,
      :player_uid,
      metadata: %{}
    ]

    @type t :: %__MODULE__{
            id: non_neg_integer(),
            timestamp: DateTime.t(),
            level: level(),
            source: source(),
            category: String.t(),
            message: String.t(),
            player_uid: String.t() | nil,
            metadata: map()
          }
  end

  # --- Public API ---

  def start_link(_opts) do
    GenServer.start_link(__MODULE__, [], name: __MODULE__)
  end

  @doc "Asynchronously log a debug entry. `attrs` is a map or keyword list."
  def log(attrs) when is_map(attrs) or is_list(attrs) do
    GenServer.cast(__MODULE__, {:log, Map.new(attrs)})
  end

  @doc """
  Query log entries with optional filters. Returns entries newest-first.

  Options:
    - `:level` - filter by level atom
    - `:source` - filter by source atom
    - `:category` - filter by exact category string
    - `:player_uid` - filter by player_uid prefix match
  """
  def list(opts \\ []) do
    filters = Map.new(opts)

    :ets.tab2list(@table)
    |> Enum.map(fn {_id, entry} -> entry end)
    |> Enum.filter(&matches_filters?(&1, filters))
    |> Enum.sort_by(& &1.id, :desc)
  end

  @doc "Returns the PubSub topic for debug log broadcasts."
  def topic, do: @pubsub_topic

  # --- GenServer callbacks ---

  @impl true
  def init(_) do
    table = :ets.new(@table, [:named_table, :set, :public, read_concurrency: true])
    {:ok, %{table: table, counter: 0}}
  end

  @impl true
  def handle_cast({:log, attrs}, %{counter: counter} = state) do
    next_id = counter + 1

    entry = %Entry{
      id: next_id,
      timestamp: Map.get(attrs, :timestamp, DateTime.utc_now()),
      level: Map.get(attrs, :level, :info),
      source: Map.get(attrs, :source, :server),
      category: Map.get(attrs, :category, "general"),
      message: Map.get(attrs, :message, ""),
      player_uid: Map.get(attrs, :player_uid),
      metadata: Map.get(attrs, :metadata, %{})
    }

    :ets.insert(@table, {next_id, entry})

    # Evict oldest entry when we exceed the ring buffer size
    if next_id > @max_entries do
      :ets.delete(@table, next_id - @max_entries)
    end

    Phoenix.PubSub.broadcast(CampFire.PubSub, @pubsub_topic, {:new_log_entry, entry})

    {:noreply, %{state | counter: next_id}}
  end

  # --- Private helpers ---

  defp matches_filters?(entry, filters) do
    matches_field?(entry.level, filters[:level]) and
      matches_field?(entry.source, filters[:source]) and
      matches_field?(entry.category, filters[:category]) and
      matches_player_uid?(entry.player_uid, filters[:player_uid])
  end

  defp matches_field?(_value, nil), do: true
  defp matches_field?(value, filter), do: value == filter

  defp matches_player_uid?(_value, nil), do: true
  defp matches_player_uid?(nil, _filter), do: false
  defp matches_player_uid?(value, filter), do: String.starts_with?(value, filter)
end
