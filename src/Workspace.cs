using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Carpeta desde la que se abre Claude Code para una ventana: la del expediente («1234567 - Título») si el
/// fichero de la ventana está dentro de una, o la carpeta general (CarpetaTrabajo) en otro caso.
/// Lógica pura, sin llamadas al IDE ni a Windows, para poder probarla aparte.
/// </summary>
internal sealed class Workspace
{
    public string Root { get; }

    /// <summary>Expediente o tema: carpeta con su propia conversación de Claude, sus copias en .px y sus BBDD.</summary>
    public bool IsProject { get; }

    /// <summary>Tema: una carpeta con título que no es un expediente (la marca .px\tema.json), creada desde el plug-in.</summary>
    public bool IsTopic { get; }

    private Workspace(string root, bool isProject)
    {
        Root = root.Length > 3 ? root.TrimEnd('\\', '/') : root;
        IsProject = isProject;
        IsTopic = isProject && !IsProjectFolderName(Path.GetFileName(Root));
    }

    public static Workspace General(string folder) => new(folder, false);

    /// <summary>Expediente («1234567 - Título») o tema (carpeta con .px\tema.json).</summary>
    public static Workspace Project(string root) => new(root, true);

    /// <summary>Marca de un tema, dentro de su carpeta.</summary>
    public static string TopicMarker(string root) => Path.Combine(root, ".px", "tema.json");

    public static bool IsTopicFolder(string dir)
    {
        try
        {
            return File.Exists(TopicMarker(dir));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>True si la carpeta es un expediente o un tema.</summary>
    public static bool IsProjectFolder(string dir) =>
        IsProjectFolderName(Path.GetFileName(dir.TrimEnd('\\', '/'))) || IsTopicFolder(dir);

    /// <summary>
    /// Carpeta de una sesión de Claude: expediente o tema si lo es; si no, carpeta general (la de CarpetaTrabajo u otra).
    /// </summary>
    public static Workspace FromFolder(string folder, string general)
    {
        string f = folder.Trim().TrimEnd('\\', '/');
        if (f.Length == 0) return General(general);
        return IsProjectFolder(f) ? Project(f) : General(f);
    }

    /// <summary>«expediente» o «tema», para los textos.</summary>
    public string Kind => IsTopic ? "tema" : "expediente";

    /// <summary>Nombre de carpeta válido en Windows: sin \ / : * ? " &lt; &gt; |, sin espacios dobles ni punto o espacio final.</summary>
    public static string FolderName(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text.Trim())
            sb.Append(c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(c) ? ' ' : c);
        string name = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), " {2,}", " ").TrimEnd(' ', '.').Trim();
        return name.Length > 120 ? name[..120].TrimEnd(' ', '.') : name;
    }

    /// <summary>
    /// Crea (o completa, si ya existe) un expediente «1234567 - Título» con la estructura estándar, o un tema con su
    /// carpeta doc y la marca .px\tema.json si no hay número. Devuelve la carpeta.
    /// </summary>
    public static Workspace Create(string parent, string number, string title)
    {
        number = number.Trim();
        string name = FolderName(number.Length > 0 ? $"{number} - {title}" : title);
        if (name.Length == 0) throw new ArgumentException("El título no puede quedar vacío.");
        string root = Path.Combine(parent, name);
        if (number.Length > 0)
        {
            foreach (string d in new[] { @"cfcliente\cfsubidos", @"cfcliente\prog_estandar\cfsubidos", @"cfstd\cfsubidos", "doc",
                         @"instalacion\libraupdate", @"instalacion\manual", "test" })
                Directory.CreateDirectory(Path.Combine(root, d));
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(root, "doc"));
            Directory.CreateDirectory(Path.Combine(root, ".px"));
            if (!File.Exists(TopicMarker(root)))
            {
                string json = "{\"titulo\":\"" + System.Text.Json.JsonEncodedText.Encode(title.Trim()) + "\"," +
                              $"\"creado\":\"{DateTime.Now:yyyy-MM-dd HH:mm}\"}}";
                File.WriteAllText(TopicMarker(root), json, new UTF8Encoding(false));
            }
        }
        return Project(root);
    }

    /// <summary>Copias de las ventanas: .px\ventanas en un expediente (junto a px.py), ventanas en la carpeta general.</summary>
    public string WindowsDir => IsProject ? Path.Combine(Root, ".px", "ventanas") : Path.Combine(Root, "ventanas");

    /// <summary>Prompts y lanzadores temporales.</summary>
    public string InternalDir => IsProject ? Path.Combine(Root, ".px", "plsqldev") : Path.Combine(Root, ".plsqldev");

    /// <summary>CLAUDE.md que crea el plug-in si falta. En un expediente va en .px: el de la raíz es del proyecto.</summary>
    public string ClaudeMdPath => IsProject ? Path.Combine(Root, ".px", "CLAUDE.md") : Path.Combine(Root, "CLAUDE.md");

    /// <summary>Nombre de la carpeta: «1234567 - Título» en un expediente.</summary>
    public string Label => Path.GetFileName(Root);

    /// <summary>Número de expediente (7 dígitos) o vacío (también en un tema).</summary>
    public string Number => IsProject && !IsTopic ? Label[..7] : "";

    public bool SameAs(Workspace other) =>
        IsProject == other.IsProject && Root.Equals(other.Root, StringComparison.OrdinalIgnoreCase);

    /// <summary>True si el nombre es el de una carpeta de expediente: 7 dígitos, « - » y el título.</summary>
    public static bool IsProjectFolderName(string name)
    {
        if (name.Length <= 10 || name[7] != ' ' || name[8] != '-' || name[9] != ' ') return false;
        for (int i = 0; i < 7; i++)
            if (name[i] < '0' || name[i] > '9') return false;
        return true;
    }

    /// <summary>Carpeta del expediente que contiene el fichero (subiendo por sus carpetas), o null si no hay.</summary>
    public static string? FindProjectRoot(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            while (!string.IsNullOrEmpty(dir))
            {
                if (IsProjectFolderName(Path.GetFileName(dir)) || IsTopicFolder(dir)) return dir;
                string? parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || parent == dir) break;
                dir = parent;
            }
        }
        catch (Exception)
        {
            // ruta que no se puede analizar: se trata como fuera de cualquier expediente
        }
        return null;
    }

    /// <summary>Ruta relativa a la carpeta si está dentro de ella; si no, la ruta completa.</summary>
    public string Relative(string path)
    {
        string rel = Path.GetRelativePath(Root, path);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? path : rel;
    }
}

/// <summary>Textos con los que se abre Claude Code. Empiezan siempre igual para que la skill los reconozca.</summary>
internal static class Prompts
{
    public const string Marker = "Abierto desde PL/SQL Developer (plug-in Claude Code)";

    /// <param name="copy">Copia UTF-8 de la ventana, relativa a la carpeta de trabajo.</param>
    /// <param name="connection">« (conexión usuario@bd)» o vacío.</param>
    /// <param name="original">Fichero abierto en la ventana (relativo si está en la carpeta) o null si no tiene.</param>
    public static string Open(Workspace ws, string title, string copy, string connection, string? original)
    {
        var sb = Start(ws, title, connection);
        sb.Append($" Su texto está en {copy}, una copia UTF-8: si te pido cambios, edita esa copia y nada más; ");
        sb.Append("el plug-in los carga en el editor y yo reviso, compilo y guardo desde PL/SQL Developer.");
        sb.Append(OriginalText(original));
        sb.Append(End(ws));
        sb.Append(" Léelo y espera mis instrucciones.");
        return sb.ToString();
    }

    /// <param name="selection">Fichero con el fragmento seleccionado, relativo a la carpeta de trabajo.</param>
    /// <param name="line">Línea del cursor (0 si no se sabe).</param>
    public static string Selection(Workspace ws, string title, string copy, string connection, string? original,
        string selection, int line)
    {
        var sb = Start(ws, title, connection);
        sb.Append($" El texto completo está en {copy} (copia UTF-8) y el fragmento que tengo seleccionado, en {selection}");
        sb.Append(line > 0 ? $" (el cursor está en la línea {line})." : ".");
        sb.Append($" Si propones cambios, aplícalos en {copy} y no en el fichero de la selección; ");
        sb.Append("el plug-in los carga en el editor y yo reviso, compilo y guardo desde PL/SQL Developer.");
        sb.Append(OriginalText(original));
        sb.Append(End(ws));
        sb.Append(" Lee ambos y espera mi pregunta sobre ese fragmento.");
        return sb.ToString();
    }

    /// <summary>Encargo concreto sobre la ventana (botones de Herramientas): como Open, pero con la tarea al final.</summary>
    public static string Task(Workspace ws, string title, string copy, string connection, string? original, string task)
    {
        var sb = Start(ws, title, connection);
        sb.Append($" Su texto está en {copy}, una copia UTF-8: si te pido cambios, edita esa copia y nada más.");
        sb.Append(OriginalText(original));
        sb.Append(End(ws));
        sb.Append(' ').Append(task);
        return sb.ToString();
    }

    /// <summary>El mismo encargo para pegar en una ventana de Claude que ya estaba abierta.</summary>
    public static string TaskAgain(string title, string copy, string connection, string task) =>
        $"Sigo en PL/SQL Developer con la ventana {title}{connection}. Su copia está en @{copy.Replace('\\', '/')} " +
        $"(actualizada ahora). {task}";

    /// <summary>Botón "Documentación": acta de entrega y manual del expediente.</summary>
    public const string DocsTask =
        "Prepara la documentación de entrega de este expediente: el acta de entrega (A18) y el manual de desarrollo, " +
        "con las skills a18-acta-entrega y manual-desarrollo si las tienes (si no, dime qué falta y para). Sácalo de la " +
        "carpeta del expediente (doc, cfstd, cfcliente, instalacion) y pregúntame lo que no esté ahí; no inventes " +
        "programas, tablas ni fechas.";

    /// <summary>Botón "Preparar instalación": script de instalación con los fuentes del expediente.</summary>
    public const string InstallTask =
        "Prepara en instalacion/ la instalación de este expediente con los fuentes de cfstd y cfcliente: qué objetos " +
        "lleva y en qué orden se compilan según sus dependencias (tablas y DDL, vistas, especificaciones, cuerpos, " +
        "disparadores, datos). Enséñame antes la lista y el orden. Después escribe el script como un .sql nuevo en " +
        "Windows-1252, con CRLF y sin BOM, sin tocar los fuentes originales.";

    /// <summary>Sesión abierta con el botón «Nueva sesión» del panel, sin ninguna ventana del IDE.</summary>
    public static string FromPanel(Workspace ws) =>
        Marker + (ws.IsProject ? $" en el {ws.Kind} {ws.Label}." : ", fuera de cualquier expediente.") +
        " Me la he abierto desde el panel de sesiones, sin ninguna ventana concreta." + End(ws) + " Espera mis instrucciones.";

    /// <summary>Encargo sobre el expediente entero, sin una ventana concreta.</summary>
    public static string ProjectTask(Workspace ws, string task) =>
        $"{Marker} en el {ws.Kind} {ws.Label}.{End(ws)} {task}";

    /// <summary>Texto para pegar en una ventana de Claude que ya estaba abierta.</summary>
    public static string Again(string title, string copy, string connection, (string Path, int Line)? selection)
    {
        string at = "@" + copy.Replace('\\', '/');
        var sb = new StringBuilder($"Sigo en PL/SQL Developer con la ventana {title}{connection}. Su copia está en {at} (actualizada ahora)");
        if (selection is { } s)
        {
            sb.Append($" y el fragmento que tengo seleccionado, en @{s.Path.Replace('\\', '/')}");
            if (s.Line > 0) sb.Append($" (el cursor está en la línea {s.Line})");
            sb.Append(". Léelos y espera mi pregunta sobre ese fragmento. ");
        }
        else
        {
            sb.Append(". Léela y espera mis instrucciones. ");
        }
        return sb.ToString();
    }

    private static StringBuilder Start(Workspace ws, string title, string connection)
    {
        var sb = new StringBuilder(Marker);
        sb.Append(ws.IsProject ? $" en el {ws.Kind} {ws.Label}." : ", fuera de cualquier expediente.");
        sb.Append($" Ventana: {title}{connection}.");
        return sb;
    }

    private static string OriginalText(string? original) =>
        original == null
            ? " No tiene fichero: viene de la base de datos o es una ventana sin guardar."
            : $" Fichero original: {original} (no lo edites: los cambios le llegan cuando los guardo en PL/SQL Developer).";

    private static string End(Workspace ws) =>
        ws.IsTopic ? " Es un tema con título, no un expediente: su documentación va en doc." :
        ws.IsProject ? " Si tienes la skill proyecto-expediente, úsala para el seguimiento de este expediente." : "";

    /// <summary>
    /// Mensaje breve al pasar una ventana (preferencia «mensajes breves»): solo qué ventana es y su copia, y la
    /// selección si la hay. Lo que tiene que hacer Claude con ella va en <see cref="WindowRule"/>, en el prompt de sistema.
    /// </summary>
    public static string Short(string title, string copy, string connection, (string Path, int Line)? selection = null)
    {
        var sb = new StringBuilder($"{title}{connection}: @{copy.Replace('\\', '/')}");
        if (selection is { } s)
        {
            sb.Append($", selección en @{s.Path.Replace('\\', '/')}");
            if (s.Line > 0) sb.Append($" (línea {s.Line})");
            return sb.Append(". Espera mi pregunta.").ToString();
        }
        return sb.Append(". Espera mis instrucciones.").ToString();
    }

    /// <summary>Encargo de un botón de Herramientas en breve: la ventana y la tarea.</summary>
    public static string ShortTask(string title, string copy, string connection, string task) =>
        $"{title}{connection}: @{copy.Replace('\\', '/')}. {task}";

    /// <summary>
    /// Lo que Claude tiene que saber de las ventanas que le pasa el plug-in. Va siempre en el prompt de sistema de la
    /// sesión (no se ve en la conversación), para que los mensajes breves basten.
    /// </summary>
    public static string WindowRule(Workspace ws) =>
        Marker + (ws.IsProject ? $", en el {ws.Kind} {ws.Label}." : ", fuera de cualquier expediente.") +
        " Las ventanas de PL/SQL Developer te llegan como una referencia @ a su copia UTF-8 (en " +
        (ws.IsProject ? ".px\\ventanas" : "ventanas") + "), a veces con la conexión de la ventana y, si el usuario " +
        "seleccionó un trozo, con un fichero .seleccion aparte. Si te pide cambios, edita esa copia y nada más (nunca el " +
        "fichero .seleccion ni el fichero original de la ventana): el plug-in carga los cambios en el editor y el usuario " +
        "revisa, compila y guarda desde PL/SQL Developer. Si solo te pasa una ventana, léela y espera sus instrucciones." +
        End(ws);

    /// <summary>Sesión reabierta en el expediente o tema que se le acaba de asignar (retoma la misma conversación).</summary>
    public static string Assigned(Workspace ws, string from) =>
        $"{Marker} en el {ws.Kind} {ws.Label}. Esta conversación venía de {from} y la he asignado a este {ws.Kind}: " +
        "desde ahora trabajas en esta carpeta (las copias de las ventanas de PL/SQL Developer van en .px\\ventanas)." +
        End(ws) + " Sigue donde lo dejamos.";
}

/// <summary>Comprobación de que un texto se puede guardar en Windows-1252, la codificación de los fuentes de LIBRA.</summary>
internal static class TextCheck
{
    private static readonly Encoding Strict = CreateStrict();

    private static Encoding CreateStrict()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    public static bool IsCp1252(string text)
    {
        try
        {
            Strict.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Hasta <paramref name="max"/> caracteres que no existen en Windows-1252, como «línea 12: → (U+2192)».</summary>
    public static List<string> NonCp1252(string text, int max = 5)
    {
        var found = new List<string>();
        if (IsCp1252(text)) return found;

        int line = 1;
        for (int i = 0; i < text.Length && found.Count < max; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                line++;
                continue;
            }

            bool pair = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
            string piece = pair ? text.Substring(i, 2) : c.ToString();
            if (pair) i++;
            if (IsCp1252(piece)) continue;

            int code = pair ? char.ConvertToUtf32(piece[0], piece[1]) : c;
            string shown = char.IsSurrogate(c) && !pair ? "?" : piece;
            found.Add($"línea {line}: {shown} (U+{code:X4})");
        }
        return found;
    }
}
