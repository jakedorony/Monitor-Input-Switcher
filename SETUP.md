# Monitor Input Switcher — Setup Guide

A tiny app that lives in your system tray (bottom-right corner, by the clock)
and switches your monitors between two inputs with one click. Optionally, your
saved profiles back up to a free online account so they follow you across
computers and reinstalls.

This guide has two parts: building the installer (one-time, technical) and
installing/using the app (anyone).

---

## Part 1 — Build the app and installer (one-time, technical)

You do this once to produce a `setup.exe` you can hand to anyone. They never
need any of these tools.

### 1a. Build the program

1. Install the **.NET 8 SDK** (free, no admin needed with the per-user
   installer): https://dot.net — or `winget install Microsoft.DotNet.SDK.8`.
2. In the project folder, double-click **`build.bat`** (or run
   `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`).
3. It should say **"Build succeeded: publish\MonitorSwitch.exe"**. That exe is
   self-contained — target PCs don't need .NET installed.

### 1b. Build the installer

1. Install **Inno Setup** (free): https://jrsoftware.org/isdl.php
2. Open **`MonitorSwitch.iss`** in Inno Setup and click **Build → Compile**.
   (Or from a command prompt:
   `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" MonitorSwitch.iss`)
3. The finished installer appears in a new **`Output`** folder as
   **`MonitorSwitch-Setup-2.0.0.exe`**. That single file is everything the end
   user needs.

---

## Part 2 — Install the app (for anyone, non-technical)

1. Double-click **`MonitorSwitch-Setup-2.0.0.exe`**.

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

- **Right-click** the tray icon → click either named button to switch, or use
  **"Switch (toggle A/B)"** to flip between the two.
- **Double-click** the tray icon → opens the main window: a big Personal /
  Work switch, each monitor by name with its current input (change it right
  there), and the two profiles with a dropdown per monitor so you can edit a
  profile without touching the monitor buttons. The sun/moon pill switches
  light and dark; the gear opens Settings.
- A small popup confirms each switch.

### Optional: sync across computers

Open the main window (double-click the tray icon), click the gear, and find
**"Account & sync"**:

1. Type an email and password and click **Create account** (one time only).
2. Click the confirmation link in the email you receive, then press
   **Sign in** in the app.
3. Sign in with the same account on your other computers.

Your two profiles are **shared**: save them once on any computer and every
signed-in computer gets them automatically. That's the whole idea — each
computer can send the monitors over to the other one, and the app remembers
which physical monitor gets which input (it identifies monitors by their
hardware ID, so it doesn't matter if your computers list them in a different
order). Everything works fine without an account — syncing is optional, and you can
delete the account (and everything it stored online) from **Settings → Account
& sync → Delete my account** whenever you like.

### Optional: let the dock button switch the monitors

If your dock or KVM switch has a button that moves your keyboard and mouse
between two computers (like the Plugable TBT4-UD5), the app can switch the
monitors at the same press. Open **Settings → Dock button → Set up dock...**,
press the dock's button away and back while the app listens, and save. From
then on, one press moves keyboard, mouse, and monitors together. Each
computer configures this separately.

---

## Part 4 — Optional polish (technical)

- **Code signing.** The installer and exe are unsigned, so first-run shows a
  SmartScreen warning. For wider distribution, sign both with an Authenticode
  certificate to remove it. Overkill for home use.
- **Versioning.** To release an update, bump the version in two places —
  `Version`/`AssemblyVersion`/`FileVersion` in `MonitorSwitch.csproj` and
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
  **`%APPDATA%\MonitorSwitch\config.json`** (upgrading from v1.x migrates the
  old `config.txt` automatically). If you use sync, your sign-in is kept in
  the same folder as `auth.dat`, encrypted so only your Windows account can
  read it. The folder is intentionally left in place if you uninstall —
  delete it by hand if you want a fully clean removal.
- Profiles remember monitors by their **hardware ID**, so driver updates or
  re-ordered monitor lists don't break them. Some monitors report a slightly
  different ID to each computer; the app works out which screens are the same
  and remembers it. If a profile ever sends a monitor to the wrong input
  (typically after moving cables or swapping a monitor), open Settings (the
  gear) and click **Clear learned matches** under *Monitor matching*, then
  switch again — your profiles themselves are untouched.
- To make the app stop starting at sign-in without uninstalling: re-run the
  installer and untick the startup option, or remove the "MonitorSwitch" entry
  under Task Manager → Startup apps.
