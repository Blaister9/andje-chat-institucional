[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Run-Db($sql) {
    $sql | docker compose exec -T db psql -U andje -d andje_chat -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo comando PostgreSQL."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

docker compose ps db *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "El servicio db no esta disponible. Ejecute .\scripts\demo\start-demo.ps1 primero."
    exit 1
}

Write-Host "Conteos antes del reset:"
Run-Db 'SELECT ''AuditEvents'' AS table_name, COUNT(*) FROM "AuditEvents" UNION ALL SELECT ''Messages'', COUNT(*) FROM "Messages" UNION ALL SELECT ''Conversations'', COUNT(*) FROM "Conversations";'

if (-not $Force) {
    $confirmation = Read-Host "Escriba RESET DEMO para borrar datos demo sin borrar estructura"
    if ($confirmation -ne "RESET DEMO") {
        Write-Host "Reset cancelado."
        exit 0
    }
}

Run-Db 'TRUNCATE TABLE "AuditEvents", "Messages", "Conversations" RESTART IDENTITY CASCADE;'

Write-Host "Conteos despues del reset:"
Run-Db 'SELECT ''AuditEvents'' AS table_name, COUNT(*) FROM "AuditEvents" UNION ALL SELECT ''Messages'', COUNT(*) FROM "Messages" UNION ALL SELECT ''Conversations'', COUNT(*) FROM "Conversations";'

Write-Host "Datos demo limpiados. Estructura, migraciones y volumenes se conservan."
