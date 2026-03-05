.PHONY: setup dev start test psql reset up down logs tunnel tunnel-stop deps

DEV_CONFIG := ../Assets/Scripts/Services/DevServerConfig.cs
PORT := 4000
NGROK_PID_FILE := .ngrok.pid

# Install Elixir dependencies
deps:
	mix deps.get

# Start PostgreSQL
up:
	docker compose up -d

# Stop PostgreSQL
down:
	docker compose down

# Stop PostgreSQL and delete data
reset:
	docker compose down -v
	mix ecto.reset

# Full setup: start db, wait for ready, create + migrate + seed
setup: up deps
	@echo "Waiting for PostgreSQL..."
	@until docker exec $$(docker compose ps -q db) pg_isready -U campfire > /dev/null 2>&1; do sleep 0.5; done
	@echo "PostgreSQL ready."
	mix ecto.setup
	@echo "Setup complete. Run 'make dev' to start the server."

# Start server in dev mode with ngrok tunnel
dev: tunnel
	mix phx.server; $(MAKE) tunnel-stop

# Start server without tunnel
start:
	mix phx.server

# Run tests
test:
	mix test

# Tail PostgreSQL logs
logs:
	docker compose logs -f db

# Open psql shell
psql:
	docker exec -it $$(docker compose ps -q db) psql -U campfire campfire_dev

# Start ngrok tunnel and write URL into DevServerConfig.cs
tunnel:
	@if [ -f $(NGROK_PID_FILE) ] && kill -0 $$(cat $(NGROK_PID_FILE)) 2>/dev/null; then \
		echo "ngrok already running (pid $$(cat $(NGROK_PID_FILE)))"; \
	else \
		ngrok http $(PORT) --log=stdout > /dev/null & echo $$! > $(NGROK_PID_FILE); \
		echo "Starting ngrok..."; \
		sleep 2; \
	fi
	@URL=$$(curl -s http://localhost:4040/api/tunnels | python3 -c "import sys,json; print(json.load(sys.stdin)['tunnels'][0]['public_url'])" 2>/dev/null) && \
	if [ -z "$$URL" ]; then echo "ERROR: Could not get ngrok URL. Is ngrok running?"; exit 1; fi && \
	printf 'namespace Garden\n{\n    public static class DevServerConfig\n    {\n        public const string BaseUrl = "%s";\n    }\n}\n' "$$URL" > $(DEV_CONFIG) && \
	echo "DevServerConfig.cs updated: $$URL"

# Stop ngrok tunnel and reset DevServerConfig to localhost
tunnel-stop:
	@if [ -f $(NGROK_PID_FILE) ]; then \
		kill $$(cat $(NGROK_PID_FILE)) 2>/dev/null; \
		rm $(NGROK_PID_FILE); \
		echo "ngrok stopped."; \
	else \
		echo "ngrok not running."; \
	fi
	@printf 'namespace Garden\n{\n    public static class DevServerConfig\n    {\n        public const string BaseUrl = "http://localhost:4000";\n    }\n}\n' > $(DEV_CONFIG)
	@echo "DevServerConfig.cs reset to localhost."
