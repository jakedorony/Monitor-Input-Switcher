@echo off
REM Builds MonitorSwitch.exe using the C# compiler that ships with Windows.
REM No Visual Studio or SDK install required. Run from the folder containing
REM MonitorSwitch.cs (double-clicking this file works).

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Could not find csc.exe - .NET Framework 4.x is required.
    pause
    exit /b 1
)

set ICON=
if exist "MonitorSwitch.ico" set ICON=/win32icon:MonitorSwitch.ico

"%CSC%" /nologo /target:winexe /out:MonitorSwitch.exe %ICON% ^
    /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
    MonitorSwitch.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build succeeded: MonitorSwitch.exe
) else (
    echo.
    echo Build FAILED - see errors above.
)
pause
