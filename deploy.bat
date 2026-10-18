@echo off
rem Builds the Windows Release player and publishes it to C:\Apps\GridGameGaiden.
rem Thin forwarder to tools\deploy.ps1 (the authoritative implementation); pass -Launch to also
rem run the published exe once it's copied. Requires the Unity Editor closed (batchmode build).
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" %*
exit /b %ERRORLEVEL%
