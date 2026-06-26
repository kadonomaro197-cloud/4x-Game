@echo off
setlocal
rem ============================================================================
rem Pulsar4X launcher (debug-friendly).
rem  - Runs the client.
rem  - Captures ALL console output (stdout + stderr, including any crash stack
rem    trace) to console_output.txt next to this script.
rem  - Keeps this window OPEN after the game exits/crashes so the output is
rem    readable, and opens the capture file in Notepad for easy copy/paste.
rem ============================================================================
rem The game (Program.cs) writes game_log.txt to the repo root (the folder holding .git/launch.bat),
rem NOT %AppData% -- so point this label at the real location: next to this script + console_output.txt.
set "LOGFILE=%~dp0game_log.txt"
set "CONSOLELOG=%~dp0console_output.txt"
cd /d "%~dp0"

echo Starting Pulsar4X...
echo   Game log:        %LOGFILE%
echo   Console capture: %CONSOLELOG%
echo.
echo The game opens in its own window. This console's output -- including any
echo crash stack trace -- is being saved to the console capture file above.
echo.

rem 2>&1 merges the error stream into the capture so a crash trace is saved too.
dotnet run --project Pulsar4X/Pulsar4X.Client/Pulsar4X.Client.csproj > "%CONSOLELOG%" 2>&1
set "EXITCODE=%ERRORLEVEL%"

echo.
echo ============================================================
echo  Game process exited with code %EXITCODE%.
if not "%EXITCODE%"=="0" echo  A non-zero exit code usually means a CRASH -- details are below.
echo ============================================================
echo.
type "%CONSOLELOG%"
echo.
echo ============================================================
echo  Full console output was saved to:
echo    %CONSOLELOG%
echo  If the game crashed, send that file (or copy the text above).
echo ============================================================
echo.

rem Open the captured console output for easy copy/paste.
start notepad "%CONSOLELOG%"

rem Keep this window open until you close it.
echo This window will stay open. Press any key to close it when you are done.
pause >nul
endlocal
