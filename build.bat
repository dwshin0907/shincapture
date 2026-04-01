@echo off
chcp 65001 >nul 2>nul
echo ========================================
echo   ShinCapture Build
echo ========================================
echo.

set DOTNET=C:\Users\popol\dotnet-sdk2\dotnet.exe

if not exist "%DOTNET%" (
    echo [ERROR] .NET SDK not found: %DOTNET%
    pause
    exit /b 1
)

echo Building...
"%DOTNET%" publish src\ShinCapture\ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo Build complete! Starting...
start "" "publish\ShinCapture.exe"
