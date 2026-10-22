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
# Norma: desde el plug-in, contra la base de datos solo se consulta (SELECT/WITH). Todo lo demas se bloquea.
# Salida 2 = bloquear (el motivo va por stderr a Claude). Ante cualquier error, bloquea.
$ErrorActionPreference = 'Stop'

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
    if ($s -notmatch '^\(*\s*(SELECT|WITH)\b') { Bloquear 'solo se admiten consultas que empiecen por SELECT o WITH.' }
    $prohibidas = 'INSERT|UPDATE|DELETE|MERGE|UPSERT|CREATE|ALTER|DROP|TRUNCATE|RENAME|GRANT|REVOKE|COMMENT|AUDIT|NOAUDIT|PURGE|FLASHBACK|ANALYZE|LOCK|COMMIT|ROLLBACK|SAVEPOINT|BEGIN|DECLARE|EXEC|EXECUTE|CALL|FUNCTION|PROCEDURE'
    $m = [regex]::Match($s, "(?i)\b($prohibidas)\b")
    if ($m.Success) { Bloquear "la consulta contiene '$($m.Value.ToUpper())'." }
    # Comandos de SQLcl al principio de una linea (host, @script, spool, conn...). CONNECT BY y START WITH son SQL.
    $m = [regex]::Match($s, '(?im)^\s*([@!$]|(host|spool|conn|connect(?!\s+BY\b)|disc|disconnect|set|define|store|save|run|start(?!\s+WITH\b)|get|edit|alias|script|load|liquibase|lb|ddl|ctas|sshtunnel|repeat|apex|oci|mle|cd)\b)')
    if ($m.Success) { Bloquear "comando de SQLcl no permitido: '$($m.Value.Trim())'." }
}

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
        Bloquear "solo se puede usar la base de datos con el servidor sqlcl del plug-in ($herramienta)."
    }

    # 3) Consola: nada de clientes de Oracle ni de tocar la configuracion del plug-in
    if ($herramienta -in 'Bash', 'PowerShell') {
        $cmd = [string]$ti.command
        $clientes = '(^|[\s;&|(''"`/\\])(sql|sqlplus|sqlcl|sqlldr|impdp|expdp|exp|imp|rman|dgmgrl|tnsping|orapki)(\.exe|\.cmd|\.bat)?(?=$|[\s;&|)''"`])'
        if ($cmd -match "(?i)$clientes") { Bloquear 'no se pueden lanzar clientes de Oracle desde la consola; usa el servidor sqlcl.' }
        if ($cmd -match '(?i)oracledb|cx_Oracle|Oracle\.ManagedDataAccess|OracleClient|jdbc:oracle|Invoke-Sqlcmd|odbc') { Bloquear 'no se puede acceder a la base de datos desde la consola.' }
        if ($cmd -match '(?i)ClaudeCodePlsql') { Bloquear 'no se puede tocar la carpeta del plug-in desde la consola.' }
        if ($cmd -match '(?i)settings(\.local)?\.json|disableAllHooks') { Bloquear 'no se pueden tocar los ajustes de Claude Code desde la consola.' }
        exit 0
    }

    # 4) Edicion de ficheros: ni la configuracion del plug-in, ni los ajustes de Claude Code (podrian desactivar
    #    este hook), ni scripts que abran conexiones a Oracle
    if ($herramienta -in 'Edit', 'Write', 'MultiEdit', 'NotebookEdit') {
        $ruta = [string]$(if ($ti.file_path) { $ti.file_path } else { $ti.notebook_path })
        if ($ruta) {
            # Por nombre y no solo por ruta completa, para que no valgan rutas cortas como RUBEN~1.HER
            if ($ruta -match '(?i)[\\/]ClaudeCodePlsql([\\/]|$)') { Bloquear 'no se puede modificar la configuracion del plug-in.' }
            if ($ruta -match '(?i)[\\/]\.claude[\\/].*settings(\.local)?\.json$|[\\/]managed-settings\.json$') { Bloquear 'no se pueden modificar los ajustes de Claude Code.' }
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

    /// <summary>Norma que se añade al prompt de sistema de Claude Code en las sesiones del plug-in.</summary>
    public const string ReglaSoloConsultas =
        "Sesión abierta desde el plug-in de PL/SQL Developer: contra la base de datos solo puedes consultar, con una " +
        "sentencia SELECT o WITH por llamada a la herramienta run-sql del servidor sqlcl. Nada de DML, DDL, PL/SQL, " +
        "COMMIT ni clientes de Oracle desde la consola. Un hook del plug-in bloquea todo lo demás: si te bloquea, no " +
        "busques otra vía; explica qué habría que ejecutar y deja que lo haga el usuario.";

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
               "        \"matcher\": \"mcp__.*|Bash|PowerShell|Edit|Write|MultiEdit|NotebookEdit\",\n" +
               "        \"hooks\": [{ \"type\": \"command\", \"shell\": \"powershell\", \"command\": \"" + escaped + "\" }]\n" +
               "      }\n" +
               "    ]\n" +
               "  }\n" +
               "}\n";
    }

    public static string McpJson(string sqlclPath)
    {
        string escaped = sqlclPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\n  \"mcpServers\": {\n    \"sqlcl\": {\n      \"command\": \"" + escaped +
               "\",\n      \"args\": [\"-mcp\"]\n    }\n  }\n}\n";
    }
}
