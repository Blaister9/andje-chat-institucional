# Levanta el entorno local completo (base de datos, API, consola y widget).
# Requiere Docker Desktop en ejecución.
$ErrorActionPreference = 'Stop'

docker compose -f "$PSScriptRoot\..\docker-compose.yml" up --build -d

Write-Host ''
Write-Host 'Entorno local levantado:'
Write-Host '  API (healthcheck) : http://localhost:8080/health'
Write-Host '  Hub SignalR       : http://localhost:8080/hubs/chat'
Write-Host '  Consola de agentes: http://localhost:5173'
Write-Host '  Demo del widget   : http://localhost:5174'
Write-Host '  PostgreSQL        : localhost:5432 (db: andje_chat)'
