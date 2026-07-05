#!/usr/bin/env sh
# Levanta el entorno local completo (base de datos, API, consola y widget).
# Requiere Docker en ejecución.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
docker compose -f "$SCRIPT_DIR/../docker-compose.yml" up --build -d

echo ''
echo 'Entorno local levantado:'
echo '  API (healthcheck) : http://localhost:8080/health'
echo '  Hub SignalR       : http://localhost:8080/hubs/chat'
echo '  Consola de agentes: http://localhost:5173'
echo '  Demo del widget   : http://localhost:5174'
echo '  PostgreSQL        : localhost:5432 (db: andje_chat)'
