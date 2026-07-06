[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Error $message
    exit 1
}

function Import-DemoEnv($path) {
    if (-not (Test-Path -LiteralPath $path)) {
        return
    }

    Get-Content -LiteralPath $path | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) {
            return
        }

        $parts = $line.Split("=", 2)
        if ($parts.Count -ne 2) {
            Fail "Linea invalida en ${path}: $line"
        }

        [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim(), "Process")
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Fail "Docker no esta disponible en PATH. Instale o inicie Docker Desktop."
}

docker version *> $null
if ($LASTEXITCODE -ne 0) {
    Fail "Docker no responde. Verifique que Docker Desktop este iniciado."
}

docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    Fail "'docker compose' no esta disponible."
}

Import-DemoEnv (Join-Path $repoRoot ".env.demo")

Write-Host "Levantando demo con Docker Compose..."
docker compose up --build -d
if ($LASTEXITCODE -ne 0) {
    Fail "No fue posible levantar el stack."
}

$apiBase = $env:DEMO_API_URL
if ([string]::IsNullOrWhiteSpace($apiBase)) {
    $apiBase = "http://localhost:8080"
}

$healthUrl = "$apiBase/health"
$deadline = (Get-Date).AddSeconds(120)
$healthy = $false
do {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200 -and $response.Content.Trim() -eq "Healthy") {
            $healthy = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 3
    }
} while ((Get-Date) -lt $deadline)

if (-not $healthy) {
    docker compose ps
    Fail "La API no respondio Healthy en $healthUrl."
}

$agentCode = $env:ANDJE_AGENT_DEV_CODE
if ([string]::IsNullOrWhiteSpace($agentCode)) {
    $agentCode = "andje-agent-local"
}

Write-Host ""
Write-Host "Demo ANDJE lista."
Write-Host "Widget demo:   http://localhost:5174"
Write-Host "Consola:       http://localhost:5173"
Write-Host "API health:    $healthUrl"
$dbPort = $env:ANDJE_DB_PORT
if ([string]::IsNullOrWhiteSpace($dbPort)) {
    $dbPort = "5433"
}
Write-Host "PostgreSQL:    localhost:$dbPort, db andje_chat"
Write-Host ""
Write-Host "Credenciales demo consola:"
Write-Host "Nombre: Funcionario Demo"
Write-Host "Codigo: $agentCode"
Write-Host ""
Write-Host "Logs utiles:"
Write-Host "docker compose logs -f api"
Write-Host "docker compose logs -f console widget"
Write-Host ""
Write-Host "No use datos reales. Este perfil es solo demo local/LAN, no produccion."
