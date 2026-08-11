@echo off
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0hera-arm.ps1" %*
exit /b %ERRORLEVEL%
