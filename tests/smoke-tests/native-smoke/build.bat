@echo off
setlocal

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
	echo ERROR: vswhere.exe not found at "%VSWHERE%".
	exit /b 1
)

for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSINSTALLPATH=%%I"

if not defined VSINSTALLPATH (
	echo ERROR: No Visual Studio installation with C++ tools was found.
	exit /b 1
)

set "VSDEVCMD=%VSINSTALLPATH%\Common7\Tools\VsDevCmd.bat"
if not exist "%VSDEVCMD%" (
	echo ERROR: VsDevCmd.bat not found at "%VSDEVCMD%".
	exit /b 1
)

call "%VSDEVCMD%" -no_logo -arch=amd64
if errorlevel 1 exit /b %ERRORLEVEL%

cd /d %~dp0
if exist build\win-x64 rmdir /s /q build\win-x64
echo === CONFIGURE ===
cmake --preset win-x64
if errorlevel 1 exit /b %ERRORLEVEL%
echo === BUILD ===
cmake --build build\win-x64
if errorlevel 1 exit /b %ERRORLEVEL%
echo === RUN ===
build\win-x64\native-smoke.exe
echo === DONE (exit code: %ERRORLEVEL%) ===
