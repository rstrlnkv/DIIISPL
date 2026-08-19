@echo off
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo Не найден компилятор C# из .NET Framework.
  exit /b 1
)
set WPF=%NETDIR%\WPF

"%NETDIR%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
  /out:"%~dp0Triptych.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Xml.dll ^
  /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%NETDIR%\System.Xaml.dll" ^
  /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" ^
  "%~dp0Native.cs" "%~dp0Edid.cs" "%~dp0Monitors.cs" "%~dp0Layout.cs" "%~dp0Applier.cs" ^
  "%~dp0Store.cs" "%~dp0Hotkey.cs" "%~dp0Cli.cs" "%~dp0Theme.cs" "%~dp0Fluent.cs" ^
  "%~dp0MonitorCanvas.cs" "%~dp0ConfirmDialog.cs" "%~dp0MainWindow.cs" "%~dp0App.cs"

if errorlevel 1 exit /b 1
echo Готово: %~dp0Triptych.exe
