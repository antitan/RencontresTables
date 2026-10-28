@echo off
cd /d "%~dp0"
dotnet run -- hommes.json femmes.json 15
pause
