#!/bin/sh
# Renders the container's settings from environment variables, then starts nginx. One image serves every environment.
#
#   HARNESS_BASE_PATH    where the app lives: /dev/, /preprod/ or / (prod). Must start and end with a slash.
#   HARNESS_API_URL      where the browser finds the gateway's /api. Default: <base path>api (same origin).
#   HARNESS_API_MODE     open (dev: everything of the gateway is reachable) or restricted (preprod, prod). Default: restricted.
#   HARNESS_GATEWAY_URL  the gateway on the private network. Default: http://gateway:8080
#   HARNESS_MOCK_SLUG    slug of the mock project for the "whoami" button. Default: mock
set -eu

fail() {
    echo "harness: $*" >&2
    exit 1
}

BASE_PATH="${HARNESS_BASE_PATH:-/}"
API_URL="${HARNESS_API_URL:-${BASE_PATH}api}"
API_MODE="${HARNESS_API_MODE:-restricted}"
GATEWAY_URL="${HARNESS_GATEWAY_URL:-http://gateway:8080}"
MOCK_SLUG="${HARNESS_MOCK_SLUG:-mock}"

# The values end up in HTML, JavaScript and nginx config: accept only plain characters.
printf '%s' "$BASE_PATH" | grep -Eq '^/([A-Za-z0-9_.-]+/)*$' || fail "HARNESS_BASE_PATH must look like /, /dev/ or /a/b/ (got '$BASE_PATH')"
printf '%s' "$API_URL" | grep -Eq '^[A-Za-z0-9_:./-]+$' || fail "HARNESS_API_URL has unexpected characters"
printf '%s' "$GATEWAY_URL" | grep -Eq '^http://[A-Za-z0-9_.-]+(:[0-9]+)?$' || fail "HARNESS_GATEWAY_URL must look like http://gateway:8080"
printf '%s' "$MOCK_SLUG" | grep -Eq '^[a-z0-9-]+$' || fail "HARNESS_MOCK_SLUG must be lowercase letters, digits and dashes"
case "$API_MODE" in open | restricted) ;; *) fail "HARNESS_API_MODE must be open or restricted (got '$API_MODE')" ;; esac

mkdir -p /tmp/site /tmp/harness
cp -R /usr/share/harness/site/. /tmp/site/

# Assets are relative, so the <base href> is all the app needs to find itself under any path.
sed -i "s|<base href=\"/\" />|<base href=\"$BASE_PATH\" />|" /tmp/site/index.html
grep -q "<base href=\"$BASE_PATH\" />" /tmp/site/index.html || fail "index.html has no <base href> to fill in"

printf 'window.__HARNESS_CONFIG__ = { apiUrl: "%s", mockSlug: "%s" };\n' "$API_URL" "$MOCK_SLUG" >/tmp/site/config.js

sed "s|__GATEWAY_URL__|$GATEWAY_URL|g" /etc/harness/api-proxy.conf >/tmp/harness/api-proxy.conf
cp "/etc/harness/api-$API_MODE.conf" /tmp/harness/api.conf

echo "harness: base path $BASE_PATH, api $API_URL, api mode $API_MODE, gateway $GATEWAY_URL"
nginx -t
exec nginx -g 'daemon off;'
