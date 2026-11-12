# Krok F10: lokalny test PWA i pusha na "prawdziwym" buildzie – API serwuje frontend z jednej domeny.
#   serve-local.cmd            build frontendu -> ICanHelp.Api\wwwroot -> API na https://localhost:7080 (+ http://localhost:5080)
#   serve-local.cmd -NoBuild   bez ponownego builda frontendu (tylko kopiowanie www\ i start API)
# Telefon: tunel HTTPS na port 5080, np. `devtunnel host -p 5080 --allow-anonymous` albo `ngrok http 5080`.
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root 'ICanHelp.Web'
$www = Join-Path $web 'www'
$wwwroot = Join-Path $root 'ICanHelp.Api\wwwroot'

# Baza: kontener z docker-compose.yml (bez niego API nie wystartuje – migracje przy starcie).
Push-Location $root
try { docker compose up -d | Out-Host } finally { Pop-Location }

if (-not $NoBuild) {
    Push-Location $web
    try {
        if (-not (Test-Path (Join-Path $web 'node_modules'))) { npm ci | Out-Host }
        npm run build | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "npm run build zakonczyl sie bledem ($LASTEXITCODE)" }
    } finally { Pop-Location }
}

if (-not (Test-Path (Join-Path $www 'index.html'))) { throw "Brak $www\index.html - uruchom bez -NoBuild" }

# /MIR: wwwroot = dokladna kopia www\ (stare chunki znikaja). Kody robocopy < 8 to sukces.
robocopy $www $wwwroot /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy zakonczyl sie bledem ($LASTEXITCODE)" }

Write-Host ''
Write-Host 'Aplikacja: https://localhost:7080  (Swagger: https://localhost:7080/swagger)' -ForegroundColor Green
Write-Host 'Push: user-secrets Push:Enabled=true + Push:Fcm:CredentialsPath (patrz CLAUDE.md).' -ForegroundColor Green
Write-Host ''
dotnet run --project (Join-Path $root 'ICanHelp.Api') --launch-profile https
