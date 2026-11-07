# Build with the repository root as the context (like the other images):
#   docker build -f harness-web/Dockerfile -t harness .
# One image for every environment. Where it lives and how it reaches the gateway are environment variables at start
# (see nginx/entrypoint.sh), not build arguments.
FROM node:24-alpine AS build
WORKDIR /src

# Install first, so the layer is cached until the dependencies change.
COPY harness-web/package.json harness-web/package-lock.json ./
RUN npm ci

COPY harness-web/ ./
# Lint, format check and type check must pass for an image to exist; then the production build.
RUN npm run ci && npm run build

FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY --from=build /src/dist /usr/share/harness/site
COPY harness-web/nginx/nginx.conf /etc/nginx/nginx.conf
COPY harness-web/nginx/security-headers.conf harness-web/nginx/api-proxy.conf harness-web/nginx/api-open.conf harness-web/nginx/api-restricted.conf /etc/harness/
COPY --chmod=755 harness-web/nginx/entrypoint.sh /harness-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/harness-entrypoint.sh"]
