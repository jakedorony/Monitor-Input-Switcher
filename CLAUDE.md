# Monitor Input Switcher

Windows system-tray app that switches monitor inputs via DDC/CI (VCP code
0x60), with optional cross-machine profile sync backed by Supabase. C# /
WinForms on .NET 8 (`net8.0-windows`), BCL only — deliberately zero NuGet
package references. Distributed as a self-contained single-file exe via an
Inno Setup per-user installer. Built for a non-technical end user.

## Current status

- Version 2.3.0 in both `MonitorSwitch.csproj` and `MonitorSwitch.iss`
  (`MyAppVersion`). v2.0 = the .NET 8 port + per-machine sync; v2.1 = shared
  profiles matched by monitor hardware id; v2.2 = publishing prep (real
  icon, update checker, crash log, release workflow, LICENSE/README);
  v2.2.1 = leftover-pairing fix for monitors whose PnP id differs per PC;
  v2.3.0 = learned id aliases + "Clear learned matches", state-based toggle.
  The single-file C#5 original lives in git history; the user runs an installed copy at
  `%LOCALAPPDATA%\Programs\Monitor Input Switcher` (v2.0.0 as of 2026-07-27;
  installers upgrade it in place via the shared AppId).
- Builds clean with `dotnet build`.

## Build

```
build.bat        # dotnet publish -> publish\MonitorSwitch.exe (self-contained single file)
```
The .NET 8 SDK on this dev machine is a per-user install at
`%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` (not on PATH; build.bat handles
both). `nuget.config` exists only so restore can pull runtime packs for
self-contained publishing — do not add package references.

Installer: `ISCC.exe MonitorSwitch.iss` (Inno Setup 6) after build.bat.
Output lands in `Output\MonitorSwitch-Setup-<ver>.exe`.

CI: `.github/workflows/build.yml` publishes the exe as an artifact on push.
Releases: push a tag `vX.Y.Z` (matching the two version fields!) and
`.github/workflows/release.yml` builds and publishes a GitHub Release with
the installer + exe. winget manifests live in `winget/` — update the version,
URL, and sha256 per release before submitting to microsoft/winget-pkgs.

## Hard constraints — do not violate

1. **No admin rights anywhere.** Per-user install (`PrivilegesRequired=lowest`),
   HKCU-only registry, config in `%APPDATA%`. Nothing may require elevation.
2. **No NuGet dependencies.** BCL only (`HttpClient`, `System.Text.Json`,
   WinForms). DPAPI is P/Invoked (crypt32) precisely to avoid the
   ProtectedData package. If a change needs a library, push back.
3. **DLL search-path hardening stays.** All non-KnownDLL P/Invokes carry
   `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]` (dxva2.dll
   especially; this blocks planting attacks). Any new P/Invoke to a
   non-KnownDLL must get the same attribute.
4. **Everything that arrives from config.json or the cloud goes through
   `Limits.Apply`** (src/Profile.cs): name ≤ 80 chars, ids `[A-Za-z0-9#_-]`
   ≤ 32, ≤ 16 entries, ≤ 16 aliases, input values ≤ 255. `MonitorNames`
   refuses anything `Limits.Id` rejects before touching the registry. A new
   ingest path must call it too.
5. **Supabase grants are minimal.** `anon` has no table privileges at all;
   `authenticated` has only SELECT/INSERT/UPDATE/DELETE (RLS does not cover
   TRUNCATE). Default privileges for new public tables revoke `anon`.
6. **CI actions are pinned to commit SHAs** with the tag in a comment; bump
   deliberately, never back to a floating `@v4`. build.yml is read-only.
7. **Sync must stay optional.** Every DDC/profile feature works signed-out and
   offline; network failures degrade to warning balloons, never dialogs or
   blocked UI.

## Coupling points (change together or break things)

- **Mutex name** `MonitorInputSwitcher_SingleInstance_7E04BDB0` — `MutexName`
  in src/Program.cs AND `AppMutex` in MonitorSwitch.iss. Must match exactly.
- **Startup registry value** `MonitorSwitch` under
  `HKCU\...\CurrentVersion\Run` — written by the installer's `startupicon`
  task AND the app's checkbox (`RunValueName` in src/TrayApp.cs).
- **Version** — bump in two places: `Version`/`AssemblyVersion`/`FileVersion`
  in MonitorSwitch.csproj, `MyAppVersion` in MonitorSwitch.iss.
- **Installer AppId GUID** `{7E04BDB0-0970-4FC3-B0F2-EF204F09A3C3}` — never
  change; it's the upgrade/uninstall identity.
- **Supabase project** `monitor-switch` (ref `cvnpmmmkzphhgmimfrpi`, org
  "Noodle House Incorporated") — `SupabaseUrl`/`ApiKey` constants in
  src/SyncClient.cs. The publishable key is safe to embed; **RLS is the
  security boundary** — every policy on `public.shared_profiles` requires
  `user_id = auth.uid()`. Schema changes go through Supabase migrations AND
  the wire DTOs in SyncClient.cs together. `shared_profiles` is the only
  table (the v2.0 per-machine `profiles` table was dropped 2026-08-22).
  Auth settings (dashboard): email confirmation ON, minimum password length
  8 with complexity. **Do not enable CAPTCHA on Supabase Auth** — the
  WinForms client has no browser surface to render hCaptcha/Turnstile, so
  it would break sign-up and sign-in; use rate limits for abuse control.

## Architecture (src/)

- `Native.cs` — P/Invoke: dxva2 DDC/CI (+ capabilities string), user32 (incl.
  GetMonitorInfoW / EnumDisplayDevicesW for monitor identity), crypt32
  DPAPI, dwmapi (dark title bar).
- `Ddc.cs` — enumeration + VCP 0x60. Each `PhysMon` gets an `Id` = PnP
  hardware id (e.g. "DEL40A8"; duplicates get "#2", "#3"). `Plan` does the
  three-tier match (id / legacy positional / leftover pairing — see Sync
  model) and `ApplyProfile` returns an `ApplyOutcome` counting applied,
  failed and unmatched monitors. `CaptureCurrent` (all-or-null, keyed by id),
  `ReadInputs`, `SupportedInputs`/`CachedInputs` (per-monitor 0x60 list from
  the capabilities string, cached; `FallbackInputs` otherwise), `SetInput`,
  `DetectInputs`, `UpgradeLegacyEntries` (fills ids into positional data).
- `Profile.cs` — Name + `Inputs` (List<InputSetting>{MonitorId, Value};
  MonitorId null = legacy positional) + `UpdatedAtUtc` (MinValue = untouched
  built-in default, never pushed to cloud). Empty Inputs = "not set"; see
  `TrayApp.IsSet`.
- `ConfigStore.cs` — `%APPDATA%\MonitorSwitch\config.json`; auto-migrates
  v1.x `config.txt` AND the v2.0 positional-`Values` JSON (empty entry list
  stays valid = deleted slot, or deleted profiles resurrect).
- `AuthStore.cs` — email + refresh token in `auth.dat`, DPAPI-encrypted
  (CurrentUser).
- `SyncClient.cs` — raw Supabase REST (GoTrue password/refresh grants,
  PostgREST upsert with `Prefer: resolution=merge-duplicates`). Throws
  `SyncException` with user-showable messages.
- `TrayApp.cs` — tray menu, profile actions, startup checkbox plumbing, and
  sync orchestration: per-slot last-write-wins with 1s slack (`MergeSlot`),
  push-after-save fire-and-forget, silent restore+sync at startup, legacy
  positional→id upgrade on launch.
- `MainWindow.cs` — modeless singleton themed window (`MainForm`): header
  (sync pill, theme pill, gear), the `SegmentedSwitch` hero, one tile per
  connected monitor with a live `InputPicker` (switches that monitor now),
  two profile cards with per-monitor pickers (`TrayApp.UpdateProfileInput`),
  footer (startup toggle, account, Advanced). Rebuilt wholesale on
  `ProfilesChanged`/`SyncStateChanged`/`Theme.Changed` and on activation;
  scales by `DeviceDpi` itself (`AutoScaleMode.None`, every size via `L()`).
- `SettingsWindow.cs` — account/sync sign-in, appearance (System/Light/Dark),
  startup, monitor matching (Clear learned matches), help/version.
- `Controls.cs` — owner-drawn `Card`, `FlatButton`, `LinkAction`,
  `SegmentedSwitch`, `ToggleSwitch`, `ThemePill`, `GlyphButton`,
  `StatusPill`, `InputPicker` (themed dropdown; ComboBox ignores dark
  BackColors). Glyphs come from Segoe MDL2 Assets (`Theme.Glyph*`).
- `Theme.cs` — `Palette.Light/Dark`, `Theme.Mode` (System follows the
  Windows app theme via `AppsUseLightTheme`; live via
  `SystemEvents.UserPreferenceChanged`), `Theme.Changed`, dark title bars via
  `DwmSetWindowAttribute`. Persisted as `"Theme"` in config.json (device-local,
  never synced).
- `MonitorNames.cs` — friendly names ("ASUS PG27AQDM") from the EDID in
  `HKLM\SYSTEM\...\Enum\DISPLAY` (readable without admin). Display only;
  identity is still the PnP id.
- `UpdateCheck.cs` — daily GitHub Releases check (state in
  `update-check.txt`; notifies once per release, click opens the page).
- `HelpWindow.cs`, `Prompt.cs`, `Program.cs` (mutex + WinForms init + crash
  logging to `%APPDATA%\MonitorSwitch\log.txt`).
- App icon: `MonitorSwitch.ico` doubles as `ApplicationIcon` and an
  `EmbeddedResource` with LogicalName `MonitorSwitch.AppIcon` (loaded by
  `TrayApp.AppIcon(size)` — keep the two in sync in the csproj).

## Sync model (decided, don't redesign casually)

Rows in `public.shared_profiles` are keyed `(user_id, slot)` — the two
profiles are SHARED across every signed-in PC. Rationale: the same physical
monitors are plugged into all the user's PCs; Profile A/B mean "send the
monitors to PC A / PC B", so every machine needs both. Inputs are stored per
monitor hardware id (`inputs` jsonb: `[{"monitor":"DEL40A8","value":15}]`),
not per enumeration position, so PCs that enumerate the monitors in
different orders still apply them correctly.

**A monitor's PnP id is not guaranteed stable across PCs.** Verified on real
hardware: one Dell panel reports `DELA07A` on one machine and `DELA07B` on
another (the product code varies with the active input). `Ddc.Plan` therefore
matches in three tiers — exact id, then legacy positional (null-id) entries,
then leftover pairing of any still-unmatched monitor with the next unclaimed
entry. Exact matches always win; pairing only consumes leftovers. Do not
"simplify" this back to id-only matching — that is the v2.1 bug where a
synced profile silently switched only one of two monitors.

**Learned aliases.** A forced pairing (exactly one monitor and one entry
left over, and no positional guesses anywhere in the plan) proves the two ids
are the same panel, so `Ddc.LearnAliases` records the monitor's id as an
alias on that entry (`InputSetting.Aliases`). Aliases match in tier 2, are
persisted (`"Aliases"` in config.json, `"aliases"` in the jsonb row - both
omitted when empty, and older clients ignore them), and sync like any other
profile change. Because an alias is an inference, the main window's "Monitor
matching" group has "Clear learned matches" (`TrayApp.ForgetLearnedMatches`),
which wipes them, bumps `UpdatedAtUtc`, and pushes so other PCs don't restore
them. Keep that escape hatch; it is the only way to undo a wrong deduction.

Connected monitors that end up with no entry at all are counted in
`ApplyOutcome.Unmatched` and surfaced as a warning balloon. Never go back to
silently skipping them: reporting success while leaving a monitor untouched
is what made that bug so hard to see. Identical monitor models still get
"#2"-style suffixes in enumeration order — a swap between two identical
models across PCs remains an accepted ambiguity.

## Behavioral decisions already made (don't regress)

- Deleting a profile resets the slot to "(not set)", persists as an empty
  value list (and syncs that way); the menu button remains and warns if
  clicked.
- Double-click on tray icon opens the main window (NOT toggle).
- `ToggleProfiles` is STATE-based: `PickToggleTarget` reads the live inputs,
  counts how many monitors already sit on each profile (`Ddc.CountOnProfile`,
  same matching as apply) and goes to the one they're NOT on. The last
  applied slot (`lastSlot`, a char - never a Profile reference, sync replaces
  those objects) is only a tie-break. The old memory-only toggle defaulted to
  Profile A after every restart/sync, which looked like "toggle only switches
  one monitor". An unset slot is never the target while the other is saved.
- Friendly input names via `FriendlyInput` (MCCS: 1/2 VGA, 3/4 DVI, 15/16 DP,
  17/18 HDMI; fallback "Input N").
- Uninstall leaves `%APPDATA%\MonitorSwitch` in place (user data).
- Monitor identity is the PnP hardware id since v2.1; enumeration position
  is only a fallback for un-upgraded legacy data.
- Supabase email confirmation is ON (project default): Create account →
  confirmation email → Sign in. The app's copy accounts for this.

## Testing notes

- DDC/CI needs real monitors; no mock layer. Smoke-test: build, run, tray
  icon appears, window opens, Detect reads plausible values.
- Sync: rows are visible via Supabase SQL; simulate "another PC edited a
  profile" by updating a row's name/inputs + `updated_at = now()` in SQL and
  re-syncing. Timestamps within 1s count as in-sync by design.
- Balloon tips may be suppressed by Focus Assist — not a bug.
- Some monitors report different read vs write values for VCP 0x60 (spec
  allows it) — capture/switch mismatches on exotic hardware are known.
