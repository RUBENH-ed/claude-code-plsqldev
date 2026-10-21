# Cambios

## 1.2 (EDISA)

- **Una ventana de Claude por expediente, como panel.** La primera vez se abre y se coloca a la derecha de la
  pantalla de PL/SQL Developer (`AnchoPanel`, 35 % por defecto), con PL/SQL Developer en el resto. Las siguientes
  veces no abre otra: trae esa ventana al frente y deja en el portapapeles la referencia de la ventana actual
  (`@.px/ventanas/...`) para pegarla con Ctrl+V. Cada expediente tiene su ventana. `PanelLateral=no` vuelve al
  comportamiento anterior. La ventana se reconoce por su título, «Claude Code - 1234567 - Título», que Claude ya no
  cambia (`CLAUDE_CODE_DISABLE_TERMINAL_TITLE`).
- **Retoma la conversación del proyecto.** Claude se abre con `--continue` y sigue la última conversación de esa
  carpeta; si no hay ninguna, empieza una nueva. `RetomarConversacion=no` para empezar siempre de cero.
- PL/SQL Developer no permite paneles acoplados desde un plug-in, así que la ventana de Claude es una ventana
  aparte colocada al lado, no un panel dentro del programa.

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
- 59 pruebas (versión 1.2) con un PL/SQL Developer simulado (callbacks con las mismas firmas): expediente, ventana de base
  de datos (sí/no/cancelar), carpeta general, selección, referencia, sincronización, bloqueo de caracteres,
  SQLcl, `ModoExpedientes=no`, reutilización de la ventana, panel y `--continue`. No se ha podido probar dentro de PL/SQL Developer: la DLL nativa solo se genera
  en Windows.
