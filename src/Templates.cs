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

[MCP]
; Ruta a sql.exe de SQLcl 25.2 o superior para que Claude pueda consultar la base de datos. Si la rellenas, el
; plug-in se la pasa a Claude Code con --mcp-config (%APPDATA%\ClaudeCodePlsql\mcp-sqlcl.json), sin dejar nada en
; las carpetas de trabajo. Claude solo puede consultar (SELECT): el plug-in bloquea el resto y te pide aprobación
; en cada consulta.
; Ejemplo: C:\sqlcl\bin\sql.exe
RutaSqlcl=

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
# Las BBDD se consultan con el query.ps1 de la skill desa04-sqlplus: la de desarrollo (para todos los proyectos) y
# las que se conectaron con el boton "Conectar a BBDD" en ESTE proyecto (CLAUDE_PLSQL_PROYECTO, que pone el plug-in).
# Salida 2 = bloquear (el motivo va por stderr a Claude). Ante cualquier error, bloquea.
$ErrorActionPreference = 'Stop'
$dirBbdd = Join-Path $env:APPDATA 'ClaudeCodePlsql\bbdd'

function Bloquear([string]$motivo) {
    [Console]::Error.WriteLine("Bloqueado por el plug-in de PL/SQL Developer (solo consultas): $motivo")
    exit 2
}

function Preguntar([string]$motivo) {
    # Obliga a pedir aprobacion aunque haya reglas "allow" o se salten los permisos
    $r = @{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; permissionDecision = 'ask'; permissionDecisionReason = $motivo } }
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

# El .env tiene que ser el de desarrollo o uno de las BBDD conectadas en este proyecto
function Comprobar-EnvFile([string]$ruta) {
    $completa = [System.IO.Path]::GetFullPath($ruta)
    $desarrollo = [System.IO.Path]::GetFullPath((Join-Path $dirBbdd 'desarrollo.env'))
    if ($completa -ieq $desarrollo) { return }
    $proyecto = $env:CLAUDE_PLSQL_PROYECTO
    if ($proyecto -match '^[0-9a-f]{12}$') {
        $dirProyecto = [System.IO.Path]::GetFullPath((Join-Path $dirBbdd $proyecto)) + '\'
        if ($completa.StartsWith($dirProyecto, [StringComparison]::OrdinalIgnoreCase) -and $completa -match '(?i)\.env$' -and
            -not $completa.Substring($dirProyecto.Length).Contains('\')) { return }
    }
    Bloquear "esa BD no esta conectada en este proyecto ($ruta). Usa la de desarrollo o pide al usuario que pulse Conectar a BBDD."
}

# Llamada al query.ps1 de la skill desa04-sqlplus:
#   & '<ruta>\desa04-sqlplus\scripts\query.ps1' -Sql 'SELECT ...' [-EnvFile '<.env>'] [-MaxLines n]
function Comprobar-Query([string]$cmd) {
    $c = $cmd.Trim()
    $arg = '(?:''(?:[^'']|'''')*''|"[^"`]*"|[^\s''"`;|&<>(){}]+)'
    $m = [regex]::Match($c, "^(?:&\s*|(?:pwsh|powershell)(?:\.exe)?(?:\s+-(?:NoProfile|NonInteractive|NoLogo|ExecutionPolicy\s+Bypass))*\s+-File\s+)(?<ruta>$arg)(?<resto>.*)$", 'Singleline')
    if (-not $m.Success) {
        Bloquear "llama al envoltorio asi: & '<ruta>\query.ps1' -Sql 'SELECT ...' [-EnvFile '<fichero .env>'] [-MaxLines n], sin nada mas en la linea."
    }
    $script = Valor-Argumento $m.Groups['ruta'].Value
    if ($script -notmatch '(?i)[\\/]\.claude[\\/]skills[\\/](.+[\\/])?desa04-sqlplus[\\/]scripts[\\/]query\.ps1$') {
        Bloquear 'solo se admite el query.ps1 de la skill desa04-sqlplus.'
    }
    $resto = $m.Groups['resto'].Value
    $sql = $null; $fichero = $null; $envFile = $null; $diagnostico = $false
    $p = [regex]::Match($resto, "\G\s+-(?:(?<n>Sql|File|EnvFile|MaxLines)\s+(?<v>$arg)|(?<s>Diagnostico|MostrarScript))(?=\s|$)", 'IgnoreCase')
    $pos = 0
    while ($p.Success -and $p.Index -eq $pos) {
        $pos = $p.Index + $p.Length
        if ($p.Groups['s'].Success) { if ($p.Groups['s'].Value -ieq 'Diagnostico') { $diagnostico = $true } }
        else {
            $v = Valor-Argumento $p.Groups['v'].Value
            switch ($p.Groups['n'].Value) {
                'Sql'      { $sql = $v }
                'File'     { $fichero = $v }
                'EnvFile'  { $envFile = $v }
                'MaxLines' { if ($v -notmatch '^\d+$') { Bloquear '-MaxLines tiene que ser un numero.' } }
            }
        }
        $p = $p.NextMatch()
    }
    if ($resto.Substring($pos).Trim()) { Bloquear "argumentos no permitidos en la llamada a query.ps1: '$($resto.Substring($pos).Trim())'." }
    if ($envFile) { Comprobar-EnvFile $envFile }
    if ($sql -and $fichero) { Bloquear 'usa -Sql o -File, no los dos.' }
    if ($fichero) {
        if (-not (Test-Path -LiteralPath $fichero -PathType Leaf)) { Bloquear "no existe el fichero $fichero." }
        $sql = Get-Content -LiteralPath $fichero -Raw -Encoding Default
    }
    if ($sql) { Comprobar-Select $sql }
    elseif (-not $diagnostico) { Bloquear 'falta la consulta (-Sql o -File).' }
}

# Llamada a consulta.ps1 de la skill oracle-desas (BBDD de desarrollo con usuarios _RO):
#   "SELECT ..." | powershell -NoProfile -ExecutionPolicy Bypass -File <ruta>\consulta.ps1 [-Bd base] [-Formato texto]
function Comprobar-Envoltorio([string]$cmd) {
    $c = $cmd.Trim()
    $sql = $null
    $m = [regex]::Match($c, "^@'\r?\n(?<sql>.*?)\r?\n'@\s*\|(?<resto>.*)$", 'Singleline')
    if ($m.Success) { $sql = $m.Groups['sql'].Value }
    if (-not $m.Success) {
        $m = [regex]::Match($c, "^'(?<sql>(?:[^']|'')*)'\s*\|(?<resto>.*)$", 'Singleline')
        if ($m.Success) { $sql = $m.Groups['sql'].Value.Replace("''", "'") }
    }
    if (-not $m.Success) {
        # Entre comillas dobles, sin $ ni ` para que PowerShell no sustituya nada
        $m = [regex]::Match($c, '^"(?<sql>[^"`$]*)"\s*\|(?<resto>.*)$', 'Singleline')
        if ($m.Success) { $sql = $m.Groups['sql'].Value }
    }
    if (-not $m.Success) {
        Bloquear 'pasa la sentencia entre comillas por la tuberia: ''SELECT ...'' | powershell -NoProfile -ExecutionPolicy Bypass -File <ruta>\consulta.ps1 [-Bd base] [-Formato texto].'
    }
    $resto = $m.Groups['resto'].Value.Trim()
    $ruta = '("[^"$`]+[\\/]consulta\.ps1"|''[^''$`]+[\\/]consulta\.ps1''|[^\s"''`$;|&]+[\\/]consulta\.ps1)'
    $forma = '^(pwsh|powershell)(\.exe)?(\s+-(NoProfile|NonInteractive|NoLogo|ExecutionPolicy\s+Bypass))*\s+-File\s+' + $ruta +
             '(\s+-(Bd\s+[A-Za-z0-9_]+|Formato\s+(csv|texto)|TimeoutSegundos\s+\d+))*$'
    if ($resto -notmatch "(?i)$forma" -or $resto -notmatch '(?i)-ExecutionPolicy\s+Bypass') {
        Bloquear 'la llamada a consulta.ps1 no tiene la forma permitida.'
    }
    if ($resto -notmatch '(?i)[\\/]\.claude[\\/]plugins[\\/].*[\\/]scripts[\\/]consulta\.ps1') {
        Bloquear 'solo se admite el consulta.ps1 de la skill oracle-desas.'
    }
    Comprobar-Select $sql
}

# Rutas de credenciales: los .env, el wallet de oracle-desas y la carpeta de BBDD del plug-in
$credenciales = '(?i)\.env$|[\\/]ClaudeCodePlsql[\\/]bbdd([\\/]|$)|[\\/]\.secrets([\\/]|$)|wallet'

try {
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    $texto = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($texto)) { Bloquear 'no ha llegado la descripcion de la accion.' }
    $entrada = $texto | ConvertFrom-Json
    $herramienta = [string]$entrada.tool_name
    if (-not $herramienta) { Bloquear 'no se sabe que herramienta es.' }
    $ti = $entrada.tool_input
    $todo = if ($ti) { $ti | ConvertTo-Json -Depth 10 -Compress } else { '' }

    # 1) Servidor MCP de SQLcl del plug-in
    if ($herramienta -like 'mcp__sqlcl__*') {
        $accion = $herramienta.Substring('mcp__sqlcl__'.Length)
        switch ($accion) {
            'run-sql' {
                $sql = if ($ti.sql) { [string]$ti.sql } else { ($ti.PSObject.Properties | Where-Object { $_.Value -is [string] } | Select-Object -First 1).Value }
                Comprobar-Select $sql
                Preguntar 'Consulta de solo lectura: revisala antes de aprobarla.'
            }
            { $_ -in 'list-connections', 'connect', 'disconnect', 'schema-information' } { exit 0 }
            default { Bloquear "la herramienta '$accion' de SQLcl no esta permitida; usa run-sql con un SELECT." }
        }
    }

    # 2) Cualquier otro servidor MCP que parezca de base de datos
    if ($herramienta -like 'mcp__*' -and $herramienta -match '(?i)oracle|sql|database|_db') {
        Bloquear "solo se puede usar la base de datos con los envoltorios de consulta ($herramienta)."
    }

    # 3) Consola: consultas solo con los envoltorios; nada de clientes de Oracle, credenciales ni configuracion
    if ($herramienta -in 'Bash', 'PowerShell') {
        $cmd = [string]$ti.command
        # Buscar la ruta de un envoltorio (lo que indican las skills) es de solo lectura: Get-ChildItem y poco mas
        if ($cmd -match '(?i)(query|consulta)\.ps1' -and $cmd -notmatch '[;&>`]|\$\(' -and
            (($cmd -replace '^\s*\(|\)(\.FullName)?\s*$', '') -split '\|' | Where-Object { $_.Trim() -notmatch '^(Get-ChildItem|Sort-Object|Select-Object|Where-Object)\b' }).Count -eq 0) {
            exit 0
        }
        if ($cmd -match '(?i)query\.ps1') {
            Comprobar-Query $cmd
            Preguntar 'Consulta de solo lectura: revisala antes de aprobarla.'
        }
        if ($cmd -match '(?i)consulta\.ps1') {
            Comprobar-Envoltorio $cmd
            Preguntar 'Consulta de solo lectura: revisala antes de aprobarla.'
        }
        $clientes = '(^|[\s;&|(''"`/\\])(sql|sqlplus|sqlcl|sqlldr|impdp|expdp|exp|imp|rman|dgmgrl|tnsping|orapki|mkstore)(\.exe|\.cmd|\.bat)?(?=$|[\s;&|)''"`])'
        if ($cmd -match "(?i)$clientes") { Bloquear 'no se pueden lanzar clientes de Oracle desde la consola; usa el query.ps1 de la skill desa04-sqlplus.' }
        if ($cmd -match '(?i)oracledb|cx_Oracle|Oracle\.ManagedDataAccess|OracleClient|jdbc:oracle|Invoke-Sqlcmd|odbc') { Bloquear 'no se puede acceder a la base de datos desde la consola.' }
        if ($cmd -match '(?i)ClaudeCodePlsql') { Bloquear 'no se puede tocar la carpeta del plug-in desde la consola.' }
        if ($cmd -match '(?i)settings(\.local)?\.json|disableAllHooks') { Bloquear 'no se pueden tocar los ajustes de Claude Code desde la consola.' }
        if ($cmd -match '(?i)\.env\b|\.secrets|oracle_desas|wallet|libratmp|run_sql') { Bloquear 'no se pueden leer ni tocar credenciales de BBDD desde la consola.' }
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
    #    este hook), ni envoltorios, credenciales o scripts que abran conexiones a Oracle
    if ($herramienta -in 'Edit', 'Write', 'MultiEdit', 'NotebookEdit') {
        $ruta = [string]$(if ($ti.file_path) { $ti.file_path } else { $ti.notebook_path })
        if ($ruta) {
            # Por nombre y no solo por ruta completa, para que no valgan rutas cortas como RUBEN~1.HER
            if ($ruta -match '(?i)[\\/]ClaudeCodePlsql([\\/]|$)') { Bloquear 'no se puede modificar la configuracion del plug-in.' }
            if ($ruta -match '(?i)[\\/]\.claude[\\/].*settings(\.local)?\.json$|[\\/]managed-settings\.json$') { Bloquear 'no se pueden modificar los ajustes de Claude Code.' }
            if ($ruta -match '(?i)[\\/]\.claude[\\/](plugins|skills)[\\/]|(query|consulta)\.ps1$' -or $ruta -match $credenciales) {
                Bloquear 'no se pueden modificar las skills, los envoltorios de consulta ni las credenciales.'
            }
            if ($ruta -match '(?i)\.(py|ps1|psm1|js|ts|cs|java|bat|cmd|vbs)$' -and $todo -match '(?i)oracledb|cx_Oracle|Oracle\.ManagedDataAccess|OracleClient|jdbc:oracle|sqlplus|sqlcl') {
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
    public static string ReglaBaseDatos(string bbdd) =>
        "Sesión abierta desde el plug-in de PL/SQL Developer. Contra la base de datos solo puedes consultar, y el usuario " +
        "aprueba cada consulta: una sentencia SELECT, WITH o DESCRIBE por llamada al query.ps1 de la skill desa04-sqlplus, " +
        "así: & '<ruta del query.ps1>' -Sql 'SELECT ...' -EnvFile '<fichero>', con el SQL entre comillas simples " +
        "(doblando las del SQL) o con -File y un .sql. " + bbdd + " Cuando consultes una que no sea la de desarrollo, " +
        "di de cuál sale el dato. Nada de DML, DDL, PL/SQL ni COMMIT, ni sqlplus, sql o mkstore por tu cuenta, ni leer " +
        "los .env. Un hook del plug-in bloquea todo lo demás: si te bloquea, no busques otra vía; explica qué habría que " +
        "ejecutar y deja que lo haga el usuario.";

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
               "    \"ask\": [\"mcp__sqlcl__run-sql\"],\n" +
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
# Lo lanza el plug-in, que le pasa por stdin un JSON con la sesion activa de PL/SQL Developer. Escribe un .env con el
# formato del query.ps1 de la skill desa04-sqlplus (DESA04_USER, DESA04_PASSWORD, DESA04_HOST, DESA04_PORT,
# DESA04_SERVICE) y el indice de conexiones que leen el plug-in, el hook y el mod:
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

# query.ps1 conecta por EZConnect: hace falta host, puerto y servicio (con SID no se puede)
function Partes-Conexion([string]$descriptor) {
    $host_ = [regex]::Match($descriptor, '(?i)\(\s*HOST\s*=\s*([^)\s]+)\s*\)').Groups[1].Value
    $port  = [regex]::Match($descriptor, '(?i)\(\s*PORT\s*=\s*(\d+)\s*\)').Groups[1].Value
    $serv  = [regex]::Match($descriptor, '(?i)\(\s*SERVICE_NAME\s*=\s*([^)\s]+)\s*\)').Groups[1].Value
    if (-not $serv -and $descriptor -match '(?i)\(\s*SID\s*=') {
        throw 'La base se define con SID y no con SERVICE_NAME: el query.ps1 de la skill desa04-sqlplus solo conecta por servicio.'
    }
    if (-not $host_ -or -not $serv) { throw "No saco host y servicio de la definicion de la base: $descriptor" }
    if (-not $port) { $port = '1521' }
    return @{ host = $host_; port = $port; servicio = $serv }
}

function Escribir-Env([string]$ruta, [string]$usuario, [string]$clave, $partes) {
    $texto = "# Lo escribe el plug-in de PL/SQL Developer (boton Conectar a BBDD). No lo edites a mano.`r`n" +
             "DESA04_USER=$usuario`r`nDESA04_PASSWORD=$clave`r`nDESA04_HOST=$($partes.host)`r`n" +
             "DESA04_PORT=$($partes.port)`r`nDESA04_SERVICE=$($partes.servicio)`r`n"
    # En ANSI, como lo lee query.ps1 (Get-Content -Encoding Default)
    [System.IO.File]::WriteAllText($ruta, $texto, [System.Text.Encoding]::Default)
}

function Escribir-Json([string]$ruta, $valor) {
    [System.IO.File]::WriteAllText($ruta, (ConvertTo-Json -InputObject $valor -Depth 5), (New-Object System.Text.UTF8Encoding $false))
}

try {
    [Console]::InputEncoding = [System.Text.Encoding]::UTF8
    $e = [Console]::In.ReadToEnd() | ConvertFrom-Json
    if (-not $e.usuario -or -not $e.clave -or -not $e.bd) { throw 'Faltan datos de la conexion (usuario, clave o base de datos).' }
    if ([string]$e.clave -match '"') { throw 'La contrasena tiene una comilla doble y el query.ps1 de la skill no la admite.' }
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
    lista.push({ nombre: String(desarrollo.nombre ?? desarrollo.origen), origen: String(desarrollo.origen), tipo: 'desarrollo' })
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

  on('ui.render', { component: 'AbovePrompt' }, async ($, e, next) => {
    if (e.props.hasSurvey) return next(e)
    const lista = await read($, conexiones)
    const { Box, Text } = $.ui.resolve(e)
    const desarrollo = lista.find(c => c.tipo === 'desarrollo')
    const otras = lista.filter(c => c.tipo !== 'desarrollo')

    return (
      <Box>
        <Text dimColor>BBDD · desarrollo: </Text>
        <Text color="success">{desarrollo ? desarrollo.origen : 'sin fijar (DESA04 de la skill)'}</Text>
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
        (Plugin.MenuOpen, "Abrir en Claude Code",
            Svg("""<rect x="2.5" y="3.5" width="16" height="14" rx="2" fill="none" stroke="#434A54"/><path d="M6 8l3 2.5L6 13M10.5 13.5h4" fill="none" stroke="#434A54" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuAskSelection, "Preguntar por lo seleccionado",
            Svg("""<path d="M3.5 3.5h14a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H9l-4 3v-3H3.5a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z" fill="none" stroke="#434A54"/><path d="M8.8 7.5a1.8 1.8 0 1 1 2.6 1.6c-.6.3-.9.7-.9 1.3" fill="none" stroke="#434A54" stroke-width="1.4" stroke-linecap="round"/><circle cx="10.5" cy="12.3" r=".8" fill="#434A54"/>""")),
        (Plugin.MenuPull, "Traer cambios de Claude",
            Svg("""<path d="M5.5 2.5h7l3.5 3.5v12.5h-10.5z" fill="none" stroke="#434A54"/><path d="M10.75 8v6M8 11.5l2.75 2.75 2.75-2.75" fill="none" stroke="#2F80ED" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>""")),
        (Plugin.MenuCopyRef, "Copiar referencia para Claude",
            Svg("""<rect x="5" y="3.5" width="11" height="15" rx="1.5" fill="none" stroke="#434A54"/><rect x="8" y="2" width="5" height="3" rx="1" fill="#434A54"/><path d="M8.5 10h4M8.5 13h4" stroke="#434A54" stroke-width="1.4" stroke-linecap="round"/>""")),
        (Plugin.MenuFolder, "Abrir carpeta de trabajo",
            Svg("""<path d="M2.5 6v10a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V8a1 1 0 0 0-1-1H10L8.5 5h-5a1 1 0 0 0-1 1z" fill="none" stroke="#434A54"/>""")),
        (Plugin.MenuConfig, "Configurar",
            Svg("""<circle cx="10.5" cy="10.5" r="3" fill="none" stroke="#434A54" stroke-width="1.5"/><path d="M10.5 2.5v3M10.5 15.5v3M2.5 10.5h3M15.5 10.5h3M4.8 4.8l2.1 2.1M14.1 14.1l2.1 2.1M4.8 16.2l2.1-2.1M14.1 6.9l2.1-2.1" stroke="#434A54" stroke-width="1.8" stroke-linecap="round"/>""")),
        (Plugin.MenuConnect, "Conectar a BBDD",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#27AE60"/><path d="M16 13.8v4.4M13.8 16h4.4" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round"/>""")),
        (Plugin.MenuDisconnect, "Desconectar BBDD",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#EB5757"/><path d="M14.5 14.5l3 3M17.5 14.5l-3 3" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round"/>""")),
        (Plugin.MenuSetDev, "Fijar como BD de desarrollo",
            Svg("""<ellipse cx="9.5" cy="5" rx="6" ry="2.5" fill="none" stroke="#434A54"/><path d="M3.5 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5M3.5 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5" fill="none" stroke="#434A54"/><circle cx="16" cy="16" r="4" fill="#2F80ED"/><path d="M14 16l1.5 1.5 2.5-3" fill="none" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>""")),
    };

    private static string Svg(string body) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" width="21" height="21" viewBox="0 0 21 21">{body}</svg>""";

    public static string McpJson(string sqlclPath)
    {
        string escaped = sqlclPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\n  \"mcpServers\": {\n    \"sqlcl\": {\n      \"command\": \"" + escaped +
               "\",\n      \"args\": [\"-mcp\"]\n    }\n  }\n}\n";
    }
}
