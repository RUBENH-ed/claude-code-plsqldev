namespace ClaudeCodePlsql;

/// <summary>Ficheros que el plug-in crea la primera vez (solo si no existen; después son tuyos).</summary>
internal static class Templates
{
    public const string DefaultConfig = """
; Configuración de "Claude Code para PL/SQL Developer"
; Los cambios se aplican en la siguiente acción, sin reiniciar (salvo EstiloMenu).
; Puedes usar variables de entorno, por ejemplo %USERPROFILE%.

[General]
; Carpeta general: se usa con las ventanas que no son de ningún expediente. Ahí se guardan sus copias y desde ahí
; se abre Claude Code. Puede ser la carpeta de tu repositorio Git con el código PL/SQL.
CarpetaTrabajo=%USERPROFILE%\ClaudePLSQL

; si = si el fichero de la ventana está dentro de una carpeta de expediente («1234567 - Título»), Claude Code se
;      abre en esa carpeta (con su CLAUDE.md) y la copia va a .px\ventanas. Con las ventanas sin fichero (las
;      abiertas desde la base de datos) pregunta si usar el último expediente o la carpeta general.
; no = siempre la carpeta general.
ModoExpedientes=si

; si = no carga en el editor un texto de Claude con caracteres que no existen en Windows-1252 (flechas, símbolos
;      como ≤ o ✓, emojis...), porque se perderían al guardar o compilar. Te dice cuáles son y en qué línea.
SoloWindows1252=si

; si = al abrir Claude retoma la última conversación de esa carpeta (cada expediente tiene la suya); no = empieza una nueva
RetomarConversacion=si

; si = una sola ventana de Claude por carpeta, colocada como panel a la derecha de PL/SQL Developer. Si ya está
;      abierta, la trae al frente y te deja en el portapapeles la referencia de la ventana para pegarla (Ctrl+V).
; no = cada vez abre una ventana nueva, donde la coloque Windows.
PanelLateral=si

; Ancho del panel de Claude, en % de la pantalla (20 a 60)
AnchoPanel=35

; auto = Windows Terminal si está instalado; wt = siempre Windows Terminal; consola = ventana de PowerShell normal
Terminal=auto

; powershell.exe (Windows PowerShell) o pwsh.exe (PowerShell 7)
Shell=powershell.exe

; Comando de Claude Code (o ruta completa a claude.exe si no está en el PATH)
ComandoClaude=claude

; Argumentos extra para claude, por ejemplo: --model opus   o   --permission-mode acceptEdits
ArgumentosExtra=

; Qué hacer cuando Claude modifica el fichero de una ventana abierta:
; preguntar = te pregunta al volver a PL/SQL Developer; automatico = lo carga solo (si no has tocado el editor); no = solo con "Traer cambios"
Sincronizar=preguntar

; auto = cinta (versión 12 o superior) o menú clásico; cinta; clasico  (requiere reiniciar PL/SQL Developer)
EstiloMenu=auto

[Panel]
; Panel de sesiones: una página en tu equipo (127.0.0.1) con todas las sesiones de Claude abiertas desde el plug-in.
; Ves la terminal de cada una en directo, escribes en ella desde el panel y sabes si está trabajando, si pide
; permiso o si te espera. Se abre con el botón «Panel de sesiones». Necesita Node.js 18 o superior.
; auto = se usa si están Node.js y el panel; si = igual, pero avisa en la barra de estado cuando no se puede usar;
; no = Claude se abre como antes, sin panel.
PanelSesiones=auto

; Ruta a node.exe si no está en el PATH (vacío = el del PATH o el de C:\Program Files\nodejs)
RutaNode=

; Carpeta del panel (vacío = la que deja la actualización automática en %APPDATA%\ClaudeCodePlsql\panel-app)
RutaPanel=

[MCP]
; Ruta a sql.exe de SQLcl 25.2 o superior para que Claude pueda consultar la base de datos. Si la rellenas, el
; plug-in se la pasa a Claude Code con --mcp-config (%APPDATA%\ClaudeCodePlsql\mcp-sqlcl.json), sin dejar nada en
; las carpetas de trabajo. Claude solo puede consultar (SELECT): el plug-in bloquea el resto.
; Ejemplo: C:\sqlcl\bin\sql.exe
RutaSqlcl=

[BBDD]
; Las BBDD que conectas con «Conectar a BBDD» valen para todos los expedientes del mismo cliente. El cliente sale de la
; carpeta que contiene el expediente (saltando las de estado como «01.PENDIENTE INSTALAR REAL»). Si tus expedientes
; están en <carpeta>\<cliente>\..., pon aquí esa carpeta para que el cliente sea siempre su primera subcarpeta.
; Ejemplo: O:\Desarrollo
CarpetaClientes=

""";

    /// <summary>CLAUDE.md de la carpeta general.</summary>
    public const string ClaudeMd = """
# Contexto: código traído desde PL/SQL Developer

Esta carpeta la gestiona el plug-in "Claude Code para PL/SQL Developer". Se usa con las ventanas que no son de
ningún expediente; las de un expediente («1234567 - Título») se abren en la carpeta del expediente.

- `ventanas/` tiene copias UTF-8 de las ventanas abiertas en PL/SQL Developer (paquetes, procedimientos, scripts
  SQL, ventanas de test), con nombres como `PROPIETARIO.OBJETO.pkb`.
- Para cambiar código, edita directamente el fichero de `ventanas/`: el plug-in carga el cambio en el editor de
  PL/SQL Developer y allí se revisa, se compila y se guarda. Los `*.seleccion.*` son solo el fragmento
  seleccionado: úsalos como referencia, no los edites.
- Usa solo caracteres que existan en Windows-1252 (nada de flechas, símbolos como ≤ o ✓ ni emojis): el plug-in no
  carga una copia que los tenga.
- Mantén el estilo del código existente (mayúsculas, sangrías, nombres) y no reformatees lo que no te han pedido.

## Normas de EDISA para LIBRA

- SQL de Oracle tradicional, compatible con 19c: joins implícitos (`FROM a, b WHERE a.id = b.id`) y outer join con
  `(+)`; nunca `JOIN ... ON`. `CASE`, `COALESCE`, `WITH` y funciones analíticas, sí.
- Fechas siempre con máscara (`TO_DATE('01-01-2024','DD-MM-YYYY')`), `||` para concatenar, nada de `SELECT *` y
  top-N ordenando en una subconsulta y filtrando `ROWNUM` fuera.
- SQL dinámico (`EXECUTE IMMEDIATE`, `DBMS_SQL`) con variables bind (`USING`), nunca concatenando valores.
- `UPDATE` y `DELETE` siempre con `WHERE` explícito, proponiendo antes el `SELECT COUNT(*)`. Avisa antes de
  proponer DDL.
- Usa los packages estándar de LIBRA (pkentorno, pkerr, pk_email, PKALRT...) antes que reinventar. No inventes
  tablas, columnas, packages ni programas: búscalos (Edisa AI Search, skills `libra-*`) o pregunta.
- Comentarios del código en español. En los ejemplos, datos ficticios.
- Base de datos: nada contra producción, y no compiles ni ejecutes nada (DDL, DML, COMMIT...) sin que te lo pidan
  en ese momento. Si tienes el servidor MCP de SQLcl, úsalo para consultar y solo contra desarrollo.
- Fuera de `ventanas/`, no cambies la codificación ni los finales de línea de ningún fichero: los fuentes de LIBRA
  van en Windows-1252 con CRLF y las herramientas Edit y Write de Claude Code los estropean. Para cambiar un
  fuente que no está en `ventanas/`, pide que lo abran en PL/SQL Developer y te lo pasen con el plug-in. `.fmb`,
  `.pll` y `.rdf` son binarios.

<!-- Añade aquí tus propias normas: esquemas habituales, prefijos, etc. -->

""";

    /// <summary>CLAUDE.md que el plug-in deja en .px de un expediente (el de la raíz es del proyecto).</summary>
    public const string ProjectClaudeMd = """
# Copias de PL/SQL Developer (plug-in "Claude Code para PL/SQL Developer")

Esta carpeta `.px` es de las herramientas del expediente: `px.py` (skill proyecto-expediente) y las copias del
plug-in.

- `ventanas/` tiene copias UTF-8 de las ventanas abiertas en PL/SQL Developer, con nombres como
  `PROPIETARIO.OBJETO.pkb`. Para cambiar el código de una ventana, edita directamente su copia: el plug-in carga
  el cambio en el editor y allí se revisa, se compila y se guarda en el fichero original, con su codificación.
  No uses `px.py sacar`/`devolver` con esos ficheros ni toques el original mientras tanto.
- Los `*.seleccion.*` son solo el fragmento seleccionado: úsalos como referencia, no los edites.
- Usa solo caracteres que existan en Windows-1252 (nada de flechas, símbolos como ≤ o ✓ ni emojis): el plug-in no
  carga una copia que los tenga.
- Mantén el estilo del código (mayúsculas, sangrías, nombres) y no reformatees lo que no te han pedido.
- Las normas del expediente y de EDISA están en el `CLAUDE.md` de la carpeta del proyecto. Si no lo hay: SQL de
  Oracle tradicional (joins implícitos y `(+)`, nunca `JOIN ... ON`; fechas con máscara; `||`; nada de
  `SELECT *`; binds en el SQL dinámico; `UPDATE`/`DELETE` con `WHERE`), packages estándar de LIBRA, nada
  inventado, comentarios en español, nada contra producción y no compilar ni ejecutar nada sin que te lo pidan.

""";

    public const string GitIgnore = """
.plsqldev/

""";

    /// <summary>
    /// Hook PreToolUse de las sesiones del plug-in: contra la base de datos solo se consulta. Se reescribe en cada
    /// arranque en %APPDATA%\ClaudeCodePlsql\solo-consultas.ps1 (no es para editarlo a mano).
    /// </summary>
    public const string HookSoloConsultas = """
# Hook PreToolUse del plug-in "Claude Code para PL/SQL Developer" (lo escribe el plug-in en cada arranque).
# Norma: desde el plug-in, contra la base de datos solo se consulta (SELECT/WITH/DESCRIBE). Todo lo demas se bloquea.
# Las BBDD se consultan con el envoltorio del plug-in, consulta-bbdd.ps1: la de desarrollo (para todos los proyectos)
# y las que se conectaron con el boton "Conectar a BBDD" en el proyecto de esta sesion.
# Salida 2 = bloquear (el motivo va por stderr a Claude). Ante cualquier error, bloquea.
$ErrorActionPreference = 'Stop'

function Bloquear([string]$motivo) {
    [Console]::Error.WriteLine("Bloqueado por el plug-in de PL/SQL Developer (solo consultas): $motivo")
    exit 2
}

function Permitir([string]$motivo) {
    # Una consulta que ha pasado el filtro de solo lectura se aprueba sola, sin preguntar al usuario
    $r = @{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; permissionDecision = 'allow'; permissionDecisionReason = $motivo } }
    [Console]::Out.Write(($r | ConvertTo-Json -Depth 4 -Compress))
    exit 0
}

# Quita comentarios, literales e identificadores entre comillas para que no engañen al analisis
function Limpiar-Sql([string]$sql) {
    if ($sql -match "(?i)\b[nq]?q'") { Bloquear 'los literales q''...'' no se admiten; usa literales normales.' }
    $s = [regex]::Replace($sql, '/\*.*?\*/', ' ', 'Singleline')
    $s = [regex]::Replace($s, '--[^\r\n]*', ' ')
    $s = [regex]::Replace($s, "'(?:[^']|'')*'", ' LITERAL ')
    $s = [regex]::Replace($s, '"[^"]*"', 'X')
    if ($s -match "['""]") { Bloquear 'comillas sin cerrar.' }
    return $s
}

function Comprobar-Select([string]$sql) {
    if ([string]::IsNullOrWhiteSpace($sql)) { Bloquear 'consulta vacia.' }
    $s = (Limpiar-Sql $sql).Trim()
    $s = [regex]::Replace($s, '[\s;/]+$', '')
    if ($s.Contains(';')) { Bloquear 'solo se admite una sentencia por llamada.' }
    if ($s -match '(?i)^DESC(RIBE)?\s+[\w$#.]+$') { return }
    if ($s -notmatch '^\(*\s*(SELECT|WITH)\b') { Bloquear 'solo se admiten consultas que empiecen por SELECT o WITH (o un DESCRIBE).' }
    $prohibidas = 'INSERT|UPDATE|DELETE|MERGE|UPSERT|CREATE|ALTER|DROP|TRUNCATE|RENAME|GRANT|REVOKE|COMMENT|AUDIT|NOAUDIT|PURGE|FLASHBACK|ANALYZE|LOCK|COMMIT|ROLLBACK|SAVEPOINT|BEGIN|DECLARE|EXEC|EXECUTE|CALL|FUNCTION|PROCEDURE'
    $m = [regex]::Match($s, "(?i)\b($prohibidas)\b")
    if ($m.Success) { Bloquear "la consulta contiene '$($m.Value.ToUpper())'." }
    # Comandos de SQL*Plus/SQLcl al principio de una linea (host, @script, spool, conn...). CONNECT BY y START WITH son SQL.
    $m = [regex]::Match($s, '(?im)^\s*([@!$]|(host|spool|conn|connect(?!\s+BY\b)|disc|disconnect|set|define|store|save|run|start(?!\s+WITH\b)|get|edit|alias|script|load|liquibase|lb|ddl|ctas|sshtunnel|repeat|apex|oci|mle|cd)\b)')
    if ($m.Success) { Bloquear "comando de SQL*Plus no permitido: '$($m.Value.Trim())'." }
}

# Valor de un argumento de PowerShell: '...' (con '' dentro) o "..." solo con $env:USERPROFILE/APPDATA/TEMP/
# LOCALAPPDATA y sin acento grave, o una palabra suelta
function Valor-Argumento([string]$v) {
    if ($v.StartsWith("'")) { return $v.Substring(1, $v.Length - 2).Replace("''", "'") }
    if ($v.StartsWith('"')) { $v = $v.Substring(1, $v.Length - 2) }
    $sinVariables = [regex]::Replace($v, '(?i)\$env:(USERPROFILE|APPDATA|LOCALAPPDATA|TEMP)\b', '')
    if ($sinVariables -match '[$`]') { Bloquear 'en las comillas dobles solo vale $env:USERPROFILE, $env:APPDATA, $env:LOCALAPPDATA o $env:TEMP: pasa el SQL entre comillas simples.' }
    return [regex]::Replace($v, '(?i)\$env:(USERPROFILE|APPDATA|LOCALAPPDATA|TEMP)\b', { param($x) [Environment]::GetEnvironmentVariable($x.Groups[1].Value) })
}

# Llamada al envoltorio del plug-in:
#   & '<ruta>\ClaudeCodePlsql\consulta-bbdd.ps1' -Sql 'SELECT ...' [-Bd <nombre>] [-MaxLines n]
#   & '<ruta>\ClaudeCodePlsql\consulta-bbdd.ps1' -File <consulta.sql> [-Bd <nombre>]  /  ... -Lista
# Que la BD sea de este proyecto lo comprueba el propio envoltorio.
function Comprobar-Consulta([string]$cmd) {
    $c = $cmd.Trim()
    $arg = '(?:''(?:[^'']|'''')*''|"[^"`]*"|[^\s''"`;|&<>(){}]+)'
    $m = [regex]::Match($c, "^(?:&\s*|(?:pwsh|powershell)(?:\.exe)?(?:\s+-(?:NoProfile|NonInteractive|NoLogo|ExecutionPolicy\s+Bypass))*\s+-File\s+)(?<ruta>$arg)(?<resto>.*)$", 'Singleline')
    if (-not $m.Success) {
        Bloquear "llama al envoltorio asi: & '<ruta>\consulta-bbdd.ps1' -Sql 'SELECT ...' [-Bd <nombre>] [-MaxLines n], sin nada mas en la linea."
    }
    $script = Valor-Argumento $m.Groups['ruta'].Value
    if ($script -notmatch '(?i)[\\/]ClaudeCodePlsql[\\/]consulta-bbdd\.ps1$') { Bloquear 'solo se admite el consulta-bbdd.ps1 del plug-in.' }
    $resto = $m.Groups['resto'].Value
    $sql = $null; $fichero = $null; $lista = $false
    $p = [regex]::Match($resto, "\G\s+-(?:(?<n>Sql|File|Bd|MaxLines)\s+(?<v>$arg)|(?<s>Lista))(?=\s|$)", 'IgnoreCase')
    $pos = 0
    while ($p.Success -and $p.Index -eq $pos) {
        $pos = $p.Index + $p.Length
        if ($p.Groups['s'].Success) { $lista = $true }
        else {
            $v = Valor-Argumento $p.Groups['v'].Value
            switch ($p.Groups['n'].Value) {
                'Sql'      { $sql = $v }
                'File'     { $fichero = $v }
                'Bd'       { if ($v -notmatch '^[A-Za-z0-9_]+$') { Bloquear '-Bd tiene que ser el nombre de una BD conectada (letras, numeros y _).' } }
                'MaxLines' { if ($v -notmatch '^\d+$') { Bloquear '-MaxLines tiene que ser un numero.' } }
            }
        }
        $p = $p.NextMatch()
    }
    if ($resto.Substring($pos).Trim()) { Bloquear "argumentos no permitidos en la llamada a consulta-bbdd.ps1: '$($resto.Substring($pos).Trim())'." }
    if ($sql -and $fichero) { Bloquear 'usa -Sql o -File, no los dos.' }
    if ($fichero) {
        if (-not (Test-Path -LiteralPath $fichero -PathType Leaf)) { Bloquear "no existe el fichero $fichero." }
        $sql = Get-Content -LiteralPath $fichero -Raw -Encoding Default
    }
    if ($sql) { Comprobar-Select $sql }
    elseif (-not $lista) { Bloquear 'falta la consulta (-Sql o -File).' }
}

# Llamada a plsql.ps1 del plug-in, que abre ficheros y ventanas en PL/SQL Developer (nunca ejecuta nada en ellas):
#   & '<ruta>\ClaudeCodePlsql\plsql.ps1' -Abrir <fichero> | -Nuevo <fichero> -Desde <fichero> | -Ventana <tipo> [-Desde <fichero>] [-Bd <nombre>]
function Comprobar-Plsql([string]$cmd) {
    $c = $cmd.Trim()
    $arg = '(?:''(?:[^'']|'''')*''|"[^"`]*"|[^\s''"`;|&<>(){}]+)'
    $m = [regex]::Match($c, "^(?:&\s*|(?:pwsh|powershell)(?:\.exe)?(?:\s+-(?:NoProfile|NonInteractive|NoLogo|ExecutionPolicy\s+Bypass))*\s+-File\s+)(?<ruta>$arg)(?<resto>.*)$", 'Singleline')
    if (-not $m.Success) { Bloquear "llama a plsql.ps1 asi: & '<ruta>\plsql.ps1' -Abrir <fichero> [-Bd <nombre>], sin nada mas en la linea." }
    if ((Valor-Argumento $m.Groups['ruta'].Value) -notmatch '(?i)[\\/]ClaudeCodePlsql[\\/]plsql\.ps1$') { Bloquear 'solo se admite el plsql.ps1 del plug-in.' }
    $resto = $m.Groups['resto'].Value
    $p = [regex]::Match($resto, "\G\s+-(?<n>Abrir|Nuevo|Desde|Ventana|Bd)\s+(?<v>$arg)(?=\s|$)", 'IgnoreCase')
    $pos = 0
    while ($p.Success -and $p.Index -eq $pos) {
        $pos = $p.Index + $p.Length
        $v = Valor-Argumento $p.Groups['v'].Value
        switch ($p.Groups['n'].Value) {
            'Bd'      { if ($v -notmatch '^[A-Za-z0-9_]+$') { Bloquear '-Bd tiene que ser el nombre de una BD conectada.' } }
            'Ventana' { if ($v -notmatch '^(sql|test|programa|comandos)$') { Bloquear '-Ventana es sql, test, programa o comandos.' } }
            'Nuevo'   {
                if ($v -match $credenciales -or $v -match '(?i)[\\/]ClaudeCodePlsql([\\/]|$)|[\\/]\.claude[\\/]') { Bloquear 'no se pueden crear ficheros en la configuracion del plug-in, de Claude Code ni de credenciales.' }
            }
        }
        $p = $p.NextMatch()
    }
    if ($resto.Substring($pos).Trim()) { Bloquear "argumentos no permitidos en la llamada a plsql.ps1: '$($resto.Substring($pos).Trim())'." }
}

# Scripts que se ejecutan desde la consola: si por dentro se conectan a Oracle (sqlplus, sql de SQLcl, librerias de
# conexion), se bloquean, vengan de una skill, de un plugin o de donde sea. Solo vale el envoltorio del plug-in.
$accesoBbdd = '(?i)\bsqlplus\b|\bsqlcl\b|\bsql(\.exe)?\s+[-/]|\bsql\.exe\b|\bsqlldr\b|\bmkstore\b|oracledb|cx_Oracle|Oracle\.ManagedDataAccess|OracleClient|jdbc:oracle'
function Comprobar-Scripts([string]$cmd) {
    $tokens = [regex]::Matches($cmd, '(?i)(''[^'']+\.(ps1|psm1|py|cmd|bat|js|sh|vbs)''|"[^"]+\.(ps1|psm1|py|cmd|bat|js|sh|vbs)"|[^\s''"`;|&<>(){}]+\.(ps1|psm1|py|cmd|bat|js|sh|vbs))(?=$|[\s;|&)''"])')
    foreach ($t in $tokens) {
        $ruta = $t.Value.Trim("'", '"')
        $ruta = [regex]::Replace($ruta, '(?i)\$env:(USERPROFILE|APPDATA|LOCALAPPDATA|TEMP)\b', { param($x) [Environment]::GetEnvironmentVariable($x.Groups[1].Value) })
        $ruta = [regex]::Replace($ruta, '^~(?=[\\/])', [Environment]::GetFolderPath('UserProfile'))
        if ($ruta -match '[$`]') { continue }
        try { if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) { continue } } catch { continue }
        if ((Get-Item -LiteralPath $ruta).Length -gt 2MB) { continue }
        if ((Get-Content -LiteralPath $ruta -Raw) -match $accesoBbdd) {
            Bloquear "el script $ruta se conecta a la base de datos por su cuenta; consulta con el consulta-bbdd.ps1 del plug-in."
        }
    }
}

# Rutas de credenciales: los .env, wallets, la carpeta de BBDD del plug-in y la del panel de sesiones (sus claves)
$credenciales = '(?i)\.env$|[\\/]ClaudeCodePlsql[\\/](bbdd|panel)([\\/]|$)|[\\/]\.secrets([\\/]|$)|wallet'

try {
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    $texto = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($texto)) { Bloquear 'no ha llegado la descripcion de la accion.' }
    $entrada = $texto | ConvertFrom-Json
    $herramienta = [string]$entrada.tool_name
    if (-not $herramienta) { Bloquear 'no se sabe que herramienta es.' }
    $ti = $entrada.tool_input
    $todo = if ($ti) { $ti | ConvertTo-Json -Depth 10 -Compress } else { '' }

    # 1) Servidor MCP de SQLcl del plug-in (RutaSqlcl)
    if ($herramienta -like 'mcp__sqlcl__*') {
        $accion = $herramienta.Substring('mcp__sqlcl__'.Length)
        switch ($accion) {
            'run-sql' {
                $sql = if ($ti.sql) { [string]$ti.sql } else { ($ti.PSObject.Properties | Where-Object { $_.Value -is [string] } | Select-Object -First 1).Value }
                Comprobar-Select $sql
                Permitir 'Consulta de solo lectura: pasa el filtro del plug-in.'
            }
            { $_ -in 'list-connections', 'connect', 'disconnect', 'schema-information' } { exit 0 }
            default { Bloquear "la herramienta '$accion' de SQLcl no esta permitida; usa run-sql con un SELECT." }
        }
    }

    # 2) Cualquier otro servidor MCP que parezca de base de datos
    if ($herramienta -like 'mcp__*' -and $herramienta -match '(?i)oracle|sql|database|_db') {
        Bloquear "solo se puede usar la base de datos con el consulta-bbdd.ps1 del plug-in ($herramienta)."
    }

    # 3) Consola: consultas solo con el envoltorio; nada de clientes de Oracle, credenciales ni configuracion
    if ($herramienta -in 'Bash', 'PowerShell') {
        $cmd = [string]$ti.command
        if ($cmd -match '(?i)consulta-bbdd\.ps1') {
            Comprobar-Consulta $cmd
            Permitir 'Consulta de solo lectura: pasa el filtro del plug-in.'
        }
        if ($cmd -match '(?i)[\\/]plsql\.ps1') {
            Comprobar-Plsql $cmd
            Permitir 'Abre una ventana en PL/SQL Developer sin ejecutar nada.'
        }
        $clientes = '(^|[\s;&|(''"`/\\])(sql|sqlplus|sqlcl|sqlldr|impdp|expdp|exp|imp|rman|dgmgrl|tnsping|orapki|mkstore)(\.exe|\.cmd|\.bat)?(?=$|[\s;&|)''"`])'
        if ($cmd -match "(?i)$clientes") { Bloquear 'no se pueden lanzar clientes de Oracle desde la consola; usa el consulta-bbdd.ps1 del plug-in.' }
        if ($cmd -match '(?i)oracledb|cx_Oracle|Oracle\.ManagedDataAccess|OracleClient|jdbc:oracle|Invoke-Sqlcmd|odbc') { Bloquear 'no se puede acceder a la base de datos desde la consola.' }
        if ($cmd -match '(?i)ClaudeCodePlsql') { Bloquear 'no se puede tocar la carpeta del plug-in desde la consola.' }
        if ($cmd -match '(?i)settings(\.local)?\.json|disableAllHooks') { Bloquear 'no se pueden tocar los ajustes de Claude Code desde la consola.' }
        if ($cmd -match '(?i)\.env\b|\.secrets|wallet|libratmp|run_sql') { Bloquear 'no se pueden leer ni tocar credenciales de BBDD desde la consola.' }
        Comprobar-Scripts $cmd
        exit 0
    }

    # 4) Lectura: nada de ficheros de credenciales
    if ($herramienta -in 'Read', 'Grep') {
        $ruta = [string]$(if ($ti.file_path) { $ti.file_path } else { $ti.path })
        if ($ruta -match $credenciales -or $ruta -match '(?i)libratmp|[\\/]\.claude[\\/]?$' -or ([string]$ti.glob) -match '(?i)env') {
            Bloquear 'no se pueden leer ficheros de credenciales de BBDD.'
        }
        exit 0
    }

    # 5) Edicion de ficheros: ni la configuracion del plug-in, ni los ajustes de Claude Code (podrian desactivar
    #    este hook), ni skills, plugins o credenciales, ni scripts que se conecten a Oracle
    if ($herramienta -in 'Edit', 'Write', 'MultiEdit', 'NotebookEdit') {
        $ruta = [string]$(if ($ti.file_path) { $ti.file_path } else { $ti.notebook_path })
        if ($ruta) {
            # Por nombre y no solo por ruta completa, para que no valgan rutas cortas como RUBEN~1.HER
            if ($ruta -match '(?i)[\\/]ClaudeCodePlsql([\\/]|$)') { Bloquear 'no se puede modificar la configuracion del plug-in.' }
            if ($ruta -match '(?i)[\\/]\.claude[\\/].*settings(\.local)?\.json$|[\\/]managed-settings\.json$') { Bloquear 'no se pueden modificar los ajustes de Claude Code.' }
            if ($ruta -match '(?i)[\\/]\.claude[\\/](plugins|skills)[\\/]|consulta-bbdd\.ps1$' -or $ruta -match $credenciales) {
                Bloquear 'no se pueden modificar las skills, los plugins, el envoltorio de consultas ni las credenciales.'
            }
            if ($ruta -match '(?i)\.(py|ps1|psm1|js|ts|cs|java|bat|cmd|vbs|sh)$' -and $todo -match $accesoBbdd) {
                Bloquear 'no se pueden crear scripts que se conecten a la base de datos.'
            }
        }
        exit 0
    }
    exit 0
}
catch {
    Bloquear "error al comprobar la accion ($($_.Exception.Message))."
}
""";

    /// <summary>Norma de base de datos que se añade al prompt de sistema de Claude Code en las sesiones del plug-in.</summary>
    /// <remarks>Sin comillas dobles: Windows PowerShell 5.1 las rompe al pasar el argumento a claude.exe.</remarks>
    public static string ReglaBaseDatos(string consulta, string bbdd) =>
        "Sesión abierta desde el plug-in de PL/SQL Developer. Contra la base de datos solo puedes consultar, y el usuario " +
        "aprueba cada consulta: una sentencia SELECT, WITH o DESCRIBE por llamada al envoltorio del plug-in, así: " +
        $"& '{consulta}' -Sql 'SELECT ...' [-Bd <nombre>], con el SQL entre comillas simples (doblando las del SQL) o " +
        "con -File y un .sql; -Lista dice qué BBDD tienes. Sin -Bd consulta la de desarrollo. " + bbdd +
        " Cuando consultes una que no sea la de desarrollo, di de cuál sale el dato. Nada de DML, DDL, PL/SQL ni COMMIT, " +
        "ni sqlplus, sql u otros scripts que se conecten por su cuenta, ni leer credenciales. Un hook del plug-in bloquea " +
        "todo lo demás: si te bloquea, no busques otra vía; explica qué habría que ejecutar y deja que lo haga el usuario. " +
        "Para abrir cosas en PL/SQL Developer (aunque esté cerrado: lo arranca) usa " +
        $"& '{System.IO.Path.Combine(System.IO.Path.GetDirectoryName(consulta) ?? "", "plsql.ps1")}' con -Abrir <fichero>, " +
        "-Nuevo <fichero> -Desde <fichero con el texto en UTF-8> (lo crea en Windows-1252 y nunca pisa uno que exista) o " +
        "-Ventana sql|test|programa|comandos [-Desde <fichero>], y -Bd <nombre> para conectar la ventana a una de esas " +
        "BBDD. No ejecuta nada: compilar y ejecutar lo hace el usuario.";

    /// <summary>Ajustes de Claude Code para las sesiones del plug-in (--settings): el hook de solo consultas.</summary>
    public static string SettingsJson(string hookPath)
    {
        // Sin Git Bash el hook tiene que llevar "shell": "powershell". Se ejecuta como bloque de script para no
        // depender de la directiva de ejecución y para que lea la entrada del hook en el mismo proceso.
        string path = hookPath.Replace('\\', '/').Replace("'", "''");
        string command = "& ([scriptblock]::Create([IO.File]::ReadAllText('" + path + "'))); exit $LASTEXITCODE";
        string escaped = command.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\n" +
               "  \"permissions\": {\n" +
               "    \"deny\": [\"mcp__sqlcl__run-sqlcl\"]\n" +
               "  },\n" +
               "  \"hooks\": {\n" +
               "    \"PreToolUse\": [\n" +
               "      {\n" +
               "        \"matcher\": \"mcp__.*|Bash|PowerShell|Edit|Write|MultiEdit|NotebookEdit|Read|Grep\",\n" +
               "        \"hooks\": [{ \"type\": \"command\", \"shell\": \"powershell\", \"command\": \"" + escaped + "\" }]\n" +
               "      }\n" +
               "    ]\n" +
               "  }\n" +
               "}\n";
    }

    /// <summary>Alta de una BD para Claude (botones Conectar a BBDD y Fijar como BD de desarrollo). Se reescribe en cada uso en %APPDATA%\ClaudeCodePlsql\alta-bbdd.ps1.</summary>
    public const string AltaBbdd = """
# Da de alta una BD para Claude (botones "Conectar a BBDD" y "Fijar como BD de desarrollo" del plug-in).
# Lo lanza el plug-in, que le pasa por stdin un JSON con la sesion activa de PL/SQL Developer. Escribe el .env que lee
# consulta-bbdd.ps1 (BD_USUARIO, BD_CLAVE, BD_HOST, BD_PUERTO, BD_SERVICIO) y el indice de conexiones que leen el
# plug-in, el envoltorio y el mod:
#   modo desarrollo: %APPDATA%\ClaudeCodePlsql\bbdd\desarrollo.env y desarrollo.json (para todos los proyectos)
#   modo proyecto:   %APPDATA%\ClaudeCodePlsql\bbdd\<proyecto>\<nombre>.env y conexiones.json (solo ese proyecto)
# La clave llega por stdin y solo se escribe en el .env (en el perfil local, nunca en la carpeta del expediente).
$ErrorActionPreference = 'Stop'
$dirBbdd = Join-Path $env:APPDATA 'ClaudeCodePlsql\bbdd'

# Descriptor de la base: si ya es un descriptor o un EZConnect se usa tal cual; si es un alias, se busca en el
# tnsnames.ora de la sesion (el TNS_ADMIN con el que corre PL/SQL Developer)
function Resolver-Descriptor([string]$bd, [string]$tnsAdmin) {
    if ($bd.Contains('(')) { return $bd }
    if ($bd -match '^/{0,2}(?<h>[\w.-]+)(:(?<p>\d+))?/(?<s>[\w.$-]+)$') {
        $p = if ($Matches['p']) { $Matches['p'] } else { '1521' }
        return "(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=$($Matches['h']))(PORT=$p))(CONNECT_DATA=(SERVICE_NAME=$($Matches['s']))))"
    }
    $dirs = @($tnsAdmin, $env:TNS_ADMIN, $(if ($env:ORACLE_HOME) { Join-Path $env:ORACLE_HOME 'network\admin' })) | Where-Object { $_ }
    foreach ($d in $dirs) {
        $f = Join-Path $d 'tnsnames.ora'
        if (-not (Test-Path -LiteralPath $f)) { continue }
        $t = [regex]::Replace([System.IO.File]::ReadAllText($f), '(?m)^\s*#.*$', '')
        $m = [regex]::Match($t, '(?im)^\s*' + [regex]::Escape($bd) + '(\.[\w.]+)?\s*=\s*')
        if (-not $m.Success) { continue }
        # Desde el '=' hasta cerrar el primer parentesis de nivel 0
        $i = $t.IndexOf('(', $m.Index + $m.Length); if ($i -lt 0) { continue }
        $nivel = 0
        for ($j = $i; $j -lt $t.Length; $j++) {
            if ($t[$j] -eq '(') { $nivel++ } elseif ($t[$j] -eq ')') { $nivel--; if ($nivel -eq 0) { return $t.Substring($i, $j - $i + 1) } }
        }
    }
    throw "No encuentro la base '$bd' en ningun tnsnames.ora ($($dirs -join '; '))."
}

# consulta-bbdd.ps1 conecta por EZConnect: hace falta host, puerto y servicio (con SID no se puede)
function Partes-Conexion([string]$descriptor) {
    $host_ = [regex]::Match($descriptor, '(?i)\(\s*HOST\s*=\s*([^)\s]+)\s*\)').Groups[1].Value
    $port  = [regex]::Match($descriptor, '(?i)\(\s*PORT\s*=\s*(\d+)\s*\)').Groups[1].Value
    $serv  = [regex]::Match($descriptor, '(?i)\(\s*SERVICE_NAME\s*=\s*([^)\s]+)\s*\)').Groups[1].Value
    if (-not $serv -and $descriptor -match '(?i)\(\s*SID\s*=') {
        throw 'La base se define con SID y no con SERVICE_NAME: el plug-in solo conecta por servicio.'
    }
    if (-not $host_ -or -not $serv) { throw "No saco host y servicio de la definicion de la base: $descriptor" }
    if (-not $port) { $port = '1521' }
    return @{ host = $host_; port = $port; servicio = $serv }
}

function Escribir-Env([string]$ruta, [string]$usuario, [string]$clave, $partes) {
    $texto = "# Lo escribe el plug-in de PL/SQL Developer (boton Conectar a BBDD). No lo edites a mano.`r`n" +
             "BD_USUARIO=$usuario`r`nBD_CLAVE=$clave`r`nBD_HOST=$($partes.host)`r`n" +
             "BD_PUERTO=$($partes.port)`r`nBD_SERVICIO=$($partes.servicio)`r`n"
    # En ANSI, como lo lee consulta-bbdd.ps1 (Get-Content -Encoding Default)
    [System.IO.File]::WriteAllText($ruta, $texto, [System.Text.Encoding]::Default)
}

function Escribir-Json([string]$ruta, $valor) {
    [System.IO.File]::WriteAllText($ruta, (ConvertTo-Json -InputObject $valor -Depth 5), (New-Object System.Text.UTF8Encoding $false))
}

try {
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    $e = [Console]::In.ReadToEnd() | ConvertFrom-Json
    if (-not $e.usuario -or -not $e.clave -or -not $e.bd) { throw 'Faltan datos de la conexion (usuario, clave o base de datos).' }
    if ([string]$e.clave -match '"') { throw 'La contrasena tiene una comilla doble y no se puede usar en el CONNECT de sqlplus.' }
    $partes = Partes-Conexion (Resolver-Descriptor ([string]$e.bd) ([string]$e.tnsAdmin))
    $fecha = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    New-Item -ItemType Directory -Force -Path $dirBbdd | Out-Null

    if ($e.modo -eq 'desarrollo') {
        $env_ = Join-Path $dirBbdd 'desarrollo.env'
        Escribir-Env $env_ ([string]$e.usuario) ([string]$e.clave) $partes
        Escribir-Json (Join-Path $dirBbdd 'desarrollo.json') ([ordered]@{
            nombre = [string]$e.nombre; origen = [string]$e.origen; tipo = 'desarrollo'; env = $env_; fecha = $fecha })
    }
    elseif ($e.modo -eq 'proyecto') {
        if ([string]$e.proyecto -notmatch '^[0-9a-f]{12}$') { throw 'Identificador de proyecto no valido.' }
        $nombre = ([string]$e.nombre -replace '[^A-Za-z0-9_]', '_').ToLowerInvariant()
        $dir = Join-Path $dirBbdd ([string]$e.proyecto)
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $env_ = Join-Path $dir "$nombre.env"
        Escribir-Env $env_ ([string]$e.usuario) ([string]$e.clave) $partes

        # El indice conserva las demas conexiones del proyecto y sustituye la de este nombre
        $fIndice = Join-Path $dir 'conexiones.json'
        $lista = @()
        if (Test-Path -LiteralPath $fIndice) {
            $previo = Get-Content -LiteralPath $fIndice -Raw -Encoding UTF8 | ConvertFrom-Json
            $lista = @($previo.conexiones | Where-Object { $_ -and $_.nombre -ne $nombre })
        }
        $lista += [pscustomobject][ordered]@{ nombre = $nombre; origen = [string]$e.origen; tipo = [string]$e.tipo; env = $env_; fecha = $fecha }
        Escribir-Json $fIndice ([ordered]@{ proyecto = [string]$e.ruta; conexiones = [object[]]$lista })
    }
    else { throw "Modo desconocido: '$($e.modo)'." }

    Write-Output "OK $env_"
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
""";

    /// <summary>
    /// Envoltorio de consultas de solo lectura contra las BBDD dadas de alta con los botones del plug-in. Se reescribe
    /// en cada arranque en %APPDATA%\ClaudeCodePlsql\consulta-bbdd.ps1.
    /// </summary>
    public const string ConsultaBbdd = """
# Consulta de solo lectura contra una BD dada de alta con los botones del plug-in "Claude Code para PL/SQL Developer"
# (lo escribe el plug-in en cada arranque; no es para editarlo a mano).
#
# Uso:
#   & '<ruta>\consulta-bbdd.ps1' -Sql 'SELECT ...' [-Bd <nombre>] [-MaxLines n]
#   & '<ruta>\consulta-bbdd.ps1' -File <consulta.sql> [-Bd <nombre>]
#   & '<ruta>\consulta-bbdd.ps1' -Lista
#
# Sin -Bd consulta la BD de desarrollo (boton "Fijar como BD de desarrollo"). Con -Bd, una de las conectadas en el
# proyecto de esta sesion de Claude (boton "Conectar a BBDD"; el proyecto lo pone el plug-in en CLAUDE_PLSQL_PROYECTO).
#
# Solo lectura, en tres capas: el filtro de este script (y antes el hook del plug-in), sqlplus en modo restringido
# -R 3 (sin HOST, @, SPOOL ni nada que toque el disco) y SET TRANSACTION READ ONLY. Ninguna frena una funcion con
# PRAGMA AUTONOMOUS_TRANSACTION llamada desde un SELECT.
#
# La clave sale del .env y va en el CONNECT del guion que recibe sqlplus por stdin, en un temporal que se borra
# siempre; nunca en la linea de comandos.
param(
    [string]$Sql,
    [string]$File,
    [string]$Bd = 'desarrollo',
    [int]$MaxLines = 200,
    [switch]$Lista
)

$ErrorActionPreference = 'Stop'
$dirBbdd = Join-Path $env:APPDATA 'ClaudeCodePlsql\bbdd'

function Rechazo([string]$mensaje, [int]$codigo) {
    [Console]::Error.WriteLine($mensaje)
    exit $codigo
}

function Leer-Json([string]$ruta) {
    if (-not (Test-Path -LiteralPath $ruta)) { return $null }
    try { return Get-Content -LiteralPath $ruta -Raw -Encoding UTF8 | ConvertFrom-Json } catch { return $null }
}

# BBDD que puede consultar esta sesion: nombre (para -Bd), usuario@alias y tipo
function Disponibles {
    $lista = @()
    $d = Leer-Json (Join-Path $dirBbdd 'desarrollo.json')
    if ($d -and (Test-Path -LiteralPath (Join-Path $dirBbdd 'desarrollo.env'))) {
        $lista += [pscustomobject]@{ nombre = 'desarrollo'; origen = [string]$d.origen; tipo = 'desarrollo' }
    }
    $proyecto = $env:CLAUDE_PLSQL_PROYECTO
    if ($proyecto -match '^[0-9a-f]{12}$') {
        $i = Leer-Json (Join-Path $dirBbdd "$proyecto\conexiones.json")
        foreach ($c in @($i.conexiones)) {
            if ($c -and (Test-Path -LiteralPath (Join-Path $dirBbdd "$proyecto\$($c.nombre).env"))) {
                $lista += [pscustomobject]@{ nombre = [string]$c.nombre; origen = [string]$c.origen; tipo = [string]$c.tipo }
            }
        }
    }
    return $lista
}

if ($Lista) {
    $d = @(Disponibles)
    if ($d.Count -eq 0) { 'Ninguna BD conectada: se conectan con los botones de PL/SQL Developer.' }
    else { $d | ForEach-Object { "$($_.nombre)|$($_.origen)|$($_.tipo)" } }
    exit 0
}

# ---------------------------------------------------------------------------- la BD
if ($Bd -notmatch '^[A-Za-z0-9_]+$') { Rechazo "Nombre de BD no valido: '$Bd'." 2 }
$Bd = $Bd.ToLowerInvariant()
if ($Bd -eq 'desarrollo') {
    $envFile = Join-Path $dirBbdd 'desarrollo.env'
} else {
    $proyecto = $env:CLAUDE_PLSQL_PROYECTO
    if ($proyecto -notmatch '^[0-9a-f]{12}$') { Rechazo 'Esta sesion no es de un proyecto del plug-in: solo se puede consultar la BD de desarrollo.' 3 }
    $envFile = Join-Path $dirBbdd "$proyecto\$Bd.env"
}
if (-not (Test-Path -LiteralPath $envFile)) {
    $hay = (@(Disponibles) | ForEach-Object { $_.nombre }) -join ', '
    if (-not $hay) { $hay = 'ninguna' }
    Rechazo "La BD '$Bd' no esta conectada en esta sesion (disponibles: $hay). Se conectan con los botones de PL/SQL Developer." 3
}

# ---------------------------------------------------------------------------- la sentencia
if ($File) {
    if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { Rechazo "No existe el fichero: $File" 2 }
    $texto = Get-Content -LiteralPath $File -Raw -Encoding Default
} elseif ($Sql) {
    $texto = $Sql
} else {
    Rechazo "Indica la consulta con -Sql 'SELECT ...' o con -File <fichero.sql>." 2
}
$texto = "$texto".Trim()
if (-not $texto) { Rechazo 'La consulta esta vacia.' 2 }

# Filtro de solo lectura: quita comentarios y literales para que no den falsos positivos ni oculten nada
$limpio = [regex]::Replace($texto, '/\*.*?\*/', ' ', 'Singleline')
$limpio = [regex]::Replace($limpio, '--[^\r\n]*', ' ')
if ($limpio -match "(?i)\b[nq]?q'") { Rechazo "BLOQUEADO: los literales q'...' no se admiten." 2 }
$limpio = [regex]::Replace($limpio, "'(?:[^']|'')*'", ' LITERAL ')
$limpio = [regex]::Replace($limpio, '"[^"]*"', 'X').Trim()
$limpio = [regex]::Replace($limpio, '[\s;/]+$', '')
if ($limpio -match "['""]") { Rechazo 'BLOQUEADO: comillas sin cerrar.' 2 }
if ($limpio.Contains(';')) { Rechazo 'BLOQUEADO: una sola sentencia por llamada.' 2 }
if ($limpio -notmatch '(?i)^DESC(RIBE)?\s+[\w$#.]+$') {
    if ($limpio -notmatch '(?i)^\(*\s*(SELECT|WITH)\b') { Rechazo 'BLOQUEADO: solo SELECT, WITH o DESCRIBE.' 2 }
    $m = [regex]::Match($limpio, '(?i)\b(INSERT|UPDATE|DELETE|MERGE|UPSERT|CREATE|ALTER|DROP|TRUNCATE|RENAME|GRANT|REVOKE|COMMENT|AUDIT|NOAUDIT|PURGE|FLASHBACK|ANALYZE|LOCK|COMMIT|ROLLBACK|SAVEPOINT|BEGIN|DECLARE|EXEC|EXECUTE|CALL|FUNCTION|PROCEDURE|HOST|SPOOL|DISCONNECT|PASSWORD|SHUTDOWN|STARTUP)\b')
    if ($m.Success) { Rechazo "BLOQUEADO: la sentencia contiene '$($m.Value.ToUpper())'." 2 }
}

# ---------------------------------------------------------------------------- credenciales y sqlplus
$cfg = @{}
foreach ($linea in (Get-Content -LiteralPath $envFile -Encoding Default)) {
    $l = $linea.Trim()
    if (-not $l -or $l.StartsWith('#')) { continue }
    $i = $l.IndexOf('=')
    if ($i -lt 1) { continue }
    # Solo por el primer '=': la clave puede llevar '='
    $cfg[$l.Substring(0, $i).Trim()] = $l.Substring($i + 1).Trim()
}
foreach ($k in 'BD_USUARIO', 'BD_CLAVE', 'BD_HOST', 'BD_PUERTO', 'BD_SERVICIO') {
    if (-not $cfg[$k]) { Rechazo "Falta $k en la conexion '$Bd': vuelve a conectarla con el boton de PL/SQL Developer." 3 }
}
if ($cfg['BD_CLAVE'].Contains('"')) { Rechazo 'La clave tiene una comilla doble y no se puede entrecomillar en el CONNECT.' 3 }

$sqlplus = (Get-Command 'sqlplus' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
if (-not $sqlplus -and $env:ORACLE_HOME) { $sqlplus = Join-Path $env:ORACLE_HOME 'bin\sqlplus.exe' }
if (-not $sqlplus -or -not (Test-Path -LiteralPath $sqlplus)) {
    $sqlplus = Get-ChildItem 'C:\Oracle\*\bin\sqlplus.exe', 'C:\app\*\product\*\*\bin\sqlplus.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $sqlplus) { Rechazo 'No se encuentra sqlplus.exe: hace falta el cliente de Oracle (en el PATH o en ORACLE_HOME).' 4 }

$cuerpo = $texto.TrimEnd()
if (-not $cuerpo.EndsWith(';') -and -not $cuerpo.EndsWith('/')) { $cuerpo += ';' }
$guion = @(
    'WHENEVER SQLERROR EXIT FAILURE',
    'WHENEVER OSERROR EXIT FAILURE',
    "CONNECT $($cfg['BD_USUARIO'])/`"$($cfg['BD_CLAVE'])`"@//$($cfg['BD_HOST']):$($cfg['BD_PUERTO'])/$($cfg['BD_SERVICIO'])",
    'SET DEFINE OFF', 'SET ECHO OFF', 'SET VERIFY OFF', 'SET HEADING ON', 'SET SQLBLANKLINES ON', 'SET SERVEROUTPUT OFF',
    'SET LONG 200000', 'SET LONGCHUNKSIZE 200000', 'SET NULL [NULL]', "SET MARKUP CSV ON DELIMITER '|' QUOTE OFF",
    'SET PAGESIZE 50000', 'SET FEEDBACK OFF',
    'SET TRANSACTION READ ONLY;',
    'SET FEEDBACK ON',
    $cuerpo,
    'EXIT SUCCESS'
) -join "`r`n"

# ---------------------------------------------------------------------------- ejecucion
$w1252 = [System.Text.Encoding]::GetEncoding(1252)
$fEntrada = [System.IO.Path]::GetTempFileName()
$fSalida = [System.IO.Path]::GetTempFileName()
$nlsPrevio = $env:NLS_LANG
$cron = [System.Diagnostics.Stopwatch]::StartNew()
$cortada = $false
try {
    # Windows-1252 y CRLF, como los .sql de LIBRA
    [System.IO.File]::WriteAllText($fEntrada, $guion + "`r`n", $w1252)
    $env:NLS_LANG = 'SPANISH_SPAIN.WE8MSWIN1252'
    $p = Start-Process -FilePath $sqlplus -ArgumentList @('-S', '-L', '-R', '3', '/nolog') -RedirectStandardInput $fEntrada `
                       -RedirectStandardOutput $fSalida -NoNewWindow -PassThru
    $null = $p.Handle  # sin esto, Start-Process deja ExitCode vacio al terminar
    if (-not $p.WaitForExit(120000)) {
        try { $p.Kill() } catch { }
        $cortada = $true
    } else {
        $codigo = $p.ExitCode
        $lineas = @([System.IO.File]::ReadAllLines($fSalida, $w1252))
    }
}
finally {
    $env:NLS_LANG = $nlsPrevio
    # El de entrada lleva la clave: se borra siempre (con .NET, que no se lia con rutas 8.3 como RUBEN~1)
    foreach ($f in $fEntrada, $fSalida) { try { [System.IO.File]::Delete($f) } catch { } }
}
$cron.Stop()
if ($cortada) { Rechazo "La consulta contra '$Bd' ha pasado de 120 s y se ha cortado: acotala (ROWNUM) o partela." 124 }
# Un CONNECT fallido no es SQLERROR: sqlplus sigue y acaba en EXIT SUCCESS aunque no haya hecho nada
if ($null -eq $codigo -or ($codigo -eq 0 -and @($lineas -match '^\s*(ERROR:|ORA-\d|SP2-\d)').Count -gt 0)) { $codigo = 1 }

$truncada = $MaxLines -gt 0 -and $lineas.Count -gt $MaxLines
if ($truncada) { $lineas[0..($MaxLines - 1)] } else { $lineas }
$meta = "-- $Bd | $([math]::Round($cron.Elapsed.TotalSeconds, 2)) s"
if ($truncada) { $meta += " | SALIDA TRUNCADA: $($lineas.Count) lineas, mostradas $MaxLines (acota con ROWNUM o sube -MaxLines)" }
''
$meta
exit $codigo
""";

    /// <summary>
    /// Abre ficheros y ventanas en PL/SQL Developer desde Claude Code (peticiones que atiende el plug-in). Se reescribe en
    /// cada arranque en %APPDATA%\ClaudeCodePlsql\plsql.ps1.
    /// </summary>
    public const string PlsqlScript = """
# Abre ficheros y ventanas en PL/SQL Developer desde Claude Code (lo escribe el plug-in en cada arranque).
#
# Uso:
#   & '<ruta>\plsql.ps1' -Abrir <fichero> [-Bd <nombre>]
#   & '<ruta>\plsql.ps1' -Nuevo <fichero> -Desde <fichero con el texto> [-Bd <nombre>]
#   & '<ruta>\plsql.ps1' -Ventana sql|test|programa|comandos [-Desde <fichero con el texto>] [-Bd <nombre>]
#
#   -Abrir    abre un fichero que ya existe (el tipo de ventana sale de la extension).
#   -Nuevo    crea el fichero con el texto de -Desde (en Windows-1252, CRLF y sin BOM, como los fuentes de LIBRA) y lo
#             abre. Nunca pisa un fichero que ya exista.
#   -Ventana  abre una ventana nueva sin fichero, con el texto de -Desde si se da. Nunca se ejecuta.
#   -Bd       la ventana se conecta a esa BD: "desarrollo" o una de las conectadas en el proyecto de esta sesion
#             (consulta-bbdd.ps1 -Lista dice cuales hay). Sin -Bd, la conexion que tenga PL/SQL Developer.
#
# Con PL/SQL Developer abierto, deja la peticion en %APPDATA%\ClaudeCodePlsql\peticiones y la atiende el plug-in (que
# lee el mismo la contraseña: no va en la peticion). Si esta cerrado, lo arranca conectado a esa BD y luego abre lo
# pedido.
param(
    [string]$Abrir,
    [string]$Nuevo,
    [string]$Desde,
    [ValidateSet('sql', 'test', 'programa', 'comandos')]
    [string]$Ventana,
    [string]$Bd
)

$ErrorActionPreference = 'Stop'
$dirPlugin = Join-Path $env:APPDATA 'ClaudeCodePlsql'
$dirBbdd = Join-Path $dirPlugin 'bbdd'
$dirPeticiones = Join-Path $dirPlugin 'peticiones'
$utf8 = New-Object System.Text.UTF8Encoding $false

function Rechazo([string]$mensaje, [int]$codigo = 2) {
    [Console]::Error.WriteLine($mensaje)
    exit $codigo
}

if (@($Abrir, $Nuevo, $Ventana | Where-Object { $_ }).Count -ne 1) { Rechazo 'Indica una sola accion: -Abrir, -Nuevo o -Ventana.' }

# ---------------------------------------------------------------------------- la BD (mismas reglas que consulta-bbdd.ps1)
$proyecto = $env:CLAUDE_PLSQL_PROYECTO
$envFile = $null
if ($Bd) {
    if ($Bd -notmatch '^[A-Za-z0-9_]+$') { Rechazo "Nombre de BD no valido: '$Bd'." }
    $Bd = $Bd.ToLowerInvariant()
    if ($Bd -eq 'desarrollo') { $envFile = Join-Path $dirBbdd 'desarrollo.env' }
    elseif ($proyecto -match '^[0-9a-f]{12}$') { $envFile = Join-Path $dirBbdd "$proyecto\$Bd.env" }
    else { Rechazo 'Esta sesion no es de un proyecto del plug-in: solo se puede usar la BD de desarrollo.' 3 }
    if (-not (Test-Path -LiteralPath $envFile)) { Rechazo "La BD '$Bd' no esta conectada en esta sesion. Se conectan con los botones de PL/SQL Developer." 3 }
}

# ---------------------------------------------------------------------------- el fichero
$tipos = @{ sql = 1; test = 2; programa = 3; comandos = 4 }
$peticion = [ordered]@{ accion = ''; fichero = ''; tipo = 0; texto = ''; bd = [string]$Bd; proyecto = [string]$proyecto }

function Texto-De([string]$ruta) {
    if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) { Rechazo "No existe el fichero $ruta." }
    return [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $ruta).Path, $utf8)
}

if ($Abrir) {
    if (-not (Test-Path -LiteralPath $Abrir -PathType Leaf)) { Rechazo "No existe el fichero $Abrir." }
    $peticion.accion = 'abrir'
    $peticion.fichero = (Resolve-Path -LiteralPath $Abrir).Path
}
elseif ($Nuevo) {
    if (-not $Desde) { Rechazo 'Con -Nuevo hace falta -Desde: el fichero con el texto (en UTF-8).' }
    $completo = [System.IO.Path]::GetFullPath($Nuevo)
    if (Test-Path -LiteralPath $completo) { Rechazo "Ya existe ${completo}: no lo piso. Para abrirlo, usa -Abrir." }
    $texto = (Texto-De $Desde).TrimStart([char]0xFEFF) -replace "`r`n", "`n" -replace "`n", "`r`n"
    # Los fuentes de LIBRA van en Windows-1252: si algun caracter no existe ahi, mejor no crear el fichero
    $w1252 = [System.Text.Encoding]::GetEncoding(1252, [System.Text.EncoderFallback]::ExceptionFallback, [System.Text.DecoderFallback]::ExceptionFallback)
    try { $bytes = $w1252.GetBytes($texto) } catch { Rechazo 'El texto tiene caracteres que no existen en Windows-1252 (flechas, simbolos, emojis...): cambialos y vuelve a intentarlo.' }
    New-Item -ItemType Directory -Force -Path (Split-Path $completo) | Out-Null
    [System.IO.File]::WriteAllBytes($completo, $bytes)
    Write-Output "Creado $completo (Windows-1252, CRLF, sin BOM)."
    $peticion.accion = 'abrir'
    $peticion.fichero = $completo
}
else {
    $peticion.accion = 'ventana'
    $peticion.tipo = $tipos[$Ventana]
    if ($Desde) { $peticion.texto = (Texto-De $Desde).TrimStart([char]0xFEFF) -replace "`r`n", "`n" -replace "`n", "`r`n" }
}

# ---------------------------------------------------------------------------- a PL/SQL Developer
New-Item -ItemType Directory -Force -Path $dirPeticiones | Out-Null
$abierto = @(Get-Process plsqldev -ErrorAction SilentlyContinue).Count -gt 0

if (-not $abierto) {
    # Se arranca PL/SQL Developer, conectado a la BD si se ha pedido; la peticion la atiende el plug-in al activarse
    $exe = $null
    $ini = Join-Path $dirPlugin 'ClaudeCode.ini'
    if (Test-Path -LiteralPath $ini) {
        $l = [System.IO.File]::ReadAllLines($ini, $utf8) | Where-Object { $_ -match '^\s*DllInstalada\s*=' } | Select-Object -First 1
        if ($l) { $c = Join-Path (Split-Path (Split-Path ($l -replace '^\s*DllInstalada\s*=\s*', ''))) 'plsqldev.exe'; if (Test-Path -LiteralPath $c) { $exe = $c } }
    }
    if (-not $exe) {
        $exe = Get-ChildItem 'C:\Oracle\PLSQLDeveloper*\plsqldev.exe', "$env:ProgramFiles\PLSQL Developer*\plsqldev.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $exe) { Rechazo 'PL/SQL Developer esta cerrado y no encuentro plsqldev.exe para arrancarlo: abrelo y vuelve a intentarlo.' 4 }

    $argumentos = @()
    if ($envFile) {
        $cfg = @{}
        foreach ($linea in (Get-Content -LiteralPath $envFile -Encoding Default)) {
            $i = $linea.IndexOf('='); if ($linea -notmatch '^\s*#' -and $i -gt 0) { $cfg[$linea.Substring(0, $i).Trim()] = $linea.Substring($i + 1).Trim() }
        }
        # Como lo arranca el lanzador de LIBRA: la clave va en la linea de comandos de plsqldev.exe
        $argumentos += "userid=$($cfg['BD_USUARIO'])/`"$($cfg['BD_CLAVE'])`"@$($cfg['BD_HOST']):$($cfg['BD_PUERTO'])/$($cfg['BD_SERVICIO'])"
        $peticion.bd = ''   # ya va conectado
    }
    Start-Process -FilePath $exe -ArgumentList $argumentos | Out-Null
    Write-Output 'PL/SQL Developer arrancando; en cuanto cargue el plug-in abrira lo pedido.'
}

$f = Join-Path $dirPeticiones ("{0:yyyyMMddHHmmssfff}-{1}.json" -f (Get-Date), [guid]::NewGuid().ToString('N').Substring(0, 6))
[System.IO.File]::WriteAllText($f, (ConvertTo-Json -InputObject $peticion -Compress), $utf8)
if ($abierto) { Write-Output 'Peticion enviada a PL/SQL Developer.' }
exit 0
""";

    // Mod de Claude Code con la franja de BBDD conectadas (%APPDATA%\ClaudeCodePlsql\mod-bbdd, cargado con --plugin-dir)

    /// <summary>plugin.json del mod de la franja de BBDD.</summary>
    public const string ModPluginJson = """
{
  "name": "plsql-bbdd",
  "version": "0.1.0",
  "description": "BBDD que puede consultar esta sesión de Claude abierta desde PL/SQL Developer (desarrollo y las del proyecto)",
  "types": "./types/index.d.ts"
}
""";

    /// <summary>hooks.json del mod de la franja de BBDD.</summary>
    public const string ModHooksJson = """
{ "modules": ["./register.tsx"] }
""";

    /// <summary>Contrato de tipos del mod de la franja de BBDD.</summary>
    public const string ModTypes = """
export type Conexion = { nombre: string; origen: string; tipo: 'desarrollo' | 'pruebas' | 'produccion' }

declare module 'claude-code' {
  interface PluginState {
    'plsql-bbdd': { conexiones: Conexion[] }
  }
}
""";

    /// <summary>Módulo del mod: lee los índices de BBDD (nunca los .env) y los pinta encima del cuadro de escritura.</summary>
    public const string ModRegister = """
import { atom, read, update } from 'claude-code'
import type { EngineInterface, Register } from 'claude-code'

import type { Conexion } from '../types'

// Franja encima del cuadro de escritura con las BBDD que puede consultar esta sesión de Claude: la de desarrollo
// (para todos los proyectos) y las conectadas en este proyecto con el botón "Conectar a BBDD" de PL/SQL Developer.
// La escribe el plug-in en %APPDATA%\ClaudeCodePlsql\mod-bbdd y la carga con --plugin-dir. Solo lee los índices
// (nombres y tipos), nunca los .env con las credenciales.
const conexiones = atom({ plugin: 'plsql-bbdd', key: 'conexiones' } as const, [] as Conexion[])

async function leerJson($: EngineInterface, ruta: string): Promise<any> {
  try {
    return JSON.parse(await $.fs.read(ruta))
  } catch {
    return null
  }
}

async function leer($: EngineInterface): Promise<Conexion[]> {
  const appdata = await $.env.get('APPDATA')
  if (!appdata) return []
  const dir = `${appdata}\\ClaudeCodePlsql\\bbdd`
  const lista: Conexion[] = []

  const desarrollo = await leerJson($, `${dir}\\desarrollo.json`)
  if (desarrollo?.origen) {
    lista.push({ nombre: 'desarrollo', origen: String(desarrollo.origen), tipo: 'desarrollo' })
  }

  // El proyecto de esta sesión lo pone el plug-in al abrir Claude
  const proyecto = await $.env.get('CLAUDE_PLSQL_PROYECTO')
  if (proyecto && /^[0-9a-f]{12}$/.test(proyecto)) {
    const indice = await leerJson($, `${dir}\\${proyecto}\\conexiones.json`)
    for (const c of indice?.conexiones ?? []) {
      if (c?.origen) {
        lista.push({ nombre: String(c.nombre), origen: String(c.origen), tipo: c.tipo === 'produccion' ? 'produccion' : 'pruebas' })
      }
    }
  }
  return lista
}

// Lo que se le dice a Claude de las BBDD: el nombre para -Bd, nunca dónde están las credenciales
function describir(lista: Conexion[], appdata: string): string {
  const desarrollo = lista.find(c => c.tipo === 'desarrollo')
  const otras = lista.filter(c => c.tipo !== 'desarrollo')
  const linea = (c: Conexion) => `${c.origen} (${c.tipo === 'produccion' ? 'producción' : c.tipo}), -Bd ${c.nombre}`
  return [
    'BBDD que puedes consultar ahora mismo desde esta sesión (las conecta el usuario con los botones de PL/SQL ' +
      'Developer; esta lista sustituye a cualquier otra que se te diera antes):',
    `- Desarrollo, la de por defecto: ${desarrollo ? `${desarrollo.origen} (sin -Bd)` : 'sin fijar; pide al usuario que pulse Fijar como BD de desarrollo'}.`,
    otras.length > 0 ? `- Del proyecto: ${otras.map(linea).join('; ')}.` : '- Del proyecto: ninguna.',
    `Consulta con & '${appdata}\\ClaudeCodePlsql\\consulta-bbdd.ps1' -Sql 'SELECT ...' [-Bd <nombre>].`,
  ].join('\n')
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    const refrescar = async () => {
      const nuevas = await leer($)
      if (JSON.stringify(nuevas) !== JSON.stringify(await read($, conexiones))) {
        await update($, conexiones, () => nuevas)
      }
    }
    await refrescar()
    // Los botones de PL/SQL Developer cambian los índices mientras la sesión sigue abierta
    $.clock.every(5000, () => {
      void refrescar()
    })
    return next(e)
  })

  // Cada mensaje lleva a Claude la lista de BBDD de ese momento: los botones de PL/SQL Developer la cambian con la
  // sesión ya abierta y lo que se le dijo al empezar deja de valer. Solo se añade cuando ha cambiado.
  let ultima = ''
  on('prompt.submit', async ($, e, next) => {
    if (e.command) return next(e)
    const texto = describir(await leer($), (await $.env.get('APPDATA')) ?? '%APPDATA%')
    if (texto === ultima) return next(e)
    ultima = texto
    return next({ ...e, context: [...(e.context ?? []), texto] })
  })

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    if (e.props.hasSurvey) return next(e)
    const lista = await read($, conexiones)
    const { Box, Text } = $.ui.resolve(e)
    const desarrollo = lista.find(c => c.tipo === 'desarrollo')
    const otras = lista.filter(c => c.tipo !== 'desarrollo')

    return (
      <Box>
        <Text dimColor>BBDD · desarrollo: </Text>
        <Text color="success">{desarrollo ? desarrollo.origen : 'sin fijar'}</Text>
        {otras.length > 0 ? <Text dimColor> · proyecto: </Text> : null}
        {otras.map((c, i) => (
          <Text key={c.nombre} color={c.tipo === 'produccion' ? 'error' : 'warning'}>
            {(i > 0 ? ', ' : '') + c.origen + (c.tipo === 'produccion' ? ' (⚠ producción)' : ' (pruebas)')}
          </Text>
        ))}
      </Box>
    )
  })
}
""";

    /// <summary>
    /// Iconos de la cinta, uno por opción (mismo índice que CreateMenuItem). SVG en una retícula de 21 x 21; el color
    /// #434A54 lo adapta PL/SQL Developer al tema claro u oscuro.
    /// </summary>
    public static readonly (int Index, string Nombre, string Svg)[] Iconos =
    {
        // El símbolo de Claude, en naranja; botón grande en la cinta (retícula de 37 x 37)
        (Plugin.MenuOpen, "Abrir en Claude Code",
            Svg("""<path d="M10.5 8.3L10.5 1.2M11.6 8.59L14.1 4.26M12.41 9.4L18.55 5.85M12.7 10.5L17.7 10.5M12.41 11.6L18.55 15.15M11.6 12.41L14.1 16.74M10.5 12.7L10.5 19.8M9.4 12.41L6.9 16.74M8.59 11.6L2.45 15.15M8.3 10.5L3.3 10.5M8.59 9.4L2.45 5.85M9.4 8.59L6.9 4.26" fill="none" stroke="#D97757" stroke-width="2.2" stroke-linecap="round"/>""", 37)),
        (Plugin.MenuAskSelection, "Preguntar por lo seleccionado",
            Svg("""<path d="M3.5 3.5h14a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H9l-4 3v-3H3.5a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z" fill="none" stroke="#434A54"/><path d="M8.8 7.5a1.8 1.8 0 1 1 2.6 1.6c-.6.3-.9.7-.9 1.3" fill="none" stroke="#434A54" stroke-width="1.4" stroke-linecap="round"/><circle cx="10.5" cy="12.3" r=".8" fill="#434A54"/>""")),
        (Plugin.MenuPull, "Traer cambios de Claude",
            Svg("""<path d="M5.5 2.5h7l3.5 3.5v12.5h-10.5z" fill="none" stroke="#434A54"/><path d="M10.75 8v6M8 11.5l2.75 2.75 2.75-2.75" fill="none" stroke="#2F80ED" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuCopyRef, "Copiar referencia para Claude",
            Svg("""<rect x="5" y="3.5" width="11" height="15" rx="1.5" fill="none" stroke="#434A54"/><rect x="8" y="2" width="5" height="3" rx="1" fill="#434A54"/><path d="M8.5 10h4M8.5 13h4" stroke="#434A54" stroke-width="1.4" stroke-linecap="round"/>""")),
        (Plugin.MenuFolder, "Abrir carpeta de trabajo",
            Svg("""<path d="M2.5 6v10a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V8a1 1 0 0 0-1-1H10L8.5 5h-5a1 1 0 0 0-1 1z" fill="none" stroke="#434A54"/>""")),
        (Plugin.MenuConfig, "Configurar",
            Svg("""<g fill="#434A54"><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(45 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(90 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(135 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(180 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(225 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(270 10.5 10.5)"/><rect x="9.3" y="1.4" width="2.4" height="3.8" rx=".6" transform="rotate(315 10.5 10.5)"/></g><circle cx="10.5" cy="10.5" r="4.6" fill="none" stroke="#434A54" stroke-width="2.8"/>""")),
        (Plugin.MenuConnect, "Conectar a BBDD",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#27AE60"/><path d="M16 13.8v4.4M13.8 16h4.4" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round"/>""")),
        (Plugin.MenuDisconnect, "Desconectar BBDD",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#EB5757"/><path d="M14.5 14.5l3 3M17.5 14.5l-3 3" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round"/>""")),
        (Plugin.MenuSetDev, "Fijar como BD de desarrollo",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#2F80ED"/><path d="M14 16l1.5 1.5 2.5-3" fill="none" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuCompare, "Comparar con otra BD",
            Svg("""<rect x="2.5" y="3.5" width="6.5" height="14" rx="1" fill="none" stroke="#434A54"/><rect x="12" y="3.5" width="6.5" height="14" rx="1" fill="none" stroke="#434A54"/><path d="M6.5 8.5h8M12.5 6.5l2 2-2 2M14.5 12.5h-8M8.5 10.5l-2 2 2 2" fill="none" stroke="#2F80ED" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuReview, "Revisar normas",
            Svg("""<path d="M5.5 2.5h7l3.5 3.5v12.5h-10.5z" fill="none" stroke="#434A54"/><path d="M7.5 11.5l2 2 4-4.5" fill="none" stroke="#27AE60" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuExplainError, "Explicar error",
            Svg("""<circle cx="10.5" cy="10.5" r="7.5" fill="none" stroke="#EB5757" stroke-width="1.5"/><path d="M10.5 6v5.5" stroke="#EB5757" stroke-width="1.8" stroke-linecap="round"/><circle cx="10.5" cy="14.6" r="1.1" fill="#EB5757"/>""")),
        (Plugin.MenuDocs, "Documentación del expediente",
            Svg("""<path d="M7.5 2.5h6.5l3 3v10.5h-9.5z" fill="none" stroke="#434A54"/><path d="M5 5.5v13h9" fill="none" stroke="#434A54"/><path d="M10 8.5h4.5M10 11h4.5M10 13.5h3" stroke="#2F80ED" stroke-width="1.2" stroke-linecap="round"/>""")),
        // Tres pilotos (trabajando, te espera, pide permiso), como en el propio panel; botón grande
        (Plugin.MenuSessions, "Panel de sesiones",
            Svg("""<rect x="1.5" y="2.5" width="18" height="16" rx="2.5" fill="none" stroke="#434A54" stroke-width="1.3"/><rect x="5" y="6" width="2.6" height="9" rx="1.3" fill="#E9B44C"/><rect x="9.2" y="6" width="2.6" height="9" rx="1.3" fill="#27AE60"/><rect x="13.4" y="6" width="2.6" height="9" rx="1.3" fill="#EB5757"/>""", 37)),
        (Plugin.MenuInstall, "Preparar instalación",
            Svg("""<path d="M3.5 7l7-3.5 7 3.5v8l-7 3.5-7-3.5z" fill="none" stroke="#434A54" stroke-linejoin="round"/><path d="M3.5 7l7 3.5 7-3.5M10.5 10.5v8" fill="none" stroke="#434A54" stroke-linejoin="round"/><path d="M14 2v4M12.5 4.5L14 6l1.5-1.5" fill="none" stroke="#27AE60" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>""")),
    };

    /// <summary>Icono dibujado en la retícula de 21 x 21; con tamaño 37, el mismo dibujo para un botón grande.</summary>
    private static string Svg(string body, int size = 21) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 21 21">{body}</svg>""";

    public static string McpJson(string sqlclPath)
    {
        string escaped = sqlclPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\n  \"mcpServers\": {\n    \"sqlcl\": {\n      \"command\": \"" + escaped +
               "\",\n      \"args\": [\"-mcp\"]\n    }\n  }\n}\n";
    }
}
