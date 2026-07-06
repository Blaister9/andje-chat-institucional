[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$apiBase = $env:DEMO_API_URL
if ([string]::IsNullOrWhiteSpace($apiBase)) {
    $apiBase = "http://localhost:8080"
}

try {
    $response = Invoke-WebRequest -Uri "$apiBase/api/diagnostics/status" -UseBasicParsing -TimeoutSec 10
} catch {
    Write-Error "No fue posible consultar diagnostico en $apiBase/api/diagnostics/status."
    exit 1
}

if ($response.StatusCode -ne 200) {
    Write-Error "Diagnostico respondio HTTP $($response.StatusCode)."
    exit 1
}

$diagnostics = $response.Content | ConvertFrom-Json
Write-Host "Estado demo ANDJE"
Write-Host "Status:      $($diagnostics.status)"
Write-Host "Environment: $($diagnostics.environment)"
Write-Host "Database:    $($diagnostics.database)"
Write-Host "UTC:         $($diagnostics.utcNow)"
Write-Host "Request ID:  $($response.Headers['X-Request-ID'])"

if ($diagnostics.counts) {
    Write-Host ""
    Write-Host "Conteos operativos"
    Write-Host "Conversaciones total:   $($diagnostics.counts.conversationsTotal)"
    Write-Host "Conversaciones abiertas: $($diagnostics.counts.conversationsOpen)"
    Write-Host "Conversaciones cerradas: $($diagnostics.counts.conversationsClosed)"
    Write-Host "Mensajes total:          $($diagnostics.counts.messagesTotal)"
    Write-Host "Eventos auditoria total: $($diagnostics.counts.auditEventsTotal)"
}

Write-Host ""
Write-Host "No se imprimen mensajes, tokens, codigos ni cadenas de conexion."
