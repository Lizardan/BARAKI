@echo off
setlocal
cd /d "%~dp0..\.."
title BARAKI Playtest
echo.
echo  BARAKI evening playtest
echo  Starts: dedicated server + tunnel + matchmaker register + status checks
echo  (quick tunnel if WSS_HOST empty; named tunnel if WSS_HOST set)
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0playtest-evening.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if %EXITCODE% neq 0 (
  echo FAILED with exit code %EXITCODE%
) else (
  echo Playtest stopped.
)
pause
exit /b %EXITCODE%
