@echo off
rem Сборка программы.
rem
rem   build.cmd        стабильная сборка
rem   build.cmd dev    сборка канала Dev — с настройками панели задач
rem
rem Канал — это разные сборки, а не переключатель внутри одной: в стабильной
rem кода панели задач нет вовсе, а значит в проводник ничего не грузится и
rem библиотека оформления в пакет не кладётся. Настройку, которой в сборке нет,
rem нельзя случайно включить.
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo Не найден компилятор C# из .NET Framework.
  exit /b 1
)
set WPF=%NETDIR%\WPF

set CHANNEL=%1
if not defined CHANNEL set CHANNEL=%DIIISPL_CHANNEL%
if /i "%CHANNEL%"=="dev" (set CHANNEL=dev) else (set CHANNEL=stable)
if "%CHANNEL%"=="dev" (
  set DEFINE=/define:DEV
  set TASKBAR="%~dp0ExplorerTap.cs" "%~dp0TaskbarSettings.cs" "%~dp0TaskbarWatcher.cs" "%~dp0TaskbarWindow.cs"
  echo === Сборка Dev: с панелью задач ===
) else (
  set DEFINE=
  set TASKBAR=
  echo === Сборка Stable: без панели задач ===
)

rem Номер сборки — число коммитов. Он растёт сам, не требует ведения вручную и
rem не разъезжается с версией: версия говорит «что это», номер — «какая по счёту».
rem Файл создаётся при каждой сборке и в репозиторий не попадает; без git номер 0.
set BUILD=0
for /f "delims=" %%I in ('git -C "%~dp0." rev-list --count HEAD 2^>nul') do set BUILD=%%I
> "%~dp0BuildInfo.g.cs" echo // Создаётся build.cmd при каждой сборке. Править незачем.
>> "%~dp0BuildInfo.g.cs" echo namespace DIIISPL { internal static class BuildInfo { public const string Number = "%BUILD%"; } }

"%NETDIR%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 %DEFINE% ^
  /out:"%~dp0DIIISPL.exe" ^
  /win32icon:"%~dp0DIIISPL.ico" ^
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll ^
  /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  /r:"%NETDIR%\System.Xaml.dll" ^
  /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" ^
  "%~dp0AssemblyInfo.cs" "%~dp0BuildInfo.g.cs" "%~dp0Lang.cs" "%~dp0Text.cs" "%~dp0Native.cs" "%~dp0Edid.cs" "%~dp0Monitors.cs" "%~dp0Layout.cs" "%~dp0Applier.cs" ^
  "%~dp0Store.cs" "%~dp0Settings.cs" "%~dp0ApplyGate.cs" "%~dp0Updater.cs" "%~dp0Hotkey.cs" %TASKBAR% ^
  "%~dp0Cli.cs" "%~dp0Theme.cs" "%~dp0Fluent.cs" ^
  "%~dp0MonitorCanvas.cs" "%~dp0ConfirmDialog.cs" "%~dp0Ui.cs" "%~dp0AboutWindow.cs" "%~dp0MainWindow.cs" "%~dp0App.cs"

if errorlevel 1 exit /b 1

rem Канал записываем рядом с программой — и только после удачной компиляции.
rem Пакет собирается из готового exe и раньше принимал на веру, что канал у них
rem общий: собрал build.cmd dev, потом по привычке build-msi.cmd без аргумента —
rem и выходил «стабильный» пакет с Dev-программой. Записать метку до компиляции
rem тоже нельзя: упавшая сборка оставила бы «dev» рядом со стабильным exe, и
rem сверка пропустила бы неверную пару.
> "%~dp0.channel" echo %CHANNEL%

call "%~dp0installer\sign.cmd" "%~dp0DIIISPL.exe" || exit /b 1

echo Готово: %~dp0DIIISPL.exe
