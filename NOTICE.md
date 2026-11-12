# Third-party material in DIIISPL

DIIISPL is distributed under the GNU General Public License v3 or later (see `LICENSE`).
Part of the code comes from other free software, and naming it is a condition of its
licence, not a courtesy. Each item below states what it is, where it lies in the tree, its
source, its copyright holder, its licence and what was changed.

## TranslucentTB (ExplorerTAP)

- **What:** the **ExplorerTAP** component, which styles the Windows 11 taskbar from inside
  `explorer.exe`.
- **Where:** the `native/` folder (`native/ExplorerTAP/`, `native/Common/`).
- **Source:** https://github.com/TranslucentTB/TranslucentTB. The exact upstream release or
  commit that was copied is not recorded in the tree: to be confirmed by the owner
  (the import commit in this repository's history is be02d5c).
- **Holder:** the TranslucentTB contributors (the upstream LICENSE has no copyright line).
- **Licence:** GPL-3.0 (GNU General Public License v3, upstream LICENSE.md, with no "or later" grant, so this part stays GPL v3 only).
- **Changes:** the library is built as `DIIISPL.ExplorerTAP.dll`. The name, the identifiers
  (the service CLSID, the TAP site CLSID, the interface IIDs, the ready-event name and the
  tag in Explorer), the logging and the set of methods were changed to what this program
  needs, so that it does not compete with an installed TranslucentTB for the same service.
  The headers `native/Common/config/*.hpp` are TranslucentTB files; they are listed in the
  project (`native/Common/Common.vcxitems`) but no compiled source file of the
  library includes them.

Why it exists: from outside, the Windows 11 taskbar cannot be styled. Its background is drawn
by its own XAML tree, which the system accent policy does not reach. ExplorerTAP solves this
from inside: the library is loaded into Explorer and publishes a COM styling service there.

The component's sources lie in this repository with the rest, so, as the GPL requires, the
recipient has everything needed to build the program again.

## Windows

- **What:** calls to the system (CCD API: `QueryDisplayConfig`, `SetDisplayConfig`), EDID
  parsing and HDR control.
- **Where:** the C# sources in `src/` (`Native.cs`, `Edid.cs`, `Applier.cs`).
- **Source:** written for this program from Microsoft's public documentation.
- **Holder / licence:** DIIISPL authors, GPL v3 or later.
- **Headers of undocumented Explorer interfaces:** `native/Common/undoc/` came with ExplorerTAP
  and is distributed on the same terms as the TranslucentTB item above.

## Detours

- **What:** a library for intercepting Windows API calls; linked statically into
  `DIIISPL.ExplorerTAP.dll` (called from `native/ExplorerTAP/api.cpp`).
- **Where:** it is not copied into the tree. The vcpkg port recipe is
  `native/vcpkg/ports/detours`; it fetches source at commit
  `4b8c659f549b0ab21cf649377c7a84eb708f5e68` and the build downloads it.
- **Source:** https://github.com/microsoft/Detours
- **Holder:** Microsoft Corporation.
- **Licence:** MIT.
- **Changes:** none to the source; the port only sets compiler flags.

```
Copyright (c) Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in the
Software without restriction, including without limitation the rights to use, copy,
modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

## Windows Implementation Libraries (wil)

- **What:** helper headers for Win32 and COM code (`wil/resource.h`, `wil/safecast.h`,
  `wil/win32_helpers.h`, `wil/cppwinrt_helpers.h`); header-only, used in the native library
  (`native/Common/wilx.hpp`, `native/Common/win32.hpp`).
- **Where:** not copied into the tree. `native/ExplorerTAP/vcpkg.json` lists `wil`, and
  `native/ExplorerTAP/vcpkg-configuration.json` pins the vcpkg baseline
  `84bab45d415d22042bd0b9081aea57f362da3f35`; the build downloads it.
- **Source:** https://github.com/microsoft/wil
- **Holder:** Microsoft Corporation.
- **Licence:** MIT (the text is the same as for Detours above, with the line
  "Copyright (c) Microsoft Corporation. All rights reserved.").
- **Changes:** none.

## C++/WinRT

- **What:** the C++ projection of the Windows Runtime (NuGet package
  `Microsoft.Windows.CppWinRT` 2.0.250303.1), used by the native library.
- **Where:** not copied into the tree. `native/ExplorerTAP/packages.config` lists it, and
  `native\build-native.cmd` restores it into the ignored folder `native/packages/`.
- **Source:** https://github.com/microsoft/cppwinrt
- **Holder:** Microsoft Corporation.
- **Licence:** MIT (text as above).
- **Changes:** none.

## Build-time tools (not distributed)

Everything in this section is used only to build the program. None of it is copied into the
repository (the `native/packages/` and `installer/wix/` folders are ignored by git) and none
of it is part of what is shipped, except where an entry says so. "Referenced in" names the
file of this tree that asks for the item. Facts below come from the tree and from the
`.nuspec` and licence files inside the packages restored on the maintainer's machine; what
could not be confirmed there is marked "not confirmed locally".

### WiX Toolset 3.14.1

- **What:** builds the MSI installer. The MSI carries the `CloseApplication` custom action of
  `WixUtilExtension` (`installer/DIIISPL.wxs`); this is the one part that is shipped.
- **Referenced in:** `installer/build-msi.cmd` (`nuget install WiX -Version 3.14.1`).
- **Source:** https://wixtoolset.org, https://github.com/wixtoolset/wix3
- **Holder:** .NET Foundation and contributors (package `.nuspec`).
- **Licence:** Microsoft Reciprocal License (MS-RL) (package `.nuspec`, `LICENSE.TXT`).

### NuGet command-line client (`nuget.exe`)

- **What:** restores the NuGet packages below and the WiX package.
- **Referenced in:** `native/build-native.cmd` and `installer/build-msi.cmd`, which download it
  from https://dist.nuget.org/win-x86-commandline/latest/nuget.exe into `native/nuget.exe`.
- **Source:** https://github.com/NuGet/NuGet.Client
- **Holder and licence:** not confirmed locally.

### Microsoft.Windows.CppWinRT 2.0.250303.1

- **What:** the C++/WinRT compiler and headers (see the C++/WinRT section above, where it is
  used by the library).
- **Referenced in:** `native/ExplorerTAP/packages.config`.
- **Source:** https://github.com/Microsoft/cppwinrt
- **Holder:** Microsoft Corporation. **Licence:** MIT (package `LICENSE`).

### Microsoft.Windows.SDK.BuildTools 10.0.26100.7463

- **What:** tools for building projects that target the Windows SDK.
- **Referenced in:** `native/ExplorerTAP/packages.config`.
- **Source:** https://aka.ms/WinSDKProjectURL
- **Holder:** Microsoft Corporation. **Licence:** the Windows SDK licence at
  https://aka.ms/WinSDKLicenseURL (package `.nuspec`; the terms themselves are not confirmed
  locally).

### Microsoft.Windows.SDK.CPP, .x64 and .arm64 10.0.26100.7463

- **What:** the Windows SDK headers and libraries for C++ (the base package and the x64 and
  ARM64 parts).
- **Referenced in:** `native/ExplorerTAP/packages.config`.
- **Source:** https://aka.ms/WinSDKProjectURL
- **Holder:** Microsoft Corporation. **Licence:** the Windows SDK licence at
  https://aka.ms/WinSDKLicenseURL (package `.nuspec`; the terms themselves are not confirmed
  locally).

### Microsoft.Build.Tasks.Git, Microsoft.SourceLink.Common, Microsoft.SourceLink.GitHub (each 8.0.0)

- **What:** MSBuild tasks that read git information and write Source Link data into the build;
  marked `developmentDependency` in `packages.config`.
- **Referenced in:** `native/ExplorerTAP/packages.config`.
- **Source:** https://github.com/dotnet/sourcelink
- **Holder:** Microsoft Corporation. **Licence:** MIT (package `.nuspec`).

### Microsoft.Trusted.Signing.Client 1.0.95

- **What:** client for Microsoft's code-signing service.
- **Referenced in:** `native/ExplorerTAP/packages.config`.
- **Source:** https://aka.ms/trustedsigning/
- **Holder:** Microsoft Corporation. **Licence:** Microsoft software licence terms
  (titled MICROSOFT SOFTWARE LICENSE TERMS / AZURE CODE SIGNING, package `LICENSE.md`).

The Dev build shows the licence texts of four of the components above on its Licences page (TranslucentTB (ExplorerTAP), Detours, wil and C++/WinRT); this file stays the authoritative list.

## Not in the tree

`native/Common/config/*.hpp` mention RapidJSON and spdlog, as they did in TranslucentTB. Neither
is listed in `native/ExplorerTAP/vcpkg.json`, none of the library's source files includes those
headers (they are listed in `native/Common/Common.vcxitems` but included by no compiled source), and neither is built into the DLL. If a future change makes a library part of the
build, it gets its own section here.
