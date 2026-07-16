@echo off
setlocal
REM Usage: ApplyUpdate.bat <zipPath> <installDir> <exeName> <pid>
set "ZIP=%~1"
set "DIR=%~2"
set "EXE=%~3"
set "PID=%~4"

if "%ZIP%"=="" exit /b 1
if "%DIR%"=="" exit /b 1
if "%EXE%"=="" exit /b 1

echo Waiting for process %PID% to exit...
:wait
tasklist /FI "PID eq %PID%" 2>NUL | find /I "%PID%" >NUL
if not errorlevel 1 (
  timeout /t 1 /nobreak >NUL
  goto wait
)

echo Extracting update...
powershell -NoProfile -Command "Expand-Archive -LiteralPath '%ZIP%' -DestinationPath '%DIR%' -Force"
if errorlevel 1 exit /b 1

del /f /q "%ZIP%" >NUL 2>&1
start "" "%DIR%\%EXE%"
endlocal
exit /b 0
