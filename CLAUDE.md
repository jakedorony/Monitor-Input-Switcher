# Monitor Input Switcher

Windows system-tray app that switches monitor inputs via DDC/CI (VCP code
0x60). Single C# source file, WinForms, .NET Framework 4.x, compiled with the
in-box `csc.exe` — deliberately zero external dependencies. Distributed via an
Inno Setup per-user installer. Built for a non-technical end user.

## Current status

- Version 1.1.0 in both `MonitorSwitch.cs` (assembly attrs) and
  `MonitorSwitch.iss` (`MyAppVersion`).
- **The code has NEVER been compiled.** It was written in an environment with
  no C# compiler. Structural checks (brace balance, C# 5 syntax scan) passed,
  but expect first-build errors. Priority task: build it, fix what falls out.
- The Inno script has likewise never been compiled.

## Build

```
build.bat        # uses %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```
Produces `MonitorSwitch.exe` with the icon embedded (`/win32icon`), no console
window (`/target:winexe`).

Installer: open `MonitorSwitch.iss` in Inno Setup 6 and compile, or
`ISCC.exe MonitorSwitch.iss`. Output lands in `Output\MonitorSwitch-Setup-<ver>.exe`.

## Hard constraints — do not violate

1. **C# 5 syntax only.** The in-box csc (v4.0.30319) predates C# 6. No string
   interpolation (`$""`), no null-conditional (`?.`), no expression-bodied
   members, no `nameof`, no out-var declarations in calls. Anonymous
   `delegate { }` syntax is used throughout; keep that style.
2. **No admin rights anywhere.** Per-user install (`PrivilegesRequired=lowest`),
   HKCU-only registry, config in `%APPDATA%`. Nothing may require elevation.
3. **No external dependencies.** No NuGet, no bundled DLLs. If a change needs a
   library, push back.
4. **DLL search-path hardening stays.** All `dxva2.dll` P/Invokes carry
   `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]` (dxva2 is not
   a KnownDLL; this blocks planting attacks). Any new P/Invoke to a non-KnownDLL
   must get the same attribute. `user32.dll` is a KnownDLL and doesn't need it.

## Coupling points (change together or break things)

- **Mutex name** `MonitorInputSwitcher_SingleInstance_7E04BDB0` — defined as
  `MutexName` in MonitorSwitch.cs AND as `AppMutex` in MonitorSwitch.iss. Must
  match exactly (installer uses it to detect a running copy before upgrades).
- **Startup registry value** `MonitorSwitch` under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` — written by the
  installer's `startupicon` task AND by the app's in-window checkbox
  (`RunValueName` const). Must stay the same name so they control one setting.
- **Version** — bump in two places for every release: `AssemblyVersion` /
  `AssemblyFileVersion` in the .cs, `MyAppVersion` in the .iss.
- **Installer AppId GUID** `{7E04BDB0-0970-4FC3-B0F2-EF204F09A3C3}` — never
  change; it's the upgrade/uninstall identity.

## Architecture (single file, MonitorSwitch.cs)

- `Native` — P/Invoke (dxva2.dll DDC/CI + user32 enum/DestroyIcon).
- `Ddc` — monitor enumeration and VCP 0x60 read/write.
  `ApplyProfile` (write), `CaptureCurrent` (all-or-null read, used for saving
  profiles), `ReadInputs` (per-monitor read with -1 sentinel, used for live
  status), `DetectInputs` (formatted string for balloons).
- `Profile` — Name + List<uint> Values (index = monitor enumeration order).
  Empty Values list = "not set" (deleted/fresh slot); see `IsSet`.
- `Program` — tray icon + context menu; main window (`ShowMainWindow`,
  modeless, singleton via `mainWindow` field); capture/delete/apply flows;
  config persistence at `%APPDATA%\MonitorSwitch\config.txt`
  (format: `ProfileA = Name | v0, v1, ...`; empty value list is valid and means
  unset — LoadConfig must keep accepting that or deleted profiles resurrect);
  startup checkbox (HKCU Run); embedded help text (`ShowHelp`); single-instance
  mutex in `Main`, app body in `RunApp`.

## Behavioral decisions already made (don't regress)

- Deleting a profile resets the slot to "(not set)" and persists as an empty
  value list; the menu button remains and warns if clicked.
- Double-click on tray icon opens the main window (NOT toggle — toggle lives in
  the tray menu item and window button; `ToggleProfiles` prefers a saved slot
  if the natural target is empty).
- Friendly input names via `FriendlyInput` (MCCS standard values: 1/2 VGA,
  3/4 DVI, 15/16 DP, 17/18 HDMI; fallback "Input N"). Used in window, dialogs,
  balloons.
- Uninstall leaves `%APPDATA%\MonitorSwitch` in place (user data).
- Monitor identity is enumeration order (index), which can shift after driver/
  topology changes — known limitation, documented in SETUP.md; don't try to
  "fix" silently.

## Known-risk areas for first compile

- `ShowMainWindow` (largest recent addition) — layout code, closures in the
  per-profile button loop (loop-body locals `slot`/`isA` are intentional for
  correct capture).
- `Main`/`RunApp` split with the mutex try/finally.
- The help text string concatenation blocks (easy place for a stray quote).

## Testing notes

- DDC/CI behavior needs real monitors; there is no mock layer. Smoke-test:
  build, run, tray icon appears, window opens, Detect reads plausible values.
- Balloon tips may be suppressed if Windows Focus Assist is on — not a bug.
- Some monitors report different read vs write values for VCP 0x60 (spec
  allows it); capture-then-switch mismatches on exotic hardware are a known
  possibility, not a code bug.
