@echo off
setlocal
cd /d "%~dp0..\.."
title BARAKI Playtest
echo.
echo  BARAKI evening playtest
echo  Starting dedicated server + named tunnel + matchmaker register...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0playtest-evening.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if %EXITCODE% neq 0 (
  echo FAILED with exit code %EXITCODE%
) else (
  echo Tunnel stopped.
)
pause
exit /b %EXITCODE%
