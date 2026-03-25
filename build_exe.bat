@echo off
setlocal
cd /d "%~dp0"

where csc >nul 2>&1
if errorlevel 1 (
  echo.
  echo csc.exe not found.
  echo Install Visual Studio Build Tools or .NET Framework developer tools.
  pause
  exit /b 1
)

echo Building FontCleanerGUI.exe...
csc /target:winexe /platform:anycpu /optimize+ /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll FontCleanerGUI.cs
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)

echo.
echo Done: %cd%\FontCleanerGUI.exe
pause
