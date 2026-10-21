@echo off
rem Compila el plug-in como DLL nativa.
rem Requisitos: SDK de .NET 10 y Visual Studio (o Build Tools) con "Desarrollo para el escritorio con C++".
rem Uso:  build.cmd        -> 64 bits (PL/SQL Developer 64 bits, lo normal desde la versión 13)
rem       build.cmd x86    -> 32 bits

setlocal
set ARCH=%1
if "%ARCH%"=="" set ARCH=x64

dotnet publish "%~dp0src\ClaudeCodePlsql.csproj" -c Release -r win-%ARCH% -o "%~dp0dist\%ARCH%"
if errorlevel 1 (
  echo.
  echo *** Error al compilar. Revisa que tienes el SDK de .NET 10 y las herramientas de C++ de Visual Studio.
  exit /b 1
)

echo.
echo DLL generada: %~dp0dist\%ARCH%\ClaudeCodePlsql.dll
echo Copiala a la carpeta PlugIns de PL/SQL Developer y reinicia el programa.
