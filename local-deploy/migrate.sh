#!/bin/sh
# Runs inside a <service>-migrate image. Applies that service's pending EF migrations (its own schema) as the database owner.
# Local copy of the server's stack-v2/migrate.sh, reading the connection string from an environment variable.
set -eu
exec ./efbundle --connection "$MIGRATE_CONNECTION"
