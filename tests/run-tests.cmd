@echo off
rem Автотест раскладок. ВНИМАНИЕ: он по-настоящему переключает мониторы —
rem около тридцати перестроек подряд. Сохраните работу перед запуском.
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo Не найден компилятор C# из .NET Framework.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0selftest.exe" ^
  /r:System.dll /r:System.Core.dll ^
  /r:"%NETDIR%\WPF\WindowsBase.dll" ^
  "%~dp0..\Native.cs" "%~dp0..\Edid.cs" "%~dp0..\Monitors.cs" ^
  "%~dp0..\Layout.cs" "%~dp0..\Applier.cs" ^
  "%~dp0..\ExplorerTap.cs" "%~dp0..\TaskbarSettings.cs" "%~dp0..\TaskbarWatcher.cs" ^
  "%~dp0Harness.cs"
if errorlevel 1 exit /b 1

echo.
echo Прогон займёт около пяти минут, экраны будут переключаться.
echo Отчёт также пишется в tests\selftest-log.txt
echo.
"%~dp0selftest.exe" %*
