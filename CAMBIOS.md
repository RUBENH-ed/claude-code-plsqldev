# Cambios

## 1.4 (EDISA)

- **BBDD para Claude, con la skill desa04-sqlplus.** Grupo nuevo *Base de datos* en la cinta (en el menú clásico,
  *Tools > Claude Code*):
  - **Fijar como BD de desarrollo**: la sesión activa queda como la base que Claude consulta por defecto en todos los
    proyectos.
  - **Conectar a BBDD**: añade la sesión activa al proyecto de esa ventana, junto a las que ya tuviera; con un alias
    que no parece de pruebas ni de desarrollo, pide confirmación. **Desconectar BBDD** las quita.
  - Usuario, contraseña y servidor salen de la sesión (el alias se resuelve con el `TNS_ADMIN` de PL/SQL Developer)
    y se guardan en `.env` de `%APPDATA%\ClaudeCodePlsql\bbdd`, nunca en la carpeta del expediente. La contraseña va
    por stdin al script `alta-bbdd.ps1`; Claude no la ve. Admite usuarios proxy (`SOPORTE[LIBRA]`).
- **El hook de solo consultas** admite el `query.ps1` de la skill (`-Sql`, `-File`, `-EnvFile`, `-MaxLines`) con una
  sola sentencia `SELECT`, `WITH` o `DESCRIBE` y pidiendo aprobación; `-EnvFile` solo puede ser la de desarrollo o
  una del proyecto de la sesión (`CLAUDE_PLSQL_PROYECTO`). Bloquea leer los `.env`, `mkstore` y los wallets.
- **Mod de Claude Code** con una franja encima del cuadro de escritura: a qué BBDD está conectada la sesión
  (desarrollo y las del proyecto, en rojo las de producción). Se carga con `--plugin-dir` y se refresca solo.
- **Iconos propios en la cinta** (SVG que se adaptan al tema claro u oscuro) para cada opción del plug-in.

## 1.3 (EDISA)

- **Solo consultas contra la base de datos.** Cada vez que abre Claude, el plug-in escribe en
  `%APPDATA%\ClaudeCodePlsql` un hook (`solo-consultas.ps1`) y sus ajustes (`settings-plugin.json`) y los pasa con
  `--settings`, así que solo valen para las sesiones del plug-in. El hook solo deja pasar un `SELECT` o `WITH` por
  llamada a `run-sql` del servidor `sqlcl`, y siempre pide aprobación. Bloquea DML, DDL, PL/SQL, `COMMIT`,
  `FOR UPDATE`, los comandos de SQLcl (`host`, `@script`...), el resto de herramientas de SQLcl salvo conectar y
  listar conexiones, otros servidores MCP de base de datos, los clientes de Oracle desde la consola (`sqlplus`,
  `sql`, `sqlldr`...) y que Claude toque la carpeta del plug-in o los `settings.json` de Claude Code. Si algo falla
  al comprobar, bloquea. Además se le da la norma a Claude con `--append-system-prompt`.
- **Tildes al cargar los cambios de Claude.** Después de poner el texto en el editor, el plug-in lo relee y lo
  compara con la copia. Si no coincide (tildes como «Ã³»), lo repite en UTF-8 con BOM y, si tampoco, en
  Windows-1252. Si con ninguna coincide, avisa antes de que compiles o ejecutes. En `plugin.log` queda con qué
  codificación ha llegado bien.

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
