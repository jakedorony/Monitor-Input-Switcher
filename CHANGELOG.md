# Changelog

Notes for each release, written for the people using the app. The release
workflow publishes the matching version's section as the GitHub release
body, and the app shows it in its "What's new" window after an upgrade
(Settings -> Help -> What's new... shows it any time).

## 2.7.0 - 2026-09-25

- **The dock button can move just one screen.** Each dock direction now
  offers "All monitors" or a single monitor of your choosing - so one
  press can send one screen to the other computer while the rest stay
  put. Great for keeping one screen on each machine.
- **Laptop screens no longer get in the way.** Built-in laptop panels are
  now left out of the app entirely - they have no other input to switch
  to, and they used to make "Save current setup" fail on laptops. If a
  real monitor is ever hidden by mistake, turn it off under Settings ->
  Monitor matching.
- **Patch notes in the app.** After an update, a "What's new" window
  shows what changed (this text!). It's also in Settings -> Help.
- **Fixed a crash** that could kill the app on the next click after using
  one of the input dropdowns.
- **Fixed** clicking the text next to "Start with Windows" flipping the
  switch without actually changing the setting.
- **Security hardening**: signing out now also invalidates the session on
  the server (just for that computer), and a few belt-and-suspenders
  fixes behind the scenes.

## 2.6.1 - 2026-09-07

- A visual cleanup: windows now size themselves to their content, the
  white fringes around buttons and dropdowns in dark mode are gone, and
  the Settings sign-in fields match the app's look.

## 2.6.0 - 2026-08-31

- New: dock button support. If your dock or KVM switch has a button that
  moves your keyboard and mouse between two computers (like the Plugable
  TBT4-UD5), the app can switch the monitors at the same press. Set it up
  under Settings -> Dock button. For the smoothest experience install the
  app on both computers.

## 2.5.0 - 2026-08-31

- You can now delete your sync account (and everything it stored online)
  from Settings -> Account & sync. Your local profiles stay on your PC.

## 2.4.0 - 2026-08-22

- A completely redesigned window: a big Personal/Work switch, each
  monitor listed by name with its current input, profile editing without
  touching monitor buttons, and light/dark themes.
- Security hardening around the sync service.

## 2.3.0 - 2026-08-21

- Smarter monitor matching across computers (the app learns when two
  computers call the same screen by different names), plus a "Clear
  learned matches" reset in Settings.
- The Switch toggle now looks at what your monitors are actually showing
  instead of guessing.

## 2.2.1 - 2026-08-21

- Fixed synced profiles only switching one of two monitors on some
  computers.

## 2.2.0 - 2026-07-29

- First public release: installer, winget package, update notifications,
  and a proper icon.
