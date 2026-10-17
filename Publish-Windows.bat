@echo off
setlocal
cd /d "%~dp0"
rem Publish to ONE main executable. Tools/ remains external for easy engine updates.
dotnet publish "PewTube\PewTube.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o "Publish\PewTube"
if errorlevel 1 (
  echo Publishing failed. Install .NET 10 SDK and try again.
  exit /b 1
)
if not exist "Publish\PewTube\Tools" mkdir "Publish\PewTube\Tools"
echo.
echo Published PewTube.exe to Publish\PewTube\
echo Add yt-dlp.exe and ffmpeg.exe to Publish\PewTube\Tools\
pause
