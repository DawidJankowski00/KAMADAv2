#!/bin/sh
# Runs once, when the postgres container initialises an empty data directory (docker-entrypoint-initdb.d).
# Creates the API's least-privilege role; POSTGRES_USER stays the owner that runs migrations.
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -v owner="$POSTGRES_USER" \
    -v db="$POSTGRES_DB" \
    -v app_user="${POSTGRES_APP_USER:-kamada_app}" \
    -v app_password="${POSTGRES_APP_PASSWORD:?POSTGRES_APP_PASSWORD is not set}" \
    -f /kamada/app-role.sql
echo "KAMADA: created the API role ${POSTGRES_APP_USER:-kamada_app} (data only)"
