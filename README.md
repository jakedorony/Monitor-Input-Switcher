# Monitor Input Switcher

A tiny Windows tray app that switches your monitors between two inputs with
one click — for anyone whose monitors are shared between two computers (a
personal PC and a work laptop, say) and who is tired of digging through
monitor menus with the little joystick button.

- **Two named profiles** ("Personal PC", "Work PC") saved from the tray menu
- **One-click switch** or toggle from the tray; switches every monitor at once
  via DDC/CI
- **Optional sync**: create a free account and your profiles are shared by
  every PC plugged into the same monitors — each computer can send the
  monitors to the other one. Monitors are matched by hardware ID, so it
  doesn't matter which order your PCs list them in
- **No admin rights**, per-user install, works completely offline if you skip
  the account

## Install

Download `MonitorSwitch-Setup-<version>.exe` from the
[latest release](https://github.com/jakedorony/Monitor-Input-Switcher/releases/latest)
and run it. No admin password needed.

> Windows may show a SmartScreen warning because the installer is not
> code-signed yet — click "More info" → "Run anyway".

## Use

1. Set your monitors to the inputs for your first setup, right-click the tray
   icon → *Save current setup as Profile A*.
2. Switch the monitors to the second setup (monitor buttons, one last time),
   save as Profile B.
3. From then on: right-click → pick a profile, or *Switch (toggle A/B)*.

Double-click the tray icon for the main window with live input status, and
the optional sync account. Full guide: right-click → *How to use...*, or see
[SETUP.md](SETUP.md).

## Requirements

- Windows 10/11
- Monitors with DDC/CI enabled (it usually is; look in the monitor's own menu
  under "Other"/"System" if switching fails)

## Building

```
build.bat            # needs the .NET 8 SDK; outputs publish\MonitorSwitch.exe
ISCC MonitorSwitch.iss   # optional: build the installer (Inno Setup 6)
```

No NuGet dependencies — plain BCL, by design. See [CLAUDE.md](CLAUDE.md) for
architecture notes.

## License

[MIT](LICENSE)
