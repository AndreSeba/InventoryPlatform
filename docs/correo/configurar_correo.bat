@echo off
rem Doble clic: pregunta tu Gmail y la contrasena de aplicacion y deja el correo configurado.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0configurar_correo.ps1"
echo.
pause
