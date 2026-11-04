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

| Directory | Instructions |
|-----------|--------------|
| `licenta-victor/` | [licenta-victor/CLAUDE.md](licenta-victor/CLAUDE.md) |
| `core-api/` | [core-api/CLAUDE.md](core-api/CLAUDE.md) |
| `gateway-auth/` | [gateway-auth/CLAUDE.md](gateway-auth/CLAUDE.md) |
| `gateway/` | [gateway/CLAUDE.md](gateway/CLAUDE.md) |
| `mock-api/` | [mock-api/CLAUDE.md](mock-api/CLAUDE.md) |

When a new project directory is added to this repo, give it its own `CLAUDE.md` and a `docs/` folder, and add a row to the table above.

## Repo-wide notes

- The root `/docs` is gitignored (temporary files). Do not rely on it being tracked. Project `docs/` folders (such as `core-api/docs/`) are tracked.
- LaTeX build artifacts (`.aux`, `.log`, `.bbl`, etc.) are gitignored. Do not commit them.
- Deployment: the server setup (`/home/ubuntu/codeclub-uvt`: compose files, scripts, Caddy, per-environment config with secrets) lives only on the server, not in this repo. Its `README.md` documents it. The repo holds the Dockerfiles and `.github/workflows/deploy.yml` (main to preprod, `releases/*` to prod, any other branch started by hand to dev). Do not run deploy commands against the server unless asked.
