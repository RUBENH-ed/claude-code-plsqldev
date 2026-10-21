using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>Una ventana de PL/SQL Developer vinculada con su copia en la carpeta de trabajo.</summary>
internal sealed class Link(Workspace ws, string path, string title)
{
    public Workspace Ws = ws;
    public string Path = path;
    public string Title = title;
    /// <summary>Fichero abierto en la ventana (relativo a la carpeta de trabajo si está dentro) o null si no tiene.</summary>
    public string? Original;
    /// <summary>Texto (normalizado) que había en el editor y en el fichero la última vez que coincidieron.</summary>
    public string Snapshot = "";
    public DateTime LastWriteUtc;
}

internal static unsafe class Plugin
{
    // Índices de menú (CreateMenuItem / OnMenuClick)
    public const int MenuOpen = 3, MenuAskSelection = 4, MenuPull = 5, MenuCopyRef = 6, MenuFolder = 8, MenuConfig = 9;

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly Dictionary<int, Link> Links = new();
    private static nuint _timer;
    private static bool _busy;
    private static bool _popupsCreated;

    // ================================================================ ciclo de vida

    public static void Activate()
    {
        Config.Load();
        bool utf8 = Ide.TrySetUtf8();
        if (utf8) Ide.Enc = Encoding.UTF8;
        Log.Info($"Activado. PL/SQL Developer {Ide.Version}, cadenas {(utf8 ? "UTF-8" : "ANSI")}.");

        if (!_popupsCreated)
        {
            foreach (string window in new[] { "SQLWINDOW", "PROGRAMWINDOW", "TESTWINDOW", "COMMANDWINDOW" })
            {
                Ide.AddPopup(MenuOpen, "Claude: abrir en Claude Code", window);
                Ide.AddPopup(MenuAskSelection, "Claude: preguntar por lo seleccionado", window);
                Ide.AddPopup(MenuPull, "Claude: traer cambios", window);
            }
            _popupsCreated = true;
        }

        if (_timer == 0)
            _timer = Win32.SetTimer(0, 0, 1500, (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, uint, void>)&OnTimer);
    }

    public static void Deactivate()
    {
        if (_timer != 0)
        {
            Win32.KillTimer(0, _timer);
            _timer = 0;
        }
    }

    public static void WindowClosing() => Links.Remove(Ide.ChildWindow);

    public static void MenuClick(int index)
    {
        Config.Load();
        _busy = true; // que el temporizador no abra otro mensaje mientras esta acción tiene uno abierto
        try
        {
            switch (index)
            {
                case MenuOpen: OpenInClaude(); break;
                case MenuAskSelection: AskAboutSelection(); break;
                case MenuPull: PullChanges(); break;
                case MenuCopyRef: CopyReference(); break;
                case MenuFolder: OpenFolder(); break;
                case MenuConfig: OpenConfig(); break;
            }
        }
        finally
        {
            _busy = false;
        }
    }

    // ================================================================ acciones del menú

    private static void OpenInClaude()
    {
        Link? link = ExportActiveWindow();
        if (link == null) return;

        string prompt = Prompts.Open(link.Ws, link.Title, link.Ws.Relative(link.Path), ConnectionText(), link.Original);

        if (Launch(link.Ws, prompt))
            Ide.Status($"Claude Code abierto con {System.IO.Path.GetFileName(link.Path)}{Where(link.Ws)}");
    }

    private static void AskAboutSelection()
    {
        string selection = Ide.GetSelectedText();
        if (string.IsNullOrWhiteSpace(selection))
        {
            Win32.Message("Selecciona primero el fragmento de código sobre el que quieres preguntar.", Win32.MB_ICONINFORMATION);
            return;
        }

        int line = Ide.CursorLine;
        Link? link = ExportActiveWindow();
        if (link == null) return;

        string selPath = System.IO.Path.Combine(
            link.Ws.WindowsDir,
            System.IO.Path.GetFileNameWithoutExtension(link.Path) + ".seleccion" + System.IO.Path.GetExtension(link.Path));
        File.WriteAllText(selPath, ToCrlf(Normalize(selection)) + "\r\n", Utf8NoBom);

        string prompt = Prompts.Selection(link.Ws, link.Title, link.Ws.Relative(link.Path), ConnectionText(), link.Original,
            link.Ws.Relative(selPath), line);

        if (Launch(link.Ws, prompt))
            Ide.Status("Claude Code abierto con la selección" + Where(link.Ws));
    }

    private static void PullChanges()
    {
        if (!Links.TryGetValue(Ide.ChildWindow, out Link? link) || !File.Exists(link.Path))
        {
            Win32.Message("Esta ventana todavía no está vinculada con Claude Code.\n\nUsa antes \"Abrir en Claude Code\".",
                Win32.MB_ICONINFORMATION);
            return;
        }

        string disk = Normalize(ReadShared(link.Path));
        string editor = Normalize(Ide.GetText());
        if (disk == editor)
        {
            link.Snapshot = disk;
            link.LastWriteUtc = File.GetLastWriteTimeUtc(link.Path);
            Win32.Message("No hay cambios: el editor ya tiene lo mismo que el fichero de Claude.", Win32.MB_ICONINFORMATION);
            return;
        }

        if (editor != link.Snapshot &&
            Win32.Message("También has cambiado el texto en PL/SQL Developer desde que se lo pasaste a Claude.\n\n" +
                          "Si cargas los cambios de Claude perderás los del editor (puedes deshacer con Ctrl+Z).\n\n¿Cargar igualmente?",
                Win32.MB_YESNO | Win32.MB_ICONWARNING) != Win32.IDYES)
            return;

        Apply(link, disk);
    }

    private static void CopyReference()
    {
        Link? link = ExportActiveWindow();
        if (link == null) return;

        string reference = "@" + link.Ws.Relative(link.Path).Replace('\\', '/') + " ";
        if (Win32.SetClipboardText(reference))
        {
            if (!Ide.Status($"Copiado {reference.Trim()}: pégalo en tu sesión de Claude Code"))
                Win32.Message($"Copiado al portapapeles:\n\n{reference}\n\nPégalo en tu sesión de Claude Code.", Win32.MB_ICONINFORMATION);
        }
        else
        {
            Win32.Message("No se pudo acceder al portapapeles. Inténtalo de nuevo.", Win32.MB_ICONWARNING);
        }
    }

    private static void OpenFolder()
    {
        Workspace ws = CurrentWorkspaceNoAsk();
        if (!ws.IsProject) EnsureWorkspace(ws);
        Process.Start(new ProcessStartInfo("explorer.exe", Quote(ws.Root)) { UseShellExecute = true });
    }

    public static void OpenConfig()
    {
        Config.EnsureFile();
        Process.Start(new ProcessStartInfo("notepad.exe", Quote(Config.FilePath)) { UseShellExecute = true });
    }

    // ================================================================ carpeta de trabajo de cada ventana

    /// <summary>
    /// Carpeta de trabajo para la ventana activa. Si su fichero está dentro de un expediente («1234567 - Título»),
    /// la de ese expediente. Si no, la que ya tuviera la ventana o, la primera vez, pregunta si usar el último
    /// expediente o la carpeta general. Null si el usuario cancela.
    /// </summary>
    private static Workspace? PickWorkspace(int child, string ideFile)
    {
        if (!Config.ProjectMode) return Workspace.General(Config.Workspace);

        if (Workspace.FindProjectRoot(ideFile) is string root)
        {
            Config.SaveLastProject(root);
            return Workspace.Project(root);
        }

        if (Links.TryGetValue(child, out Link? link)) return link.Ws;

        string last = Config.LastProject.TrimEnd('\\', '/');
        if (last.Length > 0 && Workspace.IsProjectFolderName(System.IO.Path.GetFileName(last)) && Directory.Exists(last))
        {
            Workspace ws = Workspace.Project(last);
            int answer = Win32.Message(
                "Esta ventana no es un fichero de ningún expediente.\n\n" +
                $"Sí: abrirla con Claude en el último expediente, {ws.Label}.\n" +
                $"No: en la carpeta general ({Config.Workspace}).\n" +
                "Cancelar: no hacer nada.",
                Win32.MB_YESNOCANCEL | Win32.MB_ICONQUESTION);

            if (answer == Win32.IDCANCEL) return null;
            if (answer == Win32.IDYES) return ws;
        }

        return Workspace.General(Config.Workspace);
    }

    /// <summary>Carpeta de la ventana activa sin preguntar: la de su vínculo, la del expediente de su fichero o la general.</summary>
    private static Workspace CurrentWorkspaceNoAsk()
    {
        int child = Ide.ChildWindow;
        if (child != 0 && Links.TryGetValue(child, out Link? link)) return link.Ws;
        if (child != 0 && Config.ProjectMode && Workspace.FindProjectRoot(Ide.Filename) is string root)
            return Workspace.Project(root);
        return Workspace.General(Config.Workspace);
    }

    private static void EnsureWorkspace(Workspace ws)
    {
        Directory.CreateDirectory(ws.WindowsDir);
        Directory.CreateDirectory(ws.InternalDir);

        if (ws.IsProject)
        {
            // En un expediente, el CLAUDE.md de la raíz es del proyecto (lo crea la skill proyecto-expediente).
            // El plug-in solo deja el suyo en .px, que Claude Code carga al leer las copias de .px\ventanas.
            WriteIfMissing(ws.ClaudeMdPath, Templates.ProjectClaudeMd);
        }
        else
        {
            WriteIfMissing(ws.ClaudeMdPath, Templates.ClaudeMd);
            WriteIfMissing(System.IO.Path.Combine(ws.Root, ".gitignore"), Templates.GitIgnore);
        }
    }

    private static void WriteIfMissing(string path, string content)
    {
        if (!File.Exists(path)) File.WriteAllText(path, ToCrlf(content.Replace("\r\n", "\n")), Utf8NoBom);
    }

    // ================================================================ sincronización editor <-> fichero

    /// <summary>
    /// Guarda el texto de la ventana activa en su copia de la carpeta de trabajo y devuelve el vínculo.
    /// Si Claude ya había cambiado esa copia y no se ha cargado, pregunta antes de sobrescribirla.
    /// </summary>
    private static Link? ExportActiveWindow()
    {
        int child = Ide.ChildWindow;
        if (child == 0 || !Ide.HasCodeEditor)
        {
            Win32.Message("Activa primero una ventana con código (SQL, programa, test o comandos).", Win32.MB_ICONINFORMATION);
            return null;
        }

        string ideFile = Ide.Filename;
        Workspace? ws = PickWorkspace(child, ideFile);
        if (ws == null) return null;

        EnsureWorkspace(ws);
        string editor = Normalize(Ide.GetText());
        (string path, string title) = ChoosePath(child, ws, ideFile);
        string? original = ideFile.Length > 0 ? ws.Relative(ideFile) : null;

        if (Links.TryGetValue(child, out Link? link) && link.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && File.Exists(link.Path))
        {
            string disk = Normalize(ReadShared(link.Path));
            if (disk != link.Snapshot && disk != editor)
            {
                int answer = Win32.Message(
                    $"Claude ha modificado {System.IO.Path.GetFileName(link.Path)} y esos cambios todavía no están en el editor.\n\n" +
                    "Sí: cargarlos en el editor antes de seguir.\n" +
                    "No: descartarlos y sobrescribir el fichero con lo que hay en el editor.\n" +
                    "Cancelar: no hacer nada.",
                    Win32.MB_YESNOCANCEL | Win32.MB_ICONQUESTION);

                if (answer == Win32.IDCANCEL) return null;
                if (answer == Win32.IDYES)
                {
                    if (!Apply(link, disk)) return null;
                    link.Original = original;
                    return link;
                }
            }
        }
        else
        {
            link = new Link(ws, path, title);
            Links[child] = link;
        }

        link.Title = title;
        link.Original = original;
        File.WriteAllText(link.Path, ToCrlf(editor) + "\r\n", Utf8NoBom);
        link.Snapshot = editor;
        link.LastWriteUtc = File.GetLastWriteTimeUtc(link.Path);
        return link;
    }

    /// <summary>Carga en el editor activo el texto que ha dejado Claude en el fichero. False si no lo ha cargado.</summary>
    private static bool Apply(Link link, string diskNormalized)
    {
        string name = System.IO.Path.GetFileName(link.Path);

        if (Config.OnlyCp1252)
        {
            List<string> bad = TextCheck.NonCp1252(diskNormalized);
            if (bad.Count > 0)
            {
                Win32.Message(
                    $"No cargo en el editor los cambios de Claude: {name} tiene caracteres que no existen en Windows-1252, " +
                    "la codificación de los fuentes de LIBRA, y se perderían al guardar o compilar:\n\n" +
                    string.Join("\n", bad) +
                    "\n\nPídele a Claude que los cambie por caracteres normales (por ejemplo, -> en vez de una flecha o <= en vez " +
                    "de ≤) y vuelve a traer los cambios.",
                    Win32.MB_ICONWARNING);
                return false;
            }
        }

        int line = Ide.CursorLine;
        if (!Ide.SetText(ToCrlf(diskNormalized)))
        {
            Win32.Message("PL/SQL Developer no ha permitido cambiar el texto (¿la ventana es de solo lectura?).", Win32.MB_ICONWARNING);
            return false;
        }

        int lines = diskNormalized.Count(c => c == '\n') + 1;
        if (line > 0) Ide.SetCursor(1, Math.Min(line, lines));

        link.Snapshot = diskNormalized;
        link.LastWriteUtc = File.GetLastWriteTimeUtc(link.Path);
        Ide.Status($"Cambios de Claude cargados ({name}). Revisa, compila y guarda.");
        return true;
    }

    /// <summary>Temporizador en el hilo principal del IDE: detecta cuándo Claude ha cambiado el fichero de la ventana activa.</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void OnTimer(nint hwnd, uint msg, nuint id, uint time)
    {
        if (_busy) return; // un MessageBox abierto sigue procesando mensajes: evitar reentrada
        _busy = true;
        try
        {
            CheckActiveWindow();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        finally
        {
            _busy = false;
        }
    }

    private static void CheckActiveWindow()
    {
        if (Links.Count == 0 || Config.Sync == "no") return;
        if (!Win32.IsOurProcessInForeground()) return; // esperar a que el usuario vuelva a PL/SQL Developer

        if (!Links.TryGetValue(Ide.ChildWindow, out Link? link) || !File.Exists(link.Path)) return;

        DateTime written = File.GetLastWriteTimeUtc(link.Path);
        if (written == link.LastWriteUtc) return;
        if (DateTime.UtcNow - written < TimeSpan.FromMilliseconds(800)) return; // Claude puede seguir escribiendo
        link.LastWriteUtc = written;

        string disk = Normalize(ReadShared(link.Path));
        string editor = Normalize(Ide.GetText());
        if (disk == editor) { link.Snapshot = disk; return; }
        if (disk == link.Snapshot) return;

        bool editorChanged = editor != link.Snapshot;
        if ((Config.Sync == "automatico" && !editorChanged) || (Config.OnlyCp1252 && !TextCheck.IsCp1252(disk)))
        {
            Apply(link, disk); // si hay caracteres fuera de Windows-1252, Apply avisa sin preguntar antes si cargar
            return;
        }

        string text = $"Claude ha modificado {System.IO.Path.GetFileName(link.Path)}.\n\n¿Cargar los cambios en esta ventana?";
        if (editorChanged)
            text += "\n\nOjo: también has cambiado el texto en el editor desde que se lo pasaste a Claude. " +
                    "Si cargas, perderás esos cambios (puedes deshacer con Ctrl+Z).";

        if (Win32.Message(text, Win32.MB_YESNO | (editorChanged ? Win32.MB_ICONWARNING : Win32.MB_ICONQUESTION)) == Win32.IDYES)
            Apply(link, disk);
    }

    // ================================================================ lanzar Claude Code

    private static bool Launch(Workspace ws, string prompt)
    {
        EnsureWorkspace(ws);
        CleanOldLaunchers(ws);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string promptFile = System.IO.Path.Combine(ws.InternalDir, $"prompt-{stamp}.txt");
        string script = System.IO.Path.Combine(ws.InternalDir, $"lanzar-{stamp}.ps1");

        File.WriteAllText(promptFile, SafePrompt(prompt), Utf8NoBom);

        // Comando de Claude Code. El servidor MCP de SQLcl se pasa con --mcp-config desde %APPDATA%,
        // para no dejar un .mcp.json en la carpeta del expediente.
        string claude = PsQuote(Config.ClaudeCommand);
        var command = new List<string> { "&", claude };
        if (Config.SqlclPath.Length > 0)
        {
            string mcpFile = System.IO.Path.Combine(Config.Dir, "mcp-sqlcl.json");
            Directory.CreateDirectory(Config.Dir);
            File.WriteAllText(mcpFile, Templates.McpJson(Config.SqlclPath), Utf8NoBom);
            command.Add("--mcp-config");
            command.Add(PsQuote(mcpFile));
        }
        if (Config.ExtraArgs.Length > 0) command.Add(Config.ExtraArgs);
        command.Add("$prompt");

        string windowTitle = ws.IsProject ? $"Claude Code - {ws.Label}" : "Claude Code - PL/SQL Developer";

        var ps = new StringBuilder();
        ps.AppendLine("try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}");
        ps.AppendLine($"$Host.UI.RawUI.WindowTitle = {PsQuote(windowTitle)}");
        ps.AppendLine($"Set-Location -LiteralPath {PsQuote(ws.Root)}");
        ps.AppendLine($"$prompt = Get-Content -LiteralPath {PsQuote(promptFile)} -Raw -Encoding UTF8");
        ps.AppendLine($"Remove-Item -LiteralPath {PsQuote(promptFile)}, {PsQuote(script)} -ErrorAction SilentlyContinue");
        ps.AppendLine($"if (-not (Get-Command {claude} -ErrorAction SilentlyContinue)) {{");
        ps.AppendLine($"  Write-Host 'No se encuentra el comando de Claude Code ({Config.ClaudeCommand.Replace("'", "''")}).' -ForegroundColor Red");
        ps.AppendLine("  Write-Host 'Instala Claude Code o pon la ruta completa en ComandoClaude (plug-in > Configurar).' -ForegroundColor Red");
        ps.AppendLine("  return");
        ps.AppendLine("}");
        ps.AppendLine(string.Join(" ", command));
        File.WriteAllText(script, ps.ToString(), new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1

        string psArgs = $"-NoLogo -NoExit -ExecutionPolicy Bypass -File {Quote(script)}";
        string wt = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        bool useWt = Config.Terminal == "wt" || (Config.Terminal == "auto" && File.Exists(wt));
        string wtTitle = ws.IsProject ? $"Claude {ws.Number}" : "Claude Code";

        try
        {
            ProcessStartInfo psi = useWt
                ? new ProcessStartInfo(File.Exists(wt) ? wt : "wt.exe", $"-d {Quote(ws.Root)} --title \"{wtTitle}\" {Config.Shell} {psArgs}")
                : new ProcessStartInfo(Config.Shell, psArgs);
            psi.UseShellExecute = true;
            psi.WorkingDirectory = ws.Root;
            Process.Start(psi);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No se pudo abrir la terminal ({(useWt ? "Windows Terminal" : Config.Shell)}):\n\n{e.Message}\n\n" +
                          "Revisa Terminal y Shell en la configuración del plug-in.", Win32.MB_ICONERROR);
            return false;
        }
    }

    private static void CleanOldLaunchers(Workspace ws)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(ws.InternalDir))
            {
                string name = System.IO.Path.GetFileName(f);
                if ((name.StartsWith("lanzar-") || name.StartsWith("prompt-")) &&
                    File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1))
                    File.Delete(f);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    // ================================================================ nombre de la copia

    /// <summary>Elige la copia para la ventana activa: PROPIETARIO.OBJETO.ext, el nombre del fichero abierto o uno genérico.</summary>
    private static (string Path, string Title) ChoosePath(int child, Workspace ws, string ideFile)
    {
        int type = Ide.WindowType;
        string baseName, ext, title;

        var obj = type == 3 ? Ide.WindowObject() : null;

        if (obj is { } o)
        {
            string tab = Ide.CurrentTab();
            baseName = (o.Owner.Length > 0 ? o.Owner + "." : "") + o.Name;
            ext = ExtensionFor(o.Type, tab);
            if (tab.Length > 0 && ext == ".sql") baseName += "." + tab;
            title = $"{o.Type} {(o.Owner.Length > 0 ? o.Owner + "." : "")}{o.Name}".Trim();
        }
        else if (ideFile.Length > 0)
        {
            baseName = System.IO.Path.GetFileNameWithoutExtension(ideFile);
            ext = System.IO.Path.GetExtension(ideFile);
            if (ext.Length == 0) ext = ".sql";
            title = System.IO.Path.GetFileName(ideFile);
        }
        else
        {
            (string name, string e) = type switch
            {
                1 => ("ventana_sql", ".sql"),
                2 => ("ventana_test", ".tst"),
                3 => ("ventana_programa", ".sql"),
                4 => ("ventana_comandos", ".sql"),
                _ => ("ventana", ".sql"),
            };
            baseName = $"{name}_{child:X}";
            ext = e;
            title = type switch { 1 => "Ventana SQL", 2 => "Ventana de test", 3 => "Ventana de programa", 4 => "Ventana de comandos", _ => "Ventana" };
        }

        string path = System.IO.Path.Combine(ws.WindowsDir, Sanitize(baseName) + Sanitize(ext));

        // Si otra ventana ya usa ese fichero (mismo objeto abierto dos veces), diferenciar
        if (Links.Any(kv => kv.Key != child && kv.Value.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            path = System.IO.Path.Combine(ws.WindowsDir, Sanitize(baseName) + $"_{child:X}" + Sanitize(ext));

        return (path, title);
    }

    private static string ExtensionFor(string objectType, string tab)
    {
        string t = objectType.ToUpperInvariant();
        bool body = tab.Contains("body", StringComparison.OrdinalIgnoreCase) || tab.Contains("cuerpo", StringComparison.OrdinalIgnoreCase);
        return t switch
        {
            "PACKAGE BODY" => ".pkb",
            "PACKAGE" or "PACKAGE SPEC AND BODY" => body ? ".pkb" : (tab.Length > 0 && !tab.Contains("spec", StringComparison.OrdinalIgnoreCase) ? ".sql" : ".pks"),
            "TYPE BODY" => ".tpb",
            "TYPE" or "TYPE SPEC AND BODY" => body ? ".tpb" : ".tps",
            "FUNCTION" => ".fnc",
            "PROCEDURE" => ".prc",
            "TRIGGER" => ".trg",
            "VIEW" => ".vw",
            "JAVA SOURCE" => ".java",
            _ => ".sql",
        };
    }

    // ================================================================ utilidades

    private static string ConnectionText()
    {
        (string user, string db) = Ide.Connection();
        if (user.Length == 0) return "";
        return db.Length > 0 ? $" (conexión {user}@{db})" : $" (conexión {user})";
    }

    private static string Where(Workspace ws) => ws.IsProject ? $" (expediente {ws.Number})" : "";

    private static string Normalize(string s) =>
        s.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n', ' ', '\t').TrimStart('﻿');

    private static string ToCrlf(string normalized) => normalized.Replace("\n", "\r\n");

    private static string ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) || c is '_' or '$' or '#' or '.' or '-' ? c : '_');
        return sb.ToString();
    }

    /// <summary>Quita caracteres que podrían romper el paso del texto como argumento (comillas, &amp;, |, etc.).</summary>
    private static string SafePrompt(string prompt)
    {
        var sb = new StringBuilder(prompt.Length);
        foreach (char c in prompt)
            sb.Append(c switch { '"' => '\'', '&' or '|' or '<' or '>' or '^' or '%' or '`' => ' ', _ => c });
        return sb.ToString();
    }

    private static string Quote(string s) => "\"" + s + "\"";

    private static string PsQuote(string s) => "'" + s.Replace("'", "''") + "'";
}
