# DIIISPL

A monitor layout manager for Windows 11: build a layout inside the program, switch profiles with a hotkey, and keep HDR (and, in Dev builds, the taskbar look) with the layout.

![Platform: Windows 11 x64](https://img.shields.io/badge/platform-Windows%2011%20x64-0078d4)
[![Latest release](https://img.shields.io/github/v/release/rstrlnkv/DIIISPL?include_prereleases)](https://github.com/rstrlnkv/DIIISPL/releases)
[![Downloads](https://img.shields.io/github/downloads/rstrlnkv/DIIISPL/total)](https://github.com/rstrlnkv/DIIISPL/releases)
[![Licence: GPL-3.0](https://img.shields.io/badge/licence-GPL--3.0-blue)](LICENSE)

**English** · [Русский](README.ru.md)

## Install

Download the `.msi` package from the [releases](https://github.com/rstrlnkv/DIIISPL/releases). It installs like any program: into `C:\Program Files\DIIISPL`, with an entry in the list of installed programs, where you also uninstall it. The installer closes a running copy by itself. Profiles live separately, in `%AppData%\DIIISPL`, and are kept when you reinstall or uninstall.

Windows will show the publisher as unknown and SmartScreen may warn you: the program is signed with a self-made certificate, and its root is not installed on your machine. The program checks updates against the thumbprint of that same certificate and will not install anyone else's package.

Autostart is switched on in the tray icon menu, "Start with Windows".

A silent install:

```
msiexec /i DIIISPL-<version>-x64.msi INSTALLFOLDER="D:\Programs\DIIISPL" INSTALLSHORTCUT=0 /qb
```

## Features

| Feature | What it does |
|---|---|
| Layout profiles | Stores where each monitor stands, its resolution, refresh rate, rotation, primary and duplicate state, in a profile file |
| EDID identification | Recognises a monitor by manufacturer, model and serial number, so changing an HDMI or DP port does not lose the layout |
| Hotkey and tray icon | Switches a profile with a key or from the tray menu |
| Follow the monitors | When the set of connected screens changes, applies the fitting profile by itself |
| HDR | HDR is part of a profile and is set after the layout is applied |
| Taskbar look (Dev builds) | The taskbar look is part of a profile, one per screen state |
| Command line | Applies, saves, lists and deletes profiles; prints what is connected and how it is identified |
| Languages | English and Russian built in; another language is added with a text file, without rebuilding |
| Updates | Checks GitHub releases, installs a package signed with the same certificate |

How to use every feature: [MANUAL.md](MANUAL.md).

## Requirements

- **System:** Windows 11, x64. The installer is 64-bit and puts the program into `C:\Program Files\DIIISPL`.
- **Permissions:** installing needs administrator rights (the package installs for all users). The program itself runs as an ordinary user and needs none.
- **Network:** only the update check, and only the hosts below; nothing else is sent or received. The check can be left to the program (once a day) or started from the About page.
  - `api.github.com`, HTTPS: the list of releases of `rstrlnkv/DIIISPL`.
  - The installer file of a release, HTTPS, by the link GitHub gives in the release (`github.com`; GitHub redirects the download to its own storage). Only when you press the update button.
  - Links in the About window (the project page, the releases page, the author's Telegram) are opened in your browser by a click; the program does not request them itself.
- **Changes outside its own folders:**
  - `%AppData%\DIIISPL`: profiles, settings, translations, the error log (see "Files" in [MANUAL.md](MANUAL.md#files)).
  - `%TEMP%\DIIISPL-update`: the downloaded installer, deleted by the next update.
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DIIISPL`: autostart, only if you switch it on in the tray menu; uninstalling the program removes this value.
  - `HKLM\Software\DIIISPL`: written by the installer, it remembers the install folder and whether the Start menu shortcut was chosen. The installer also creates the Start menu shortcut unless `INSTALLSHORTCUT=0`.
  - Display configuration: applying a profile changes the layout, resolution, refresh rate, rotation, primary monitor, duplication and HDR state of your monitors (`SetDisplayConfig`).
  - Global hotkeys: each profile hotkey is registered system-wide, so the key no longer reaches other programs while DIIISPL runs.
  - Dev channel only: `DIIISPL.ExplorerTAP.dll` is loaded into `explorer.exe` and styles the taskbar from inside it. The stable channel does not carry this library.

## Build from source

The program is built with one command, `scripts\build.cmd`; it needs the C# compiler of .NET Framework that Windows 11 has by default, and git with a full clone of the repository: the build number is the commit count, so `scripts\build.cmd` refuses to build outside a complete clone (a shallow clone, no git, `GIT_DIR` set). `scripts\build.cmd dev` builds the Dev channel.

How a layout is really applied, why the taskbar has to be styled from inside Explorer, what the installer, the icon and the release are built with, what the automatic test covers and how the repository is arranged are in [INTERNALS.md](INTERNALS.md); a short map of the code is in [ARCHITECTURE.md](ARCHITECTURE.md). Vulnerabilities are reported as described in [SECURITY.md](SECURITY.md).

## Licence

**GNU General Public License v3 or later**: see [LICENSE](LICENSE).

The project was moved from MIT to GPLv3 on purpose: it carries the ExplorerTAP component of [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (© Charles Milette and contributors), distributed under GPLv3, and copyleft requires a derived work to go under the same licence. Details are in [NOTICE.md](NOTICE.md).
