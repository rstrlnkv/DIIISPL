@echo off
rem Сборка установщика DIIISPL.msi. Требуется только сеть при первом запуске:
rem набор WiX подтягивается пакетом, ничего ставить в систему не нужно.
rem
rem   build-msi.cmd        стабильный пакет
rem   build-msi.cmd dev    пакет канала Dev — с библиотекой оформления панели задач
rem
rem Канал должен совпадать с тем, которым собрана программа: стабильная сборка
rem библиотеку не использует, и класть её в пакет незачем.
setlocal
set HERE=%~dp0
set ROOT=%HERE%..
set VERSION=0.1.0
rem На сборщике версию задаёт тег — он и есть источник правды для релиза.
if defined DIIISPL_VERSION set VERSION=%DIIISPL_VERSION%

set CHANNEL=%1
if not defined CHANNEL set CHANNEL=%DIIISPL_CHANNEL%
if /i "%CHANNEL%"=="dev" (set CHANNEL=dev) else (set CHANNEL=stable)
echo === Пакет канала %CHANNEL% ===

if not exist "%ROOT%\DIIISPL.exe" (
  echo Нет DIIISPL.exe — сначала соберите программу: build.cmd %CHANNEL%
  exit /b 1
)

rem Канал программы и канал пакета обязаны совпадать. Прежде здесь верили на
rem слово: собрал build.cmd dev, потом по привычке build-msi.cmd без аргумента —
rem и выходил «стабильный» пакет с Dev-программой. В окне значок DEV и раздел
rem настроек панели задач, а библиотеки оформления в пакете нет: раздел не
rem работает, и объяснить это человеку нечем. Собиралось это молча, до конца.
set BUILTCHANNEL=
if exist "%ROOT%\.channel" set /p BUILTCHANNEL=<"%ROOT%\.channel"
for /f "tokens=* delims= " %%C in ("%BUILTCHANNEL%") do set BUILTCHANNEL=%%C
if not defined BUILTCHANNEL (
  echo Неизвестно, каким каналом собрана программа. Пересоберите: build.cmd %CHANNEL%
  exit /b 1
)
if /i not "%BUILTCHANNEL%"=="%CHANNEL%" (
  echo Программа собрана каналом %BUILTCHANNEL%, а пакет просят каналом %CHANNEL%.
  echo Пересоберите программу: build.cmd %CHANNEL%
  exit /b 1
)
rem Эти три собирать нечем — они просто лежат в репозитории. Прежний совет
rem «соберите программу» отправлял человека делать то, что не помогает.
for %%F in ("%ROOT%\DIIISPL.ico" "%ROOT%\LICENSE" "%ROOT%\README.md") do (
  if not exist %%F (
    echo Нет файла %%F — он должен лежать в корне репозитория.
    exit /b 1
  )
)
if "%CHANNEL%"=="dev" if not exist "%ROOT%\DIIISPL.ExplorerTAP.dll" (
  echo Нет DIIISPL.ExplorerTAP.dll — соберите её: native\build-native.cmd
  exit /b 1
)

rem nuget.exe в репозиторий не кладётся, и качает его build-native.cmd — а тот
rem на стабильном канале не запускается вовсе. На своей машине файл оставался от
rem прежних сборок Dev, и нехватка не всплывала; на чистом сборщике стабильный
rem выпуск падал здесь. Качаем сами, если его нет.
set NUGET=%ROOT%\native\nuget.exe
if not exist "%NUGET%" (
  echo === nuget.exe ===
  powershell -NoProfile -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile '%NUGET%' -UseBasicParsing" || exit /b 1
)

set WIX=%HERE%wix\wix\tools
if not exist "%WIX%\candle.exe" (
  echo === Набор WiX ===
  "%NUGET%" install WiX -Version 3.14.1 -OutputDirectory "%HERE%wix" -NonInteractive -ExcludeVersion || exit /b 1
)

rem Двоичные файлы раскладываются копиями и подписываются уже там. На месте их
rem подписать нельзя: библиотека оформления загружена в проводник, а программа
rem может быть запущена — signtool упрётся в занятый файл. Заодно сборка не правит
rem то, что лежит в рабочем каталоге.
echo === Подпись файлов программы ===
if not exist "%HERE%obj\stage" mkdir "%HERE%obj\stage"
copy /y "%ROOT%\DIIISPL.exe" "%HERE%obj\stage\" >nul || exit /b 1
rem Библиотека — только в Dev. Прошлый её экземпляр из stage убирается: иначе
rem стабильный пакет собрался бы с файлом, оставшимся от предыдущей сборки.
if "%CHANNEL%"=="dev" (
  copy /y "%ROOT%\DIIISPL.ExplorerTAP.dll" "%HERE%obj\stage\" >nul || exit /b 1
  call "%HERE%sign.cmd" "%HERE%obj\stage\DIIISPL.exe" "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll" || exit /b 1
) else (
  if exist "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll" del /q "%HERE%obj\stage\DIIISPL.ExplorerTAP.dll"
  call "%HERE%sign.cmd" "%HERE%obj\stage\DIIISPL.exe" || exit /b 1
)

echo === Картинки окон ===
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 /out:"%HERE%obj\makeart.exe" ^
  /r:System.dll /r:System.Drawing.dll "%HERE%MakeArt.cs" || exit /b 1
"%HERE%obj\makeart.exe" "%HERE%obj" || exit /b 1

echo === Лицензия ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%make-license-rtf.ps1" -Source "%ROOT%\LICENSE" -Target "%HERE%LICENSE.rtf" || exit /b 1

rem Код продукта выводится из номера версии, а не берётся случайным. Пакет
rem собирается по разу на язык, и перевод внутрь встаёт разницей между сборками:
rem случайные коды сделали бы «переводом» подмену всего продукта. Одна и та же
rem версия даёт один и тот же код, разные версии — разные, и замена прежней
rem версии новой продолжает работать.
for /f %%G in ('powershell -NoProfile -Command "$b=[Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes('DIIISPL %VERSION% %CHANNEL%')); ([guid][byte[]]$b).ToString().ToUpperInvariant()"') do set PRODUCTCODE=%%G
if not defined PRODUCTCODE (
  echo Не удалось вычислить код продукта.
  exit /b 1
)

echo === Сборка пакета ===
if not exist "%HERE%obj" mkdir "%HERE%obj"
"%WIX%\candle.exe" -nologo -arch x64 ^
  -dVersion=%VERSION% -dChannel=%CHANNEL% -dProductCode=%PRODUCTCODE% -dSourceDir="%ROOT%" -dBinDir="%HERE%obj\stage" -dLicenseRtf="%HERE%LICENSE.rtf" -dArtDir="%HERE%obj" ^
  -ext WixUtilExtension -ext WixUIExtension ^
  -out "%HERE%obj\\" "%HERE%DIIISPL.wxs" "%HERE%DIIISPLUI.wxs" || exit /b 1

rem Отключённые проверки:
rem   ICE60 — про шрифты в файлах без версии, к нашим файлам не относится;
rem   ICE38, ICE43, ICE57 — считают меню «Пуск» пользовательским и требуют ключа
rem     в HKCU. Пакет ставится на всю машину (ALLUSERS=1), меню там общее, и ключ
rem     в HKLM для него верный — статические проверки этого не учитывают.
rem
rem Языков два, и пакет собирается дважды: английская сборка становится файлом
rem для скачивания, русская — только заготовкой, из которой берётся разница.
rem Английский основной потому, что пакет один на весь свет: язык, которого в нём
rem нет, оставляет человека с чем-то читаемым, а не с незнакомыми буквами.
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

echo === Переводы внутрь пакета ===
rem -t language: сравниваются только тексты. Без этого разницей считались бы и
rem файлы программы, одинаковые в обеих сборках, — перевод переписывал бы их
rem поверх самих себя.
"%WIX%\torch.exe" -nologo -p -t language "%MSI%" "%HERE%obj\ru-ru.msi" -out "%HERE%obj\ru-ru.mst" || exit /b 1
powershell -NoProfile -ExecutionPolicy Bypass -File "%HERE%embed-lang.ps1" -Msi "%MSI%" -Transform 1049="%HERE%obj\ru-ru.mst" || exit /b 1

echo === Подпись пакета ===
call "%HERE%sign.cmd" "%MSI%" || exit /b 1

echo.
echo Готово: %MSI%
