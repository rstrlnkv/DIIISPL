@echo off
rem Checks the full version (AssemblyInformationalVersion), the About/Licenses strings
rem in both languages and the bundled licence files. Text checks only: no monitors,
rem no network, no app types.
setlocal
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo C# compiler of .NET Framework not found.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0version-check.exe" ^
  "%~dp0VersionCheck.cs"
if errorlevel 1 exit /b 1

"%~dp0version-check.exe"
set RC=%ERRORLEVEL%
del "%~dp0version-check.exe"
exit /b %RC%
