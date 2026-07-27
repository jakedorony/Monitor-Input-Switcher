# Monitor Input Switcher — Setup Guide

A tiny app that lives in your system tray (bottom-right corner, by the clock)
and switches your monitors between two inputs with one click.

This guide has two parts: building the installer (one-time, technical) and
installing/using the app (anyone).

---

## Part 1 — Build the app and installer (one-time, technical)

You do this once to produce a `setup.exe` you can hand to anyone. They never
need any of these tools.

### 1a. Build the program

1. Put these files in one folder (somewhere you own — Desktop, Documents.
   **Not** `C:\Program Files`):
   - `MonitorSwitch.cs`
   - `MonitorSwitch.ico`
   - `build.bat`
2. Double-click **`build.bat`**. It uses the C# compiler already built into
   Windows — no Visual Studio or downloads needed, no admin rights.
3. It should say **"Build succeeded: MonitorSwitch.exe"**. You now have
   `MonitorSwitch.exe` (with the app icon embedded).

### 1b. Build the installer

1. Install **Inno Setup** (free): https://jrsoftware.org/isdl.php
2. Open **`MonitorSwitch.iss`** in Inno Setup and click **Build → Compile**.
   (Or from a command prompt:
   `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" MonitorSwitch.iss`)
3. The finished installer appears in a new **`Output`** folder as
   **`MonitorSwitch-Setup-1.0.0.exe`**. That single file is everything the end
   user needs.

---

## Part 2 — Install the app (for anyone, non-technical)

1. Double-click **`MonitorSwitch-Setup-1.0.0.exe`**.

   > **"Windows protected your PC"?** Windows shows this for any new program it
   > hasn't seen before. Click **More info**, then **Run anyway**. (This goes
   > away if the installer is code-signed — see Part 4.)

2. The installer runs without asking for an administrator password — it installs
   just for you. On the options screen you can tick **"Start automatically when
   I sign in"** (recommended) and optionally a desktop shortcut.
3. Click through to **Install**, then **Finish**. You can let it launch right
   away.

A small **monitor icon** appears in the system tray (bottom-right, near the
clock). Windows often tucks new icons behind the **^** arrow there — drag it
onto the taskbar to keep it visible.

To uninstall later: **Settings → Apps → Installed apps → Monitor Input
Switcher → Uninstall**, like any other program.

---

## Part 3 — Using the app

The app has a built-in guide: right-click the tray icon and choose
**"How to use…"** any time. The short version:

### First, save your two setups (once)

1. Set both monitors to the inputs you want for your **first** setup (use the
   monitors' own buttons if needed).
2. **Right-click** the tray icon → **"Save current setup as Profile A…"**.
3. Type a name you'll recognise (e.g. *Work PC*) and click **Save**.
4. Switch both monitors to your **second** setup.
5. Right-click again → **"Save current setup as Profile B…"**, name it
   (e.g. *Personal PC*), and **Save**.

Your two named buttons are now ready and are remembered across restarts.

### Day to day

- **Right-click** the tray icon → click either named button to switch.
- **Double-click** the tray icon → toggles between your two setups.
- A small popup confirms each switch.

---

## Part 4 — Optional polish (technical)

- **Code signing.** The installer and exe are unsigned, so first-run shows a
  SmartScreen warning. For wider distribution, sign both with an Authenticode
  certificate to remove it. Overkill for home use.
- **Versioning.** To release an update, bump the version in two places —
  `AssemblyVersion`/`AssemblyFileVersion` in `MonitorSwitch.cs` and
  `MyAppVersion` in `MonitorSwitch.iss` — then rebuild both. Installing over an
  existing copy upgrades it in place.

---

## Troubleshooting

**The icon isn't in the tray.** Click the **^** arrow near the clock — Windows
hides new icons there. Drag it onto the taskbar to keep it visible.

**A button switches a monitor to the wrong input (or a popup says a monitor
"failed").** That monitor's DDC/CI setting is probably off. Open the monitor's
menu with its physical buttons, find **DDC/CI** (often under "Other" or
"System"), turn it **On**, then re-save the profile.

**I switched my monitors away from this PC and can't switch back from here.**
Expected — this app only controls monitors while they're showing THIS computer.
If you have two PCs, install it on both.

---

## Good to know

- **No admin rights** are needed to install, run, or use the app.
- Your two setups are saved per-user at
  **`%APPDATA%\MonitorSwitch\config.txt`**. The app manages this file; you can
  leave it alone. It is intentionally left in place if you uninstall — delete
  that folder by hand if you want a fully clean removal.
- To make the app stop starting at sign-in without uninstalling: re-run the
  installer and untick the startup option, or remove the "MonitorSwitch" entry
  under Task Manager → Startup apps.
