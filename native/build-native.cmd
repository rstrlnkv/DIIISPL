@echo off
rem Сборка DIIISPL.ExplorerTAP.dll — библиотеки оформления панели задач.
rem Требуется Visual Studio Build Tools 2026 с рабочей нагрузкой C++ и Windows SDK.
setlocal

set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if not exist "%VSWHERE%" (
  echo Не найден vswhere: нужны Visual Studio Build Tools с C++.
  exit /b 1
)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VSDIR=%%i
if not defined VSDIR (
  echo Не найдены средства C++. Установите рабочую нагрузку "Разработка на C++".
  exit /b 1
)

set MSBUILD=%VSDIR%\MSBuild\Current\Bin\MSBuild.exe
set VCPKG=%VSDIR%\VC\vcpkg\vcpkg.exe
set NUGET=%~dp0nuget.exe

rem Короткий каталог для промежуточных файлов: заголовки C++/WinRT легко
rem перешагивают ограничение в 260 символов, если собирать на месте.
set BUILDDIR=%TEMP%\diiispl-native
if not exist "%BUILDDIR%" mkdir "%BUILDDIR%"

if not exist "%NUGET%" (
  echo Качаю nuget.exe...
  powershell -NoProfile -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile '%NUGET%' -UseBasicParsing" || exit /b 1
)

echo === Пакеты NuGet ===
"%NUGET%" install "%~dp0ExplorerTAP\packages.config" -OutputDirectory "%~dp0packages" -NonInteractive || exit /b 1

echo === Зависимости vcpkg (detours, wil) ===
pushd "%~dp0ExplorerTAP"
set VCPKG_DISABLE_METRICS=1
"%VCPKG%" install --triplet x64-windows-static --x-install-root="%BUILDDIR%\vcpkg_installed" || (popd & exit /b 1)
popd

echo === Сборка библиотеки ===
> "%BUILDDIR%\paths.props" echo ^<?xml version="1.0" encoding="utf-8"?^>
>> "%BUILDDIR%\paths.props" echo ^<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^>
>> "%BUILDDIR%\paths.props" echo   ^<ItemDefinitionGroup^>
>> "%BUILDDIR%\paths.props" echo     ^<ClCompile^>^<AdditionalIncludeDirectories^>%BUILDDIR%\vcpkg_installed\x64-windows-static\include;%%(AdditionalIncludeDirectories)^</AdditionalIncludeDirectories^>^</ClCompile^>
>> "%BUILDDIR%\paths.props" echo     ^<Link^>^<AdditionalLibraryDirectories^>%BUILDDIR%\vcpkg_installed\x64-windows-static\lib;%%(AdditionalLibraryDirectories)^</AdditionalLibraryDirectories^>^</Link^>
>> "%BUILDDIR%\paths.props" echo   ^</ItemDefinitionGroup^>
>> "%BUILDDIR%\paths.props" echo ^</Project^>

"%MSBUILD%" "%~dp0ExplorerTAP\ExplorerTAP.vcxproj" /nologo /v:minimal ^
  /p:Configuration=Release /p:Platform=x64 ^
  /p:ForceImportAfterCppTargets="%BUILDDIR%\paths.props" ^
  /p:IntDir=%BUILDDIR%\obj\ ^
  /p:OutDir=%BUILDDIR%\bin\ ^
  /p:TargetName=DIIISPL.ExplorerTAP || exit /b 1

copy /y "%BUILDDIR%\bin\DIIISPL.ExplorerTAP.dll" "%~dp0..\DIIISPL.ExplorerTAP.dll" >nul || exit /b 1
echo Готово: %~dp0..\DIIISPL.ExplorerTAP.dll
