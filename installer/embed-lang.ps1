# DIIISPL — менеджер раскладок мониторов для Windows.
# Copyright (C) 2026 r.strlnkv
# Свободная программа под GNU General Public License v3 или новее; см. LICENSE.
#
# Положить перевод внутрь пакета.
#
# Языков у установщика может быть сколько угодно, а файл для скачивания должен
# остаться один: два файла на выбор — это вопрос там, где его быть не должно.
# Windows Installer умеет хранить переводы внутри пакета отдельными потоками,
# по потоку на язык; имя потока — числовой код языка, и по нему же перевод
# выбирается при установке.
#
# То же самое делает WiSubStg.vbs из Windows SDK. Своя копия здесь потому, что
# путь к SDK на сборщике зависит от его версии, а ошибка «нет такого файла»
# всплыла бы только в день выпуска.

param(
  [Parameter(Mandatory = $true)][string]$Msi,
  # Пары «код языка = файл перевода», например 1049=obj\ru-ru.mst
  [Parameter(Mandatory = $true)][string[]]$Transform
)

$ErrorActionPreference = 'Stop'

$installer = New-Object -ComObject WindowsInstaller.Installer

# msiOpenDatabaseModeTransact = 1: правки видны только после Commit.
$db = $installer.GetType().InvokeMember(
  'OpenDatabase', 'InvokeMethod', $null, $installer, @($Msi, 1))

try {
  $view = $db.GetType().InvokeMember(
    'OpenView', 'InvokeMethod', $null, $db, @('SELECT `Name`,`Data` FROM _Storages'))
  $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null)

  foreach ($pair in $Transform) {
    $eq = $pair.IndexOf('=')
    if ($eq -lt 1) { throw "Нужна пара «код=файл», а не «$pair»." }
    $name = $pair.Substring(0, $eq).Trim()
    $file = $pair.Substring($eq + 1).Trim()
    if (-not (Test-Path $file)) { throw "Нет файла перевода: $file" }
    $file = (Resolve-Path $file).Path

    $rec = $installer.GetType().InvokeMember(
      'CreateRecord', 'InvokeMethod', $null, $installer, @(2))
    $rec.GetType().InvokeMember('StringData', 'SetProperty', $null, $rec, @(1, $name))
    $rec.GetType().InvokeMember('SetStream', 'InvokeMethod', $null, $rec, @(2, $file))

    # msiViewModifyAssign = 3: заменить строку с таким именем или добавить новую.
    # Пересборка пакета не должна плодить потоки-двойники.
    $view.GetType().InvokeMember('Modify', 'InvokeMethod', $null, $view, @(3, $rec))
    Write-Host ("  перевод {0} — внутри пакета" -f $name)
  }

  $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null)

  # Список языков пакета. Пока в нём один код, потоки с переводами лежат внутри
  # мёртвым грузом: Windows Installer ищет перевод только среди объявленных
  # языков. Собирается пакет по-прежнему одноязычным — иначе проверки при сборке
  # требуют переводов, которых на тот момент ещё нет.
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
  Write-Host ("  языки пакета: {0}" -f $updated)

  $db.GetType().InvokeMember('Commit', 'InvokeMethod', $null, $db, $null)
}
finally {
  [Runtime.InteropServices.Marshal]::ReleaseComObject($db) | Out-Null
  [Runtime.InteropServices.Marshal]::ReleaseComObject($installer) | Out-Null
  # Пакет остаётся открытым, пока сборщик мусора не доберётся до COM-объектов,
  # а следующим шагом его подписывают — и подпись упирается в занятый файл.
  [GC]::Collect()
  [GC]::WaitForPendingFinalizers()
}
