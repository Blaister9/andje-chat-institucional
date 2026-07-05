#!/usr/bin/env sh
# Detiene el entorno local. Los datos de PostgreSQL se conservan en el
# volumen db-data; para borrarlos: docker compose down -v
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
docker compose -f "$SCRIPT_DIR/../docker-compose.yml" down
