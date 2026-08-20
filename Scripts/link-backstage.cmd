@echo off
rem Связать приватное из .backstage с рабочим деревом.
rem
rem Настоящие символьные ссылки на Windows требуют прав администратора или режима
rem разработчика, поэтому здесь их нет: папки связываются соединением (mklink /J),
rem файлы — жёсткой ссылкой (mklink /H). И то, и другое обычному пользователю
rem доступно, а выглядит и работает так же: один файл, два имени.
rem
rem Запускать после git submodule update --init, а также каждый раз, когда в
rem .backstage появляется что-то новое.
setlocal
set ROOT=%~dp0..
set BACK=%ROOT%\.backstage

if not exist "%BACK%" (
  echo Нет папки .backstage. Сначала: git submodule update --init
  exit /b 1
)
dir /b "%BACK%" 2>nul | findstr . >nul
if errorlevel 1 (
  echo Папка .backstage пуста — приватный репозиторий не выкачан.
  echo Это нормально для тех, у кого нет к нему доступа: программа собирается и так.
  exit /b 0
)

call :file CLAUDE.md
call :dir  .claude
call :dir  docs

echo Готово.
exit /b 0

rem ---------------------------------------------------------------- файл
:file
if not exist "%BACK%\%~1" exit /b 0
if exist "%ROOT%\%~1" del /f /q "%ROOT%\%~1"
mklink /H "%ROOT%\%~1" "%BACK%\%~1" >nul && echo   %~1
exit /b 0

rem ---------------------------------------------------------------- папка
:dir
if not exist "%BACK%\%~1\" exit /b 0
if exist "%ROOT%\%~1\" rmdir "%ROOT%\%~1" 2>nul
mklink /J "%ROOT%\%~1" "%BACK%\%~1" >nul && echo   %~1\
exit /b 0
