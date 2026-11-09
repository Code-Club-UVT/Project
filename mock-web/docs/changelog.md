# Changelog

## Icon
- `public/icon.svg`: served at `/apps/mock/icon.svg`, which the harness shows in its app menu (an app without one gets a default icon)

## Step 1: first version
- Vanilla TypeScript and Vite app using `platform-kit-web/sdk`: init data, two API calls through the harness, a second screen for deep links, theme, role and logout events
- Dockerfile (nginx unprivileged) and `nginx.conf` with `/healthz`; image `mock-web` added to the deployment workflow and to `local-deploy`
