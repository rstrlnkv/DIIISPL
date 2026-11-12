@echo off
rem Sign the files passed in. The certificate thumbprint is taken from sign.thumbprint
rem next to this script or from the DIIISPL_SIGN_THUMBPRINT variable.
rem
rem Without a certificate, signing is silently skipped: the build must not break for
rem anyone who lacks one, and everyone lacks it except the key owner.
setlocal enabledelayedexpansion

set THUMB=
if exist "%~dp0sign.thumbprint" set /p THUMB=<"%~dp0sign.thumbprint"
if not defined THUMB set THUMB=%DIIISPL_SIGN_THUMBPRINT%
if not defined THUMB (
  echo Signing skipped: no certificate set.
  exit /b 0
)

rem signtool comes with the Windows SDK. Searching by a path mask is not possible, since dir does not
rem expand an asterisk in the middle; we search by name across the whole tree and pick x64,
rem and the last in sort order is the newest SDK release.
set SIGNTOOL=
for /f "delims=" %%I in ('dir /b /s /o:n "%ProgramFiles(x86)%\Windows Kits\10\bin\signtool.exe" 2^>nul ^| findstr /i "\\x64\\"') do set SIGNTOOL=%%I
if not defined SIGNTOOL (
  echo Signing skipped: signtool.exe from the Windows SDK not found.
  exit /b 0
)

rem The timestamp is required: without it the signature stops being valid
rem on the day the certificate expires, even for files released long ago.
rem
rem The description (/d) is visible in the User Account Control window, to everyone, including those
rem who got the English install. There is one signature per file and it cannot be
rem translated, so here is only the program name, which has no language.
"%SIGNTOOL%" sign /q /sha1 %THUMB% /fd SHA256 /td SHA256 ^
  /tr http://timestamp.digicert.com ^
  /d "DIIISPL" ^
  %* || exit /b 1

exit /b 0
