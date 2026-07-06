[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

docker compose down
if ($LASTEXITCODE -ne 0) {
    Write-Error "No fue posible detener el stack."
    exit 1
}

Write-Host "Demo detenida. Los volumenes se conservan."
Write-Host "Para reset completo de contenedores y volumenes, ejecute manualmente: docker compose down -v"
Write-Host "Para borrar solo datos demo sin borrar estructura, use: .\scripts\demo\reset-demo-data.ps1"
