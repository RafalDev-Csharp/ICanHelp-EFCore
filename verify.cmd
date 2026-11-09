@echo off
rem Weryfikacja calego repo (build, testy, klient API, frontend). Raport: .verify\report.md
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\verify.ps1" %*
exit /b %ERRORLEVEL%
