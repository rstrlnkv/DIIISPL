@echo off
rem Checks that CHANGELOG.md and CHANGELOG.ru.md parse and agree with each other.
rem Compiles only Changelog.cs and the check; touches no monitors, no network.
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo C# compiler of .NET Framework not found.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0changelog-check.exe" ^
  "%~dp0..\Changelog.cs" "%~dp0ChangelogCheck.cs"
if errorlevel 1 exit /b 1

"%~dp0changelog-check.exe"
set RC=%ERRORLEVEL%
del "%~dp0changelog-check.exe"
exit /b %RC%
