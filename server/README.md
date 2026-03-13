# CampFire Server

Elixir/Phoenix backend for Camp Fire. Provides game config, economy, social features, sprites, and admin dashboard.

## Local Development

```bash
make setup    # start Postgres (Docker), install deps, create + migrate + seed
make dev      # start Phoenix server with ngrok tunnel (port 4000)
make start    # start Phoenix server without tunnel
make test     # run tests
make psql     # open psql shell
```

Admin dashboard at `localhost:4000/admin`.

## Deployment

Hosted on Gigalixir at `https://campfire.gigalixirapp.com`.

```bash
make deploy              # push code, wait for new version, migrate forward
make redeploy            # push code, wait for new version, reset DB + re-migrate + re-seed
make deploy COMMIT=abc   # deploy a specific commit (no working tree changes)
make redeploy COMMIT=abc # redeploy a specific commit
```

`deploy` is for normal releases — pushes code, waits for the new version to be confirmed live (polls `gigalixir ps` for matching sha), then runs migrations forward.

`redeploy` is for fresh DB rebuilds — same push + wait, then runs `CampFire.Release.reset()` via `ps:remote_console` (synchronous SSH) which drops all tables, re-migrates, and re-seeds. ConfigCache also auto-seeds on boot if game configs are empty.

`COMMIT=<sha>` uses `git archive` to extract the server directory from a specific commit, so the local working tree is never touched.

### First-time Gigalixir setup

1. Install the CLI: `pip install gigalixir`
2. Log in: `gigalixir login`
3. Deploy: `make deploy`
