<#
.SYNOPSIS
  Sprawdzenie całego repo po kroku: build i testy backendu, regeneracja klienta API (NSwag), lint/test/build frontendu.

.DESCRIPTION
  Jedno polecenie zamiast kilku ręcznych. Wynik trafia do .verify\report.md (pełne logi: .verify\*.log) –
  asystent AI czyta ten plik przez połączony folder, więc po uruchomieniu wystarczy napisać „sprawdź raport”.
  Wymaga: .NET 9 SDK, Node 22, Docker Desktop (testy integracyjne i baza dla API przy `npm run api`).

.EXAMPLE
  .\verify.cmd                 # wszystko
  .\verify.cmd -Quick          # bez testów integracyjnych i bez regeneracji klienta API (szybko, bez Dockera)
  .\verify.cmd -SkipFrontend   # tylko backend
#>
[CmdletBinding()]
param(
  [switch]$Quick,
  [switch]$SkipBackend,
  [switch]$SkipFrontend,
  [switch]$SkipIntegration,
  [switch]$SkipApiClient
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root 'ICanHelp.Web'
$out = Join-Path $root '.verify'
$apiUrl = 'http://localhost:5080'
$clientFile = Join-Path $web 'src\app\core\api\api-client.ts'

if ($Quick) { $SkipIntegration = $true; $SkipApiClient = $true }
if ($SkipBackend) { $SkipIntegration = $true; $SkipApiClient = $true }

New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem $out -Filter '*.log' -ErrorAction SilentlyContinue | Remove-Item -Force

$results = New-Object System.Collections.Generic.List[object]
$notes = New-Object System.Collections.Generic.List[string]
$started = Get-Date

function Add-Result([string]$Name, [string]$Status, [string]$Log, [TimeSpan]$Duration, [string]$Info = '') {
  $results.Add([pscustomobject]@{ Name = $Name; Status = $Status; Log = $Log; Seconds = [int]$Duration.TotalSeconds; Info = $Info })
  $color = switch ($Status) { 'OK' { 'Green' } 'POMINIĘTE' { 'Yellow' } default { 'Red' } }
  Write-Host ("[{0}] {1} ({2} s) {3}" -f $Status, $Name, [int]$Duration.TotalSeconds, $Info) -ForegroundColor $color
}

# Polecenie przez cmd.exe: stdout i stderr do jednego logu, bez problemów PowerShella 5.1 ze stderr programów.
function Invoke-Step([string]$Name, [string]$WorkDir, [string]$Command) {
  $log = Join-Path $out (($Name -replace '[^\w\-]+', '-').ToLower() + '.log')
  Write-Host "→ $Name" -ForegroundColor Cyan
  $sw = [Diagnostics.Stopwatch]::StartNew()
  Push-Location $WorkDir
  try {
    cmd /c "$Command > `"$log`" 2>&1"
    $code = $LASTEXITCODE
  } finally {
    Pop-Location
  }
  $sw.Stop()
  $status = if ($code -eq 0) { 'OK' } else { 'BŁĄD' }
  Add-Result $Name $status $log $sw.Elapsed
  return ($code -eq 0)
}

function Skip-Step([string]$Name, [string]$Reason) {
  Add-Result $Name 'POMINIĘTE' '' ([TimeSpan]::Zero) $Reason
}

function Test-Docker {
  cmd /c "docker info >nul 2>&1"
  return ($LASTEXITCODE -eq 0)
}

function Test-ApiUp {
  try {
    $response = Invoke-WebRequest "$apiUrl/health" -UseBasicParsing -TimeoutSec 2
    return ($response.StatusCode -eq 200)
  } catch {
    return $false
  }
}

# ---------------- backend ----------------
$backendBuilt = $false
if ($SkipBackend) {
  Skip-Step 'dotnet build' 'parametr -SkipBackend'
} else {
  $backendBuilt = Invoke-Step 'dotnet build' $root 'dotnet build ICanHelpApp.sln -nologo -v q'
  if ($backendBuilt) {
    Invoke-Step 'dotnet test (jednostkowe)' $root 'dotnet test ICanHelpApp.sln --no-build -nologo --filter "Category!=Integration"' | Out-Null
  } else {
    Skip-Step 'dotnet test (jednostkowe)' 'build się nie udał'
  }
}

$docker = $false
if (-not $SkipIntegration -or -not $SkipApiClient) {
  $docker = Test-Docker
  if (-not $docker) { $notes.Add('Docker nie działa – uruchom Docker Desktop, żeby przeszły testy integracyjne i `npm run api`.') }
}

if ($SkipIntegration) {
  Skip-Step 'dotnet test (integracyjne)' 'parametr -Quick/-SkipIntegration/-SkipBackend'
} elseif (-not $backendBuilt) {
  Skip-Step 'dotnet test (integracyjne)' 'build się nie udał'
} elseif (-not $docker) {
  Skip-Step 'dotnet test (integracyjne)' 'Docker nie działa'
} else {
  Invoke-Step 'dotnet test (integracyjne)' $root 'dotnet test ICanHelpApp.sln --no-build -nologo --filter "Category=Integration"' | Out-Null
}

# ---------------- klient API (NSwag) ----------------
if ($SkipApiClient) {
  Skip-Step 'npm run api' 'parametr -Quick/-SkipApiClient/-SkipBackend'
} elseif (-not $backendBuilt) {
  Skip-Step 'npm run api' 'build się nie udał'
} elseif (-not $docker) {
  Skip-Step 'npm run api' 'Docker nie działa (API potrzebuje bazy)'
} elseif (Test-ApiUp) {
  # Mogłoby to być stare API (np. z Visual Studio) – klient zostałby wygenerowany ze starej wersji.
  Add-Result 'npm run api' 'BŁĄD' '' ([TimeSpan]::Zero) "Na $apiUrl już działa API – zatrzymaj je (Visual Studio / inny terminal) i uruchom skrypt ponownie."
} else {
  Invoke-Step 'docker compose up' $root 'docker compose up -d --wait' | Out-Null
  $apiLog = Join-Path $out 'api-server.log'
  $apiErr = Join-Path $out 'api-server-stderr.log'
  $api = Start-Process -FilePath 'dotnet' -ArgumentList 'run', '--project', 'ICanHelp.Api', '--no-build', '--launch-profile', 'http' `
    -WorkingDirectory $root -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -NoNewWindow -PassThru
  try {
    $deadline = (Get-Date).AddSeconds(120)
    while (-not (Test-ApiUp) -and -not $api.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
    if (-not (Test-ApiUp)) {
      Add-Result 'npm run api' 'BŁĄD' $apiLog ([TimeSpan]::Zero) 'API nie wystartowało w 2 min – zobacz api-server.log'
    } else {
      $before = (Get-FileHash $clientFile).Hash
      if (Invoke-Step 'npm run api' $web 'npm run api') {
        $after = (Get-FileHash $clientFile).Hash
        $info = if ($before -eq $after) { 'klient bez zmian' } else { 'klient ZMIENIONY – zobacz git diff api-client.ts' }
        $results[$results.Count - 1].Info = $info
        if ($before -ne $after) { $notes.Add('`npm run api` zmienił api-client.ts – to normalne po zmianie API; jeśli plik był poprawiany ręcznie, sprawdź różnice.') }
      }
    }
  } finally {
    if (-not $api.HasExited) { cmd /c "taskkill /PID $($api.Id) /T /F >nul 2>&1" }
  }
}

# ---------------- frontend ----------------
if ($SkipFrontend) {
  Skip-Step 'frontend' 'parametr -SkipFrontend'
} else {
  $nodeModules = Join-Path $web 'node_modules\.package-lock.json'
  $lock = Join-Path $web 'package-lock.json'
  $needInstall = -not (Test-Path $nodeModules) -or ((Get-Item $lock).LastWriteTime -gt (Get-Item $nodeModules).LastWriteTime)
  $installed = $true
  if ($needInstall) { $installed = Invoke-Step 'npm ci' $web 'npm ci' }
  if ($installed) {
    Invoke-Step 'npm run lint' $web 'npm run lint' | Out-Null
    Invoke-Step 'npm test' $web 'npm test' | Out-Null
    Invoke-Step 'npm run build' $web 'npm run build' | Out-Null
  } else {
    Skip-Step 'npm run lint / test / build' 'npm ci się nie udał'
  }
}

# ---------------- raport ----------------
$failed = @($results | Where-Object { $_.Status -eq 'BŁĄD' })
Push-Location $root
$branch = (git rev-parse --abbrev-ref HEAD 2>$null)
$commit = (git log -1 --format='%h %s' 2>$null)
$status = (git status --short 2>$null) -join "`n"
Pop-Location

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine('# Raport weryfikacji')
[void]$md.AppendLine()
[void]$md.AppendLine("- Kiedy: $($started.ToString('yyyy-MM-dd HH:mm')) (trwało $([int]((Get-Date) - $started).TotalSeconds) s)")
[void]$md.AppendLine("- Gałąź: $branch · ostatni commit: $commit")
$verdict = if ($failed.Count -eq 0) { 'WSZYSTKO OK' } else { "BŁĘDY: $($failed.Count)" }
[void]$md.AppendLine("- Wynik: **$verdict**")
[void]$md.AppendLine()
[void]$md.AppendLine('| Krok | Wynik | Czas | Uwagi |')
[void]$md.AppendLine('|---|---|---|---|')
foreach ($r in $results) { [void]$md.AppendLine("| $($r.Name) | $($r.Status) | $($r.Seconds) s | $($r.Info) |") }

# Podsumowania testów (np. „Passed! - Failed: 0, Passed: 186…”) – do wpisu w „Stan prac”.
$summaries = @()
foreach ($r in $results) {
  if ($r.Log -and (Test-Path $r.Log)) {
    $summaries += Select-String -Path $r.Log -Pattern '(Passed!|Failed!) +- Failed:|Tests +\d+ (passed|failed)|Test Files +\d+' |
      ForEach-Object { "- $($r.Name): $($_.Line.Trim())" }
  }
}
if ($summaries.Count -gt 0) {
  [void]$md.AppendLine()
  [void]$md.AppendLine('## Liczby testów')
  $summaries | ForEach-Object { [void]$md.AppendLine($_) }
}

if ($notes.Count -gt 0) {
  [void]$md.AppendLine()
  [void]$md.AppendLine('## Uwagi')
  $notes | ForEach-Object { [void]$md.AppendLine("- $_") }
}

foreach ($r in $failed) {
  [void]$md.AppendLine()
  [void]$md.AppendLine("## Błąd: $($r.Name)")
  if ($r.Info) { [void]$md.AppendLine($r.Info) }
  if ($r.Log -and (Test-Path $r.Log)) {
    $lines = Get-Content $r.Log
    # Najpierw linie z błędami (kompilator, nieudane testy), potem koniec logu.
    $errors = $lines | Select-String -Pattern ' error |error TS|ERROR|FAIL|Failed |✗|×|Assert|Exception' | Select-Object -First 60 | ForEach-Object { $_.Line }
    [void]$md.AppendLine()
    [void]$md.AppendLine("Pełny log: ``.verify\$(Split-Path -Leaf $r.Log)``")
    if ($errors) {
      [void]$md.AppendLine()
      [void]$md.AppendLine('```text')
      $errors | ForEach-Object { [void]$md.AppendLine($_) }
      [void]$md.AppendLine('```')
    }
    [void]$md.AppendLine()
    [void]$md.AppendLine('Koniec logu:')
    [void]$md.AppendLine('```text')
    $lines | Select-Object -Last 40 | ForEach-Object { [void]$md.AppendLine($_) }
    [void]$md.AppendLine('```')
  }
}

[void]$md.AppendLine()
[void]$md.AppendLine('## git status')
[void]$md.AppendLine('```text')
[void]$md.AppendLine($status)
[void]$md.AppendLine('```')

$reportPath = Join-Path $out 'report.md'
[IO.File]::WriteAllText($reportPath, $md.ToString(), (New-Object Text.UTF8Encoding $false))

Write-Host ''
if ($failed.Count -eq 0) {
  Write-Host "WSZYSTKO OK – raport: .verify\report.md" -ForegroundColor Green
  exit 0
}
Write-Host "BŁĘDY: $($failed.Count) – raport: .verify\report.md (napisz asystentowi: „sprawdź raport”)" -ForegroundColor Red
exit 1
