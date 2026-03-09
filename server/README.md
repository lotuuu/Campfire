# CampFire Server

## Local Development

  * `make setup` — start DB (Docker), install deps, create + migrate + seed
  * `make dev` — start Phoenix server (port 4000)
  * `make psql` — open psql shell
  * `make tunnel` / `make tunnel-stop` — ngrok tunnel for device testing

Visit [`localhost:4000`](http://localhost:4000) from your browser.

## Deploying to Gigalixir

The app is hosted on Gigalixir at `https://campfire.gigalixirapp.com`.

```bash
make deploy          # push code to Gigalixir
make deploy-migrate  # run Ecto migrations on remote
make deploy-full     # both: deploy + migrate
```

**How deploy works**: Since Gigalixir doesn't support Git LFS, `make deploy` rsyncs the server directory to a temp folder (dereferencing LFS pointers into real files), creates a fresh git repo, and force-pushes to Gigalixir.

If you only changed code (no new migrations), `make deploy` alone is sufficient.

### First-time setup

1. Install the Gigalixir CLI: `pip install gigalixir`
2. Log in: `gigalixir login`
3. Deploy: `make deploy-full`
4. Run seeds (if needed): connect via `gigalixir ps:remote_console` or run locally against the remote DB
