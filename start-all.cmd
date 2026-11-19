@echo off
rem Cala aplikacja jednym poleceniem: baza (Docker) + build frontendu -> API (https://localhost:7080, http://localhost:5080)
rem + tunel cloudflared dla telefonu. Opcje: -NoBuild (bez builda frontendu). Ctrl+C zamyka API i tunel.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\serve-local.ps1" -Tunnel %*
exit /b %ERRORLEVEL%
