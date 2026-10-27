@echo off
rem Windows 기본 .NET Framework 컴파일러만 사용합니다 (별도 설치 불필요).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:KrakenAutoSwitch.exe KrakenAutoSwitch.cs
"%CSC%" /nologo /platform:x64 /out:HidDiag.exe tools\HidDiag.cs
