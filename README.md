# Claude Code para PL/SQL Developer

> **Versión 1.2 (EDISA).** Abre Claude en la carpeta del expediente (`1234567 - Título`) cuando el fichero de la
> ventana está dentro de una, en una sola ventana por expediente colocada como panel a la derecha de PL/SQL
> Developer y retomando la conversación de ese proyecto. Trae las normas de EDISA en el `CLAUDE.md` y no carga en
> el editor texto con caracteres que no existen en Windows-1252. Detalle en [CAMBIOS.md](CAMBIOS.md).

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
| **Conectar a BBDD** | Deja que Claude consulte la BD de la sesión activa, solo en el proyecto de esa ventana (ver *Consultas de Claude a la base de datos*). |
| **Desconectar BBDD** | Quita las BBDD conectadas en el proyecto de la ventana; la de desarrollo se queda. |
| **Fijar como BD de desarrollo** | Hace de la sesión activa la BD que Claude consulta por defecto en todos los proyectos. |
| **Comparar con otra BD** | Claude compara el objeto de la ventana entre desarrollo y las BBDD del proyecto: qué cambia y qué versión tiene cada una. |
| **Revisar normas** | Claude revisa la ventana con las normas de EDISA y busca errores probables; te da la lista por línea sin tocar nada. |
| **Explicar error** | Copia el error de PL/SQL Developer y pulsa: Claude explica la causa y cómo arreglarlo (si no hay nada copiado, lo busca en `ALL_ERRORS`). |
| **Documentación** | En un expediente: A18 y manual de desarrollo con sus skills. |
| **Preparar instalación** | En un expediente: lista y orden de compilación de los fuentes y el script de instalación. |
| **Panel de sesiones** | Abre el panel con todas las sesiones de Claude abiertas desde el plug-in, en directo (ver *Panel de sesiones*). |
| **Elegir ventana de Claude** | Abre la ventana activa (guardada o no) en la sesión de Claude que elijas, o en una sesión nueva aquí o en otra carpeta. |
| **Asignar expediente** | Pasa una conversación empezada fuera de un expediente a uno (o a un tema con título): crea la carpeta, mueve la conversación y la reabre allí. |
| **Ayuda** | Abre la guía de uso (`ayuda/ayuda.html`, incrustada en la DLL): qué hace cada botón, expedientes, BBDD, panel y problemas frecuentes. Se abre sola la primera vez tras instalar o actualizar a una versión con ayuda nueva. |

### Cómo funciona

1. Se elige la carpeta de trabajo:
   - Si el fichero de la ventana está dentro de una carpeta de expediente (`1234567 - Título`, la que crea la
     skill `proyecto-expediente`), esa carpeta. La copia va a `.px\ventanas\` y el plug-in deja en `.px\` su
     propio `CLAUDE.md` (el de la raíz es del proyecto y no lo toca).
   - Si la ventana no tiene fichero de expediente (por ejemplo, un paquete abierto desde la base de datos),
     pregunta en una lista: expedientes y temas recientes, la carpeta general o *Nuevo con título* (un tema con su
     carpeta `doc` y la marca `.px\tema.json`, o un expediente si se le pone número), y lo recuerda para esa ventana.
   - En otro caso, `CarpetaTrabajo`, con las copias en `ventanas\` y un `CLAUDE.md` con las normas de EDISA.
2. Cada ventana se guarda como `PROPIETARIO.OBJETO.pkb`, `.pks`, `.prc`, `.fnc`, `.trg`… o
   `ventana_sql_XXXX.sql` si no es un objeto, en UTF-8. Claude Code se abre en la carpeta de trabajo con un
   mensaje que empieza por «Abierto desde PL/SQL Developer (plug-in Claude Code)», que la skill reconoce.
3. Un temporizador en el hilo del IDE vigila el fichero de la ventana activa. Cuando Claude lo cambia y
   vuelves a PL/SQL Developer, te pregunta si quieres cargarlo. Si también has tocado el editor desde
   entonces, te avisa antes de sobrescribir (y siempre puedes deshacer con Ctrl+Z). Si el texto de Claude tiene
   caracteres que no existen en Windows-1252 (flechas, ≤, emojis…), no lo carga y te dice cuáles y en qué línea.
4. Tú revisas, compilas y guardas en PL/SQL Developer: el fichero original conserva su codificación.

A Claude solo se le dice `usuario@base_de_datos`, nunca la contraseña. El plug-in solo la lee con los botones de
base de datos, que la guardan en un `.env` de tu perfil local (ver más abajo).

## Panel de sesiones

Una página en tu propio equipo (`http://127.0.0.1:47811`, en una ventana de Edge sin pestañas) con **todas las sesiones
de Claude que has abierto desde el plug-in**:

- **La lista**, con un piloto de color por sesión: *trabajando* (ámbar), *pide permiso* (rojo, parpadea), *te espera*
  (verde: ha terminado y espera tu mensaje), *en reposo* o *cerrada*. Debajo, lo que está haciendo en ese momento
  («Bash: git status», «Edit: PKG_FACTURAS.pkb», «Pide permiso para Bash: ...»).
- **La terminal de la sesión elegida, en directo.** Puedes escribir en ella como en Windows Terminal (o en la caja de
  abajo: Intro envía, Mayús+Intro salta de línea), interrumpir con *Interrumpir* (tecla Esc) o cerrarla.
- **La actividad**: tus mensajes, las herramientas que ha usado, los permisos que ha pedido y el principio de cada
  respuesta.
- **Todas**: las terminales de todas las sesiones a la vez, en pequeño. Pulsa una para ir a ella.
- **Nueva sesión**: abre Claude en un expediente directamente desde el panel, sin ventana de terminal. El panel deja una
  petición en `%APPDATA%\ClaudeCodePlsql\peticiones` y el plug-in la atiende con las mismas normas y BBDD que la cinta,
  así que PL/SQL Developer tiene que estar abierto.
- **Plegar** la lista y la actividad (cada una con su flecha, o las dos con *Ampliar*) para que la terminal ocupe todo.
- **Personalizar**: la lista a la izquierda, arriba o a la derecha; la actividad a la derecha, debajo o a la izquierda;
  tema (Pizarra, Noche, Claro, Alto contraste) y colores propios; tamaño de letra; caja de escribir; y **Ventanas de
  Windows Terminal: mostrar u ocultar**. Con *ocultar*, las sesiones que abre el plug-in no abren ventana: corren en
  segundo plano, se ven y se usan en el panel (que se abre solo en la sesión) y toman el tamaño del panel. Todo se
  guarda en `%APPDATA%\ClaudeCodePlsql\panel\preferencias.json`, que el plug-in lee al abrir cada sesión.
- **Una sesión por carpeta, también en el panel**: si ya hay una abierta, el plug-in le deja escrito el mensaje (sin
  enviarlo) y te la enseña en el panel.
- **Avisos** del navegador cuando una sesión pide permiso o termina y no la estás mirando (actívalos con el enlace de
  abajo a la izquierda). Con el teclado, <kbd>F6</kbd> saca el foco de la terminal (Tab es de Claude).

En la ventana de Windows Terminal no cambia nada: la sesión se ve y se usa igual, y lo que escribas en un sitio aparece
en el otro. Se usa tu sesión de Claude Code tal cual (tu cuenta, también Enterprise): el panel no habla con Anthropic
ni con nada de fuera de tu equipo.

**Cómo funciona.** Con el panel, el plug-in abre cada sesión dentro de un pequeño programa de Node.js (`anfitrion.js`)
que la comparte con el servidor del panel (`servidor.js`); el servidor se arranca solo con la primera sesión y se para
tras 20 minutos sin sesiones ni panel abierto. Un mod de Claude Code (`panel\mod`, cargado con `--plugin-dir`) le
cuenta al panel lo que hace Claude; solo observa, nunca cambia una decisión ni un permiso.

**Requisitos.** Node.js 18 o superior (`winget install OpenJS.NodeJS.LTS`). Si no está, o algo del panel falla, el
plug-in abre Claude como siempre (`PanelSesiones=no` lo desactiva). El panel lo instala y lo pone al día
`actualizar.ps1` en `%APPDATA%\ClaudeCodePlsql\panel-app`.

**Seguridad.** Solo escucha en `127.0.0.1`. Para entrar hace falta un enlace de un solo uso que genera el botón
*Panel de sesiones*; después, una cookie de tu navegador. Las claves están en `%APPDATA%\ClaudeCodePlsql\panel` (solo
tu usuario de Windows) y el hook de solo consultas impide que Claude las lea. Una página web cualquiera no puede hablar
con el panel (se comprueban `Host` y `Origin`). Ojo: quien abre el panel puede escribir en tus sesiones, igual que
quien se sienta en tu teclado.

**Límites.**
- Solo aparecen las sesiones abiertas desde el plug-in; un `claude` lanzado a mano no.
- Con ventana, la sesión vive en ella: si cierras la ventana de Windows Terminal, la sesión termina (en el panel queda
  como *cerrada*, con su última pantalla). Sin ventana, se cierra con *Cerrar sesión* en el panel.
- En la ventana de la terminal, Mayús+Intro llega como Intro: para saltar de línea usa `\` + Intro.
- El tamaño de la terminal lo manda la ventana de Windows Terminal; el panel ajusta la letra para que quepa.

## Requisitos

- PL/SQL Developer 12 o superior (con cinta). Funciona también con el menú clásico (`EstiloMenu=clasico`).
- Claude Code instalado en Windows y accesible como `claude` (o pon la ruta en `ComandoClaude`).
- Windows Terminal (opcional; si no está, se usa una ventana de PowerShell).
- Node.js 18 o superior, solo para el panel de sesiones (opcional).
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

## Instalar (con actualización automática)

Lo normal en la organización. Hace falta acceso al repositorio en GitHub y Git instalado
(`winget install --id Git.Git -e`).

1. Baja la versión publicada (la primera vez te pedirá iniciar sesión en GitHub en el navegador):

   ```powershell
   git clone --branch publicado <url del repositorio> "$env:APPDATA\ClaudeCodePlsql\repo"
   ```

2. Ejecuta el instalador y contesta a sus preguntas:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File "$env:APPDATA\ClaudeCodePlsql\repo\instalar.ps1"
   ```

   Antes de tocar nada revisa lo que hace falta y los conflictos: Git, Claude Code, el cliente de Oracle, la carpeta
   de plug-ins de PL/SQL Developer (y si se puede escribir en ella), versiones anteriores del plug-in, skills tuyas
   con el mismo nombre que las del paquete (las tuyas no se pisan si no quieres) y ajustes de Claude Code que
   desactiven los hooks. Después pregunta qué skills quieres (ver *Paquete de expedientes*) y deja la DLL en su sitio.
3. Abre PL/SQL Developer.

El instalador pregunta si quieres **recibir actualizaciones automáticas** (`ActualizacionesAutomaticas`). Si dices
que sí, **cada vez que arranca PL/SQL Developer** el plug-in lanza en segundo plano `actualizar.ps1`: baja
la última versión publicada, deja preparada la DLL nueva (se aplica al reiniciar; te avisa con un mensaje) y pone al
día las skills que elegiste. Para cambiar lo elegido, vuelve a ejecutar `instalar.ps1`. El resultado de la última
actualización queda en `%APPDATA%\ClaudeCodePlsql\actualizacion.json`.

## Paquete de expedientes (opcional)

Al instalar se puede elegir cada una de estas skills de Claude, que se instalan en `~/.claude/skills` y se
actualizan solas:

| Skill | Qué hace |
|---|---|
| `proyecto-expediente` | Monta la carpeta del expediente (`cfcliente`, `cfstd`, `doc`, `instalacion`, `test`), deja la documentación en `doc/` con su resumen y lleva el seguimiento en tu panel **Mis expedientes**, que crea la primera vez a partir de una plantilla vacía. Opcionalmente, la **tarea programada** que revisa correo y Teams dos veces al día y lo apunta en el panel. |
| `a18-acta-entrega` | Genera el A18 (Acta de Entrega de Desarrollo) con la plantilla oficial. |
| `manual-desarrollo` | Genera el manual de desarrollo para el cliente con la plantilla oficial. |

Las skills vienen de las de quien las mantiene: en su equipo, `herramientas\sincronizar-skills.ps1` las copia al
repositorio (la de expedientes, en una versión sin sus datos: cada uno usa su propio panel) y las publica cuando
cambian. Para que lo haga solo al arrancar PL/SQL Developer, ejecuta una vez el instalador con `-Publicar`
(`PublicarSkills=si`); a los demás no se les pregunta.

## Instalar a mano (sin actualizaciones)

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
| `RetomarConversacion` | `si` | `si`: abre Claude con `--continue`, retomando la última conversación de esa carpeta (si no hay, empieza una). |
| `MensajesDetallados` | `si` | `no`: al pasar una ventana o una BD, el mensaje lleva solo la referencia (`PACKAGE LIBRA.PK_X: @.px/ventanas/LIBRA.PK_X.pkb`); las instrucciones van en el prompt de sistema de la sesión. Si se elige en *Personalizar* del panel, manda lo del panel. |
| `PanelLateral` | `si` | `si`: una sola ventana de Claude por carpeta, a la derecha de PL/SQL Developer; si ya está abierta, la trae al frente y deja en el portapapeles la referencia para pegarla. |
| `AnchoPanel` | `35` | Ancho del panel de Claude en % de la pantalla (20-60). |
| `Terminal` | `auto` | `auto`, `wt` (Windows Terminal) o `consola` (PowerShell normal). |
| `Shell` | `powershell.exe` | O `pwsh.exe` si usas PowerShell 7. |
| `ComandoClaude` | `claude` | Ruta completa a `claude.exe` si no está en el PATH. |
| `ArgumentosExtra` | *(vacío)* | Por ejemplo `--model opus` o `--permission-mode acceptEdits`. |
| `Sincronizar` | `preguntar` | `preguntar`, `automatico` (carga sin preguntar si no has tocado el editor) o `no`. |
| `EstiloMenu` | `auto` | `cinta` o `clasico` si el menú no aparece donde debe (reinicia el IDE). |
| `PanelSesiones` | `auto` | `auto`: abre las sesiones con el panel si están Node.js y el panel; `si`: igual, pero avisa en la barra de estado cuando no se puede; `no`: sin panel. |
| `RutaNode` | *(vacío)* | Ruta a `node.exe` si no está en el `PATH` ni en `C:\Program Files\nodejs`. |
| `RutaPanel` | *(vacío)* | Carpeta del panel, si no quieres la que instala la actualización. |
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

## Consultas de Claude a la base de datos

El plug-in lleva todo lo necesario, sin depender de ninguna skill: Claude consulta con su envoltorio,
`%APPDATA%\ClaudeCodePlsql\consulta-bbdd.ps1`, eligiendo la base por su nombre:

```powershell
& "$env:APPDATA\ClaudeCodePlsql\consulta-bbdd.ps1" -Sql 'SELECT ...'            # la de desarrollo
& "$env:APPDATA\ClaudeCodePlsql\consulta-bbdd.ps1" -Sql 'SELECT ...' -Bd <nombre>
& "$env:APPDATA\ClaudeCodePlsql\consulta-bbdd.ps1" -Lista                       # las que tiene esta sesión
```

Las credenciales las guardan los botones en `.env` de `%APPDATA%\ClaudeCodePlsql\bbdd`, en tu perfil local y nunca en
la carpeta del expediente.

- **Desarrollo, siempre conectada.** **Fijar como BD de desarrollo** guarda la sesión activa (por ejemplo LATEST 19C)
  como la base que Claude usa por defecto en todos los proyectos.
- **Otras, por cliente.** **Conectar a BBDD** añade la sesión activa a las BBDD del cliente del expediente de esa
  ventana (pruebas o producción, para comparar datos o versiones), junto a las que ya tuviera, y valen para todos sus
  desarrollos e incidencias; si el alias no parece de pruebas o desarrollo, pide confirmación. El cliente sale de la
  carpeta que contiene el expediente, saltando las de estado como `01.PENDIENTE INSTALAR REAL` (o de
  `CarpetaClientes`); fuera de un expediente, las BBDD son de esa carpeta de trabajo. Cada sesión de Claude que abre
  el plug-in sabe de qué cliente es (`CLAUDE_PLSQL_PROYECTO`) y el envoltorio no le deja usar las BBDD de otro.
  **Desconectar BBDD** las quita.
- **Abrir en PL/SQL Developer desde Claude.** Con `plsql.ps1` Claude abre un fichero, crea uno nuevo (en
  Windows-1252, con CRLF, sin BOM y sin pisar nada) o abre una ventana con un texto, conectada a una de esas BBDD con
  `-Bd`. Si PL/SQL Developer está cerrado, lo arranca conectado. Nunca ejecuta nada.
- **Solo consultas.** El envoltorio y el hook `solo-consultas.ps1` solo dejan pasar un `SELECT`, `WITH` o `DESCRIBE`
  por llamada; el hook te pide aprobación en cada una, impide que Claude lea las credenciales y bloquea cualquier otro
  script que se conecte a Oracle por su cuenta (el de una skill, por ejemplo). Después, `sqlplus -R 3` y
  `SET TRANSACTION READ ONLY`. No es una garantía total: una función con `PRAGMA AUTONOMOUS_TRANSACTION` escribe
  aunque se llame desde un `SELECT`, y eso el filtro no lo ve.
- **A qué BBDD está conectado.** Un mod de Claude Code (`%APPDATA%\ClaudeCodePlsql\mod-bbdd`, cargado con
  `--plugin-dir`) pinta encima del cuadro de escritura la de desarrollo y las del proyecto, en rojo las de producción,
  y le pasa a Claude la lista con cada mensaje en cuanto cambia.
- **Las contraseñas van en texto plano** en los `.env` de tu perfil. Las sesiones de Claude que no abre el plug-in no
  tienen el hook, así que ahí nada impide leerlos.
- **Requisitos.** El cliente de Oracle con `sqlplus` (en el `PATH`, en `ORACLE_HOME` o en `C:\Oracle\...\bin`). La base
  tiene que definirse con servicio (`SERVICE_NAME`): el envoltorio conecta por EZConnect.

## Si algo no funciona

- **No aparece el plug-in**: arquitectura equivocada (32/64 bits) o la DLL no está en la carpeta de plug-ins.
  Arranca `plsqldev.exe DebugPlugIns` para que escriba un `debug.txt` con el detalle de la carga.
- **No aparece la pestaña o el menú**: prueba `EstiloMenu=clasico` (o `cinta`) y reinicia.
- **Caracteres raros (tildes, ñ)**: el plug-in pide al IDE cadenas UTF-8 (`CHARMODE`, documentado desde la
  versión 7.1); si no lo acepta, usa Windows-1252.
- **No se abre Claude**: revisa `ComandoClaude` y `Terminal`. La ventana de PowerShell explica el error.
  Si tu empresa bloquea los scripts de PowerShell por directiva de grupo, el lanzador (`-ExecutionPolicy Bypass`)
  no podrá ejecutarse; en ese caso coméntamelo y lo cambiamos a un `.cmd`.
- **El panel no se abre o las sesiones no salen en él**: en `plugin.log` está el motivo (falta Node.js, la comprobación
  del panel ha fallado...). El servidor deja su registro en `%APPDATA%\ClaudeCodePlsql\panel\servidor.log`. Si el
  puerto 47811 lo usa otro programa, pon otro en la variable de entorno `PANEL_PUERTO`. Mientras tanto, Claude se
  abre como siempre.
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
src/Panel.cs       panel de sesiones: busca Node.js y el panel, abre las sesiones con el anfitrión (con o sin ventana)
src/Help.cs        botón Ayuda: deja ayuda.html en %APPDATA% y la abre en Edge
ayuda/ayuda.html   la guía de uso (va dentro de la DLL y, en la publicación, también en el panel: /ayuda)
panel/servidor.js  servidor del panel (127.0.0.1): sesiones, navegadores y avisos del mod
panel/anfitrion.js envuelve cada sesión en una pseudoterminal y la comparte con el servidor
panel/abrir.js     abre el panel en el navegador con un enlace de un solo uso (botón Panel de sesiones)
panel/web/         la página del panel (xterm.js para las terminales)
panel/mod/         mod de Claude Code que avisa al panel de lo que hace Claude
panel/pruebas/     prueba de punta a punta (npm run prueba, en Linux; la ejecuta GitHub Actions)
```

Los índices y firmas de los callbacks siguen la documentación del Plug-In interface que viene con
PL/SQL Developer (`PlugInDoc\plugindoc.pdf`). Si una versión futura cambia algo, ese es el sitio donde mirar.
