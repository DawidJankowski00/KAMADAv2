#!/bin/sh
# Writes runtime configuration for the Blazor app (API address) when the container starts,
# so one image works for any environment. Runs from nginx's /docker-entrypoint.d/.
set -eu

if [ -n "${API_BASE_URL:-}" ]; then
    html=/usr/share/nginx/html
    printf '{\n  "ApiBaseUrl": "%s"\n}\n' "$API_BASE_URL" > "$html/appsettings.json"
    # Remove stale pre-compressed copies produced by publish (gzip_static would serve them).
    rm -f "$html/appsettings.json.gz" "$html/appsettings.json.br"
    echo "KAMADA: ApiBaseUrl set to $API_BASE_URL"
fi
