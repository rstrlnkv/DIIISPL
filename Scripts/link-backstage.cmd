@echo off
rem Связать приватное из .backstage с рабочим деревом.
rem
rem Настоящие символьные ссылки на Windows требуют прав администратора или
rem режима разработчика, поэтому здесь их нет: папки связываются соединением
rem (mklink /J), файлы — жёсткой ссылкой (mklink /H). И то, и другое обычному
rem пользователю доступно, а работает так же: один файл, два имени.
rem
rem Запускать после git submodule update --init, а также всякий раз, когда
rem в .backstage появилось что-то новое или ссылка порвалась: редактор,
rem заменяющий файл целиком вместо записи в него, жёсткую ссылку разрывает.
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

echo === Документы ===
call :file "diiispl-private\CLAUDE.md"       "CLAUDE.md"
call :file "diiispl-private\ARCHITECTURE.md" "ARCHITECTURE.md"
call :file "diiispl-private\VERSIONING.md"   "VERSIONING.md"
call :dir  "diiispl-private\docs"            "docs"

echo === Команда ===
if not exist "%ROOT%\.claude"        mkdir "%ROOT%\.claude"
if not exist "%ROOT%\plugins"        mkdir "%ROOT%\plugins"
if not exist "%ROOT%\.claude-plugin" mkdir "%ROOT%\.claude-plugin"
call :dir  "plugins\diiispl-crew\agents"     ".claude\agents"
call :dir  "plugins\diiispl-crew"            "plugins\diiispl-crew"
call :file ".claude-plugin\marketplace.json" ".claude-plugin\marketplace.json"

echo.
echo Готово. Проверить: .backstage\plugins\diiispl-crew\check-in-step.cmd
exit /b 0

rem ------------------------------------------------------- файл: жёсткая ссылка
:file
if not exist "%BACK%\%~1" exit /b 0
if exist "%ROOT%\%~2" del /f /q "%ROOT%\%~2"
mklink /H "%ROOT%\%~2" "%BACK%\%~1" >nul && echo   %~2
exit /b 0

rem ------------------------------------------------------- папка: соединение
:dir
if not exist "%BACK%\%~1\" exit /b 0
rem rmdir снимает только соединение; настоящую папку с файлами он не тронет,
rem и это нарочно: молча стереть чужие файлы хуже, чем не поставить ссылку.
if exist "%ROOT%\%~2\" rmdir "%ROOT%\%~2" 2>nul
mklink /J "%ROOT%\%~2" "%BACK%\%~1" >nul && echo   %~2\
exit /b 0
