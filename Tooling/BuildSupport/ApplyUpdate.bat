@echo off
setlocal
REM Usage: ApplyUpdate.bat <payloadDir> <installDir> <exeName> <pid>
REM payloadDir = already-extracted staging folder (zip unpacked while game was still open).
set "SRC=%~1"
set "DIR=%~2"
set "EXE=%~3"
set "PID=%~4"
set "LOG=%LOCALAPPDATA%\BARAKI\update-apply.log"

if "%SRC%"=="" exit /b 1
if "%DIR%"=="" exit /b 1
if "%EXE%"=="" exit /b 1

if not exist "%LOCALAPPDATA%\BARAKI" mkdir "%LOCALAPPDATA%\BARAKI" >NUL 2>&1
echo ===== %DATE% %TIME% =====> "%LOG%"
echo SRC=%SRC%>> "%LOG%"
echo DIR=%DIR%>> "%LOG%"
echo EXE=%EXE%>> "%LOG%"
echo PID=%PID%>> "%LOG%"

echo Waiting for process %PID% to exit...
echo Waiting for process %PID% to exit...>> "%LOG%"
:wait
tasklist /FI "PID eq %PID%" 2>NUL | find /I "%PID%" >NUL
if not errorlevel 1 (
  timeout /t 1 /nobreak >NUL
  goto wait
)

echo Applying update...
echo Applying update...>> "%LOG%"
robocopy "%SRC%" "%DIR%" /E /IS /IT /R:2 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np >> "%LOG%" 2>&1
set "RC=%ERRORLEVEL%"
echo robocopy RC=%RC%>> "%LOG%"

if %RC% GEQ 8 (
  echo FAIL robocopy RC=%RC%>> "%LOG%"
  REM Always relaunch so the user is not left with a dead process; old build may remain.
  start "" "%DIR%\%EXE%"
  endlocal
  exit /b 1
)

rmdir /s /q "%SRC%" >NUL 2>&1
if exist "%LOCALAPPDATA%\BARAKI\staging\pending-restart.txt" del /f /q "%LOCALAPPDATA%\BARAKI\staging\pending-restart.txt" >NUL 2>&1
echo OK>> "%LOG%"
start "" "%DIR%\%EXE%"
endlocal
exit /b 0
