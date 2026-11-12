# Build with the repository root as the context (like the other images):
#   docker build -f mock/web/Dockerfile -t mock-web .
FROM node:24-alpine AS build
# Same layout as the repository: the shared front-end library is a file dependency
# ("file:../../common/platform-kit-web"), so it must sit at that relative path.
WORKDIR /repo/mock/web
COPY common/platform-kit-web/ /repo/common/platform-kit-web/

COPY mock/web/package.json mock/web/package-lock.json ./
RUN npm ci

COPY mock/web/ ./
# Lint, format check and type check must pass for an image to exist; then the production build.
RUN npm run ci && npm run build

FROM nginxinc/nginx-unprivileged:1.30-alpine
COPY --from=build /repo/mock/web/dist /usr/share/mock-web
COPY mock/web/nginx.conf /etc/nginx/nginx.conf

EXPOSE 8080
