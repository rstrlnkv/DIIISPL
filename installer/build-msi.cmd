@echo off
rem Сборка установщика DIIIX.msi. Требуется только сеть при первом запуске:
rem набор WiX подтягивается пакетом, ничего ставить в систему не нужно.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..
set VERSION=1.0.0

for %%F in ("%ROOT%\DIIIX.exe" "%ROOT%\DIIIX.ico" "%ROOT%\LICENSE" "%ROOT%\README.md") do (
  if not exist %%F (
    echo Нет файла %%F — сначала соберите программу: build.cmd
    exit /b 1
  )
)
if not exist "%ROOT%\DIIIX.ExplorerTAP.dll" (
  echo Нет DIIIX.ExplorerTAP.dll — соберите её: native\build-native.cmd
  exit /b 1
)

set WIX=%HERE%wix\wix\tools
if not exist "%WIX%\candle.exe" (
  echo === Набор WiX ===
  "%ROOT%\native\nuget.exe" install WiX -Version 3.14.1 -OutputDirectory "%HERE%wix" -NonInteractive -ExcludeVersion || exit /b 1
)

echo === Лицензия ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%make-license-rtf.ps1" -Source "%ROOT%\LICENSE" -Target "%HERE%LICENSE.rtf" || exit /b 1

echo === Сборка пакета ===
if not exist "%HERE%obj" mkdir "%HERE%obj"
"%WIX%\candle.exe" -nologo -arch x64 ^
  -dSourceDir="%ROOT%" -dLicenseRtf="%HERE%LICENSE.rtf" ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -out "%HERE%obj\DIIIX.wixobj" "%HERE%DIIIX.wxs" || exit /b 1

rem ICE60 ругается на шрифты в файлах без версии — к нашим файлам не относится.
"%WIX%\light.exe" -nologo ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -cultures:ru-RU ^
  -sice:ICE60 ^
  -out "%ROOT%\DIIIX-%VERSION%-x64.msi" "%HERE%obj\DIIIX.wixobj" || exit /b 1

echo.
echo Готово: %ROOT%\DIIIX-%VERSION%-x64.msi
