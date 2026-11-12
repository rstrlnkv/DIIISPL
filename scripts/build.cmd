@echo off
rem Сборка программы.
rem
rem   scripts\build.cmd        стабильная сборка
rem   scripts\build.cmd dev    сборка канала Dev — с настройками панели задач
rem
rem Канал — это разные сборки, а не переключатель внутри одной: в стабильной
rem кода панели задач нет вовсе, а значит в проводник ничего не грузится и
rem библиотека оформления в пакет не кладётся. Настройку, которой в сборке нет,
rem нельзя случайно включить.
setlocal
rem The repository root is the parent of scripts\, written once and normalised.
for %%I in ("%~dp0..") do set ROOT=%%~fI
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
  set TASKBAR="%ROOT%\src\ExplorerTap.cs" "%ROOT%\src\TaskbarSettings.cs" "%ROOT%\src\TaskbarWatcher.cs" "%ROOT%\src\TaskbarWindow.cs"
  echo === Сборка Dev: с панелью задач ===
) else (
  set DEFINE=
  set TASKBAR=
  echo === Сборка Stable: без панели задач ===
)

rem Номер сборки — число коммитов. Он растёт сам, не требует ведения вручную и
rem не разъезжается с версией: версия говорит «что это», номер — «какая по счёту».
rem Файл создаётся при каждой сборке и в репозиторий не попадает.
rem
rem The number must be exact (Core standards/versioning.md rules 1-3), so every case
rem where git cannot count this project's own full history stops the build here,
rem before BuildInfo.g.cs is written or anything is compiled. There is no fallback number.
if defined GIT_DIR (
  echo Build refused: GIT_DIR is set, git would count another repository.
  exit /b 1
)
if defined GIT_WORK_TREE (
  echo Build refused: GIT_WORK_TREE is set, git would count another repository.
  exit /b 1
)
where git >nul 2>&1 || (
  echo Build refused: git is not installed or not in PATH, the build number cannot be counted.
  exit /b 1
)
set GITC=git -C "%ROOT%"
set INSIDE=
for /f "delims=" %%I in ('%GITC% rev-parse --is-inside-work-tree 2^>nul') do set INSIDE=%%I
if not "%INSIDE%"=="true" (
  echo Build refused: this folder is not in a git repository, the build number cannot be counted.
  exit /b 1
)
rem The prefix of the repository root, the parent of scripts\, is empty only when it is
rem git's top level; it needs no path normalisation (C:/x vs C:\x, letter case).
set PREFIX=
for /f "delims=" %%I in ('%GITC% rev-parse --show-prefix 2^>nul') do set PREFIX=%%I
if defined PREFIX (
  echo Build refused: git's top level is not the repository root, the parent of scripts\, it would count another repository.
  exit /b 1
)
set SHALLOW=
for /f "delims=" %%I in ('%GITC% rev-parse --is-shallow-repository 2^>nul') do set SHALLOW=%%I
if not "%SHALLOW%"=="false" (
  echo Build refused: the repository is shallow or its depth is unknown, fetch the full history.
  exit /b 1
)
set REPLACED=
for /f "delims=" %%I in ('%GITC% for-each-ref refs/replace 2^>nul') do set REPLACED=1
if defined REPLACED (
  echo Build refused: replace refs exist, git would count a rewritten history.
  exit /b 1
)
rem The grafts path git prints is relative to the folder it ran in, or absolute.
set GRAFTS=
for /f "delims=" %%I in ('%GITC% rev-parse --git-path info/grafts 2^>nul') do set GRAFTS=%%I
pushd "%ROOT%"
if defined GRAFTS if exist "%GRAFTS%" (
  popd
  echo Build refused: a grafts file exists, git would count a rewritten history.
  exit /b 1
)
popd
set BUILD=
for /f "delims=" %%I in ('%GITC% rev-list --count HEAD 2^>nul') do set BUILD=%%I
echo %BUILD%| findstr /r /x "[0-9][0-9]*" >nul || (
  echo Build refused: git rev-list --count HEAD did not give a number, the repository has no commits.
  exit /b 1
)
> "%ROOT%\src\BuildInfo.g.cs" echo // Создаётся build.cmd при каждой сборке. Править незачем.
>> "%ROOT%\src\BuildInfo.g.cs" echo namespace DIIISPL { internal static class BuildInfo { public const string Number = "%BUILD%"; } }

"%NETDIR%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 %DEFINE% ^
  /out:"%ROOT%\DIIISPL.exe" ^
  /win32icon:"%ROOT%\src\DIIISPL.ico" ^
  /resource:"%ROOT%\CHANGELOG.ru.md",changelog.ru /resource:"%ROOT%\CHANGELOG.md",changelog.en ^
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll ^
  /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  /r:"%NETDIR%\System.Xaml.dll" ^
  /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" ^
  "%ROOT%\src\AssemblyInfo.cs" "%ROOT%\src\BuildInfo.g.cs" "%ROOT%\src\Lang.cs" "%ROOT%\src\LangFile.cs" "%ROOT%\src\Text.cs" "%ROOT%\src\Native.cs" "%ROOT%\src\Edid.cs" "%ROOT%\src\Monitors.cs" "%ROOT%\src\Layout.cs" "%ROOT%\src\Applier.cs" "%ROOT%\src\AutoSwitch.cs" "%ROOT%\src\Changelog.cs" ^
  "%ROOT%\src\Store.cs" "%ROOT%\src\Settings.cs" "%ROOT%\src\ApplyGate.cs" "%ROOT%\src\Updater.cs" "%ROOT%\src\Hotkey.cs" %TASKBAR% ^
  "%ROOT%\src\Cli.cs" "%ROOT%\src\Theme.cs" "%ROOT%\src\Fluent.cs" ^
  "%ROOT%\src\MonitorCanvas.cs" "%ROOT%\src\ConfirmDialog.cs" "%ROOT%\src\Ui.cs" "%ROOT%\src\ChangelogWindow.cs" "%ROOT%\src\AboutWindow.cs" "%ROOT%\src\MainWindow.cs" "%ROOT%\src\App.cs"

if errorlevel 1 exit /b 1

rem Канал записываем рядом с программой — и только после удачной компиляции.
rem Пакет собирается из готового exe и раньше принимал на веру, что канал у них
rem общий: собрал build.cmd dev, потом по привычке build-msi.cmd без аргумента —
rem и выходил «стабильный» пакет с Dev-программой. Записать метку до компиляции
rem тоже нельзя: упавшая сборка оставила бы «dev» рядом со стабильным exe, и
rem сверка пропустила бы неверную пару.
> "%ROOT%\.channel" echo %CHANNEL%

call "%ROOT%\installer\sign.cmd" "%ROOT%\DIIISPL.exe" || exit /b 1

echo Готово: %ROOT%\DIIISPL.exe
