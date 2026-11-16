using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ClaudeCodePlsql;

/// <summary>
/// Cuadros de diálogo con lista, campos de texto y selector de carpetas (dialogo.ps1, Windows Forms en un proceso aparte).
/// Mientras está abierto, PL/SQL Developer queda deshabilitado pero sigue repintándose.
/// </summary>
internal static class Dialog
{
    internal sealed record Option(string Id, string Text, string Detail = "");

    internal sealed record Field(string Id, string Label, string Value = "", string Type = "texto", bool Required = false,
        string Pattern = "", string Error = "", string Help = "");

    internal sealed record Result(string Option, Dictionary<string, string> Fields)
    {
        public string Get(string id) => Fields.TryGetValue(id, out string? v) ? v : "";
    }

    /// <summary>Enseña el cuadro y espera. Null si se cancela o no se ha podido abrir.</summary>
    public static Result? Show(string title, string text, IList<Option>? options = null, int preselect = 0,
        IList<Field>? fields = null, string? fieldsForOption = null, string accept = "Aceptar")
    {
        string dir = Path.Combine(Config.Dir, "dialogos");
        string script = Path.Combine(Config.Dir, "dialogo.ps1");
        string stamp = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}";
        string input = Path.Combine(dir, $"entrada-{stamp}.json");
        string output = Path.Combine(dir, $"salida-{stamp}.json");
        nint ide = Ide.MainWindow;
        try
        {
            Directory.CreateDirectory(dir);
            Plugin.WriteIfChanged(script, Templates.Dialogo, new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1
            File.WriteAllBytes(input, Json(title, text, options, preselect, fields, fieldsForOption, accept));

            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File \"{script}\" \"{input}\" \"{output}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process proc = Process.Start(psi)!;
            if (ide != 0) Win32.EnableWindow(ide, false);
            try
            {
                Win32.PumpUntil(proc.Handle);
            }
            finally
            {
                if (ide != 0)
                {
                    Win32.EnableWindow(ide, true);
                    Win32.BringToFront(ide);
                }
            }
            if (!proc.HasExited || proc.ExitCode != 0 || !File.Exists(output)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(output, Encoding.UTF8));
            var root = doc.RootElement;
            string option = root.TryGetProperty("opcion", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString()! : "";
            var values = new Dictionary<string, string>();
            if (root.TryGetProperty("campos", out var c) && c.ValueKind == JsonValueKind.Object)
                foreach (var p in c.EnumerateObject())
                    values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : "";
            return new Result(option, values);
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No he podido abrir el cuadro de diálogo del plug-in:\n\n{e.Message}", Win32.MB_ICONWARNING);
            return null;
        }
        finally
        {
            try { File.Delete(input); } catch { }
            try { File.Delete(output); } catch { }
        }
    }

    private static byte[] Json(string title, string text, IList<Option>? options, int preselect, IList<Field>? fields,
        string? fieldsForOption, string accept)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("titulo", title);
            w.WriteString("texto", text);
            w.WriteString("aceptar", accept);
            w.WriteNumber("predeterminada", preselect);
            if (fieldsForOption != null) w.WriteString("camposSiOpcion", fieldsForOption);
            w.WriteStartArray("opciones");
            foreach (var o in options ?? Array.Empty<Option>())
            {
                w.WriteStartObject();
                w.WriteString("id", o.Id);
                w.WriteString("texto", o.Text);
                w.WriteString("detalle", o.Detail);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("campos");
            foreach (var f in fields ?? Array.Empty<Field>())
            {
                w.WriteStartObject();
                w.WriteString("id", f.Id);
                w.WriteString("etiqueta", f.Label);
                w.WriteString("valor", f.Value);
                w.WriteString("tipo", f.Type);
                w.WriteBoolean("obligatorio", f.Required);
                w.WriteString("patron", f.Pattern);
                w.WriteString("mensaje", f.Error);
                w.WriteString("ayuda", f.Help);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }
}

/// <summary>Expedientes y temas abiertos hace poco (%APPDATA%\ClaudeCodePlsql\recientes.txt, uno por línea).</summary>
internal static class Recent
{
    private static string FilePath => Path.Combine(Config.Dir, "recientes.txt");

    public static void Add(Workspace ws)
    {
        if (!ws.IsProject) return;
        try
        {
            var list = new List<string> { ws.Root };
            list.AddRange(Read().Where(r => !r.Equals(ws.Root, StringComparison.OrdinalIgnoreCase)));
            Directory.CreateDirectory(Config.Dir);
            File.WriteAllLines(FilePath, list.Take(20), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Los que siguen existiendo, del más reciente al más antiguo (con el último expediente del estado.ini).</summary>
    public static List<Workspace> List(int max = 10)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Workspace>();
        foreach (string r in Read().Prepend(Config.LastProject.TrimEnd('\\', '/')))
        {
            if (r.Length == 0 || !seen.Add(r)) continue;
            try
            {
                if (Directory.Exists(r) && Workspace.IsProjectFolder(r)) list.Add(Workspace.Project(r));
            }
            catch
            {
                // ruta que ya no vale
            }
            if (list.Count >= max) break;
        }
        return list;
    }

    private static IEnumerable<string> Read()
    {
        try
        {
            return File.Exists(FilePath)
                ? File.ReadAllLines(FilePath, Encoding.UTF8).Select(l => l.Trim().TrimEnd('\\', '/')).Where(l => l.Length > 0).ToList()
                : new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

/// <summary>Una sesión de Claude abierta desde el plug-in.</summary>
/// <param name="Id">Id en el panel de sesiones, o vacío si solo se conoce su ventana de Windows Terminal.</param>
/// <param name="ClaudeId">Id de la conversación de Claude (lo cuenta el mod del panel), o vacío.</param>
internal sealed record Session(string Id, string Title, string Folder, bool Hidden, string ClaudeId)
{
    public bool InPanel => Id.Length > 0;

    /// <summary>Nombre para las listas: el título sin «Claude Code - ».</summary>
    public string Name => Title.StartsWith("Claude Code - ", StringComparison.Ordinal) ? Title["Claude Code - ".Length..] : Title;
}

internal static class Sessions
{
    public const string TitlePrefix = "Claude Code - ";

    /// <summary>
    /// Sesiones de Claude abiertas ahora: las del panel (con su carpeta) si está en marcha; si no, las ventanas de Windows
    /// Terminal del plug-in, cuya carpeta se deduce del título (expedientes y temas recientes, o la carpeta general).
    /// </summary>
    public static List<Session> Live()
    {
        var panel = Panel.Available();
        if (panel != null)
        {
            var list = Panel.ListSessions(panel.Value);
            if (list != null) return list;
        }

        var result = new List<Session>();
        var recent = Recent.List(20);
        foreach (string title in Win32.FindTopWindowTitles(TitlePrefix).Distinct())
        {
            if (!title.StartsWith(TitlePrefix, StringComparison.Ordinal)) continue;
            string label = System.Text.RegularExpressions.Regex.Replace(title[TitlePrefix.Length..], @" \(\d+\)$", "");
            string folder = label == "PL/SQL Developer" ? Config.Workspace
                : recent.FirstOrDefault(w => w.Label.Replace(';', ',').Equals(label, StringComparison.OrdinalIgnoreCase))?.Root ?? "";
            result.Add(new Session("", title, folder, false, ""));
        }
        return result;
    }

    /// <summary>Título libre para otra sesión en la misma carpeta: «Claude Code - X», «... (2)», «... (3)»...</summary>
    public static string UniqueTitle(string title, IEnumerable<Session> live)
    {
        var used = new HashSet<string>(live.Select(s => s.Title), StringComparer.OrdinalIgnoreCase);
        foreach (string t in Win32.FindTopWindowTitles(TitlePrefix)) used.Add(t);
        if (!used.Contains(title)) return title;
        for (int n = 2; ; n++)
            if (!used.Contains($"{title} ({n})")) return $"{title} ({n})";
    }

    /// <summary>Opciones de un cuadro de diálogo con las sesiones abiertas (id = «s:» + índice en la lista).</summary>
    public static List<Dialog.Option> Options(List<Session> live) =>
        live.Select((s, i) => new Dialog.Option($"s:{i}", s.Name,
            (s.Folder.Length > 0 ? s.Folder : "carpeta desconocida") + (s.InPanel ? (s.Hidden ? " · solo en el panel" : "") : " · ventana sin panel"))).ToList();

    public static Session? FromOption(string option, List<Session> live) =>
        option.StartsWith("s:", StringComparison.Ordinal) && int.TryParse(option[2..], out int i) && i >= 0 && i < live.Count ? live[i] : null;
}

/// <summary>
/// Conversaciones de Claude Code: viven en %USERPROFILE%\.claude\projects\&lt;carpeta codificada&gt;\&lt;id&gt;.jsonl, y
/// --continue retoma la más reciente de la carpeta donde se abre. Para asignar una conversación a otra carpeta se
/// mueve allí su fichero.
/// </summary>
internal static class Transcripts
{
    private static string ProjectsDir =>
        Path.Combine(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } c ? c
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"), "projects");

    /// <summary>Nombre de la carpeta de conversaciones de una ruta: todo lo que no es letra o número, cambiado por «-».</summary>
    public static string Encode(string folder)
    {
        var sb = new StringBuilder(folder.Length);
        foreach (char c in Path.GetFullPath(folder).TrimEnd('\\', '/'))
            sb.Append(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ? c : '-');
        return sb.ToString();
    }

    /// <summary>Carpeta de conversaciones de una ruta, o null si es tan larga que Claude Code la acorta a su manera.</summary>
    public static string? DirFor(string folder)
    {
        string name = Encode(folder);
        if (name.Length <= 200) return Path.Combine(ProjectsDir, name);
        // Claude Code acorta los nombres largos y les añade un resumen: se usa la que ya exista con ese principio
        try
        {
            return Directory.Exists(ProjectsDir)
                ? Directory.EnumerateDirectories(ProjectsDir, name[..200] + "*").FirstOrDefault()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fichero de la conversación: el de su id si se conoce y existe; si no, el más reciente de la carpeta de origen.
    /// </summary>
    public static string? Find(string claudeId, string fromFolder)
    {
        try
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(claudeId, "^[A-Za-z0-9-]{8,80}$") && Directory.Exists(ProjectsDir))
            {
                string? fromDir = DirFor(fromFolder);
                if (fromDir != null && File.Exists(Path.Combine(fromDir, claudeId + ".jsonl"))) return Path.Combine(fromDir, claudeId + ".jsonl");
                foreach (string dir in Directory.EnumerateDirectories(ProjectsDir))
                {
                    string f = Path.Combine(dir, claudeId + ".jsonl");
                    if (File.Exists(f)) return f;
                }
            }
            string? source = DirFor(fromFolder);
            if (source == null || !Directory.Exists(source)) return null;
            return new DirectoryInfo(source).EnumerateFiles("*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        }
        catch (Exception e)
        {
            Log.Error(e);
            return null;
        }
    }

    /// <summary>
    /// Mueve la conversación a la carpeta de conversaciones de <paramref name="toFolder"/> (con la carpeta de sus
    /// subagentes y resultados, si la tiene) y la deja como la más reciente, para que --continue la retome allí.
    /// Devuelve null si ha ido bien, o el motivo.
    /// </summary>
    public static string? Move(string file, string toFolder)
    {
        try
        {
            string? target = DirFor(toFolder);
            if (target == null) return "la ruta de la carpeta es demasiado larga para saber dónde guarda Claude Code sus conversaciones";
            Directory.CreateDirectory(target);
            string name = Path.GetFileName(file);
            string dest = Path.Combine(target, name);
            if (!Path.GetFullPath(file).Equals(Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, dest, overwrite: true);
                string extra = Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));
                if (Directory.Exists(extra)) CopyDir(extra, Path.Combine(target, Path.GetFileNameWithoutExtension(file)));
                try
                {
                    File.Delete(file);
                    if (Directory.Exists(extra)) Directory.Delete(extra, recursive: true);
                }
                catch (Exception e)
                {
                    Log.Info($"La conversación se ha copiado a {target}, pero no he podido borrar la original: {e.Message}");
                }
            }
            File.SetLastWriteTimeUtc(dest, DateTime.UtcNow);
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e);
            return e.Message;
        }
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), overwrite: true);
        foreach (string d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
