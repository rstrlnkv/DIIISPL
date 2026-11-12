# DIIISPL — monitor layout manager for Windows.
# Copyright (C) 2026 r.strlnkv
# Free software under the GNU General Public License v3 or later; see LICENSE.
#
# Put a translation inside the package.
#
# The installer can have any number of languages, but the file to download must
# stay single: two files to choose from is a question where there should be none.
# Windows Installer can keep translations inside the package as separate streams,
# one stream per language; the stream name is the numeric language code, and the translation
# is picked by it at install time.
#
# WiSubStg.vbs from the Windows SDK does the same. This is our own copy because
# the SDK path on the build server depends on its version, and a "no such file" error
# would only surface on release day.

param(
  [Parameter(Mandatory = $true)][string]$Msi,
  # Pairs "language code = translation file", for example 1049=obj\ru-ru.mst
  [Parameter(Mandatory = $true)][string[]]$Transform
)

$ErrorActionPreference = 'Stop'

$installer = New-Object -ComObject WindowsInstaller.Installer

# msiOpenDatabaseModeTransact = 1: changes are visible only after Commit.
$db = $installer.GetType().InvokeMember(
  'OpenDatabase', 'InvokeMethod', $null, $installer, @($Msi, 1))

try {
  $view = $db.GetType().InvokeMember(
    'OpenView', 'InvokeMethod', $null, $db, @('SELECT `Name`,`Data` FROM _Storages'))
  $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)

  foreach ($pair in $Transform) {
    $eq = $pair.IndexOf('=')
    if ($eq -lt 1) { throw "Expected a code=file pair, not '$pair'." }
    $name = $pair.Substring(0, $eq).Trim()
    $file = $pair.Substring($eq + 1).Trim()
    if (-not (Test-Path $file)) { throw "Translation file not found: $file" }
    $file = (Resolve-Path $file).Path

    $rec = $installer.GetType().InvokeMember(
      'CreateRecord', 'InvokeMethod', $null, $installer, @(2))
    $rec.GetType().InvokeMember('StringData', 'SetProperty', $null, $rec, @(1, $name))
    $rec.GetType().InvokeMember('SetStream', 'InvokeMethod', $null, $rec, @(2, $file))

    # msiViewModifyAssign = 3: replace the row with this name or add a new one.
    # Rebuilding the package must not breed duplicate streams.
    $view.GetType().InvokeMember('Modify', 'InvokeMethod', $null, $view, @(3, $rec))
    Write-Host ("  translation {0} is inside the package" -f $name)
  }

  $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null)

  # The package language list. While it holds one code, the translation streams lie inside
  # as dead weight: Windows Installer looks for a translation only among the declared
  # languages. The package is still built single-language, otherwise the build-time checks
  # demand translations that do not exist yet.
  $si = $db.GetType().InvokeMember('SummaryInformation', 'GetProperty', $null, $db, @(1))
  $template = $si.GetType().InvokeMember('Property', 'GetProperty', $null, $si, @(7))

  $semi = $template.IndexOf(';')
  $platform = if ($semi -ge 0) { $template.Substring(0, $semi) } else { $template }
  $listed = if ($semi -ge 0) { $template.Substring($semi + 1) } else { '' }

  $langs = New-Object System.Collections.Generic.List[string]
  foreach ($one in $listed.Split(',')) { if ($one.Trim()) { $langs.Add($one.Trim()) } }
  foreach ($pair in $Transform) {
    $name = $pair.Substring(0, $pair.IndexOf('=')).Trim()
    if (-not $langs.Contains($name)) { $langs.Add($name) }
  }

  $updated = $platform + ';' + ($langs -join ',')
  $si.GetType().InvokeMember('Property', 'SetProperty', $null, $si, @(7, $updated))
  $si.GetType().InvokeMember('Persist', 'InvokeMethod', $null, $si, $null)
  Write-Host ("  package languages: {0}" -f $updated)

  $db.GetType().InvokeMember('Commit', 'InvokeMethod', $null, $db, $null)
}
finally {
  [Runtime.InteropServices.Marshal]::ReleaseComObject($db) | Out-Null
  [Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
  # The package stays open until the garbage collector gets to the COM objects,
  # and the next step signs it, so the signature hits a locked file.
  [GC]::Collect()
  [GC]::WaitForPendingFinalizers()
}
