# DIIISPL — monitor layout manager for Windows.
# Copyright (C) 2026 r.strlnkv
# Free software under the GNU General Public License v3 or later; see LICENSE.
#
# Converts LICENSE to RTF: the licence text dialog in the installer can only show that.
param([string]$Source, [string]$Target)

$text = [System.IO.File]::ReadAllText($Source)
$text = $text -replace '\\', '\\\\' -replace '\{', '\{' -replace '\}', '\}'
$text = $text -replace "`r`n", "`n"

$body = New-Object System.Text.StringBuilder
foreach ($line in $text -split "`n") {
    [void]$body.Append($line)
    [void]$body.Append("\par`r`n")
}

$rtf = "{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 Consolas;}}`r`n\fs16 " +
       $body.ToString() + "}"
[System.IO.File]::WriteAllText($Target, $rtf, [System.Text.Encoding]::ASCII)
"Licence for the installer: $Target"
