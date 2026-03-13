defmodule CampFire.DebugLog.LoggerBackend do
  @behaviour :gen_event

  @impl true
  def init(_) do
    {:ok, %{level: :warning}}
  end

  @impl true
  def handle_event({level, _gl, {Logger, message, _timestamp, metadata}}, state)
      when level in [:warning, :error] do
    CampFire.DebugLog.log(%{
      level: level,
      source: :server,
      category: "logger",
      message: IO.iodata_to_binary(message),
      player_uid: metadata[:player_uid],
      metadata: %{
        module: inspect(metadata[:module]),
        function: metadata[:function],
        file: metadata[:file],
        line: metadata[:line]
      }
    })

    {:ok, state}
  end

  @impl true
  def handle_event(_event, state), do: {:ok, state}

  @impl true
  def handle_call({:configure, _opts}, state), do: {:ok, :ok, state}

  @impl true
  def handle_info(_msg, state), do: {:ok, state}

  @impl true
  def code_change(_old, state, _extra), do: {:ok, state}

  @impl true
  def terminate(_reason, _state), do: :ok
end
