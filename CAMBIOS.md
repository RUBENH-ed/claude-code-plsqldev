# Cambios

## 1.1 (EDISA) — sobre la 1.0 original

**Carpeta del expediente**
- Si el fichero de la ventana está dentro de una carpeta `1234567 - Título`, Claude Code se abre en esa carpeta
  (carga su `CLAUDE.md` y el seguimiento de la skill `proyecto-expediente`). Las copias van a `.px\ventanas\`
  y los lanzadores a `.px\plsqldev\`, junto a `px.py`.
- En un expediente el plug-in no crea `CLAUDE.md`, `.gitignore` ni `.mcp.json` en la raíz: solo `.px\CLAUDE.md`,
  que Claude Code carga al leer las copias.
- Ventanas sin fichero de expediente (las abiertas desde la base de datos): pregunta si usar el último
  expediente (`%APPDATA%\ClaudeCodePlsql\estado.ini`) o la carpeta general, y lo recuerda para esa ventana.
- Nuevas claves `ModoExpedientes` y `SoloWindows1252` en `ClaudeCode.ini` (las dos `si` por defecto).

**Mensaje para Claude**
- Empieza siempre por «Abierto desde PL/SQL Developer (plug-in Claude Code)», dice el expediente, la copia que
  debe editar y el fichero original que no debe tocar, y en un expediente remite a la skill (modo E).

**Codificación**
- No carga en el editor un texto con caracteres que no existen en Windows-1252 (se perderían al guardar o
  compilar): avisa de cuáles son y en qué línea, sin preguntar antes si cargar.
- Los caracteres BOM literales del código pasan a `'﻿'`.
- `ClaudeCode.ini` y los `CLAUDE.md` se crean con CRLF.

**Normas**
- El `CLAUDE.md` de la carpeta general lleva las normas de EDISA para LIBRA (SQL tradicional, packages estándar,
  nada inventado, comentarios en español, nada contra producción, codificación de los fuentes).

**SQLcl**
- El servidor MCP se pasa con `--mcp-config` desde `%APPDATA%\ClaudeCodePlsql\mcp-sqlcl.json`; ya no se escribe
  `.mcp.json` en la carpeta de trabajo.

**Otros**
- Mientras una acción del menú tiene un mensaje abierto, el temporizador no abre otro encima.
- El título de la terminal lleva el expediente.

## Comprobado

- Funciones del IDE (índices de `RegisterCallback`), `CHARMODE` y la sintaxis de la cinta, contra
  `PlugInDoc` (PlugInIntf.pas y plugindoc.pdf) de PL/SQL Developer 16.
- Compila sin avisos con .NET 10 y los analizadores de NativeAOT.
- 49 pruebas con un PL/SQL Developer simulado (callbacks con las mismas firmas): expediente, ventana de base
  de datos (sí/no/cancelar), carpeta general, selección, referencia, sincronización, bloqueo de caracteres,
  SQLcl y `ModoExpedientes=no`. No se ha podido probar dentro de PL/SQL Developer: la DLL nativa solo se genera
  en Windows.
