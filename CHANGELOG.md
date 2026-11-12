# Changelog

All notable changes to this project are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**English** · [Русский](CHANGELOG.ru.md)

## [Unreleased]

### Removed

- The Copy button on the About screen.

## [0.2.4-dev.1] - 2026-11-13

### Added

- The About screen shows the full version with its -dev.N suffix and has a Copy button for “version (build)”.
- A Licences page in Dev builds, with the licences of the bundled third-party components.

### Changed

- The README is now a short overview, and the user manual has moved to MANUAL.md (MANUAL.ru.md in Russian); the installer also ships MANUAL.md.

### Fixed

- The thin line at the taskbar edge is set for each case separately, not once for all of them.

## [0.2.3-dev.1] - 2026-08-28

### Changed

- The “What’s new” window has been redesigned.

## [0.2.2-dev.1] - 2026-08-27

### Added

- The profile follows the monitors: plug a display in and the layout that fits it is applied.
- A profile can be kept out of the automatic pick — “Pick automatically” in the “···” menu; a hotkey still applies it.
- The whole release history lives inside the program: the “What’s new” button opens it in a window, no browser needed.
- The `/auto` command prints which profile matches the monitors now and what the program would do.
- “Reset the taskbar look” — a button in the taskbar settings and an item in the tray menu.

### Changed

- The taskbar looks are named for what they do: “Transparent” is now “Colour with transparency”, “Opaque” is “Colour, no transparency”. The difference is not the look’s transparency but whether the colour’s transparency is honoured.
- “As usual” in the list of cases is now “No special look”: such a case is not considered at all, and the taskbar takes the look of the next matching case, not the usual one.

### Fixed

- The taskbar look no longer gets stuck from a previous layout: the memory of what had been applied was written from two threads without a lock.

## [0.1.4-dev.1] - 2026-08-26

### Added

- `/help` and `/?` print the help, and the `/state` command is finally listed in it.

### Changed

- The taskbar button is labelled in words while they fit, and its icon is about the taskbar, not a monitor.

### Fixed

- Updates are checked more than once per launch: if GitHub cannot be reached, the program tries again in an hour.

## [0.1.3-dev.1] - 2026-08-21

### Fixed

- The window opens right away instead of waiting for the taskbar to connect.

## [0.1.1] - 2026-08-21

### Changed

- The README is reordered for reading and has a table of contents.

### Fixed

- A downloaded package can no longer be swapped between the signature check and install.
- Settings edited in a text editor are picked up while the program is running.
- A hand-edited profile is corrected to valid values instead of being rejected by Windows.
- In the installer, Back returns where you came from.
- The window header no longer pushes the profile name off the edge.
- A typo in `/taskbar` no longer answers "applied".

## [0.1.0] - 2026-08-20

First release.

### Added

- Layout profiles: screen positions, resolution, refresh rate, rotation, primary display.
- Monitors are identified by EDID, not by the graphics card output number.
- HDR is switched together with the layout.
- A profile is switched by a hotkey.
- Tray icon and start together with Windows.
- Command line: apply, save, delete, list, monitor state and HDR state.
- Updates from GitHub with a signature check on the package.
- Installed from an `.msi` package.
- Two languages — Russian and English; your own is added by a file, without a rebuild.
