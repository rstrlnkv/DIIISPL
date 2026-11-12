@echo off
rem Build DIIISPL.ExplorerTAP.dll, the taskbar styling library.
rem Requires Visual Studio Build Tools 2026 with the C++ workload and Windows SDK.
setlocal

set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if not exist "%VSWHERE%" (
  echo vswhere not found: Visual Studio Build Tools with C++ are required.
  exit /b 1
)
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VSDIR=%%i
if not defined VSDIR (
  echo C++ tools not found. Install the "Desktop development with C++" workload.
  exit /b 1
)

set MSBUILD=%VSDIR%\MSBuild\Current\Bin\MSBuild.exe
set VCPKG=%VSDIR%\VC\vcpkg\vcpkg.exe
set NUGET=%~dp0nuget.exe

rem Short directory for intermediate files: C++/WinRT headers easily
rem exceed the 260 character limit when built in place.
set BUILDDIR=%TEMP%\diiispl-native
if not exist "%BUILDDIR%" mkdir "%BUILDDIR%"

if not exist "%NUGET%" (
  echo Downloading nuget.exe...
  powershell -NoProfile -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile '%NUGET%' -UseBasicParsing" || exit /b 1
)

echo === NuGet packages ===
"%NUGET%" install "%~dp0ExplorerTAP\packages.config" -OutputDirectory "%~dp0packages" -NonInteractive || exit /b 1

echo === vcpkg dependencies (detours, wil) ===
pushd "%~dp0ExplorerTAP"
set VCPKG_DISABLE_METRICS=1
"%VCPKG%" install --triplet x64-windows-static --x-install-root="%BUILDDIR%\vcpkg_installed" || (popd & exit /b 1)
popd

echo === Building the library ===
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
echo Done: %~dp0..\DIIISPL.ExplorerTAP.dll
