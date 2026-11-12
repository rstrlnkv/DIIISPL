# Architecture

A short map for people who read or change the code. The detailed text (why each piece works as
it does, build, installer, signing, release) is in [INTERNALS.md](INTERNALS.md).

## Bird's-eye view

DIIISPL is a tray application for Windows 11 that stores monitor layouts as profiles and
switches them with a hotkey.

- It is written in C# 5 and compiled by `csc.exe` of .NET Framework from `scripts\build.cmd`. There is
  no NuGet and no project file.
- It does not ask Windows to restore a saved configuration. It builds the CCD path and mode
  arrays (`DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`) itself and passes them to
  `SetDisplayConfig`.
- It identifies monitors by EDID, not by the number of the graphics-card output.
- Profiles are files in `%AppData%\DIIISPL`.
- The Dev channel also carries a native C++ library, `DIIISPL.ExplorerTAP.dll`. It is loaded
  into `explorer.exe` and publishes a COM service that styles the taskbar.

## Where things live

Core (sources are in `src/`):

- `src/Native.cs` WinAPI declarations; `src/Edid.cs` EDID parsing; `src/Monitors.cs` monitor
  identification and modes; `src/Layout.cs` the profile.
- `src/Applier.cs` builds and applies the configuration, then HDR; `src/AutoSwitch.cs` picks a
  profile by the set of connected monitors.
- `src/Store.cs` files; `src/Hotkey.cs` key combinations; `src/App.cs` tray, autostart, watching the set
  of monitors; `src/Cli.cs` the command line.

Taskbar (Dev channel only):

- `src/TaskbarSettings.cs`, `src/TaskbarWatcher.cs`, `src/TaskbarWindow.cs`, `src/ExplorerTap.cs`.

UI (WPF windows; the tray icon uses Windows Forms):

- `src/Theme.cs`, `src/Fluent.cs`, `src/Ui.cs`, `src/MonitorCanvas.cs`, `src/MainWindow.cs`, `src/ConfirmDialog.cs`, `src/ChangelogWindow.cs`,
  `src/AboutWindow.cs`.

Folders:

- `native/` the ExplorerTAP library (C++, built by `native\build-native.cmd`).
- `scripts/` `build.cmd`.
- `installer/` the WiX 3 MSI.
- `tests/` the self-test.
- `tools/` the icon generator.
- `.github/workflows/release.yml` the release build.

## Invariants

- If the requested layout already matches the current one, nothing is applied. Every
  `SetDisplayConfig` call renegotiates the output and the screen flickers.
- The request is applied strictly first, then relaxed step by step, and the result is
  verified against the request.
- The primary monitor sits at (0,0).
- A duplicated output gets a different target id from the driver, so a duplicate is first
  built by Windows and the exact layout is applied over it.
- When the profile changes, the link to the taskbar service and what was applied to the
  taskbars are reset, because the taskbars are recreated.
- Taskbar windows are found with `FindWindowEx` by class, never with `EnumWindows`.
- The Stable channel has no taskbar code and no DLL; nothing is loaded into `explorer.exe`.
- The release tag decides the version and the channel: `vX.Y.Z` is Stable, `vX.Y.Z-dev.N` is
  Dev.

## Boundaries

- `DIIISPL.ExplorerTAP.dll` runs inside `explorer.exe`; DIIISPL talks to it through the COM
  interface `ITaskbarAppearanceService`. It depends on undocumented Explorer internals.
- `tests\run-tests.cmd` switches real monitors and is written for the author's three-monitor
  setup. It does not run in CI.
- The signing certificate and its password are never in the repository (only on the key
  owner's machine and in the CI secrets).
- The maintainers' private working notes are kept outside this repository.

## Build, installer, signing, release

See [INTERNALS.md](INTERNALS.md).
