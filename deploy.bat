@echo off
rem Pulls latest, builds Release only if the source changed, publishes to C:\Apps\GridGameGaiden,
rem refreshes the Desktop shortcut, and launches it (tools\deploy.ps1 does the work).
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" -Launch %*
exit /b %ERRORLEVEL%
