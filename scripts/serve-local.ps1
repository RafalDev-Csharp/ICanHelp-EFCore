# Krok F10: lokalny test PWA i pusha na "prawdziwym" buildzie – API serwuje frontend z jednej domeny.
#   serve-local.cmd            build frontendu -> ICanHelp.Api\wwwroot -> API na https://localhost:7080 (+ http://localhost:5080)
#   serve-local.cmd -NoBuild   bez ponownego builda frontendu (tylko kopiowanie www\ i start API)
#   serve-local.cmd -Tunnel    do tego tunel cloudflared na port 5080 (adres https://….trycloudflare.com – dla telefonu);
#                              start-all.cmd = to samo jednym poleceniem. Ctrl+C zamyka API i tunel.
param([switch]$NoBuild, [switch]$Tunnel)

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

# Tunel HTTPS dla telefonu (cloudflared, bez konta – adres zmienia się przy każdym starcie). Działa w tle, a jego
# wyjście idzie do .verify\cloudflared.log; adres wypisujemy, gdy tylko się pojawi.
$tunnelProcess = $null
if ($Tunnel) {
    $cloudflared = (Get-Command cloudflared -ErrorAction SilentlyContinue).Source
    if (-not $cloudflared) { $cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe' }
    if (-not (Test-Path $cloudflared)) { throw "Nie znaleziono cloudflared ($cloudflared)" }

    # Tunel z poprzedniego startu (okno zamknięte krzyżykiem, nie Ctrl+C) – inaczej działałyby dwa.
    Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
        Where-Object { $_.CommandLine -like '*localhost:5080*' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

    $logDir = Join-Path $root '.verify'
    New-Item -ItemType Directory -Force $logDir | Out-Null
    $tunnelLog = Join-Path $logDir 'cloudflared.log'
    Remove-Item $tunnelLog -ErrorAction SilentlyContinue
    $tunnelProcess = Start-Process -FilePath $cloudflared -WindowStyle Hidden -PassThru `
        -ArgumentList 'tunnel', '--no-autoupdate', '--url', 'http://localhost:5080', '--logfile', "`"$tunnelLog`""

    $tunnelUrl = $null
    for ($i = 0; $i -lt 30 -and -not $tunnelUrl; $i++) {
        Start-Sleep -Seconds 1
        if (Test-Path $tunnelLog) {
            $match = Select-String -Path $tunnelLog -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
            if ($match) { $tunnelUrl = $match.Matches[0].Value }
        }
    }
}

try {
    Write-Host ''
    Write-Host 'Aplikacja: https://localhost:7080  (Swagger: https://localhost:7080/swagger)' -ForegroundColor Green
    if ($Tunnel) {
        if ($tunnelUrl) {
            Write-Host "Telefon:   $tunnelUrl" -ForegroundColor Cyan
        } else {
            Write-Host "Tunel startuje - adres pojawi sie w $tunnelLog" -ForegroundColor Yellow
        }
    }
    Write-Host 'Push: user-secrets Push:Enabled=true + Push:Fcm:CredentialsPath (patrz CLAUDE.md).' -ForegroundColor Green
    Write-Host 'Zatrzymanie: Ctrl+C' -ForegroundColor Green
    Write-Host ''
    dotnet run --project (Join-Path $root 'ICanHelp.Api') --launch-profile https
} finally {
    # Ctrl+C / koniec API – tunel też (inaczej zostałby w tle).
    if ($tunnelProcess -and -not $tunnelProcess.HasExited) { Stop-Process -Id $tunnelProcess.Id -Force }
}
