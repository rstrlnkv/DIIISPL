# DIIISPL

A monitor layout manager for Windows 11: build a layout inside the program, switch profiles with a hotkey, and keep HDR (and, in Dev builds, the taskbar look) with the layout.

![Platform: Windows 11 x64](https://img.shields.io/badge/platform-Windows%2011%20x64-0078d4) ![Latest release](https://img.shields.io/github/v/release/rstrlnkv/DIIISPL?include_prereleases) ![Downloads](https://img.shields.io/github/downloads/rstrlnkv/DIIISPL/total) ![Licence: GPL-3.0](https://img.shields.io/badge/licence-GPL--3.0-blue)

**English** · [Русский](README.ru.md)

Monitors are identified by EDID, not by the number of the graphics-card output, so a profile keeps working when you move a cable to another port.

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

## How it differs from Windows Display settings

| | Windows | DIIISPL |
|---|---|---|
| Who remembers the layout | the internal display database | a profile file in `%AppData%\DIIISPL` |
| How a monitor is identified | the number of the graphics-card output | EDID: manufacturer, model, serial number |
| An HDMI or DP port changed | the monitor becomes "new", the layout is lost | the profile keeps working |
| Switching | by hand, with the mouse | a hotkey or the tray icon |
| A screen was switched on or unplugged | you repair the layout yourself | the fitting profile is applied by itself |
| HDR | a toggle per screen, switched off when the layout changes | part of the profile |
| Taskbar look | one for the whole system | part of the profile, one per screen state (Dev builds) |

A monitor is identified by a string such as `GSM:LG ULTRAGEAR+:306NTGY9H903`. That is why the program does not get confused where Windows does: the hardware ID of a Samsung TV changes (`SAM713F` or `SAM7140`, depending on the input mode), while the serial number in its EDID stays the same.

## How to use

**Profiles are on the left.** The **+** button creates a profile from the way the monitors stand now; **···** and the right mouse button open the actions: rename, reorder, delete. The dot to the right of a name marks the layout that is in effect right now. `F2` renames, `Alt+↑` and `Alt+↓` reorder.

**The canvas is in the centre.** Monitors are dragged with the mouse and snap to each other's edges. Those that are not in the profile sit below the dividing line: drag one above it to add it. Desktops cannot overlap: the program says so at the bottom and does not apply.

**The properties of the selected monitor are at the bottom:** whether it is on, resolution, refresh rate, rotation, whether it is primary, duplication, HDR. Only what applies is shown: the refresh rate where a resolution offers a choice, duplication when there is something to duplicate.

**Apply** writes the profile and switches to the layout at once; **Save** only writes it. Edits on the canvas and in the properties do not reach the disk by themselves: when you switch to another profile or close the window, the program asks whether to save them.

**Keyboard shortcut:** press the button of that name, then the keys themselves. A single key works only for `F13`–`F24`, `Pause` and `ScrollLock`; any other key needs Ctrl, Alt or Win in the combination.

The close button does not quit the program but hides it: it stays as an icon in the notification area and the hotkeys keep working. To quit, use **Exit** in the icon menu.

The look follows the system theme and the Windows accent colour.

## Profile by monitors

When the set of connected screens changes (you switched on a TV, pulled a cable), the program applies the fitting profile by itself. A profile fits when all its enabled monitors are connected now; of the fitting ones the one that uses more screens is taken, and on a tie the first in the list. If none fits, nothing happens.

The program announces the switch with a notification at the tray icon, so that screens that rearranged themselves do not seem to act on their own. A layout that you set by hand is not touched: the trigger is a change of the **set** of screens, not a change of the layout.

It is switched off in the tray menu, "Follow the monitors". What the program sees now and what follows from it:

```
DIIISPL.exe /auto
```

## HDR

HDR is part of a profile. It is set after the layout is applied, and at once for the whole duplicate group: Windows switches HDR off on any change of topology, and if the outputs of a group have it on differently, it sends both to SDR.

**There is no HDR in duplicate mode.** Windows gives no extended colour at all to outputs joined into a clone; this was checked on a monitor plus a TV, at 60 Hz and at 120. To have HDR on each screen, extend them instead.

The program does not pretend that everything worked: after applying, it re-reads the real state and, if HDR did not hold, names the reason. `DIIISPL.exe /hdr` shows what is on now.

## Taskbar

> **Dev builds only.** The stable build has none of it: no settings screen, no code, no styling library; the library is not even put into the package. The feature rests on a library that works inside the Explorer process and lives there longer than the program itself; until that settles, the stable channel does not need it. Settings written by a Dev build are not damaged by the stable one.

The taskbar look is part of a profile: switch to "Cinema" and the taskbar becomes transparent, return to "Work" and it is ordinary again. The look is set not once per profile but separately for each screen state: an empty desktop, an open window, a full-screen window, Start, search, task view, battery saver.

The **Taskbar** button in the window header opens a separate screen. It is built as "the usual look plus exceptions": first you set how the taskbar always looks, then in which cases it looks different. Changes show at once on the live taskbar. The looks: as in the system, colour without transparency, colour with transparency, blur, acrylic; the colour is `AARRGGBB` or the shorter `RRGGBB`. Each taskbar is worked out separately, so the look can differ between monitors: maximise a window on the left screen and the left taskbar changes while the right one stays as it was.

A case left **without its own look** is skipped: the taskbar takes the look of the next fitting case, not the usual one. A maximised window is also an open window, so "A window is full screen" without its own look passes the taskbar to the "A window is open" case.

Once the look is set, there is no need to repeat it in every profile: at the bottom of the same screen there is a transfer of settings between profiles.

## Language

The program speaks the language of Windows; you can change it in the About window. English and Russian are built in.

Another language is added with a file, without rebuilding. `DIIISPL.exe /lang cs` writes a template to `%AppData%\DIIISPL\lang\cs.lang`: plain text `key = value`, one line per caption. A line that stayed English is not translated yet; translate the ones you need, save as UTF-8, and the language appears in the list. What the file lacks is taken from the built-in translation, so an unfinished file breaks nothing.

`{0}` and `{1}` are what the program substitutes itself: a monitor name, a number, a version. Keep them, but you may reorder them if the language needs another order. Where a line break is needed, write `\n`.

## Command line

```
DIIISPL.exe                 open the manager window
DIIISPL.exe /tray           start as a tray icon only (used by autostart)
DIIISPL.exe /apply "Name"   apply a profile; /switch is the same
DIIISPL.exe /save "Name"    write the current layout into a profile
DIIISPL.exe /list           list the profiles
DIIISPL.exe /delete "Name"  delete a profile
DIIISPL.exe /monitors       what is connected and how it is identified
DIIISPL.exe /hdr            the HDR state on each output
DIIISPL.exe /state          the same for scripts: a line per monitor, fields separated by tabs
DIIISPL.exe /auto           which profile fits the monitors now and what the program would do
DIIISPL.exe /lang cs        write a translation template for the language "cs"
DIIISPL.exe /help           this list; the same as /?
```

Dev builds only:

```
DIIISPL.exe /taskbar <look> [AARRGGBB] [line]   set the taskbar look right now
DIIISPL.exe /taskbar-state ["Name"]             what is on the screens and which look follows from it
```

`<look>` is one of `default`, `opaque`, `clear`, `blur`, `acrylic`.

**Do not fire switches in a row.** Rebuilding displays is a heavy operation in the kernel of the graphics stack, and a dense queue of such calls can bring the video driver down: while debugging this program, a series of about forty rebuilds led to a blue screen `0x13A KERNEL_MODE_HEAP_CORRUPTION`. The program keeps a pause of at least 1.2 seconds between applies, shared by all its copies, but if you switch layouts from a script, leave pauses yourself too.

## Files

- profiles: `%AppData%\DIIISPL\profiles\*.layout`, plain text that can be edited by hand
- translations: `%AppData%\DIIISPL\lang\*.lang`, plain text too, see "Language"; a `lang` folder next to the program is read as well, and a file in `%AppData%` wins
- program settings: `%AppData%\DIIISPL\settings.ini`: update channel, language, the "Follow the monitors" switch and a few small state flags (the last update check, a skipped version, a shown tray hint)
- error log: `%AppData%\DIIISPL\error.log`, written when the program fails to start
- autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DIIISPL`

A profile looks like this:

```ini
[profile]
name = Cinema
hotkey = F17
order = 2

[display]
id = SAM:Q80A:16780800
name = Q80A
enabled = 1
x = 0
y = 0
width = 3840
height = 2160
hz = 60
rotation = 1
primary = 1
clone = -1
hdr = 1
```

In Dev builds a `[taskbar]` section is added to the profile: a dash means "state is off", and `opaque,FF101010,0` reads as "look, colour `AARRGGBB`, edge line".

## Updates

The program looks at the GitHub releases by itself, once a day, and if it could not connect, it retries in an hour. Having found a new one, it says so with a tip at the tray icon; the update is installed on the About page, with a button, and the same page shows a short list of what is new, so there is no need to go to the browser. Profiles and settings stay in place.

A downloaded package must be signed with the same certificate as the program itself: the thumbprint is built into it, and it will not run anyone else's installer.

To see what changed from version to version, press **What’s new** on the same page: it opens the whole release history that lies inside the program, in its language and without a network. The same entries are in [CHANGELOG.md](CHANGELOG.md) and [CHANGELOG.ru.md](CHANGELOG.ru.md), and they also greet you on the page of each release on GitHub.

There are two channels, the switch is on the same page:

| Channel | What arrives |
|---|---|
| Stable | only finished releases |
| Dev | prereleases too: earlier, with unfinished parts and the taskbar settings |

## Troubleshooting

- **The layout was not applied**: the reason is in the status line. Most often a monitor from the profile is physically off: over HDMI a switched-off TV simply vanishes from the system.
- **The screen went dark after an experiment**: `Win+P` brings the picture back, then you can apply a working profile.
- **A hotkey does not work**: `DIIISPL.exe /list` shows the assigned ones, and a conflict with another program shows as a warning at start.
- **The taskbar does not change**: only Dev builds can change it. Check that `DIIISPL.ExplorerTAP.dll` is next to the program, and look at `DIIISPL.exe /taskbar-state`. If TranslucentTB works next to it, both programs style the taskbar in a race; keep one.
- **The taskbar kept its old look**: "Reset the taskbar look" in the icon menu or the "Reset the look" button in the taskbar settings: the program forgets what it has applied and paints the taskbars anew.

## Requirements

- **System:** Windows 11, x64. The installer is 64-bit and puts the program into `C:\Program Files\DIIISPL`.
- **Permissions:** installing needs administrator rights (the package installs for all users). The program itself runs as an ordinary user and needs none.
- **Network:** only the update check, and only the hosts below; nothing else is sent or received. The check can be left to the program (once a day) or started from the About page.
  - `api.github.com`, HTTPS: the list of releases of `rstrlnkv/DIIISPL`.
  - The installer file of a release, HTTPS, by the link GitHub gives in the release (`github.com`; GitHub redirects the download to its own storage). Only when you press the update button.
  - Links in the About window (the project page, the releases page, the author's Telegram) are opened in your browser by a click; the program does not request them itself.
- **Changes outside its own folders:**
  - `%AppData%\DIIISPL`: profiles, settings, translations, the error log (see "Files").
  - `%TEMP%\DIIISPL-update`: the downloaded installer, deleted by the next update.
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DIIISPL`: autostart, only if you switch it on in the tray menu; uninstalling the program removes this value.
  - `HKLM\Software\DIIISPL`: written by the installer, it remembers the install folder and whether the Start menu shortcut was chosen. The installer also creates the Start menu shortcut unless `INSTALLSHORTCUT=0`.
  - Display configuration: applying a profile changes the layout, resolution, refresh rate, rotation, primary monitor, duplication and HDR state of your monitors (`SetDisplayConfig`).
  - Global hotkeys: each profile hotkey is registered system-wide, so the key no longer reaches other programs while DIIISPL runs.
  - Dev channel only: `DIIISPL.ExplorerTAP.dll` is loaded into `explorer.exe` and styles the taskbar from inside it. The stable channel does not carry this library.

## Build from source

The program is built with one command, `build.cmd`; it needs the C# compiler of .NET Framework that Windows 11 has by default, and git with a full clone of the repository: the build number is the commit count, so `build.cmd` refuses to build outside a complete clone (a shallow clone, no git, `GIT_DIR` set). `build.cmd dev` builds the Dev channel.

How a layout is really applied, why the taskbar has to be styled from inside Explorer, what the installer, the icon and the release are built with, what the automatic test covers and how the repository is arranged are in [INTERNALS.md](INTERNALS.md) (in Russian); a short map of the code is in [ARCHITECTURE.md](ARCHITECTURE.md). Vulnerabilities are reported as described in [SECURITY.md](SECURITY.md).

## Licence

**GNU General Public License v3 or later**: see [LICENSE](LICENSE).

The project was moved from MIT to GPLv3 on purpose: it carries the ExplorerTAP component of [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (© Charles Milette and contributors), distributed under GPLv3, and copyleft requires a derived work to go under the same licence. Details are in [NOTICE.md](NOTICE.md).
