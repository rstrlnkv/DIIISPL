@echo off
rem Rebuild the program icon: tools\MakeIcon.cs -> src\DIIISPL.ico
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo C# compiler from .NET Framework not found.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%TEMP%\diiispl-makeicon.exe" ^
  /r:System.dll /r:System.Drawing.dll ^
  "%~dp0MakeIcon.cs" || exit /b 1

"%TEMP%\diiispl-makeicon.exe" "%~dp0..\src\DIIISPL.ico" %1
