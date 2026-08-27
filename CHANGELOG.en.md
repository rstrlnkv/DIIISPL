# Changelog

Notable changes in DIIISPL, newest first. The badge at the start of a line says
what kind of change it is: **NEW** — added, **FIX** — fixed, **UPD** — reworked.

The same in Russian — [CHANGELOG.md](CHANGELOG.md).

Releases with a `-dev` suffix go out on the Dev channel: they carry the taskbar
settings, which stable builds do not have at all.

## 0.2.3-dev.1 — 2026-08-28

- **UPD** The “What’s new” window has been redesigned.

## 0.2.2-dev.1 — 2026-08-27

- **NEW** The profile follows the monitors: plug a display in and the layout that fits it is applied.
- **NEW** A profile can be kept out of the automatic pick — “Pick automatically” in the “···” menu; a hotkey still applies it.
- **NEW** The whole release history lives inside the program: the “What’s new” button opens it in a window, no browser needed.
- **NEW** The `/auto` command prints which profile matches the monitors now and what the program would do.
- **NEW** “Reset the taskbar look” — a button in the taskbar settings and an item in the tray menu.
- **FIX** The taskbar look no longer gets stuck from a previous layout: the memory of what had been applied was written from two threads without a lock.
- **UPD** The taskbar looks are named for what they do: “Transparent” is now “Colour with transparency”, “Opaque” is “Colour, no transparency”. The difference is not the look’s transparency but whether the colour’s transparency is honoured.
- **UPD** “As usual” in the list of cases is now “No special look”: such a case is not considered at all, and the taskbar takes the look of the next matching case, not the usual one.

## 0.1.4-dev.1 — 2026-08-26

- **FIX** Updates are checked more than once per launch: if GitHub cannot be reached, the program tries again in an hour.
- **UPD** The taskbar button is labelled in words while they fit, and its icon is about the taskbar, not a monitor.
- **NEW** `/help` and `/?` print the help, and the `/state` command is finally listed in it.

## 0.1.3-dev.1 — 2026-08-21

- **FIX** The window opens right away instead of waiting for the taskbar to connect.

## 0.1.1 — 2026-08-21

- **FIX** A downloaded package can no longer be swapped between the signature check and install.
- **FIX** Settings edited in a text editor are picked up while the program is running.
- **FIX** A hand-edited profile is corrected to valid values instead of being rejected by Windows.
- **FIX** In the installer, Back returns where you came from.
- **FIX** The window header no longer pushes the profile name off the edge.
- **FIX** A typo in `/taskbar` no longer answers "applied".
- **UPD** The README is reordered for reading and has a table of contents.

## 0.1.0 — 2026-08-20

First release.

- **NEW** Layout profiles: screen positions, resolution, refresh rate, rotation, primary display.
- **NEW** Monitors are identified by EDID, not by the graphics card output number.
- **NEW** HDR is switched together with the layout.
- **NEW** A profile is switched by a hotkey.
- **NEW** Tray icon and start together with Windows.
- **NEW** Command line: apply, save, delete, list, monitor state and HDR state.
- **NEW** Updates from GitHub with a signature check on the package.
- **NEW** Installed from an `.msi` package.
- **NEW** Two languages — Russian and English; your own is added by a file, without a rebuild.
