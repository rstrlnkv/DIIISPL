@echo off
rem Checks the pure update-version text logic (VersionText.cs): parsing, newer-than, display cut.
rem Compiles only VersionText.cs and the check; touches no monitors, no network.
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo C# compiler of .NET Framework not found.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0updater-check.exe" ^
  "%~dp0..\src\VersionText.cs" "%~dp0UpdaterCheck.cs"
if errorlevel 1 exit /b 1

"%~dp0updater-check.exe"
set RC=%ERRORLEVEL%
del "%~dp0updater-check.exe"
exit /b %RC%
