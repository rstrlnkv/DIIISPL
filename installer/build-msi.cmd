@echo off
rem Сборка установщика DIIISPL.msi. Требуется только сеть при первом запуске:
rem набор WiX подтягивается пакетом, ничего ставить в систему не нужно.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..
set VERSION=1.0.1
rem На сборщике версию задаёт тег — он и есть источник правды для релиза.
if defined DIIISPL_VERSION set VERSION=%DIIISPL_VERSION%

for %%F in ("%ROOT%\DIIISPL.exe" "%ROOT%\DIIISPL.ico" "%ROOT%\LICENSE" "%ROOT%\README.md") do (
  if not exist %%F (
    echo Нет файла %%F — сначала соберите программу: build.cmd
    exit /b 1
  )
)
if not exist "%ROOT%\DIIISPL.ExplorerTAP.dll" (
  echo Нет DIIISPL.ExplorerTAP.dll — соберите её: native\build-native.cmd
  exit /b 1
)

set WIX=%HERE%wix\wix\tools
if not exist "%WIX%\candle.exe" (
  echo === Набор WiX ===
  "%ROOT%\native\nuget.exe" install WiX -Version 3.14.1 -OutputDirectory "%HERE%wix" -NonInteractive -ExcludeVersion || exit /b 1
)

rem Двоичные файлы раскладываются копиями и подписываются уже там. На месте их
rem подписать нельзя: библиотека оформления загружена в проводник, а программа
rem может быть запущена — signtool упрётся в занятый файл. Заодно сборка не правит
rem то, что лежит в рабочем каталоге.
echo === Подпись файлов программы ===
if not exist "%HERE%obj\stage" mkdir "%HERE%obj\stage"
copy /y "%ROOT%\DIIISPL.exe" "%HERE%obj\stage\" >nul || exit /b 1
copy /y "%ROOT%\DIIISPL.ExplorerTAP.dll" "%HERE%obj\stage\" >nul || exit /b 1
call "%HERE%sign.cmd" "%HERE%obj\stage\DIIISPL.exe" "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll" || exit /b 1

echo === Картинки окон ===
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 /out:"%HERE%obj\makeart.exe" ^
  /r:System.dll /r:System.Drawing.dll "%HERE%MakeArt.cs" || exit /b 1
"%HERE%obj\makeart.exe" "%HERE%obj" || exit /b 1

echo === Лицензия ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%make-license-rtf.ps1" -Source "%ROOT%\LICENSE" -Target "%HERE%LICENSE.rtf" || exit /b 1

echo === Сборка пакета ===
if not exist "%HERE%obj" mkdir "%HERE%obj"
"%WIX%\candle.exe" -nologo -arch x64 ^
  -dVersion=%VERSION% -dSourceDir="%ROOT%" -dBinDir="%HERE%obj\stage" -dLicenseRtf="%HERE%LICENSE.rtf" -dArtDir="%HERE%obj" ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -out "%HERE%obj\\" "%HERE%DIIISPL.wxs" "%HERE%DIIISPLUI.wxs" || exit /b 1

rem Отключённые проверки:
rem   ICE60 — про шрифты в файлах без версии, к нашим файлам не относится;
rem   ICE38, ICE43, ICE57 — считают меню «Пуск» пользовательским и требуют ключа
rem     в HKCU. Пакет ставится на всю машину (ALLUSERS=1), меню там общее, и ключ
rem     в HKLM для него верный — статические проверки этого не учитывают.
"%WIX%\light.exe" -nologo ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -cultures:ru-RU ^
  -sice:ICE60 -sice:ICE38 -sice:ICE43 -sice:ICE57 ^
  -out "%ROOT%\DIIISPL-%VERSION%-x64.msi" "%HERE%obj\DIIISPL.wixobj" "%HERE%obj\DIIISPLUI.wixobj" || exit /b 1

echo === Подпись пакета ===
call "%HERE%sign.cmd" "%ROOT%\DIIISPL-%VERSION%-x64.msi" || exit /b 1

echo.
echo Готово: %ROOT%\DIIISPL-%VERSION%-x64.msi
