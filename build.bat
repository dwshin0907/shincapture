@echo off
echo Building ShinCapture...
dotnet publish src\ShinCapture\ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish
echo.
echo Build complete. Output in publish/
echo To create installer: run Inno Setup on installer/setup.iss
echo To create portable: zip publish/ folder and add portable.txt
