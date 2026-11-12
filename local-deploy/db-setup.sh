#!/bin/sh
# Runs inside a postgres image after the migrations. Creates the roles and grants (see grants.sql); safe to run every time.
# Local copy of the server's stack-v2/db-setup.sh, reading the passwords from environment variables.
set -eu
export PGPASSWORD="$PG_ADMIN_PASSWORD"
exec psql -h postgres -U postgres -d platform -v ON_ERROR_STOP=1 \
    -v identity_pw="$PG_IDENTITY_PASSWORD" \
    -v core_pw="$PG_CORE_PASSWORD" \
    -v email_pw="$PG_EMAIL_PASSWORD" \
    -v gateway_pw="$PG_GATEWAY_PASSWORD" \
    -f /grants.sql
