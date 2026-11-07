# CLAUDE.md

This is the root instruction file. Read it at the start of every session.

## Required steps

1. Read this file first.
2. Before working on anything inside a project directory, read that project's own `CLAUDE.md` and follow it. Project files add to this one; if they conflict, the project file wins for work inside that project.
3. If you work across several projects, read each project's `CLAUDE.md` before touching it.

## Working rules

- Do not commit anything unless explicitly asked to. Just update the files.
- Before any implementation, write a detailed plan for it and ask for approval. Do not start implementing until the plan is approved. The plan covers what will change, which files are added or modified, new dependencies, and open questions. Give it in the chat. If it is too long to read comfortably there, put it in a temporary markdown file in the session scratchpad (outside the repo) and point to it.
- After each implementation, report the changes made in the chat: the most important files and what each one does. If the report is too long, use a temporary markdown file in the session scratchpad and point to it.
- Never save plans or change reports inside the repo (not even in the gitignored root `docs/`).
- Delete any temporary plan or report file once the implementation or its review is done.
- Every code project keeps a `docs/` folder inside its own directory (for example `core-api/docs/`) with markdown files for the current implementation, what has been done, pending decisions, and things to look at in the future. Update it at the end of each step.

## Projects

| Directory | What it is | Instructions |
|-----------|------------|--------------|
| `licenta-victor/` | Thesis (LaTeX) | [licenta-victor/CLAUDE.md](licenta-victor/CLAUDE.md) |
| `identity-api/` | Accounts, login, edge/refresh/project tokens, JWKS | [identity-api/CLAUDE.md](identity-api/CLAUDE.md) |
| `core-api/` | Profiles, projects, project roles, members, default roles | [core-api/CLAUDE.md](core-api/CLAUDE.md) |
| `email-api/` | Templated email (internal only) | [email-api/CLAUDE.md](email-api/CLAUDE.md) |
| `gateway/` | The single entry point (YARP): routing, access check, token swap | [gateway/CLAUDE.md](gateway/CLAUDE.md) |
| `platform-kit/` | Shared library: token validation, request context, results/ProblemDetails, internal-key auth | [platform-kit/CLAUDE.md](platform-kit/CLAUDE.md) |
| `mock-api/` | Throw-away project API for end-to-end tests | [mock-api/CLAUDE.md](mock-api/CLAUDE.md) |
| `harness-web/` | React front end (test page) and the nginx that is each environment's public door to the gateway | [harness-web/CLAUDE.md](harness-web/CLAUDE.md) |

When a new project directory is added to this repo, give it its own `CLAUDE.md` and a `docs/` folder, and add a row to the table above.

How they fit together: the browser talks only to the gateway (`/api/<slug>/...`), through the harness container's nginx, which is the only thing Caddy exposes per environment (the gateway has no public route). Identity is the one signer of tokens; every other API validates project tokens with platform-kit. Each service owns one schema of the single PostgreSQL database `platform` (`identity`, `core`, `email`); only the owner writes, and the gateway and identity read the few tables they need. Identity tells core and email what happened through an outbox (internal HTTP with `X-Internal-Key`, retried).

## Repo-wide notes

- The root `/docs` is gitignored (temporary files). Do not rely on it being tracked. Project `docs/` folders (such as `core-api/docs/`) are tracked.
- LaTeX build artifacts (`.aux`, `.log`, `.bbl`, etc.) are gitignored. Do not commit them.
- Deployment: the server setup (`/home/ubuntu/codeclub-uvt`: compose files, scripts, Caddy, per-environment config with secrets) lives only on the server, not in this repo. Its `README.md` documents it. The repo holds the Dockerfiles and `.github/workflows/deploy.yml` (push to main to preprod, a published GitHub Release to prod, any other branch started by hand to dev; images are tagged with the commit sha, plus `latest` on main, or with the release tag on a release). Do not run deploy commands against the server unless asked.
- Server access: `ssh CodeClubUVT` (alias in the local `~/.ssh/config`: HostName `194.102.62.209`, User `ubuntu`, key-based). The setup is in `~/codeclub-uvt` (`dev/`, `preprod/`, `prod/`, `proxy/`, `scripts/`, `stack/`, `README.md`). Read-only commands (reading files, `docker ps`, logs) are fine; ask before changing anything there (files, containers, databases, deploys).
