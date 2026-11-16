@echo off
rem Lokalny test PWA i pusha: build frontendu -> ICanHelp.Api\wwwroot -> API na https://localhost:7080 (scripts\serve-local.ps1)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\serve-local.ps1" %*
exit /b %ERRORLEVEL%
