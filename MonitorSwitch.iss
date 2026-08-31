; MonitorSwitch.iss - Inno Setup script for Monitor Input Switcher
; ---------------------------------------------------------------------------
; Builds a per-user installer (no administrator rights required). Installs into
; %LOCALAPPDATA%\Programs\MonitorSwitch, adds a Start Menu entry, and optionally
; starts the app at sign-in.
;
; HOW TO BUILD THE INSTALLER:
;   1. Build publish\MonitorSwitch.exe first (run build.bat).
;   2. Install Inno Setup (free): https://jrsoftware.org/isdl.php
;   3. Open this file in Inno Setup and click Build > Compile
;      (or run:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" MonitorSwitch.iss)
;   4. The finished installer appears in the .\Output folder as
;      MonitorSwitch-Setup-<version>.exe
; ---------------------------------------------------------------------------

#define MyAppName "Monitor Input Switcher"
#define MyAppVersion "2.6.0"
#define MyAppExeName "MonitorSwitch.exe"
#define MyAppPublisher "Monitor Input Switcher"

[Setup]
; A unique identity for this app (used for upgrades/uninstall). Do not reuse.
AppId={{7E04BDB0-0970-4FC3-B0F2-EF204F09A3C3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}

; Must match the MutexName constant in MonitorSwitch.cs. Lets Setup and the
; uninstaller detect a running copy and prompt the user to close it first -
; important because a running .exe can't be overwritten during an upgrade.
AppMutex=MonitorInputSwitcher_SingleInstance_7E04BDB0

; Per-user install: no UAC prompt, lands in %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes

; Output installer naming and appearance.
OutputDir=Output
OutputBaseFilename=MonitorSwitch-Setup-{#MyAppVersion}
SetupIconFile=MonitorSwitch.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

; Require Windows 10 or newer (DDC/CI API is present on all supported Windows,
; but this keeps it sensible).
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; \
    Description: "Start {#MyAppName} automatically when I sign in"; \
    GroupDescription: "Startup:"
Name: "desktopicon"; \
    Description: "Create a desktop shortcut"; \
    GroupDescription: "Additional shortcuts:"; \
    Flags: unchecked

[Files]
Source: "publish\MonitorSwitch.exe"; DestDir: "{app}"; Flags: ignoreversion
; Optional supporting files - included only if present next to the script.
Source: "SETUP.md";          DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}";              Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; "Start at sign-in" - HKCU Run entry, removed cleanly on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "MonitorSwitch"; \
    ValueData: """{app}\{#MyAppExeName}"""; \
    Tasks: startupicon; Flags: uninsdeletevalue

[Run]
; Offer to launch right after install (won't block the wizard finishing).
Filename: "{app}\{#MyAppExeName}"; \
    Description: "Launch {#MyAppName} now"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; AppMutex (above) already prompts to close a running copy before uninstall.
; This is a belt-and-suspenders auto-close in case a stray process lingers;
; it harmlessly does nothing if the app isn't running.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; \
    Flags: runhidden; RunOnceId: "StopMonitorSwitch"

; Note: per-user settings at %APPDATA%\MonitorSwitch (config.json and the
; encrypted sync sign-in auth.dat) are intentionally left in place on uninstall
; (treated as user data). Delete that folder manually for a clean removal.
