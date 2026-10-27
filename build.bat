@echo off
rem Uses only the .NET Framework compiler that ships with Windows (nothing to install).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:KrakenAutoSwitch.exe KrakenAutoSwitch.cs
if errorlevel 1 exit /b 1
"%CSC%" /nologo /platform:x64 /out:HidDiag.exe tools\HidDiag.cs
if errorlevel 1 exit /b 1
echo Built KrakenAutoSwitch.exe and HidDiag.exe
