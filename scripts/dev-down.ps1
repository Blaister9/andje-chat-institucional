# Detiene el entorno local. Los datos de PostgreSQL se conservan en el
# volumen db-data; para borrarlos: docker compose down -v
$ErrorActionPreference = 'Stop'

docker compose -f "$PSScriptRoot\..\docker-compose.yml" down
