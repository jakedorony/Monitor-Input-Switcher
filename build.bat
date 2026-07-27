@echo off
REM Builds a self-contained single-file MonitorSwitch.exe (no .NET runtime
REM needed on the target PC). Requires the .NET 8 SDK - either on PATH or
REM installed per-user at %LOCALAPPDATA%\Microsoft\dotnet.
REM Output: publish\MonitorSwitch.exe

set DOTNET=dotnet
where dotnet >nul 2>nul
if errorlevel 1 set DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe

if not exist "%DOTNET%" (
    where dotnet >nul 2>nul
    if errorlevel 1 (
        echo Could not find the .NET SDK. Install it from https://dot.net
        pause
        exit /b 1
    )
)

"%DOTNET%" publish MonitorSwitch.csproj -c Release -r win-x64 --self-contained ^
    -p:PublishSingleFile=true -o publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build succeeded: publish\MonitorSwitch.exe
) else (
    echo.
    echo Build FAILED - see errors above.
)
pause
