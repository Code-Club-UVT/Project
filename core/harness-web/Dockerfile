# Build with the repository root as the context (like the other images):
#   docker build -f core/harness-web/Dockerfile -t harness .
# One image for every environment. Where it lives and how it reaches the gateway are environment variables at start
# (see nginx/entrypoint.sh), not build arguments.
FROM node:24-alpine AS build
# Same layout as the repository: the shared front-end library is a file dependency
# ("file:../../common/platform-kit-web"), so it must sit at that relative path.
WORKDIR /repo/core/harness-web
COPY common/platform-kit-web/ /repo/common/platform-kit-web/

# Install first, so the layer is cached until the dependencies change.
COPY core/harness-web/package.json core/harness-web/package-lock.json ./
RUN npm ci

COPY core/harness-web/ ./
# Lint, format check and type check must pass for an image to exist; then the production build.
RUN npm run ci && npm run build

FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY --from=build /repo/core/harness-web/dist /usr/share/harness/site
COPY core/harness-web/nginx/nginx.conf /etc/nginx/nginx.conf
COPY core/harness-web/nginx/security-headers.conf core/harness-web/nginx/api-proxy.conf core/harness-web/nginx/api-open.conf core/harness-web/nginx/api-restricted.conf core/harness-web/nginx/apps.conf core/harness-web/nginx/apps-headers.conf /etc/harness/
COPY --chmod=755 core/harness-web/nginx/entrypoint.sh /harness-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/harness-entrypoint.sh"]
