.PHONY: up down db migrate dev start logs psql reset

DATABASE_URL ?= postgresql://campfire:campfire@localhost:5432/campfire

# Start PostgreSQL
up:
	docker compose up -d

# Stop PostgreSQL
down:
	docker compose down

# Stop PostgreSQL and delete data
reset:
	docker compose down -v

# Run database migrations
migrate:
	DATABASE_URL=$(DATABASE_URL) node src/db/migrate.js

# Start server in dev mode (auto-reload)
dev:
	DATABASE_URL=$(DATABASE_URL) node --watch src/index.js

# Start server
start:
	DATABASE_URL=$(DATABASE_URL) node src/index.js

# Tail PostgreSQL logs
logs:
	docker compose logs -f db

# Open psql shell
psql:
	docker exec -it $$(docker compose ps -q db) psql -U campfire campfire

# Install npm dependencies
install:
	npm install

# Full setup: start db, wait for ready, migrate
setup: up
	@echo "Waiting for PostgreSQL..."
	@until docker exec $$(docker compose ps -q db) pg_isready -U campfire > /dev/null 2>&1; do sleep 0.5; done
	@echo "PostgreSQL ready."
	$(MAKE) migrate
	@echo "Setup complete. Run 'make dev' to start the server."
