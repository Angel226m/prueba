@echo off
REM ============================================================
REM setup-images.cmd — Copia imagenes de fondo a wwwroot/images/
REM Ejecutar desde el directorio raiz del proyecto CEPLAN
REM ============================================================

SET SRC=%APPDATA%\Code\User\globalStorage\github.copilot-chat\copilot-cli-images
SET DST=%~dp0wwwroot\images

echo Creando directorio wwwroot\images...
if not exist "%DST%" mkdir "%DST%"

echo Copiando imagen de fondo del login (bg-login.png)...
copy "%SRC%\1774154110758-htxu0v29.png" "%DST%\bg-login.png"
if errorlevel 1 echo ERROR: No se pudo copiar bg-login.png

echo Copiando imagen de fondo de bienvenida (bg-bienvenida.png)...
copy "%SRC%\1774154110758-vj0pzfzf.png" "%DST%\bg-bienvenida.png"
if errorlevel 1 echo ERROR: No se pudo copiar bg-bienvenida.png

echo.
echo === Listo! Imagenes copiadas a wwwroot\images\ ===
echo   bg-login.png       -^> Fondo de la pantalla de Login
echo   bg-bienvenida.png  -^> Fondo de la pantalla de Bienvenida
pause
