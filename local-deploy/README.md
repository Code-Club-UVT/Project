# local-deploy

The whole platform on your own machine, built from the source in this repository: PostgreSQL, identity-api, core-api, email-api, the gateway, mock-api, mock-web and the harness (front end). It mirrors the server's `stack-v2`, so what works here works on dev. Real email is sent through your SMTP account.

## What you need

- Docker (Docker Desktop on Windows or macOS) with Compose v2
- Node 20 or later (only for `init.mjs`)
- About 6 GB of free disk and a few minutes for the first build (five .NET images and the front end)

## First time

Run everything from this folder (`local-deploy/`).

```bash
node init.mjs
docker compose up -d --build
```

`init.mjs` creates `.env` (gitignored) with random passwords and keys, and asks for the SMTP username and password the emails are sent with (an app password for Gmail, or the account of your mail provider). Leave the password empty to skip real email: the emails are then only written to the `email-api` log. You can change this later with `node init.mjs --smtp` followed by `docker compose up -d email-api`.

The first `up` builds the images, runs the three database migrations and the grants, and starts the services. Check that everything is healthy:

```bash
docker compose ps
```

## Use it

| What | Where |
|------|-------|
| The harness (front end) | http://localhost:8080 |
| The API, for Postman or curl | http://localhost:8080/api/... (for example `POST /api/identity/auth/login`) |
| The gateway's health | http://localhost:8080/health |
| PostgreSQL | `127.0.0.1:5432`, user `postgres`, database `platform`, password `PG_ADMIN_PASSWORD` in `.env` |

Nothing is published except those two ports, and both only on `127.0.0.1` (your machine). Change the ports with `LOCAL_HTTP_PORT` and `LOCAL_PG_PORT` in `.env`.

**Log in as the admin**: username `admin`, password `ADMIN_PASSWORD` from `.env`. The admin has the `Admin` role on the `core` project. The "Who am I" buttons then show your profile and projects, which proves the gateway got a project token from identity.

**Register a new account**: use the Register card. An email with a confirmation link is sent to the address. The harness does not have a confirmation page yet, so take the `token` from the link and confirm with the API:

```bash
curl -X POST http://localhost:8080/api/identity/auth/confirm-email -H "Content-Type: application/json" -d "{\"token\":\"<the token from the link>\"}"
```

If you chose not to send real email, the same link is in the log: `docker compose logs email-api`.

**The mock project** (the third button) only works after an admin creates a project with slug `mock`, backend URL `http://mock-api:8080/api`, health URL `http://mock-api:8080/health` and, for the iframe tab, frontend URL `http://mock-web:8080/`, and adds a member or default role. See `mock/api/docs` and `mock/web/docs`. The harness then shows a `Mock` tab for members; `/apps/<slug>/` is served by the gateway.

## Work on the front end with hot reload

Keep the stack running, then in `core/harness-web/`:

```bash
npm ci
npm run dev
```

Open http://localhost:5173. Vite forwards `/api` to http://localhost:8080 (see `core/harness-web/.env.example`).

## Everyday commands

```bash
docker compose logs -f gateway          # follow one service (identity-api, core-api, email-api, gateway, mock-api, mock-web, harness)
docker compose up -d --build core-api   # rebuild and restart one service after a code change
docker compose down                     # stop and remove the containers (the database stays)
docker compose down -v                  # also delete the database: the next `up` starts from scratch
```

If you recreate `.env` (`node init.mjs --force`), also run `docker compose down -v`: the database roles keep the old passwords otherwise.

## Differences from the server

- Settings are environment variables in `docker-compose.yml`, not JSON files; secrets come from `.env`.
- Services run in `Development` mode. Identity makes a temporary signing key at each start, so restarting identity logs everybody out.
- The refresh cookie is not `Secure` (plain HTTP) and its path is `/api/identity/auth`.
- The harness runs in `open` mode, like dev.
- `db-setup.sh`, `migrate.sh` and `grants.sql` are copies of the server's `stack-v2` files (the server setup is not in this repository). If the server's grants change, update `grants.sql` here too.

## Troubleshooting

| Symptom | What to do |
|---------|------------|
| `run node init.mjs first` | `.env` is missing: run `node init.mjs` |
| `docker compose ps` shows a service restarting | `docker compose logs <service>` |
| A migration job fails | `docker compose logs migrate-core` (or `migrate-identity`, `migrate-email`, `db-setup`) |
| Port 8080 or 5432 is in use | set `LOCAL_HTTP_PORT` or `LOCAL_PG_PORT` in `.env` and run `docker compose up -d` |
| "Pool overlaps" for the network `uvt-local` | `docker network rm uvt-local` (or change the subnet in `docker-compose.yml`) |
| Login says the email is not confirmed | confirm it as described above |
| No email arrives | check the SMTP settings with `node init.mjs --smtp`; `docker compose logs email-api` shows the send errors |
