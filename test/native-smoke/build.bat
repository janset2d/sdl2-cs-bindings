@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -no_logo -arch=amd64
cd /d %~dp0
if exist build\win-x64 rmdir /s /q build\win-x64
echo === CONFIGURE ===
cmake --preset win-x64
echo === BUILD ===
cmake --build build\win-x64
echo === RUN ===
build\win-x64\native-smoke.exe
echo === DONE (exit code: %ERRORLEVEL%) ===
