@echo off
setlocal
REM Usage: ApplyUpdate.bat <payloadDir> <installDir> <exeName> <pid>
REM payloadDir = already-extracted staging folder (zip unpacked while game was still open).
set "SRC=%~1"
set "DIR=%~2"
set "EXE=%~3"
set "PID=%~4"

if "%SRC%"=="" exit /b 1
if "%DIR%"=="" exit /b 1
if "%EXE%"=="" exit /b 1

echo Waiting for process %PID% to exit...
:wait
tasklist /FI "PID eq %PID%" 2>NUL | find /I "%PID%" >NUL
if not errorlevel 1 (
  timeout /t 1 /nobreak >NUL
  goto wait
)

echo Applying update...
robocopy "%SRC%" "%DIR%" /E /IS /IT /R:2 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np >NUL
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 exit /b 1

rmdir /s /q "%SRC%" >NUL 2>&1
start "" "%DIR%\%EXE%"
endlocal
exit /b 0
