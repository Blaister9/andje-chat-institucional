[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Error $message
    exit 1
}

function Assert-HttpOk($name, $url) {
    $deadline = (Get-Date).AddSeconds(60)
    $response = $null
    do {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                return
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    if ($null -eq $response) {
        Fail "$name no responde en $url."
    }

    if ($response.StatusCode -ne 200) {
        Fail "$name respondio HTTP $($response.StatusCode) en $url."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$services = docker compose ps --services --filter "status=running"
foreach ($required in @("db", "api", "console", "widget")) {
    if ($services -notcontains $required) {
        Fail "El contenedor '$required' no esta corriendo."
    }
}

$apiBase = $env:DEMO_API_URL
if ([string]::IsNullOrWhiteSpace($apiBase)) {
    $apiBase = "http://localhost:8080"
}

$health = Invoke-WebRequest -Uri "$apiBase/health" -UseBasicParsing -TimeoutSec 10
if ($health.StatusCode -ne 200 -or $health.Content.Trim() -ne "Healthy") {
    Fail "Healthcheck invalido en $apiBase/health."
}

$requiredHeaders = @(
    "X-Content-Type-Options",
    "X-Frame-Options",
    "Referrer-Policy",
    "Permissions-Policy",
    "Cache-Control"
)
foreach ($header in $requiredHeaders) {
    if (-not $health.Headers.ContainsKey($header)) {
        Fail "Header requerido ausente en /health: $header"
    }
}

Assert-HttpOk "Consola" "http://localhost:5173"
Assert-HttpOk "Widget" "http://localhost:5174"

docker compose exec -T db pg_isready -U andje -d andje_chat
if ($LASTEXITCODE -ne 0) {
    Fail "PostgreSQL no responde pg_isready."
}

Write-Host "Conteo de conversaciones:"
$countSql = 'SELECT COUNT(*) AS conversations FROM "Conversations";'
$countSql | docker compose exec -T db psql -U andje -d andje_chat -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) {
    Fail "No fue posible consultar conversaciones."
}

Write-Host "Demo OK."
Write-Host "API health: $apiBase/health"
Write-Host "Consola:    http://localhost:5173"
Write-Host "Widget:     http://localhost:5174"
