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
    public bool IsProject { get; }

    private Workspace(string root, bool isProject)
    {
        Root = root.Length > 3 ? root.TrimEnd('\\', '/') : root;
        IsProject = isProject;
    }

    public static Workspace General(string folder) => new(folder, false);

    public static Workspace Project(string root) => new(root, true);

    /// <summary>Copias de las ventanas: .px\ventanas en un expediente (junto a px.py), ventanas en la carpeta general.</summary>
    public string WindowsDir => IsProject ? Path.Combine(Root, ".px", "ventanas") : Path.Combine(Root, "ventanas");

    /// <summary>Prompts y lanzadores temporales.</summary>
    public string InternalDir => IsProject ? Path.Combine(Root, ".px", "plsqldev") : Path.Combine(Root, ".plsqldev");

    /// <summary>CLAUDE.md que crea el plug-in si falta. En un expediente va en .px: el de la raíz es del proyecto.</summary>
    public string ClaudeMdPath => IsProject ? Path.Combine(Root, ".px", "CLAUDE.md") : Path.Combine(Root, "CLAUDE.md");

    /// <summary>Nombre de la carpeta: «1234567 - Título» en un expediente.</summary>
    public string Label => Path.GetFileName(Root);

    /// <summary>Número de expediente (7 dígitos) o vacío.</summary>
    public string Number => IsProject ? Label[..7] : "";

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
                if (IsProjectFolderName(Path.GetFileName(dir))) return dir;
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

    /// <summary>Encargo sobre el expediente entero, sin una ventana concreta.</summary>
    public static string ProjectTask(Workspace ws, string task) =>
        $"{Marker} en el expediente {ws.Label}.{End(ws)} {task}";

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
        sb.Append(ws.IsProject ? $" en el expediente {ws.Label}." : ", fuera de cualquier expediente.");
        sb.Append($" Ventana: {title}{connection}.");
        return sb;
    }

    private static string OriginalText(string? original) =>
        original == null
            ? " No tiene fichero: viene de la base de datos o es una ventana sin guardar."
            : $" Fichero original: {original} (no lo edites: los cambios le llegan cuando los guardo en PL/SQL Developer).";

    private static string End(Workspace ws) =>
        ws.IsProject ? " Si tienes la skill proyecto-expediente, úsala para el seguimiento de este expediente." : "";
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
