@echo off
setlocal enabledelayedexpansion

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

rem Locate newest editbin.exe (path changes whenever Visual Studio updates)
set "EDITBIN="
for /f "usebackq delims=" %%i in (`""!VSWHERE!" -latest -find "VC\Tools\MSVC\**\bin\Hostx64\x64\editbin.exe""`) do set "EDITBIN=%%i"
if not defined EDITBIN (
    echo ERROR: editbin.exe not found. Install the "Desktop development with C++" workload in Visual Studio.
    exit /b 1
)

rem Locate Inno Setup compiler
set "ISCC="
for /f "tokens=2,*" %%a in ('reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1" /v InstallLocation 2^>nul') do set "ISCC=%%bISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"

cd ..\Apollo
rd /S /Q bin
rd /S /Q obj
dotnet clean
dotnet publish -r win-x64 -c Release
"%EDITBIN%" /subsystem:windows bin\Release\net5.0\win-x64\publish\Apollo.exe

echo.

cd ..\ApolloUpdate
rd /S /Q bin
rd /S /Q obj
dotnet clean
dotnet publish -r win-x64 -c Release
"%EDITBIN%" /subsystem:windows bin\Release\net5.0\win-x64\publish\ApolloUpdate.exe

echo.
echo Merging...

cd ..
rd /S /Q Build >nul 2>&1
mkdir Build
cd Build

mkdir Apollo
mkdir M4L
mkdir Update

robocopy ..\Apollo\bin\Release\net5.0\win-x64\publish Apollo /E >nul 2>&1
robocopy ..\ApolloUpdate\bin\Release\net5.0\win-x64\publish Update /E >nul 2>&1

robocopy ..\M4L M4L *.amxd >nul 2>&1

echo Creating Windows Installer...

cd ..
rd /S /Q Dist >nul 2>&1
mkdir Dist

if not defined ISCC (
    echo.
    echo Build\ is ready, but Inno Setup 6 was not found so the installer was not built.
    echo Install it from https://jrsoftware.org/isdl.php then either re-run this script,
    echo or open Publish\Apollo.iss in the Inno Setup IDE and press Compile.
    exit /b 1
)
"%ISCC%" /q Publish\Apollo.iss

echo Done.
