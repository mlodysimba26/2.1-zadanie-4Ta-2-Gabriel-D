@echo off
chcp 65001 >nul
title Generator Haseł
cd /d "%~dp0\PasswordGenerator\bin\Debug\net9.0-windows"
if exist "PasswordGenerator.exe" (
    start "" "PasswordGenerator.exe"
) else (
    cd /d "%~dp0\PasswordGenerator"
    start "" dotnet run
)
exit
