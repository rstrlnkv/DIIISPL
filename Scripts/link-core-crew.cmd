@echo off
rem Связать приватное из Core Crew с рабочим деревом.
rem
rem Настоящие символьные ссылки на Windows требуют прав администратора или
rem режима разработчика, поэтому здесь их нет: папки связываются соединением
rem (mklink /J), файлы — жёсткой ссылкой (mklink /H). И то, и другое обычному
rem пользователю доступно, а работает так же: один файл, два имени.
rem
rem Жёсткая ссылка не пересекает тома: Core Crew обязан лежать на том же диске.
rem
rem Запускать после клонирования Core Crew рядом, а также всякий раз, когда
rem ссылка порвалась: редактор, заменяющий файл целиком вместо записи в него,
rem жёсткую ссылку разрывает.
rem
rem Прежде это же делал link-backstage.cmd — из подмодуля .backstage, которого
rem больше нет: приватная половина переехала в Core Crew 05.09.2026.
setlocal
set ROOT=%~dp0..
set CC=%ROOT%\..\Core-Crew\projects\DIIISPL

if not exist "%CC%\" (
  echo Нет Core Crew рядом с этим деревом. Ожидается:
  echo   %CC%
  echo Поставить: git clone https://github.com/rstrlnkv/Core-Crew.git
  exit /b 1
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
rem Файлы знания. Без этой ссылки десять общих ролей читают «файла нет»,
rem и четыре из них при этом молча останавливаются.
call :dir  "diiispl-private\crew"            ".claude\crew"
call :dir  "plugins\diiispl-crew\agents"     ".claude\agents"
call :dir  "plugins\diiispl-crew"            "plugins\diiispl-crew"
call :file ".claude-plugin\marketplace.json" ".claude-plugin\marketplace.json"

echo.
echo Готово. Проверить: plugins\diiispl-crew\check-in-step.cmd
exit /b 0

rem ------------------------------------------------------- файл: жёсткая ссылка
:file
if not exist "%CC%\%~1" exit /b 0
if exist "%ROOT%\%~2" del /f /q "%ROOT%\%~2"
mklink /H "%ROOT%\%~2" "%CC%\%~1" >nul && echo   %~2
exit /b 0

rem ------------------------------------------------------- папка: соединение
:dir
if not exist "%CC%\%~1\" exit /b 0
rem rmdir снимает только соединение; настоящую папку с файлами он не тронет,
rem и это нарочно: молча стереть чужие файлы хуже, чем не поставить ссылку.
if exist "%ROOT%\%~2\" rmdir "%ROOT%\%~2" 2>nul
mklink /J "%ROOT%\%~2" "%CC%\%~1" >nul && echo   %~2\
exit /b 0
