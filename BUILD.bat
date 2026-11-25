@echo off
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "BladeBallMacro.cs" (
  echo FEHLER: BladeBallMacro.cs liegt nicht im selben Ordner wie BUILD.bat
  pause
  exit /b
)
"%CSC%" /nologo /optimize+ /codepage:65001 /target:winexe /out:"%~dp0BladeBallMacro.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0BladeBallMacro.cs"
echo.
if exist "%~dp0BladeBallMacro.exe" (echo FERTIG: %~dp0BladeBallMacro.exe) else (echo FEHLER beim Bauen - Meldung oben lesen)
pause
