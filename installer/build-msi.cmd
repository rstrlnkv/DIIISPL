@echo off
rem Build the DIIISPL.msi installer. Network is needed only on first run:
rem the WiX toolset is pulled in as a package, nothing is installed into the system.
rem
rem   build-msi.cmd        stable package
rem   build-msi.cmd dev    Dev channel package, with the taskbar styling library
rem
rem The channel must match the one the program was built with: the stable build
rem does not use the library, so there is no point putting it in the package.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..
rem Default version: the first three parts of AssemblyVersion in AssemblyInfo.cs.
set VERSION=
for /f "tokens=2 delims=()" %%A in ('findstr /b /c:"[assembly: AssemblyVersion(" "%ROOT%\src\AssemblyInfo.cs"') do set VERSION=%%~A
if defined VERSION for /f "tokens=1-3 delims=." %%a in ("%VERSION%") do set VERSION=%%a.%%b.%%c
if not defined VERSION (
  echo Cannot read AssemblyVersion from AssemblyInfo.cs.
  exit /b 1
)
set CHANNEL=%1
rem Values from the environment are read with delayed expansion and checked before
rem they are ever expanded with percent signs: "set X=%VAR%" would run a "&" in VAR.
rem On success endlocal hands the checked values to the rest of the script.
setlocal EnableDelayedExpansion
rem On the build server the version comes from the tag, which is the source of truth for a release.
if defined DIIISPL_VERSION set "VERSION=!DIIISPL_VERSION!"
(set VERSION) 2>nul | findstr /r /x "VERSION=[0-9][0-9.]*" >nul || (
  echo Refused: the version must be digits and dots only.
  exit /b 1
)
if not defined CHANNEL set "CHANNEL=!DIIISPL_CHANNEL!"
if not defined CHANNEL set "CHANNEL=stable"
if /i "!CHANNEL!"=="dev" (set "CHANNEL=dev") else if /i "!CHANNEL!"=="stable" (set "CHANNEL=stable") else (
  echo Refused: the channel must be dev or stable.
  exit /b 1
)
endlocal & set "VERSION=%VERSION%" & set "CHANNEL=%CHANNEL%"
echo === Package, channel %CHANNEL% ===

if not exist "%ROOT%\DIIISPL.exe" (
  echo DIIISPL.exe not found. Build the program first: scripts\build.cmd %CHANNEL%
  exit /b 1
)

rem The program channel and the package channel must match. This used to be taken
rem on trust: build.cmd dev, then out of habit build-msi.cmd with no argument,
rem and a "stable" package came out with a Dev program. The window shows the DEV badge and the
rem taskbar settings section, but the styling library is not in the package: the section
rem does not work, and there is nothing to explain it to the user. It was built silently, to the end.
set BUILTCHANNEL=
if exist "%ROOT%\.channel" set /p BUILTCHANNEL=<"%ROOT%\.channel"
for /f "tokens=* delims= " %%C in ("%BUILTCHANNEL%") do set BUILTCHANNEL=%%C
if not defined BUILTCHANNEL (
  echo Cannot tell which channel the program was built with. Rebuild: scripts\build.cmd %CHANNEL%
  exit /b 1
)
if /i not "%BUILTCHANNEL%"=="%CHANNEL%" (
  echo The program was built with channel %BUILTCHANNEL%, but the package was requested with channel %CHANNEL%.
  echo Rebuild the program: scripts\build.cmd %CHANNEL%
  exit /b 1
)
rem These three cannot be built; they simply sit in the repository. The earlier advice
rem "build the program" sent people to do something that does not help.
for %%F in ("%ROOT%\src\DIIISPL.ico" "%ROOT%\LICENSE" "%ROOT%\README.md") do (
  if not exist %%F (
    echo Missing file %%F, it must be in the repository at this path.
    exit /b 1
  )
)
if "%CHANNEL%"=="dev" if not exist "%ROOT%\DIIISPL.ExplorerTAP.dll" (
  echo DIIISPL.ExplorerTAP.dll not found. Build it: native\build-native.cmd
  exit /b 1
)

rem nuget.exe is not kept in the repository, and build-native.cmd downloads it, but that
rem is not run on the stable channel at all. On a dev machine the file was left over from
rem earlier Dev builds, so the gap did not show; on a clean build server the stable
rem release failed here. We download it ourselves if it is missing.
set NUGET=%ROOT%\native\nuget.exe
if not exist "%NUGET%" (
  echo === nuget.exe ===
  powershell -NoProfile -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile '%NUGET%' -UseBasicParsing" || exit /b 1
)

set WIX=%HERE%wix\wix\tools
if not exist "%WIX%\candle.exe" (
  echo === WiX toolset ===
  "%NUGET%" install WiX -Version 3.14.1 -OutputDirectory "%HERE%wix" -NonInteractive -ExcludeVersion || exit /b 1
)

rem Binaries are laid out as copies and signed there. Signing them in place is
rem not possible: the styling library is loaded into Explorer, and the program
rem may be running, so signtool would hit a locked file. It also keeps the build
rem from modifying what is in the working directory.
echo === Signing program files ===
if not exist "%HERE%obj\stage" mkdir "%HERE%obj\stage"
copy /y "%ROOT%\DIIISPL.exe" "%HERE%obj\stage\" >nul || exit /b 1
rem The library is Dev only. Any earlier copy of it is removed from stage: otherwise
rem a stable package would be built with a file left over from the previous build.
if "%CHANNEL%"=="dev" (
  copy /y "%ROOT%\DIIISPL.ExplorerTAP.dll" "%HERE%obj\stage\" >nul || exit /b 1
  call "%HERE%sign.cmd" "%HERE%obj\stage\DIIISPL.exe" "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll" || exit /b 1
) else (
  if exist "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll" del /q "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll"
  call "%HERE%sign.cmd" "%HERE%obj\stage\DIIISPL.exe" || exit /b 1
)

echo === Window images ===
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 /out:"%HERE%obj\makeart.exe" ^
  /r:System.dll /r:System.Drawing.dll "%HERE%MakeArt.cs" || exit /b 1
"%HERE%obj\makeart.exe" "%HERE%obj" || exit /b 1

echo === Licence ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%make-license-rtf.ps1" -Source "%ROOT%\LICENSE" -Target "%HERE%LICENSE.rtf" || exit /b 1

rem The product code is set here, not as an asterisk in the markup. The package is built once
rem per language, and a translation embeds as the difference between builds: an asterisk would give
rem each build its own code, and a "translation" would first of all replace the whole
rem product.
rem
rem The code is still new on every build and not derived from the version number. It
rem was like that before, and it is right: the same code with the same version means
rem "the same product", and reinstalling the same release, for example
rem to repair a damaged one, would hit "already installed" instead of
rem just going through.
for /f %%G in ('powershell -NoProfile -Command "[guid]::NewGuid().ToString().ToUpperInvariant()"') do set PRODUCTCODE=%%G
if not defined PRODUCTCODE (
  echo Failed to compute the product code.
  exit /b 1
)

echo === Building the package ===
if not exist "%HERE%obj" mkdir "%HERE%obj"
"%WIX%\candle.exe" -nologo -arch x64 ^
  -dVersion=%VERSION% -dChannel=%CHANNEL% -dProductCode=%PRODUCTCODE% -dSourceDir="%ROOT%" -dBinDir="%HERE%obj\stage" -dLicenseRtf="%HERE%LICENSE.rtf" -dArtDir="%HERE%obj" ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -out "%HERE%obj\\" "%HERE%DIIISPL.wxs" "%HERE%DIIISPLUI.wxs" || exit /b 1

rem Disabled checks:
rem   ICE60: about fonts in unversioned files, does not apply to our files;
rem   ICE38, ICE43, ICE57: treat the Start menu as per-user and require a key
rem     in HKCU. The package installs machine-wide (ALLUSERS=1), the menu is shared there, and a key
rem     in HKLM is correct for it; the static checks do not account for this.
rem
rem There are two languages and the package is built twice: the English build becomes the file
rem to download, the Russian one is only a blank from which the difference is taken.
rem English is the base because there is one package for the whole world: a language it
rem lacks leaves the user with something readable, not with unfamiliar letters.
set MSI=%ROOT%\DIIISPL-%VERSION%-%CHANNEL%-x64.msi
"%WIX%\light.exe" -nologo ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -cultures:en-us -loc "%HERE%lang\en-us.wxl" ^
  -sice:ICE60 -sice:ICE38 -sice:ICE43 -sice:ICE57 ^
  -out "%MSI%" "%HERE%obj\DIIISPL.wixobj" "%HERE%obj\DIIISPLUI.wixobj" || exit /b 1

"%WIX%\light.exe" -nologo ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -cultures:ru-ru -loc "%HERE%lang\ru-ru.wxl" ^
  -sice:ICE60 -sice:ICE38 -sice:ICE43 -sice:ICE57 ^
  -out "%HERE%obj\ru-ru.msi" "%HERE%obj\DIIISPL.wixobj" "%HERE%obj\DIIISPLUI.wixobj" || exit /b 1

echo === Translations into the package ===
rem -t language sets the validation suite for a language transform. The program files
rem stay out of the difference not for that reason, but because in both builds they are byte for byte
rem the same.
rem
rem -serr f lets the transform change the database code page: the Russian build has
rem 1251 against English 1252. Windows applies such a transform even without this
rem permission, but everything else (manual install with TRANSFORMS, install
rem from a shared folder, repackaging) hits an error.
rem -t language sets the whole suite of checks and it cannot be changed within it: torch does not accept -t and
rem -serr together. The suite was written out by hand, and install broke
rem with error 1624, "Expected language 1033, found language 1049":
rem Windows refused to apply a transform built with other checks, although each
rem one looked reasonable on its own.
rem
rem Hence the cost: the transform does not allow changing the database code page (the Russian
rem build has 1251 against English 1252), and it cannot be applied by hand, via
rem TRANSFORMS, from an administrative install. A normal install is not
rem affected: Windows finds the transform by language code and applies it itself, which was
rem verified on a live machine.
"%WIX%\torch.exe" -nologo -p -t language "%MSI%" "%HERE%obj\ru-ru.msi" -out "%HERE%obj\ru-ru.mst" || exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%embed-lang.ps1" -Msi "%MSI%" -Transform 1049="%HERE%obj\ru-ru.mst" || exit /b 1

echo === Signing the package ===
call "%HERE%sign.cmd" "%MSI%" || exit /b 1

echo.
echo Done: %MSI%
