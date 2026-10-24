@echo off
setlocal
cd /d "%~dp0"
echo.
echo ================================================
echo  Sound Device Switcher - Windows x64 build
echo ================================================
echo.
if not exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
  echo [ERROR] Windows .NET Framework x64 C# compiler was not found.
  echo Required: %SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
  echo This project requires Windows 10/11 with .NET Framework 4.x.
  goto :failed
)
where powershell.exe >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Windows PowerShell could not be found.
  goto :failed
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
  echo [ERROR] C# compiler returned an error.
  goto :failed
)
if not exist "%~dp0bin\SoundDeviceSwitcher.exe" (
  echo [ERROR] Expected executable was not produced.
  goto :failed
)
echo.
echo [SUCCESS] bin\SoundDeviceSwitcher.exe
echo.
powershell.exe -NoProfile -Command "Get-FileHash -Algorithm SHA256 -LiteralPath '%~dp0bin\SoundDeviceSwitcher.exe' | Format-List Hash"
echo.
echo Use the built executable only after Windows audio device testing.
pause
exit /b 0
:failed
echo.
echo Build failed. No fixed executable is available in this package.
pause
exit /b 1
