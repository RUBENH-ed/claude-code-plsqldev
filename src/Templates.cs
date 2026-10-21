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
; las carpetas de trabajo. Guarda en SQLcl solo conexiones de desarrollo: Claude ejecuta SQL de verdad.
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

    public static string McpJson(string sqlclPath)
    {
        string escaped = sqlclPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\n  \"mcpServers\": {\n    \"sqlcl\": {\n      \"command\": \"" + escaped +
               "\",\n      \"args\": [\"-mcp\"]\n    }\n  }\n}\n";
    }
}
