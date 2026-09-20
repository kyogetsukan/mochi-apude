@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK 8.0 not found. Install from https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)
echo === build: self-contained folder ===
if exist dist rmdir /s /q dist
dotnet publish -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o dist
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Copy the messages above.
  pause
  exit /b 1
)
echo.
echo OK: %~dp0dist\MochiApude.exe
echo Copy the ENTIRE dist folder contents into package\Tools~\app\
pause
