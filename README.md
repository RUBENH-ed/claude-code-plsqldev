# Claude Code para PL/SQL Developer

> **Versión 1.1 (EDISA).** Abre Claude en la carpeta del expediente (`1234567 - Título`) cuando el fichero de la
> ventana está dentro de una, trae las normas de EDISA en el `CLAUDE.md` y no carga en el editor texto con
> caracteres que no existen en Windows-1252. Detalle en [CAMBIOS.md](CAMBIOS.md).

Plug-in nativo (DLL) para **PL/SQL Developer** de Allround Automations que conecta el editor con
**Claude Code**, de forma parecida a la integración de Visual Studio Code:

- Pasa a Claude el contenido de la ventana activa (paquete, procedimiento, script SQL, test…).
- Abre una sesión de Claude Code en la terminal con ese contexto ya cargado.
- Cuando Claude modifica el fichero, al volver a PL/SQL Developer te ofrece cargar los cambios en el editor.
  Tú revisas, compilas y ejecutas como siempre.

## Qué añade al IDE

Una pestaña **Claude Code** en la cinta (en versiones antiguas, en *Tools > Claude Code*) y opciones en el
menú contextual (clic derecho) de las ventanas SQL, de programa, de test y de comandos:

| Opción | Qué hace |
|---|---|
| **Abrir en Claude Code** | Guarda el texto de la ventana en la carpeta de trabajo y abre Claude Code con él. |
| **Preguntar por lo seleccionado** | Igual, pero además le pasa el fragmento seleccionado y la línea del cursor. |
| **Traer cambios de Claude** | Carga en el editor lo que Claude haya cambiado en el fichero (también ocurre solo, ver *Sincronizar*). |
| **Copiar referencia para Claude** | Actualiza el fichero y copia `@ventanas/OBJETO.pkb` al portapapeles para pegarlo en una sesión que ya tengas abierta. |
| **Abrir carpeta de trabajo** | Abre la carpeta en el Explorador. |
| **Configurar** | Abre `ClaudeCode.ini` en el Bloc de notas. |

### Cómo funciona

1. Se elige la carpeta de trabajo:
   - Si el fichero de la ventana está dentro de una carpeta de expediente (`1234567 - Título`, la que crea la
     skill `proyecto-expediente`), esa carpeta. La copia va a `.px\ventanas\` y el plug-in deja en `.px\` su
     propio `CLAUDE.md` (el de la raíz es del proyecto y no lo toca).
   - Si la ventana no tiene fichero de expediente (por ejemplo, un paquete abierto desde la base de datos),
     pregunta si usar el último expediente o la carpeta general, y lo recuerda para esa ventana.
   - En otro caso, `CarpetaTrabajo`, con las copias en `ventanas\` y un `CLAUDE.md` con las normas de EDISA.
2. Cada ventana se guarda como `PROPIETARIO.OBJETO.pkb`, `.pks`, `.prc`, `.fnc`, `.trg`… o
   `ventana_sql_XXXX.sql` si no es un objeto, en UTF-8. Claude Code se abre en la carpeta de trabajo con un
   mensaje que empieza por «Abierto desde PL/SQL Developer (plug-in Claude Code)», que la skill reconoce.
3. Un temporizador en el hilo del IDE vigila el fichero de la ventana activa. Cuando Claude lo cambia y
   vuelves a PL/SQL Developer, te pregunta si quieres cargarlo. Si también has tocado el editor desde
   entonces, te avisa antes de sobrescribir (y siempre puedes deshacer con Ctrl+Z). Si el texto de Claude tiene
   caracteres que no existen en Windows-1252 (flechas, ≤, emojis…), no lo carga y te dice cuáles y en qué línea.
4. Tú revisas, compilas y guardas en PL/SQL Developer: el fichero original conserva su codificación.

Nunca se lee ni se guarda la contraseña de la conexión: a Claude solo se le dice `usuario@base_de_datos`.

## Requisitos

- PL/SQL Developer 12 o superior (con cinta). Funciona también con el menú clásico (`EstiloMenu=clasico`).
- Claude Code instalado en Windows y accesible como `claude` (o pon la ruta en `ComandoClaude`).
- Windows Terminal (opcional; si no está, se usa una ventana de PowerShell).
- Para compilar: SDK de **.NET 10** y Visual Studio 2022/2026 o *Build Tools* con la carga de trabajo
  **Desarrollo para el escritorio con C++** (NativeAOT la usa para enlazar). En el equipo donde se usa
  la DLL no hace falta .NET.

## Compilar

**Opción A — en tu equipo**

```cmd
build.cmd          :: 64 bits -> dist\x64\ClaudeCodePlsql.dll
build.cmd x86      :: 32 bits -> dist\x86\ClaudeCodePlsql.dll
```

**Opción B — en GitHub, sin instalar nada**

Sube la carpeta a un repositorio de GitHub, entra en *Actions > build > Run workflow* y descarga el
artefacto `ClaudeCodePlsql-x64` (o `-x86`).

> La DLL tiene que ser de la misma arquitectura que PL/SQL Developer. Desde la versión 16 solo hay
> 64 bits. Lo puedes ver en *Help > About*.

## Instalar

1. Cierra PL/SQL Developer.
2. Copia `ClaudeCodePlsql.dll` a la carpeta `PlugIns` de la instalación, por ejemplo
   `C:\Oracle\PLSQLDeveloper\PlugIns` o `C:\Program Files\PLSQL Developer 16\PlugIns` (puede pedir permisos de
   administrador). Para que sea solo tuyo, en `PlugIns\<tu usuario de Windows>\`. Si no puedes escribir ahí,
   pon una carpeta tuya en la preferencia de carpeta de Plug-Ins (*Configure > Preferences*): PL/SQL Developer
   la mira la primera.
3. Abre PL/SQL Developer. Comprueba en *Configure > Plug-Ins* que aparece **Claude Code para PL/SQL Developer**
   y está activado.

La primera vez se crea `%APPDATA%\ClaudeCodePlsql\ClaudeCode.ini` con la configuración por defecto.

## Configuración (`ClaudeCode.ini`)

| Clave | Por defecto | Para qué |
|---|---|---|
| `CarpetaTrabajo` | `%USERPROFILE%\ClaudePLSQL` | Carpeta general, para las ventanas que no son de un expediente. Puede ser tu repositorio Git. |
| `ModoExpedientes` | `si` | `si`: abre Claude en la carpeta del expediente; `no`: siempre en la general. |
| `SoloWindows1252` | `si` | `si`: no carga en el editor texto con caracteres que no existen en Windows-1252. |
| `Terminal` | `auto` | `auto`, `wt` (Windows Terminal) o `consola` (PowerShell normal). |
| `Shell` | `powershell.exe` | O `pwsh.exe` si usas PowerShell 7. |
| `ComandoClaude` | `claude` | Ruta completa a `claude.exe` si no está en el PATH. |
| `ArgumentosExtra` | *(vacío)* | Por ejemplo `--model opus` o `--permission-mode acceptEdits`. |
| `Sincronizar` | `preguntar` | `preguntar`, `automatico` (carga sin preguntar si no has tocado el editor) o `no`. |
| `EstiloMenu` | `auto` | `cinta` o `clasico` si el menú no aparece donde debe (reinicia el IDE). |
| `RutaSqlcl` | *(vacío)* | Ruta a `sql.exe` de SQLcl 25.2+ para que Claude consulte la base de datos (ver abajo). |

El plug-in guarda en `%APPDATA%\ClaudeCodePlsql\estado.ini` el último expediente usado.

## Acceso a la base de datos (opcional, recomendado)

Con el servidor MCP de SQLcl, Claude puede mirar cómo son las tablas, ver el código de otros paquetes o
lanzar consultas:

1. Instala SQLcl 25.2 o superior (necesita Java 17+).
2. Guarda una conexión con nombre en SQLcl:
   `conn -save desarrollo -savepwd usuario/clave@host:1521/servicio`
3. Pon `RutaSqlcl=C:\sqlcl\bin\sql.exe` en la configuración. El plug-in escribe
   `%APPDATA%\ClaudeCodePlsql\mcp-sqlcl.json` y se lo pasa a Claude Code con `--mcp-config`, sin dejar
   `.mcp.json` en las carpetas de trabajo ni en los expedientes.

**Guarda en SQLcl solo conexiones de desarrollo, nunca de producción**: Claude puede conectarse a cualquier
conexión guardada y ejecutar SQL real. Los `CLAUDE.md` le dicen que solo consulte y que no lance DDL ni DML sin
que se lo pidas, pero eso no es una barrera de seguridad.

## Si algo no funciona

- **No aparece el plug-in**: arquitectura equivocada (32/64 bits) o la DLL no está en la carpeta de plug-ins.
  Arranca `plsqldev.exe DebugPlugIns` para que escriba un `debug.txt` con el detalle de la carga.
- **No aparece la pestaña o el menú**: prueba `EstiloMenu=clasico` (o `cinta`) y reinicia.
- **Caracteres raros (tildes, ñ)**: el plug-in pide al IDE cadenas UTF-8 (`CHARMODE`, documentado desde la
  versión 7.1); si no lo acepta, usa Windows-1252.
- **No se abre Claude**: revisa `ComandoClaude` y `Terminal`. La ventana de PowerShell explica el error.
  Si tu empresa bloquea los scripts de PowerShell por directiva de grupo, el lanzador (`-ExecutionPolicy Bypass`)
  no podrá ejecutarse; en ese caso coméntamelo y lo cambiamos a un `.cmd`.
- **Errores internos**: `%APPDATA%\ClaudeCodePlsql\plugin.log`.

## Estructura

```
src/Exports.cs     funciones que exporta la DLL (IdentifyPlugIn, CreateMenuItem, OnMenuClick…)
src/Ide.cs         callbacks del IDE (IDE_GetText, IDE_SetText, IDE_GetWindowObject…) con sus índices
src/Plugin.cs      lógica: exportar ventanas, sincronizar cambios, lanzar Claude Code
src/Config.cs      lectura de ClaudeCode.ini
src/Workspace.cs   carpeta de trabajo (expediente o general), textos para Claude y comprobación de Windows-1252
src/Templates.cs   ClaudeCode.ini, los CLAUDE.md y el JSON de SQLcl
src/Win32.cs       MessageBox, temporizador y portapapeles
```

Los índices y firmas de los callbacks siguen la documentación del Plug-In interface que viene con
PL/SQL Developer (`PlugInDoc\plugindoc.pdf`). Si una versión futura cambia algo, ese es el sitio donde mirar.
