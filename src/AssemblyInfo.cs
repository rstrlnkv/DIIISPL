// DIIISPL — a monitor layout manager for Windows.
// Copyright (C) 2026 r.strlnkv
// Free software under the GNU General Public License v3 or later; see LICENSE.
//
// Program details: shown by the file properties, Task Manager and the list of
// installed programs. Without them Windows leaves the publisher column empty.

using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("DIIISPL")]
[assembly: AssemblyDescription("Monitor layout manager")]
[assembly: AssemblyProduct("DIIISPL")]
[assembly: AssemblyCompany("Rostislav Strelnikov")]
[assembly: AssemblyCopyright("© 2026 Rostislav Strelnikov. GNU GPL v3 or later")]
[assembly: AssemblyVersion("0.2.4.0")]
[assembly: AssemblyFileVersion("0.2.4.0")]
// The full version with its -dev.N suffix, shown on the About screen. release.yml checks
// that it equals the whole release tag.
[assembly: AssemblyInformationalVersion("0.2.4-dev.2")]
[assembly: ComVisible(false)]
