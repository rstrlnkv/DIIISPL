# How DIIISPL works and how to build it

Everything you need if you rebuild the program or want to understand why it behaves the way
it does. What a user needs is in [README.md](README.md) and [MANUAL.md](MANUAL.md).

- [How a layout is applied](#how-a-layout-is-applied)
- [The taskbar from the inside](#the-taskbar-from-the-inside)
- [What was verified](#what-was-verified)
- [Building](#building)
- [Installer and signing](#installer-and-signing)
- [Release](#release)

## How a layout is applied

First the program checks whether this layout is already in place: whether the set of monitors,
resolutions, refresh rates, rotations, duplication, primary monitor and relative positions all
match. If everything matches, the displays are not touched at all. This is not an optimisation
but a necessity: every `SetDisplayConfig` call renegotiates the output, the screen blinks, and
on an HDR monitor there is also a visible jump in brightness. Pressing the hotkey of a profile
that is already applied must be harmless.

If the layout differs, the program does not ask Windows to "recall the saved configuration".
It builds the CCD path and mode arrays itself (`DISPLAYCONFIG_PATH_INFO` /
`DISPLAYCONFIG_MODE_INFO`) and passes them to `SetDisplayConfig`:

1. It finds the current outputs for the profile's monitors by EDID.
2. It hands out source ids to groups: one group = one desktop, duplication = several outputs
   on one source id.
3. It builds source modes with coordinates and normalises the positions so that the primary
   monitor sits at (0,0), as Windows requires.
4. It applies strictly, without the flag that lets Windows silently rewrite the request. If
   the strict attempt is rejected, the requirements are relaxed step by step (mode
   enumeration, then the refresh rate left to the driver).
5. It checks the result against the request: whether everything came on, in the right modes,
   whether duplication formed. If something is missing, it retries and, if needed, wakes the
   outputs with the standard topology.
6. After the layout changes, it sets HDR.

**About duplication.** An output that is part of a duplicate gets a different `target id` from
the driver: the Q80A TV gets `16781568` instead of the usual `4352`. A pair of "ordinary"
identifiers does not exist at all, so a hand-built configuration loses the second screen even
though `SetDisplayConfig` reports success. The program accounts for this: if the duplicate is
not built yet, Windows builds it first, and the exact layout is laid over it afterwards, using
the identifiers that are valid in duplication.

## The taskbar from the inside

From the outside the Windows 11 taskbar cannot be styled: its background is drawn by its own
XAML tree, and the old accent policy (`SetWindowCompositionAttribute`) does not reach it. This
was verified by measurement: the colour of the bar does not change in any mode, although the
call returns success, and this holds even if the effect is applied to all ten child windows of
the taskbar, including `Windows.UI.Composition.DesktopWindowContentBridge`.

So DIIISPL carries over the **ExplorerTAP** component from
[TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (© Charles Milette
and contributors, GPLv3): a small library, `DIIISPL.ExplorerTAP.dll`, is loaded into Explorer,
reaches the taskbar tree from inside and publishes a COM styling service, and DIIISPL calls its
methods. No separate program is needed: it is one process.

The library exists only in Dev builds, both in the program and in the package. The Stable
build has none of it: no settings screen, no taskbar code, no library, and nothing is loaded
into Explorer.

When the profile changes, the connection to the service and the memory of what was applied are
reset: the taskbars are recreated, the old windows are gone, and everything remembered about
them becomes false.

### What Windows 11 throws at you along the way

Three things that are easy to get wrong and that are handled here (each one broke things
silently, with no error in the log):

- **The taskbar cannot be found by enumerating windows.** While Start, search or task view is
  open, shell windows are created and destroyed quickly, `EnumWindows` breaks off halfway, and
  the taskbar does not make it into the list at all, in exactly the states the whole thing was
  built for. So taskbars are looked up by class directly, with `FindWindowEx`.
- **Start cannot be recognised by the focused window.** On current builds it hands focus to the
  `SearchHost` window, so by focus Start and search are indistinguishable. An open Start is a
  visible `StartMenuExperienceHost` window across the full width of the screen; search on its
  own is a `SearchHost` window with no Start above it.
- **Task view is not a `CoreWindow`.** It is an Explorer window of class
  `XamlExplorerHostIslandWindow`.

To see what the program sees right now and which look follows from it:

```bash
DIIISPL.exe /taskbar-state "Movies"
```

## What was verified

The repository holds an automatic test of 30 schemes that builds a configuration, applies it
and compares the actual state of the displays with the requested one, including positions,
refresh rates, rotations, the composition of duplication groups and HDR. The last run on a
"two monitors + TV" setup: **30 of 30**. The report itself stays on the machine where the run
took place, `tests\selftest-log.txt`; it is not committed to the repository.

What is covered: each screen on its own; all pairs extended and all pairs duplicated; three
extended, a duplicated pair with a third alongside in all combinations, all three duplicated;
vertical stack, corner, a screen to the left of the primary (negative coordinates), primary in
the middle; reduced resolution, mixed refresh rates, rotation by 90° and 270°; each screen as
the primary; HDR on and off, HDR on three screens at once and HDR refused in a duplicate;
re-applying the same profile and a profile that refers to a monitor that is not connected.

The taskbar was verified differently, by sampling colours from the screen: each look was
applied and a pixel of the taskbar itself was measured, and each state was reproduced for real
(a window, maximised, Start, search, task view) and painted its own colour. Six states of seven
matched; the seventh, battery saver, cannot be reproduced on a desktop machine, which has no
battery.

## Building

Locally, without a CI builder:

```
scripts\build.cmd
```

All it needs is the C# compiler from .NET Framework, which Windows 11 has by default. Nothing
has to be downloaded or installed.

With no arguments the Stable channel is built. The Dev build, with its own taskbar settings
and the `/taskbar` commands, is built like this:

```
scripts\build.cmd dev
```

The taskbar styling library is built separately. It is not in the repository (binaries are not
committed), so after cloning build it once (or take the ready one from a release); there is no
need to rebuild it during later work:

```
native\build-native.cmd
```

This one does need Visual Studio Build Tools with the "Desktop development with C++" workload
and the Windows SDK; the rest, the NuGet packages and vcpkg dependencies (detours, wil), the
script fetches itself. It puts intermediate files in `%TEMP%\diiispl-native`: the C++/WinRT
headers easily overstep the 260-character path limit when built in place.

The program icon is in the repository ready-made (`src/DIIISPL.ico`, nine sizes from 16 to
256), but it was not drawn in an editor: it is built by code, so it can be adjusted without
opening a graphics editor and without guessing what it was made with.

The large sizes are done in the Fluent style: layers with a soft shadow, a diagonal gradient,
a highlight along the top edge and a muted glare, with one light for the whole mark, falling
from the top left. The small ones are drawn differently, as a flat silhouette: at 16 pixels
the shadow and glare turn into mud. Windows itself does the same: its icons have separate
artwork for small sizes rather than a scaled-down large one.

```
tools\make-icon.cmd
```

The script can be given a path to a PNG, and then it also lays out a sheet with all sizes on
light and dark backgrounds, including the small ones close up.

The automatic test is separate:

```
tests\run-tests.cmd
```

It really switches the monitors about forty times, takes roughly five minutes and writes a
report to `tests\selftest-log.txt`. Before starting it asks for consent: type the answer it
prompts for ("да", Russian for "yes") or pass `--yes` straight away. The schemes are designed
for the author's three specific monitors: without them the run refuses to start. Individual
checks go by number: `run-tests.cmd 21 26`.

Sources: `src/Native.cs` (WinAPI), `src/Edid.cs` (EDID parsing), `src/Monitors.cs` (monitor
identification and modes), `src/Layout.cs` (profile), `src/Applier.cs` (configuration building
and HDR), `src/AutoSwitch.cs` (profile choice by the set of screens), `src/Store.cs` (files),
`src/Hotkey.cs` (shortcuts), `src/Theme.cs` + `src/Fluent.cs` (styling), `src/MonitorCanvas.cs`
(canvas), `src/MainWindow.cs` (window), `src/ConfirmDialog.cs` (questions), `src/App.cs` (tray,
hotkeys, autostart, watching the set of screens), `src/Cli.cs` (command line).

Taskbar: `src/TaskbarSettings.cs` (states and looks in the profile), `src/TaskbarWatcher.cs`
(what happens on the screens and which look follows from it), `src/TaskbarWindow.cs` (settings
screen), `src/ExplorerTap.cs` (calls to the styling service), `native/` (the library itself,
C++).

Shared by the windows: `src/Ui.cs`, the cards, settings rows and headings the screens are made
of, so that they look alike by design and not by coincidence; `src/AboutWindow.cs`, "About".

Icon: `tools/MakeIcon.cs` draws it and writes the `.ico`. The ready `src/DIIISPL.ico` is in the
program as a single copy, a Win32 resource; Explorer, the taskbar and Alt+Tab show it, and the
tray icon is taken from the same place, with the required size specified.

## Installer and signing

To build the package from source:

```
installer\build-msi.cmd          stable package
installer\build-msi.cmd dev      Dev channel package
```

The script fetches the WiX toolset itself; it does not have to be installed on the system. Of
the ready-made files it needs `DIIISPL.exe` built for **the same channel**, otherwise the
package build refuses to run. The Dev package also needs `DIIISPL.ExplorerTAP.dll`, that is,
`native\build-native.cmd`.

The installer windows use their own images instead of the standard ones from the WiX set: the
banner at the top and the dark side panel of the welcome page are drawn by code
(`installer\MakeArt.cs`) in the same colours as the program icon, and the window font is
Segoe UI instead of Tahoma. The mark on the banner is pushed to the right edge not for looks:
the installer stretches the description under the title almost up to it, and a larger mark
ended up right on top of the text.

### What it is signed with

The build signs the program, the library and the package if `installer\sign.thumbprint` with
the certificate thumbprint lies next to the scripts (or the `DIIISPL_SIGN_THUMBPRINT` variable
is set). With no certificate, signing is silently skipped and the build goes on as usual.

Binaries are signed not in place but as copies in `installer\obj\stage`: the styling library is
loaded into Explorer, the program may be running, and `signtool` would run into a busy file.
This also keeps the build from modifying the working directory.

A timestamp is mandatory: without it a signature stops being considered valid on the day the
certificate expires, even for files released long before.

The signing certificate is not in the repository, only on the key owner's machine and in the
builder's secrets.

A self-signed certificate removes the warning only on machines where its root is added to the
trusted ones. For distribution a certificate from a certification authority is needed: since
June 2023 its private key must be kept in attested hardware or a cloud signing service, and
such certificates are no longer issued as a `.pfx` file.

## Release

Before tagging, write the section for the new version in [CHANGELOG.ru.md](CHANGELOG.ru.md) and
[CHANGELOG.md](CHANGELOG.md): the builder takes the release description from there and fails
if the section is missing in either language. The heading carries the number from the tag in
square brackets: `## [0.1.3-dev.1] - 2026-08-21`.

The version in `src/AssemblyInfo.cs` (`AssemblyVersion` and `AssemblyFileVersion`) must also be
written before the tag: the builder does not edit it but compares it with the tag, and fails if
the first three parts differ or if the first released heading of CHANGELOG.md /
CHANGELOG.ru.md (under `## [Unreleased]`) is not equal to the tag's version.

The third version bump is `[assembly: AssemblyInformationalVersion("x.y.z[-dev.N]")]` in
`src/AssemblyInfo.cs`: it must equal the whole tag, suffix included. It is what the About
screen shows (the exe's `ProductVersion`), and `release.yml` fails the run if the attribute
is missing, appears twice or differs from the tag. `tests\version-check.cmd` checks the form
of the attribute and its first three parts locally; only `release.yml` compares it with the tag. The updater reads the suffix too: between equal x.y.z, `-dev.2` is newer
than `-dev.1`, and a release without a suffix is newer than any `-dev.N` of its number.

The `licenses/` folder and the `DEVRES` resources in `scripts\build.cmd` are Dev-only: the
Stable build bundles no third-party component, so it embeds no licence text and has no
Licences page. To bundle a new component, add it to `NOTICE.md`, put its licence text in
`licenses/`, embed it in `scripts\build.cmd` (`DEVRES`, with an existence guard) and add a
card to `src/LicensesWindow.cs`.

The build number is the commit count (`git rev-list --count HEAD`), and `scripts\build.cmd`
refuses to build before compiling if the count may be inexact: there is no git or no
repository, the git root is not the repository root (the parent of `scripts\`), `GIT_DIR` or
`GIT_WORK_TREE` is set, the clone is shallow, or there are replace refs or a grafts file.
There is no fallback number. That is why the builder fetches the whole history
(`fetch-depth: 0`). `build-msi.cmd` without `DIIISPL_VERSION` takes the version from
`src/AssemblyInfo.cs`.

The release is built by GitHub Actions on a tag, which also supplies the version:

```
git push && git tag v0.1.1 && git push --tags
```

The builder builds the program and the installer (and, for the Dev channel, the styling
library too), signs them if the repository secrets hold a certificate (`SIGN_PFX` is the PFX
in base64, `SIGN_PFX_PASSWORD` its password), and attaches the package to the release.

The tag sets not only the version but also the channel. No suffix means a Stable build; with a
suffix (`v0.1.1-dev.1`) it is a Dev build: it goes out as a pre-release, and only it has the
taskbar settings.
