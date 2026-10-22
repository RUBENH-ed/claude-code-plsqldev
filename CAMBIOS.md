# Cambios

## 1.7 (EDISA)

- **Claude abre cosas en PL/SQL Developer** con `plsql.ps1`: un fichero (`-Abrir`), un fichero nuevo que crea en
  Windows-1252, con CRLF y sin BOM y sin pisar nunca uno existente (`-Nuevo ... -Desde ...`), o una ventana SQL, Test,
  de Programa o de Comandos con un texto (`-Ventana`). Con `-Bd`, la ventana queda conectada a esa BD. Si PL/SQL
  Developer está cerrado, lo arranca conectado. Nunca ejecuta nada: el plug-in atiende la petición y lee él mismo la
  contraseña del `.env`.
- **Las BBDD conectadas son del cliente, no del expediente**: valen para todos sus desarrollos e incidencias. El
  cliente sale de la carpeta que contiene el expediente (saltando las de estado como `01.PENDIENTE INSTALAR REAL`) o
  de `CarpetaClientes` en el `.ini`. Las conectadas con la 1.6 hay que volver a conectarlas una vez.
- **Instalador**: pregunta si quieres recibir actualizaciones automáticas (`ActualizacionesAutomaticas`); publicar las
  skills ya no se pregunta, lo activa quien las mantiene con `instalar.ps1 -Publicar`. Corregidos el error al guardar
  los ajustes («La colección tiene un tamaño fijo») y el nombre de la skill que faltaba en la pregunta.
- **La actualización ya no puede dejarte sin plug-in si el antivirus retira la DLL nueva**: primero la copia con otro
  nombre y solo cambia la que funciona si la nueva sigue ahí; si desaparece después, vuelve a poner la anterior. En
  los dos casos avisa de que ha sido el antivirus y de qué excepción pedir a sistemas.

## 1.6 (EDISA)

- **Grupo Herramientas en la cinta**: *Comparar con otra BD*, *Revisar normas*, *Explicar error* (con el error
  copiado al portapapeles), *Documentación* (A18 y manual) y *Preparar instalación*. Al conectar una BD del proyecto,
  Claude avisa de las diferencias de versión de los objetos del expediente respecto a desarrollo.
- **Instalador y actualización automática**: `instalar.ps1` revisa requisitos y conflictos antes de tocar nada y
  pregunta qué instalar; al arrancar PL/SQL Developer, `actualizar.ps1` baja la última versión de la rama
  `publicado` (que rehace GitHub Actions con cada cambio en `main`), deja preparada la DLL nueva y pone al día las
  skills elegidas.
- **Paquete de expedientes opcional**: las skills `proyecto-expediente` (en versión sin datos personales, con la
  plantilla del panel **Mis expedientes** y la tarea programada opcional), `a18-acta-entrega` y `manual-desarrollo`.
  `herramientas\sincronizar-skills.ps1` las publica desde el equipo de quien las mantiene.

## 1.5 (EDISA)

- **Sin depender de ninguna skill.** El plug-in trae su propio envoltorio de consultas,
  `%APPDATA%\ClaudeCodePlsql\consulta-bbdd.ps1`, que se escribe en cada arranque: Claude elige la base por su nombre
  (`-Bd`; sin él, la de desarrollo) y el propio envoltorio solo admite las del proyecto de la sesión. `-Lista` dice
  cuáles hay. Los `.env` pasan a tener claves propias (`BD_USUARIO`, `BD_CLAVE`, `BD_HOST`, `BD_PUERTO`,
  `BD_SERVICIO`): **hay que volver a pulsar Fijar como BD de desarrollo y Conectar a BBDD** después de actualizar.
- **El hook bloquea cualquier script que se conecte a Oracle por su cuenta** (mira dentro: `sqlplus`, `sql`, librerías
  de conexión), venga de una skill o de donde sea; solo vale el envoltorio del plug-in.
- Sin BD de desarrollo fijada, Claude pide que la fijes en lugar de usar ninguna por su cuenta.
- **Las consultas de solo lectura ya no piden aprobación**: si pasan el filtro (una sentencia `SELECT`, `WITH` o
  `DESCRIBE` con el envoltorio), el hook las aprueba solo; todo lo demás se sigue bloqueando.
- **Cinta**: *Abrir en Claude Code* es un botón grande con el símbolo de Claude en naranja, con *Preguntar*, *Traer
  cambios* y *Copiar referencia* juntos a su derecha; *Configurar* lleva un engranaje.

## 1.4.1 (EDISA)

- **Claude se entera de las BBDD que conectas con su sesión ya abierta.** El mod añade a cada mensaje la lista de
  BBDD de ese momento (desarrollo y las del proyecto, con su `-EnvFile`) cuando ha cambiado. Antes solo la recibía al
  abrirse, y lo que se conectaba después dependía de pegar el aviso del portapapeles.
- **El alias se guarda sin el número de sesión** que añade el lanzador de LIBRA (`ALIAS#3`): volver a conectar la
  misma base la sustituye en vez de añadir otra.

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
