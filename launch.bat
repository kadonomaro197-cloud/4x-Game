@echo off
set LOGFILE=%APPDATA%\Pulsar4X\Pulsar4X\game_log.txt
cd /d "%~dp0"
echo Starting Pulsar4X...
echo Log will be saved to: %LOGFILE%
echo.
dotnet run --project Pulsar4X/Pulsar4X.Client/Pulsar4X.Client.csproj
echo.
echo Game closed. Opening log in Notepad...
start notepad "%LOGFILE%"
