@echo off
setlocal
chcp 65001 >nul
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0CsvExporter\Export-Csv.ps1" %*
set "csv_export_exit=%ERRORLEVEL%"
echo.
if not "%csv_export_exit%"=="0" echo CSV export failed. See the message above.
pause
exit /b %csv_export_exit%
