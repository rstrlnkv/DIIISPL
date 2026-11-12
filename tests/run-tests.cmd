@echo off
rem Layout autotest. It really switches monitors, about forty
rem rebuilds in a row, and it is the most dangerous thing in the tree: on 19.08.2026 such a
rem run took the machine to a BSOD 0x13A KERNEL_MODE_HEAP_CORRUPTION. User
rem code cannot corrupt the kernel heap by itself, that is a graphics driver defect,
rem but it is provoked from here.
rem
rem So the run requires confirmation. Before, the file started the run at once, and
rem the warning was only a comment, read afterwards; and CLAUDE.md
rem promised that mass runs go "only with explicit consent",
rem a promise with nothing behind it in the code.
rem
rem   run-tests.cmd            asks, and stops if not answered
rem   run-tests.cmd --yes      consent already given, no question
rem
rem Without an answer the run does not start: when started by something other than a person (a script, a task,
rem an agent) it reads empty input and exits, which is what is required.
setlocal enabledelayedexpansion

set AGREED=
for %%A in (%*) do if /i "%%~A"=="--yes" set AGREED=1

if not defined AGREED (
  echo.
  echo   The run will really switch monitors about forty times in a row.
  echo   Once such a run crashed the machine into a blue screen.
  echo   Save your work.
  echo.
  set ANSWER=
  set /p ANSWER=  Напечатайте «да» и нажмите Enter, чтобы начать:
  if /i not "!ANSWER!"=="да" (
    echo   Cancelled.
    exit /b 2
  )
)
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo C# compiler from .NET Framework not found.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0selftest.exe" ^
  /r:System.dll /r:System.Core.dll ^
  /r:"%NETDIR%\WPF\WindowsBase.dll" ^
  "%~dp0..\src\Lang.cs" "%~dp0..\src\LangFile.cs" "%~dp0..\src\Settings.cs" "%~dp0..\src\Text.cs" ^
  "%~dp0..\src\Native.cs" "%~dp0..\src\Edid.cs" "%~dp0..\src\Monitors.cs" ^
  "%~dp0..\src\Layout.cs" "%~dp0..\src\Applier.cs" "%~dp0..\src\Store.cs" "%~dp0..\src\ApplyGate.cs" ^
  "%~dp0Harness.cs"
if errorlevel 1 exit /b 1

echo.
echo The run takes about five minutes, the screens will be switching.
echo The report is also written to tests\selftest-log.txt
echo.
"%~dp0selftest.exe" --agreed %*
set CODE=%ERRORLEVEL%

rem Clean up after ourselves: the built selftest.exe is forty real monitor
rem switches in one file. Left lying around, it waits for a double click, and
rem there will be nobody to ask for consent. It also refuses to work without --agreed.
del "%~dp0selftest.exe" >nul 2>&1
exit /b %CODE%
