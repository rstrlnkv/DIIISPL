@echo off
rem Автотест раскладок. Он по-настоящему переключает мониторы — около сорока
rem перестроений подряд, и это самая опасная вещь в дереве: 19.08.2026 такой
rem прогон увёл машину в BSOD 0x13A KERNEL_MODE_HEAP_CORRUPTION. Пользовательский
rem код не может испортить кучу ядра сам — это дефект графического драйвера,
rem но провоцируется он отсюда.
rem
rem Поэтому запуск требует подтверждения. Раньше файл начинал прогон сразу, и
rem предупреждение было только комментарием, который читают уже после; а CLAUDE.md
rem при этом обещал, что массовые прогоны идут «только с явного согласия» —
rem обещание, под которым в коде не стояло ничего.
rem
rem   run-tests.cmd            спросит и остановится, если не ответить
rem   run-tests.cmd --yes      согласие уже дано, вопроса не будет
rem
rem Без ответа прогон не начинается: запущенный не человеком (скриптом, задачей,
rem агентом) он читает пустой ввод и выходит — это и требуется.
setlocal enabledelayedexpansion

set AGREED=
for %%A in (%*) do if /i "%%~A"=="--yes" set AGREED=1

if not defined AGREED (
  echo.
  echo   Прогон по-настоящему переключит мониторы около сорока раз подряд.
  echo   Однажды такой прогон уронил машину в синий экран.
  echo   Сохраните работу.
  echo.
  set ANSWER=
  set /p ANSWER=  Напечатайте «да» и нажмите Enter, чтобы начать:
  if /i not "!ANSWER!"=="да" (
    echo   Отменено.
    exit /b 2
  )
)
set NETDIR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%NETDIR%\csc.exe" set NETDIR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
if not exist "%NETDIR%\csc.exe" (
  echo Не найден компилятор C# из .NET Framework.
  exit /b 1
)

"%NETDIR%\csc.exe" /nologo /target:exe /codepage:65001 ^
  /out:"%~dp0selftest.exe" ^
  /r:System.dll /r:System.Core.dll ^
  /r:"%NETDIR%\WPF\WindowsBase.dll" ^
  "%~dp0..\Native.cs" "%~dp0..\Edid.cs" "%~dp0..\Monitors.cs" ^
  "%~dp0..\Layout.cs" "%~dp0..\Applier.cs" "%~dp0..\Store.cs" "%~dp0..\ApplyGate.cs" ^
  "%~dp0Harness.cs"
if errorlevel 1 exit /b 1

echo.
echo Прогон займёт около пяти минут, экраны будут переключаться.
echo Отчёт также пишется в tests\selftest-log.txt
echo.
"%~dp0selftest.exe" --agreed %*
set CODE=%ERRORLEVEL%

rem Убираем за собой: собранный selftest.exe — это сорок настоящих переключений
rem мониторов в одном файле. Оставленный лежать, он ждёт двойного щелчка, и
rem спрашивать согласия будет уже некому. Он же откажется работать без --agreed.
del "%~dp0selftest.exe" >nul 2>&1
exit /b %CODE%
