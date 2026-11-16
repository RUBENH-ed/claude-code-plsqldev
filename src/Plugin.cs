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
    public const int MenuOpen = 3, MenuAskSelection = 4, MenuPull = 5, MenuCopyRef = 6, MenuFolder = 8, MenuConfig = 9,
        MenuConnect = 11, MenuDisconnect = 12, MenuSetDev = 13,
        MenuCompare = 15, MenuReview = 16, MenuExplainError = 17, MenuDocs = 18, MenuInstall = 19,
        MenuSessions = 21, MenuOpenIn = 22, MenuAssign = 23, MenuHelp = 25;

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly Dictionary<int, Link> Links = new();
    private static nuint _timer;
    private static bool _busy;
    private static bool _popupsCreated;
    private static bool _iconsCreated;

    // Ventana de Claude recién lanzada que hay que colocar como panel en cuanto aparezca
    private static string _placeTitle = "";
    private static DateTime _placeUntil;

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

        if (!_iconsCreated)
        {
            CreateIcons();
            _iconsCreated = true;
        }

        StartUpdate();
        Help.CheckFirstRun();

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
                case MenuConnect: ConnectDatabase(development: false); break;
                case MenuDisconnect: DisconnectDatabases(); break;
                case MenuSetDev: ConnectDatabase(development: true); break;
                case MenuCompare: CompareWithDatabases(); break;
                case MenuReview: ReviewWindow(); break;
                case MenuExplainError: ExplainError(); break;
                case MenuDocs: ProjectTask(Prompts.DocsTask, "Claude prepara la documentación del expediente"); break;
                case MenuInstall: ProjectTask(Prompts.InstallTask, "Claude prepara la instalación del expediente"); break;
                case MenuSessions: Panel.Open(); break;
                case MenuOpenIn: OpenInSession(); break;
                case MenuAssign: AssignProject(null); break;
                case MenuHelp: Help.Open(); break;
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

        string copy = link.Ws.Relative(link.Path);
        bool brief = Panel.BriefMessages();
        string prompt = brief ? Prompts.Short(link.Title, copy, ConnectionText())
            : Prompts.Open(link.Ws, link.Title, copy, ConnectionText(), link.Original);
        string again = brief ? prompt : Prompts.Again(link.Title, copy, ConnectionText(), null);

        Launch(link.Ws, prompt, again, $"Claude Code abierto con {System.IO.Path.GetFileName(link.Path)}{Where(link.Ws)}");
    }

    /// <summary>
    /// Botón «Abrir en sesión…»: la ventana activa (guardada o no) en la sesión de Claude que se elija de las abiertas,
    /// o en una sesión nueva (en la carpeta de la ventana o en otra, incluido un tema nuevo con título).
    /// </summary>
    private static void OpenInSession()
    {
        int child = Ide.ChildWindow;
        if (child == 0 || !Ide.HasCodeEditor)
        {
            Win32.Message("Activa primero una ventana con código (SQL, programa, test o comandos).", Win32.MB_ICONINFORMATION);
            return;
        }

        List<Session> live = Sessions.Live();
        Workspace here = CurrentWorkspaceNoAsk();
        var options = Sessions.Options(live);
        options.Add(new Dialog.Option("nueva-aqui", "Sesión nueva aquí",
            (here.IsProject ? $"{here.Kind} {here.Label}" : "Carpeta general") + " · " + here.Root));
        options.Add(new Dialog.Option("nueva-otra", "Sesión nueva en otra carpeta o tema…", "Recientes, carpeta general o uno nuevo con título"));
        int pre = live.FindIndex(s => s.Folder.Length > 0 && Workspace.FromFolder(s.Folder, Config.Workspace).SameAs(here));

        var r = Dialog.Show("Claude Code: abrir en sesión",
            "¿En qué sesión de Claude abro esta ventana? Su texto se pasa a la carpeta de esa sesión (no hace falta que " +
            "la ventana esté guardada) y Claude lo recibe allí.",
            options, pre >= 0 ? pre : live.Count, accept: "Abrir");
        if (r == null) return;

        if (Sessions.FromOption(r.Option, live) is Session target)
        {
            if (target.Folder.Length == 0)
            {
                Win32.Message($"No sé en qué carpeta está la sesión «{target.Name}» (se abrió sin el panel de sesiones). " +
                              "Ábrela desde su carpeta o con el panel de sesiones activado.", Win32.MB_ICONINFORMATION);
                return;
            }
            Workspace ws = Workspace.FromFolder(target.Folder, Config.Workspace);
            Link? link = ExportActiveWindow(ws);
            if (link == null) return;
            string copy = link.Ws.Relative(link.Path);
            Deliver(target, link.Ws, Panel.BriefMessages() ? Prompts.Short(link.Title, copy, ConnectionText())
                    : Prompts.Again(link.Title, copy, ConnectionText(), null),
                $"Ventana pasada a la sesión {target.Name}");
            return;
        }

        Workspace? dest = r.Option == "nueva-aqui" ? here
            : r.Option == "nueva-otra" ? ChooseFolder("¿En qué carpeta abro la sesión nueva?") : null;
        if (dest == null) return;
        Link? l = ExportActiveWindow(dest);
        if (l == null) return;
        string c = l.Ws.Relative(l.Path);
        bool shortText = Panel.BriefMessages();
        Launch(l.Ws,
            shortText ? Prompts.Short(l.Title, c, ConnectionText()) : Prompts.Open(l.Ws, l.Title, c, ConnectionText(), l.Original),
            shortText ? Prompts.Short(l.Title, c, ConnectionText()) : Prompts.Again(l.Title, c, ConnectionText(), null),
            $"Claude Code abierto con {System.IO.Path.GetFileName(l.Path)}{Where(l.Ws)}", forceNew: true);
    }

    /// <summary>
    /// Deja un texto escrito (sin enviarlo) en una sesión concreta: en el panel, por su id; sin panel, en el portapapeles
    /// y con su ventana delante.
    /// </summary>
    private static void Deliver(Session target, Workspace ws, string text, string status)
    {
        var panel = Panel.Available();
        if (target.InPanel && panel != null && Panel.PasteTo(panel.Value, target.Id, text, ws.InternalDir))
        {
            Ide.Status($"{status}: te he dejado el mensaje escrito en el panel; revísalo y pulsa Intro.");
            return;
        }
        nint window = Win32.FindTopWindow(target.Title);
        bool copied = Win32.SetClipboardText(text);
        if (window != 0) Win32.BringToFront(window);
        Ide.Status(copied
            ? $"{status}: pega con Ctrl+V en la sesión lo que te he copiado y pulsa Intro."
            : $"{status}, pero no he podido copiar el mensaje (usa Copiar referencia).");
    }

    /// <summary>
    /// Botón «Asignar expediente» (o el del panel, con la sesión ya elegida): una conversación empezada fuera de un
    /// expediente pasa a uno (nuevo o existente, con número) o a un tema con título. Crea la carpeta donde se elija,
    /// cierra la sesión, mueve su conversación a la carpeta nueva y la reabre allí retomándola. Desde entonces, lo que
    /// se abra de esa carpeta va a esa conversación.
    /// </summary>
    private static void AssignProject(string? panelId)
    {
        List<Session> live = Sessions.Live();
        Session? target = panelId != null ? live.FirstOrDefault(s => s.Id == panelId) : null;
        if (target == null)
        {
            if (panelId != null)
            {
                Win32.Message("Esa sesión ya no está abierta.", Win32.MB_ICONINFORMATION);
                return;
            }
            if (live.Count == 0)
            {
                Win32.Message("No hay ninguna sesión de Claude abierta desde el plug-in.", Win32.MB_ICONINFORMATION);
                return;
            }
            Workspace here = CurrentWorkspaceNoAsk();
            int pre = live.FindIndex(s => s.Folder.Length > 0 && Workspace.FromFolder(s.Folder, Config.Workspace).SameAs(here));
            var pick = Dialog.Show("Claude Code: asignar expediente", "¿Qué conversación quieres asignar a un expediente o tema?",
                Sessions.Options(live), Math.Max(pre, 0), accept: "Siguiente");
            target = pick == null ? null : Sessions.FromOption(pick.Option, live);
            if (target == null) return;
        }
        if (target.Folder.Length == 0)
        {
            Win32.Message($"No sé en qué carpeta está la sesión «{target.Name}» (se abrió sin el panel de sesiones), así que " +
                          "no puedo encontrar su conversación.", Win32.MB_ICONINFORMATION);
            return;
        }
        Workspace from = Workspace.FromFolder(target.Folder, Config.Workspace);

        // Donde se crea: por defecto, junto al último expediente (o la carpeta de temas)
        string last = Config.LastProject.TrimEnd('\\', '/');
        string parent = last.Length > 0 && Directory.Exists(last) ? System.IO.Path.GetDirectoryName(last) ?? "" : "";
        if (parent.Length == 0) parent = System.IO.Path.Combine(Config.Workspace, "temas");
        try { Directory.CreateDirectory(parent); } catch (Exception e) { Log.Error(e); }

        var fields = NewProjectFields(parent);
        fields[1] = fields[1] with { Label = "Número de expediente (7 cifras; vacío para un tema)" };
        var r = Dialog.Show("Claude Code: asignar expediente",
            $"La conversación de «{target.Name}» ({target.Folder}) pasa a este expediente o tema. Si la carpeta ya existe, " +
            "se usa tal cual. La sesión se cierra y se vuelve a abrir allí con la misma conversación.",
            fields: fields, accept: "Asignar");
        if (r == null) return;
        Workspace? ws = CreateFromFields(r);
        if (ws == null) return;
        if (ws.SameAs(from))
        {
            Win32.Message($"La sesión ya está en {ws.Label}.", Win32.MB_ICONINFORMATION);
            return;
        }

        // La conversación se mueve con la sesión cerrada, para llevársela entera
        var panel = Panel.Available();
        if (target.InPanel && panel != null)
        {
            Ide.Status($"Cerrando la sesión {target.Name}...");
            if (!Panel.Close(panel.Value, target.Id) &&
                Win32.Message("La sesión no se ha cerrado del todo. ¿Sigo igualmente? Lo que diga a partir de ahora no se " +
                              "llevaría al expediente.", Win32.MB_YESNO | Win32.MB_ICONWARNING) != Win32.IDYES)
                return;
        }
        else if (Win32.Message($"Cierra la ventana de Claude «{target.Name}» (con /exit o cerrando la ventana) y pulsa Aceptar " +
                               "para llevarme su conversación al expediente.", 0x1 /* MB_OKCANCEL */ | Win32.MB_ICONINFORMATION) != 1)
        {
            return;
        }

        string? file = Transcripts.Find(target.ClaudeId, target.Folder);
        string? problem = file == null ? "no he encontrado el fichero de la conversación" : Transcripts.Move(file, ws.Root);
        if (problem != null)
        {
            Log.Info($"Asignar {target.Folder} a {ws.Root}: {problem}");
            Win32.Message($"He creado {ws.Root}, pero no he podido llevar allí la conversación ({problem}). La sesión se " +
                          "abrirá en la carpeta nueva con una conversación nueva.", Win32.MB_ICONWARNING);
        }
        else
        {
            Log.Info($"Conversación de {target.Folder} asignada a {ws.Root} ({System.IO.Path.GetFileName(file)}).");
        }

        // Las ventanas que iban a la carpeta de antes pasan a la nueva (con su copia), para que sigan en esta conversación
        foreach (Link link in Links.Values.Where(l => l.Ws.SameAs(from)).ToList())
        {
            try
            {
                Directory.CreateDirectory(ws.WindowsDir);
                string dest = System.IO.Path.Combine(ws.WindowsDir, System.IO.Path.GetFileName(link.Path));
                if (File.Exists(link.Path)) File.Copy(link.Path, dest, overwrite: true);
                link.Ws = ws;
                link.Path = dest;
                if (File.Exists(dest)) link.LastWriteUtc = File.GetLastWriteTimeUtc(dest);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        string prompt = Prompts.Assigned(ws, target.Folder);
        Launch(ws, prompt, prompt, $"Conversación asignada al {ws.Kind} {ws.Label}", forceContinue: problem == null);
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

        string copy = link.Ws.Relative(link.Path);
        bool brief = Panel.BriefMessages();
        string prompt = brief ? Prompts.Short(link.Title, copy, ConnectionText(), (link.Ws.Relative(selPath), line)) :
            Prompts.Selection(link.Ws, link.Title, copy, ConnectionText(), link.Original,
            link.Ws.Relative(selPath), line);
        string again = brief ? prompt : Prompts.Again(link.Title, copy, ConnectionText(), (link.Ws.Relative(selPath), line));

        Launch(link.Ws, prompt, again, "Claude Code abierto con la selección" + Where(link.Ws));
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

    // ================================================================ herramientas: encargos concretos a Claude

    /// <summary>Abre Claude (o le deja el encargo en el portapapeles) con la ventana activa y una tarea concreta.</summary>
    private static void TaskWithWindow(string task, string status)
    {
        Link? link = ExportActiveWindow();
        if (link == null) return;

        string copy = link.Ws.Relative(link.Path);
        bool brief = Panel.BriefMessages();
        string prompt = brief ? Prompts.ShortTask(link.Title, copy, ConnectionText(), task)
            : Prompts.Task(link.Ws, link.Title, copy, ConnectionText(), link.Original, task);
        string again = brief ? prompt : Prompts.TaskAgain(link.Title, copy, ConnectionText(), task);
        Launch(link.Ws, prompt, again, status + Where(link.Ws));
    }

    /// <summary>Botón "Comparar con otra BD": el objeto de la ventana entre desarrollo y las BBDD del proyecto.</summary>
    private static void CompareWithDatabases()
    {
        var obj = Ide.WindowType == 3 ? Ide.WindowObject() : null;
        string what = obj is { } o ? $"el objeto {o.Owner}.{o.Name} ({o.Type})" : "los objetos que usa esta ventana";
        TaskWithWindow(
            $"Compara {what} entre la BD de desarrollo y las BBDD conectadas en el proyecto (-Lista te dice cuáles hay): " +
            "su fuente en ALL_SOURCE o DBA_SOURCE, o su estructura en ALL_TAB_COLUMNS si es una tabla o vista. Dime qué " +
            "cambia, qué versión parece tener cada una y con cuál coincide mi copia. Si no hay más BBDD conectadas que la " +
            "de desarrollo, dímelo y para. No cambies nada.",
            "Claude compara el objeto entre BBDD");
    }

    /// <summary>Botón "Revisar normas": la ventana contra las normas de EDISA, sin tocarla.</summary>
    private static void ReviewWindow() =>
        TaskWithWindow(
            "Revisa la copia antes de que la compile: normas de EDISA (joins implícitos con (+) y nunca JOIN ... ON, " +
            "fechas con máscara, || para concatenar, nada de SELECT *, binds en el SQL dinámico, UPDATE y DELETE con " +
            "WHERE, comentarios en español, packages estándar de LIBRA en vez de reinventar, solo caracteres de " +
            "Windows-1252) y errores probables (variables sin usar, excepciones que se tragan, cursores sin cerrar, " +
            "nombres de tablas o columnas que no existan: compruébalos en la BD de desarrollo). Dame una lista por " +
            "línea con lo que cambiarías y por qué, de lo más grave a lo menos. No toques la copia hasta que te lo pida.",
            "Claude revisa la ventana");

    /// <summary>
    /// Botón "Explicar error": si hay texto en el portapapeles (el error copiado de PL/SQL Developer) se lo pasa en un
    /// fichero, porque un mensaje con comillas dobles no llega bien a claude.exe; si no, que lo busque en ALL_ERRORS.
    /// </summary>
    private static void ExplainError()
    {
        string clip = Win32.GetClipboardText().Trim();
        string task;
        if (clip.Length > 0 && clip.Length < 20_000)
        {
            Workspace ws = CurrentWorkspaceNoAsk();
            EnsureWorkspace(ws);
            string file = System.IO.Path.Combine(ws.InternalDir, $"error-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, ToCrlf(Normalize(clip)) + "\r\n", Utf8NoBom);
            task = $"Te dejo en {ws.Relative(file)} el error que me ha dado (copiado de PL/SQL Developer). ";
        }
        else
        {
            task = "Me ha dado un error al compilar o ejecutar esto. Búscalo en ALL_ERRORS del objeto, en la BD donde " +
                   "lo he compilado si está conectada, o pídeme el mensaje. ";
        }
        TaskWithWindow(task + "Explícame la causa con la línea donde está y cómo lo arreglarías en la copia. " +
                       "No cambies nada hasta que te lo diga.", "Claude explica el error");
    }

    /// <summary>Encargo sobre el expediente de la ventana activa (documentación, instalación).</summary>
    private static void ProjectTask(string task, string status)
    {
        Workspace ws = CurrentWorkspaceNoAsk();
        if (!ws.IsProject)
        {
            Win32.Message("Esta opción es para un expediente: abre una ventana de un fichero que esté dentro de la carpeta " +
                          "del expediente («1234567 - Título») y vuelve a pulsarla.", Win32.MB_ICONINFORMATION);
            return;
        }
        string prompt = Prompts.ProjectTask(ws, task);
        Launch(ws, prompt, task, status + Where(ws));
    }

    // ================================================================ bases de datos para Claude

    private static string BbddDir => System.IO.Path.Combine(Config.Dir, "bbdd");

    /// <summary>
    /// Carpeta del cliente de un expediente, para que todos sus desarrollos e incidencias compartan las BBDD: la
    /// subcarpeta de CarpetaClientes donde está el expediente o, sin ese ajuste, la que lo contiene, saltando las de
    /// estado como «01.PENDIENTE INSTALAR REAL». Null fuera de un expediente.
    /// </summary>
    private static string? ClientFolder(Workspace ws)
    {
        if (!ws.IsProject || ws.IsTopic) return null; // un tema tiene sus propias BBDD
        string root = System.IO.Path.GetFullPath(ws.Root).TrimEnd('\\', '/');
        if (Config.ClientsRoot.Length > 0)
        {
            string clients = System.IO.Path.GetFullPath(Config.ClientsRoot).TrimEnd('\\', '/') + "\\";
            if (root.StartsWith(clients, StringComparison.OrdinalIgnoreCase))
                return clients + root[clients.Length..].Split('\\', '/')[0];
        }
        string? parent = System.IO.Path.GetDirectoryName(root);
        if (parent != null && System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(parent), @"^\d+\s*[.\-_ ]"))
            parent = System.IO.Path.GetDirectoryName(parent);
        return string.IsNullOrEmpty(System.IO.Path.GetFileName(parent ?? "")) ? null : parent;
    }

    /// <summary>A quién pertenecen las BBDD conectadas desde esta ventana: «del cliente X» o «de este proyecto».</summary>
    private static string ScopeText(Workspace ws) =>
        ClientFolder(ws) is string c ? $"del cliente {System.IO.Path.GetFileName(c)}" : "de este proyecto";

    /// <summary>
    /// Identificador del ámbito de las BBDD conectadas (el cliente del expediente o, si no se sabe, la carpeta de
    /// trabajo): 12 caracteres hexadecimales de su ruta.
    /// </summary>
    private static string ProjectId(Workspace ws)
    {
        string root = System.IO.Path.GetFullPath(ClientFolder(ws) ?? ws.Root).TrimEnd('\\', '/').ToLowerInvariant();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Botones "Conectar a BBDD" (la sesión activa, solo para el proyecto de la ventana) y "Fijar como BD de
    /// desarrollo" (la de todos los proyectos). Escriben el .env que lee el envoltorio consulta-bbdd.ps1 en
    /// %APPDATA%\ClaudeCodePlsql\bbdd, nunca en la carpeta del expediente. Claude no ve la contraseña.
    /// </summary>
    private static void ConnectDatabase(bool development)
    {
        (string user, string password, string db) = Ide.ConnectionWithPassword();
        if (user.Length == 0 || db.Length == 0)
        {
            Win32.Message("PL/SQL Developer no está conectado a ninguna base de datos.", Win32.MB_ICONINFORMATION);
            return;
        }
        if (password.Length == 0)
        {
            Win32.Message("No he podido leer la contraseña de la sesión activa, así que no puedo darla de alta para Claude.",
                Win32.MB_ICONWARNING);
            return;
        }

        // El lanzador de LIBRA numera el alias de cada sesión (ALIAS#1, ALIAS#2...): se guarda sin el número, para que
        // volver a conectar la misma base la sustituya en vez de añadir otra
        string dbName = System.Text.RegularExpressions.Regex.Replace(db, "#\\d+$", "");
        string origin = $"{user}@{dbName}";
        Workspace ws = CurrentWorkspaceNoAsk();

        // A qué sesión de Claude se conecta: la de la carpeta de la ventana no siempre es la que se quiere
        Session? target = null;
        if (!development)
        {
            List<Session> live = Sessions.Live();
            if (live.Count > 0)
            {
                var options = Sessions.Options(live);
                options.Add(new Dialog.Option("ventana", "La carpeta de esta ventana",
                    (ws.IsProject ? $"{ws.Kind} {ws.Label}" : "Carpeta general") + " · " + ws.Root));
                int pre = live.FindIndex(s => s.Folder.Length > 0 && Workspace.FromFolder(s.Folder, Config.Workspace).SameAs(ws));
                var r = Dialog.Show("Claude Code: conectar BD",
                    $"¿A qué sesión de Claude conecto {origin}? Queda conectada a la carpeta de esa sesión (en un expediente, a " +
                    "todos los del mismo cliente) y se lo digo a esa sesión.",
                    options, pre >= 0 ? pre : live.Count, accept: "Conectar");
                if (r == null) return;
                target = Sessions.FromOption(r.Option, live);
                if (target != null)
                {
                    if (target.Folder.Length == 0)
                    {
                        Win32.Message($"No sé en qué carpeta está la sesión «{target.Name}» (se abrió sin el panel de sesiones). " +
                                      "Conéctala desde una ventana de su carpeta.", Win32.MB_ICONINFORMATION);
                        return;
                    }
                    ws = Workspace.FromFolder(target.Folder, Config.Workspace);
                }
            }
        }

        string kind = development ? "desarrollo"
            : System.Text.RegularExpressions.Regex.IsMatch(db, "PRUEB|TEST|DESA|DEV|QA|PRE",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? "pruebas" : "produccion";
        if (kind == "produccion" &&
            Win32.Message($"{origin} no parece una base de pruebas ni de desarrollo.\n\n" +
                          "Si es de producción, Claude podrá consultarla (solo lectura y con tu aprobación en cada consulta), " +
                          "y los datos que lea saldrán de tu equipo hacia Anthropic.\n\n" +
                          $"¿La conecto en las BBDD {ScopeText(ws)}?",
                Win32.MB_YESNO | Win32.MB_ICONWARNING) != Win32.IDYES)
            return;

        Directory.CreateDirectory(Config.Dir);
        string alta = System.IO.Path.Combine(Config.Dir, "alta-bbdd.ps1");
        WriteIfChanged(alta, Templates.AltaBbdd, new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1
        string input = "{" +
            $"\"modo\":{Json(development ? "desarrollo" : "proyecto")},\"proyecto\":{Json(ProjectId(ws))},\"ruta\":{Json(ClientFolder(ws) ?? ws.Root)}," +
            $"\"nombre\":{Json(dbName)},\"tipo\":{Json(kind)},\"usuario\":{Json(user)},\"clave\":{Json(password)},\"bd\":{Json(db)}," +
            $"\"tnsAdmin\":{Json(Environment.GetEnvironmentVariable("TNS_ADMIN") ?? "")},\"origen\":{Json(origin)}" + "}";

        Ide.Status($"Dando de alta para Claude la BD {origin}...");
        (int exit, string output, string error) = RunPowerShell(alta, input);
        if (exit != 0)
        {
            Log.Info($"Alta de la BD {origin} para Claude: {error}");
            Win32.Message($"No he podido dar de alta la BD {origin} para Claude:\n\n{error}", Win32.MB_ICONWARNING);
            return;
        }
        Log.Info($"BD {origin} dada de alta para Claude ({kind}).");

        if (development)
        {
            Win32.Message($"{origin} queda como BD de desarrollo: Claude la consulta por defecto en todas las sesiones que " +
                          "abras desde PL/SQL Developer (en las que ya están abiertas, la franja de BBDD se actualiza sola).",
                Win32.MB_ICONINFORMATION);
            return;
        }

        // Sin comillas dobles: Windows PowerShell 5.1 las rompe al pasar el mensaje a claude.exe. El nombre para -Bd es
        // el que deja alta-bbdd.ps1: el alias en minúsculas, con _ en lo que no sea letra, número o _
        string name = System.Text.RegularExpressions.Regex.Replace(dbName, "[^A-Za-z0-9_]", "_").ToLowerInvariant();
        // Breve: el mod de BBDD ya le pasa a Claude la lista con cada mensaje, y la forma de consultar va en su prompt de sistema
        string prompt = Panel.BriefMessages()
            ? $"BD {origin} ({kind}) conectada: -Bd {name}."
            : $"He conectado la BD {origin} ({kind}) a las BBDD {ScopeText(ws)}. Consúltala con & '{WriteQueryScript()}' " +
            $"-Sql 'SELECT ...' -Bd {name}; la de desarrollo sigue siendo la de por defecto. " +
            "Compruébalo con SELECT USER FROM dual. Si el expediente tiene fuentes (cfstd, cfcliente), mira además si los " +
            "objetos que tocan existen en esa BD y si su versión difiere de la de desarrollo, y avísame de lo que cambie.";
        if (target != null)
            Deliver(target, ws, prompt, $"BD {origin} disponible para Claude ({ScopeText(ws)})");
        else
            Launch(ws, prompt, prompt, $"BD {origin} disponible para Claude ({ScopeText(ws)})");
    }

    /// <summary>Botón "Desconectar BBDD": borra las BBDD del proyecto de la ventana (la de desarrollo se queda).</summary>
    private static void DisconnectDatabases()
    {
        Workspace ws = CurrentWorkspaceNoAsk();
        string dir = System.IO.Path.Combine(BbddDir, ProjectId(ws));
        if (!Directory.Exists(dir))
        {
            Win32.Message($"No hay BBDD conectadas {ScopeText(ws)}: Claude solo tiene la de desarrollo.",
                Win32.MB_ICONINFORMATION);
            return;
        }
        Directory.Delete(dir, recursive: true);
        Log.Info($"BBDD {ScopeText(ws)} desconectadas ({ws.Root}).");

        if (Config.SidePanel && Win32.FindTopWindow(ClaudeTitle(ws)) != 0)
        {
            string prompt = $"He desconectado las BBDD {ScopeText(ws)}: ya solo tienes la de desarrollo.";
            Launch(ws, prompt, prompt, $"BBDD {ScopeText(ws)} desconectadas");
        }
        else
        {
            Ide.Status($"BBDD {ScopeText(ws)} desconectadas: Claude solo tiene la de desarrollo.");
        }
    }

    /// <summary>Texto para Claude con las BBDD que puede consultar desde esta carpeta (sin comillas dobles).</summary>
    private static string DatabaseSummary(Workspace ws)
    {
        string? dev = null;
        var project = new List<(string Origin, string Name, string Kind)>();
        try
        {
            string devFile = System.IO.Path.Combine(BbddDir, "desarrollo.json");
            if (File.Exists(devFile))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(devFile));
                dev = Prop(doc.RootElement, "origen");
            }
            string index = System.IO.Path.Combine(BbddDir, ProjectId(ws), "conexiones.json");
            if (File.Exists(index))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(index));
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("conexiones", out var list) && list.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var c in list.EnumerateArray())
                        project.Add((Prop(c, "origen"), Prop(c, "nombre"), Prop(c, "tipo")));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        var sb = new StringBuilder();
        sb.Append(!string.IsNullOrEmpty(dev)
            ? $"BD de desarrollo, la de por defecto (sin -Bd): {dev}. "
            : "Todavía no hay BD de desarrollo fijada: pide al usuario que la fije con el botón Fijar como BD de desarrollo. ");
        if (project.Count == 0)
        {
            sb.Append($"No hay más BBDD conectadas {ScopeText(ws)}.");
        }
        else
        {
            sb.Append($"BBDD conectadas {ScopeText(ws)} (sirven para todos sus expedientes): ");
            sb.Append(string.Join("; ", project.Select(c => $"{c.Origin} ({c.Kind}), -Bd {c.Name}")));
            sb.Append('.');
        }
        return sb.ToString().Replace("\"", "'");
    }

    private static string Prop(System.Text.Json.JsonElement e, string name) =>
        e.ValueKind == System.Text.Json.JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ToString() : "";

    /// <summary>Escribe (si ha cambiado) el envoltorio de consultas y devuelve su ruta.</summary>
    private static string WriteQueryScript()
    {
        Directory.CreateDirectory(Config.Dir);
        string file = System.IO.Path.Combine(Config.Dir, "consulta-bbdd.ps1");
        WriteIfChanged(file, Templates.ConsultaBbdd, new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1
        // Junto a él, el que abre ficheros y ventanas en PL/SQL Developer
        WriteIfChanged(System.IO.Path.Combine(Config.Dir, "plsql.ps1"), Templates.PlsqlScript, new UTF8Encoding(true));
        return file;
    }

    /// <summary>Escribe (si ha cambiado) el mod de la franja de BBDD y devuelve su carpeta, para --plugin-dir.</summary>
    private static string WriteMod()
    {
        string dir = System.IO.Path.Combine(Config.Dir, "mod-bbdd");
        Directory.CreateDirectory(System.IO.Path.Combine(dir, ".claude-plugin"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "hooks"));
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "types"));
        WriteIfChanged(System.IO.Path.Combine(dir, ".claude-plugin", "plugin.json"), Templates.ModPluginJson, Utf8NoBom);
        WriteIfChanged(System.IO.Path.Combine(dir, "hooks", "hooks.json"), Templates.ModHooksJson, Utf8NoBom);
        WriteIfChanged(System.IO.Path.Combine(dir, "hooks", "register.tsx"), Templates.ModRegister, Utf8NoBom);
        WriteIfChanged(System.IO.Path.Combine(dir, "types", "index.d.ts"), Templates.ModTypes, Utf8NoBom);
        return dir;
    }

    /// <summary>Iconos de la cinta: PL/SQL Developer los asigna con IDE_CreateToolButton al índice de cada opción.</summary>
    private static void CreateIcons()
    {
        try
        {
            string dir = System.IO.Path.Combine(Config.Dir, "iconos");
            Directory.CreateDirectory(dir);
            foreach ((int index, string name, string svg) in Templates.Iconos)
            {
                string file = System.IO.Path.Combine(dir, $"{index}.svg");
                WriteIfChanged(file, svg, Utf8NoBom);
                Ide.CreateToolButton(index, name, file);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Lanza un script de PowerShell sin ventana, le pasa el texto por stdin y espera (como mucho un minuto).</summary>
    private static (int Exit, string Output, string Error) RunPowerShell(string script, string input)
    {
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File {Quote(script)}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8NoBom,
        };
        using Process proc = Process.Start(psi)!;
        proc.StandardInput.Write(input);
        proc.StandardInput.Close();
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd().Trim();
        if (!proc.WaitForExit(60_000))
        {
            try { proc.Kill(); } catch { }
            return (-1, output, "PowerShell no ha terminado en un minuto.");
        }
        return (proc.ExitCode, output.Trim(), error);
    }

    private static string Json(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char ch in s)
        {
            if (ch is '"' or '\\') sb.Append('\\').Append(ch);
            else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
            else sb.Append(ch);
        }
        return sb.Append('"').ToString();
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
            Workspace project = Workspace.Project(root);
            if (!project.IsTopic) Config.SaveLastProject(root);
            return project;
        }

        if (Links.TryGetValue(child, out Link? link)) return link.Ws;

        return ChooseFolder("Esta ventana no es un fichero de ningún expediente. ¿En qué carpeta la abro con Claude? " +
                            "Con «Nuevo con título» creas un tema (o un expediente, si le pones número) al que podrás " +
                            "volver como a cualquier expediente.");
    }

    /// <summary>
    /// Pregunta la carpeta: los expedientes y temas recientes, la carpeta general o uno nuevo con título (tema, o
    /// expediente si se le pone número), que se crea donde se elija. Null si se cancela.
    /// </summary>
    private static Workspace? ChooseFolder(string text)
    {
        var recent = Recent.List();
        var options = recent.Select((w, i) => new Dialog.Option($"r:{i}", w.Label, (w.IsTopic ? "Tema · " : "Expediente · ") + w.Root)).ToList();
        options.Add(new Dialog.Option("general", "Carpeta general", Config.Workspace));
        options.Add(new Dialog.Option("nuevo", "Nuevo con título…", "Crea la carpeta y la recuerda como un expediente"));

        string topics = System.IO.Path.Combine(Config.Workspace, "temas");
        try { Directory.CreateDirectory(topics); } catch (Exception e) { Log.Error(e); }

        var r = Dialog.Show("Claude Code: ¿dónde lo abro?", text, options, 0, NewProjectFields(topics), "nuevo", "Abrir");
        if (r == null) return null;
        if (r.Option == "general") return Workspace.General(Config.Workspace);
        if (r.Option.StartsWith("r:", StringComparison.Ordinal) && int.TryParse(r.Option[2..], out int ix) && ix < recent.Count)
            return recent[ix];
        return r.Option == "nuevo" ? CreateFromFields(r) : null;
    }

    /// <summary>Campos para crear un expediente o tema: número (opcional), título y carpeta donde crearlo.</summary>
    private static List<Dialog.Field> NewProjectFields(string parent) => new()
    {
        new Dialog.Field("titulo", "Título", Required: true,
            Help: "Para volver a abrirlo más tarde: aparecerá en esta lista y todo lo que guardes en su carpeta se abrirá con su conversación."),
        new Dialog.Field("numero", "Número de expediente (opcional, 7 cifras)", Pattern: "^\\d{7}$",
            Error: "El número de expediente son 7 cifras (o déjalo vacío para un tema).",
            Help: "Con número se crea «1234567 - Título» con la estructura de un expediente; sin número, un tema con su carpeta doc."),
        new Dialog.Field("carpeta", "Dentro de la carpeta", parent, "carpeta", Required: true),
    };

    private static Workspace? CreateFromFields(Dialog.Result r)
    {
        try
        {
            Workspace ws = Workspace.Create(r.Get("carpeta"), r.Get("numero"), r.Get("titulo"));
            if (!ws.IsTopic) Config.SaveLastProject(ws.Root);
            Recent.Add(ws);
            Log.Info($"Creado el {ws.Kind} {ws.Root}.");
            return ws;
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No he podido crear la carpeta:\n\n{e.Message}", Win32.MB_ICONWARNING);
            return null;
        }
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

    /// <summary>Escribe el fichero solo si su contenido ha cambiado (otra sesión de Claude puede estar leyéndolo).</summary>
    internal static void WriteIfChanged(string path, string content, Encoding encoding)
    {
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return;
        }
        catch (IOException)
        {
            // si no se puede leer, se intenta escribir igualmente
        }
        File.WriteAllText(path, content, encoding);
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
    private static Link? ExportActiveWindow(Workspace? forced = null)
    {
        int child = Ide.ChildWindow;
        if (child == 0 || !Ide.HasCodeEditor)
        {
            Win32.Message("Activa primero una ventana con código (SQL, programa, test o comandos).", Win32.MB_ICONINFORMATION);
            return null;
        }

        string ideFile = Ide.Filename;
        Workspace? ws = forced ?? PickWorkspace(child, ideFile);
        if (ws == null) return null;
        Recent.Add(ws);

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
        (bool set, string? encoding) = Ide.SetTextVerified(ToCrlf(diskNormalized), Normalize);
        if (!set)
        {
            Win32.Message("PL/SQL Developer no ha permitido cambiar el texto (¿la ventana es de solo lectura?).", Win32.MB_ICONWARNING);
            return false;
        }
        if (encoding == null)
        {
            Log.Info($"{name}: después de cargarlo, el texto del editor no coincide con el fichero con ninguna codificación.");
            Win32.Message(
                $"He cargado los cambios de Claude ({name}), pero al releer el editor el texto no coincide con el fichero: " +
                "puede que las tildes o algún carácter no hayan llegado bien. Revísalo antes de compilar o ejecutar.",
                Win32.MB_ICONWARNING);
        }
        else if (encoding != Ide.Enc.WebName)
        {
            Log.Info($"{name}: con {Ide.Enc.WebName} el texto llegaba mal al editor; cargado en {encoding}.");
        }

        int lines = diskNormalized.Count(c => c == '\n') + 1;
        if (line > 0) Ide.SetCursor(1, Math.Min(line, lines));

        link.Snapshot = diskNormalized;
        link.LastWriteUtc = File.GetLastWriteTimeUtc(link.Path);
        Ide.Status($"Cambios de Claude cargados ({name}). Revisa, compila y guarda.");
        return true;
    }

    // ================================================================ actualización automática

    private static DateTime _updateStartedUtc;
    private static bool _updateStarted, _updateWaiting;

    /// <summary>
    /// Lanza en segundo plano actualizar.ps1 de la copia del repositorio que deja instalar.ps1 (%APPDATA%\ClaudeCodePlsql\repo):
    /// baja la versión publicada, deja preparada la DLL nueva y pone al día las skills elegidas. Una vez por arranque.
    /// </summary>
    private static void StartUpdate()
    {
        if (_updateStarted) return;
        _updateStarted = true;
        string script = System.IO.Path.Combine(Config.Dir, "repo", "actualizar.ps1");
        if (!File.Exists(script)) return; // instalado a mano, sin actualizaciones automáticas
        try
        {
            _updateStartedUtc = DateTime.UtcNow;
            Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File {Quote(script)}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })?.Dispose();
            _updateWaiting = true;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Desde el temporizador: cuando actualizar.ps1 deja su resultado, avisa una vez de lo que haya.</summary>
    private static void CheckUpdate()
    {
        if (!_updateWaiting) return;
        if (DateTime.UtcNow - _updateStartedUtc > TimeSpan.FromMinutes(5)) { _updateWaiting = false; return; }
        string file = System.IO.Path.Combine(Config.Dir, "actualizacion.json");
        if (!File.Exists(file) || File.GetLastWriteTimeUtc(file) < _updateStartedUtc) return;
        _updateWaiting = false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            string version = Prop(root, "version");
            List<string> errors = Strings(root, "errores"), conflicts = Strings(root, "conflictos"), skills = Strings(root, "skills");
            foreach (string s in errors.Concat(conflicts)) Log.Info($"Actualización: {s}");

            // Con novedades, actualizar.ps1 ya ha abierto su ventana (que dice lo del reinicio): basta la barra de estado
            bool novedades = Prop(root, "novedades").Length > 0;
            if (root.TryGetProperty("dll_nueva", out var dllNueva) && dllNueva.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                Log.Info($"Actualización: versión {version} preparada.");
                if (novedades)
                    Ide.Status($"Versión nueva del plug-in de Claude Code ({version}): se aplicará cuando reinicies PL/SQL Developer.");
                else Win32.Message($"Hay una versión nueva del plug-in de Claude Code ({version}). Se aplicará cuando reinicies " +
                              "PL/SQL Developer.\n\nLas novedades están en CAMBIOS.md.", Win32.MB_ICONINFORMATION);
            }
            else if (skills.Count > 0)
            {
                Ide.Status($"Skills de Claude actualizadas: {string.Join(", ", skills)}");
            }
            if (errors.Count > 0) Ide.Status($"No se ha podido actualizar el plug-in de Claude Code: {errors[0]}");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    // ================================================================ peticiones de Claude (plsql.ps1)

    /// <summary>
    /// Desde el temporizador: atiende las peticiones que deja plsql.ps1 en %APPDATA%\ClaudeCodePlsql\peticiones (abrir
    /// un fichero o una ventana nueva, con la conexión de una BD si se pide). Nunca se ejecuta nada en la ventana. La
    /// contraseña no viaja en la petición: la lee el plug-in del .env de esa BD.
    /// </summary>
    private static void CheckRequests()
    {
        string dir = System.IO.Path.Combine(Config.Dir, "peticiones");
        if (!Directory.Exists(dir)) return;
        foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            string json;
            try
            {
                json = File.ReadAllText(file);
                File.Delete(file);
            }
            catch (IOException)
            {
                continue; // todavía la están escribiendo: en el siguiente tic
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var r = doc.RootElement;
                string action = Prop(r, "accion"), path = Prop(r, "fichero"), text = Prop(r, "texto"), db = Prop(r, "bd");
                if (action == "claude")
                {
                    OpenFromPanel(Prop(r, "carpeta"));
                    continue;
                }
                if (action == "asignar")
                {
                    // Botón «Asignar expediente» del panel: los cuadros de diálogo salen delante de todo
                    if (System.Text.RegularExpressions.Regex.IsMatch(Prop(r, "sesion"), "^[A-Za-z0-9_-]{8,64}$"))
                        AssignProject(Prop(r, "sesion"));
                    continue;
                }
                int type = r.TryGetProperty("tipo", out var t) && t.TryGetInt32(out int n) ? n : 0;

                int ix = -1;
                if (db.Length > 0)
                {
                    ix = ConnectionFor(db, Prop(r, "proyecto"));
                    if (ix < 0)
                    {
                        Win32.Message($"Claude ha pedido abrir una ventana con la BD «{db}», pero no he podido conectarla. " +
                                      "Comprueba que sigue dada de alta (botón Conectar a BBDD).", Win32.MB_ICONWARNING);
                        continue;
                    }
                }

                bool ok = action == "abrir" ? Ide.OpenFile(type, path) : Ide.CreateWindow(type is >= 1 and <= 4 ? type : 1, text);
                if (!ok)
                {
                    Ide.Status(action == "abrir" ? $"No se ha podido abrir {path}" : "No se ha podido crear la ventana");
                    continue;
                }
                if (ix >= 0) Ide.SetWindowConnection(ix);
                Win32.BringToFront(Ide.MainWindow);
                Ide.Status(action == "abrir"
                    ? $"Claude ha abierto {System.IO.Path.GetFileName(path)}{(db.Length > 0 ? $" con la BD {db}" : "")}"
                    : $"Claude ha abierto una ventana nueva{(db.Length > 0 ? $" con la BD {db}" : "")}");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    /// <summary>
    /// Botón «Nueva sesión» del panel: abre Claude en un expediente (o en la carpeta general si llega vacía), sin ventana,
    /// con las mismas normas y BBDD que si se abriera desde la cinta. El panel solo manda carpetas de expediente.
    /// </summary>
    private static void OpenFromPanel(string folder)
    {
        Config.Load();
        Workspace ws;
        string f = folder.Trim().TrimEnd('\\', '/');
        if (f.Length == 0) ws = Workspace.General(Config.Workspace);
        else if (Directory.Exists(f) && Workspace.IsProjectFolder(f)) ws = Workspace.Project(f);
        else
        {
            Log.Info($"El panel ha pedido abrir Claude en una carpeta que no es un expediente: {folder}");
            return;
        }
        if (ws.IsProject && !ws.IsTopic) Config.SaveLastProject(ws.Root);
        Launch(ws, Prompts.FromPanel(ws), "", "Claude abierto desde el panel" + Where(ws), onlyInPanel: true);
    }

    /// <summary>Conecta en PL/SQL Developer una de las BBDD dadas de alta para Claude. Devuelve su índice o -1.</summary>
    private static int ConnectionFor(string name, string scope)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_]+$")) return -1;
        string env = name == "desarrollo"
            ? System.IO.Path.Combine(BbddDir, "desarrollo.env")
            : System.Text.RegularExpressions.Regex.IsMatch(scope, "^[0-9a-f]{12}$") ? System.IO.Path.Combine(BbddDir, scope, name + ".env") : "";
        if (env.Length == 0 || !File.Exists(env)) return -1;

        // Los .env van en Windows-1252, como los escribe alta-bbdd.ps1
        var cfg = new Dictionary<string, string>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (string line in File.ReadAllLines(env, Encoding.GetEncoding(1252)))
        {
            int eq = line.IndexOf('=');
            if (eq > 0 && !line.TrimStart().StartsWith('#')) cfg[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        if (!cfg.TryGetValue("BD_USUARIO", out string? user) || !cfg.TryGetValue("BD_CLAVE", out string? password) ||
            !cfg.TryGetValue("BD_HOST", out string? host) || !cfg.TryGetValue("BD_PUERTO", out string? port) ||
            !cfg.TryGetValue("BD_SERVICIO", out string? service))
            return -1;
        return Ide.Connect(user, password, $"{host}:{port}/{service}");
    }

    private static List<string> Strings(System.Text.Json.JsonElement e, string name)
    {
        var list = new List<string>();
        if (e.ValueKind != System.Text.Json.JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return list;
        if (v.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var x in v.EnumerateArray())
                if (x.ValueKind == System.Text.Json.JsonValueKind.String && x.GetString() is { Length: > 0 } s) list.Add(s);
        }
        else if (v.ValueKind == System.Text.Json.JsonValueKind.String && v.GetString() is { Length: > 0 } one)
        {
            list.Add(one);
        }
        return list;
    }

    /// <summary>Temporizador en el hilo principal del IDE: detecta cuándo Claude ha cambiado el fichero de la ventana activa.</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void OnTimer(nint hwnd, uint msg, nuint id, uint time)
    {
        if (_busy) return; // un MessageBox abierto sigue procesando mensajes: evitar reentrada
        _busy = true;
        try
        {
            CheckUpdate();
            Help.OpenIfPending();
            CheckRequests();
            PlacePending();
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

    /// <summary>Título de la ventana de Claude de una carpeta: sirve para encontrarla y reutilizarla.</summary>
    private static string ClaudeTitle(Workspace ws) =>
        (ws.IsProject ? $"Claude Code - {ws.Label}" : "Claude Code - PL/SQL Developer").Replace(';', ',');

    /// <summary>Coloca como panel la ventana de Claude que se acaba de lanzar, en cuanto aparece (como mucho 30 s).</summary>
    private static void PlacePending()
    {
        if (_placeTitle.Length == 0) return;
        if (DateTime.UtcNow > _placeUntil)
        {
            _placeTitle = "";
            return;
        }
        nint hwnd = Win32.FindTopWindow(_placeTitle);
        if (hwnd == 0) return;
        _placeTitle = "";
        Win32.PlaceSide(hwnd, Config.PanelPercent);
    }

    /// <summary>
    /// Abre Claude Code en la carpeta de trabajo. Con PanelLateral, si ya hay una ventana de Claude de esa carpeta,
    /// la trae al frente y deja en el portapapeles <paramref name="again"/> para pegarlo, en vez de abrir otra.
    /// </summary>
    /// <param name="forceNew">Abrir otra sesión aunque ya haya una en esa carpeta (botón «Abrir en sesión…»): con su
    /// propio título y, si ya había otra, con una conversación nueva.</param>
    /// <param name="forceContinue">Retomar la última conversación de la carpeta aunque RetomarConversacion=no
    /// (al reabrir una sesión en el expediente que se le acaba de asignar).</param>
    private static bool Launch(Workspace ws, string prompt, string again, string status, bool onlyInPanel = false,
        bool forceNew = false, bool forceContinue = false)
    {
        if (ws.IsProject) Recent.Add(ws);
        string title = ClaudeTitle(ws);
        bool continueConversation = forceContinue || Config.ContinueConversation;
        if (forceNew)
        {
            string unique = Sessions.UniqueTitle(title, Sessions.Live());
            if (unique != title)
            {
                title = unique;
                continueConversation = false; // la otra sesión de esta carpeta sigue con la suya
            }
        }

        // Panel de sesiones: la sesión se abre dentro de su anfitrión, que la comparte con el panel, y el mod del panel
        // le cuenta lo que hace Claude. Sin Node.js o sin panel, Claude se abre como siempre. Con la preferencia del
        // panel «ocultar ventanas» (o si lo pide el propio panel), sin ventana de Windows Terminal: solo en el panel.
        var panel = Panel.Available();
        if (onlyInPanel && panel == null)
        {
            Win32.Message($"No se puede abrir la sesión en el panel: {Panel.LastProblem}", Win32.MB_ICONWARNING);
            return false;
        }
        bool hidden = panel != null && (onlyInPanel || Panel.HideWindows());

        if (Config.SidePanel && !hidden && !forceNew)
        {
            nint existing = Win32.FindTopWindow(title);
            if (existing != 0)
            {
                bool copied = Win32.SetClipboardText(again);
                Win32.BringToFront(existing);
                Win32.PlaceSide(existing, Config.PanelPercent);
                Ide.Status(copied
                    ? "Claude ya estaba abierto para esta carpeta: pega con Ctrl+V lo que te he copiado y pulsa Intro."
                    : "Claude ya estaba abierto para esta carpeta (no he podido copiar la referencia: usa Copiar referencia).");
                return true;
            }
        }

        // Una sola sesión por carpeta también en el panel: si ya hay una, se le deja el texto escrito y se enseña
        if (!forceNew && panel != null && (hidden || Config.SidePanel) && Panel.Reuse(panel.Value, title, again, ws.InternalDir))
        {
            Ide.Status(again.Length > 0
                ? "Claude ya estaba abierto para esta carpeta: te he dejado el mensaje escrito en el panel; revísalo y pulsa Intro."
                : "Claude ya estaba abierto para esta carpeta: lo tienes en el panel de sesiones.");
            return true;
        }

        EnsureWorkspace(ws);
        CleanOldLaunchers(ws);

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        string promptFile = System.IO.Path.Combine(ws.InternalDir, $"prompt-{stamp}.txt");
        string script = System.IO.Path.Combine(ws.InternalDir, $"lanzar-{stamp}.ps1");

        File.WriteAllText(promptFile, SafePrompt(prompt), Utf8NoBom);

        // Comando de Claude Code. El servidor MCP de SQLcl se pasa con --mcp-config desde %APPDATA%,
        // para no dejar un .mcp.json en la carpeta del expediente.
        string claude = PsQuote(Config.ClaudeCommand);
        var options = new List<string>();

        // Norma de solo consultas contra la base de datos: el hook y sus ajustes se reescriben en cada arranque (por si
        // alguien los ha tocado) y se pasan con --settings, así que solo valen para las sesiones del plug-in.
        Directory.CreateDirectory(Config.Dir);
        string hookFile = System.IO.Path.Combine(Config.Dir, "solo-consultas.ps1");
        string settingsFile = System.IO.Path.Combine(Config.Dir, "settings-plugin.json");
        WriteIfChanged(hookFile, Templates.HookSoloConsultas, new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1
        WriteIfChanged(settingsFile, Templates.SettingsJson(hookFile), Utf8NoBom);
        options.Add("--settings");
        options.Add(PsQuote(settingsFile));
        options.Add("--append-system-prompt");
        // Las normas de las ventanas van siempre aquí, en el prompt de sistema: así los mensajes breves bastan
        options.Add(PsQuote(Templates.ReglaBaseDatos(WriteQueryScript(), DatabaseSummary(ws)) + " " + Prompts.WindowRule(ws)));
        options.Add("--plugin-dir");
        options.Add(PsQuote(WriteMod()));

        if (Config.SqlclPath.Length > 0)
        {
            string mcpFile = System.IO.Path.Combine(Config.Dir, "mcp-sqlcl.json");
            Directory.CreateDirectory(Config.Dir);
            File.WriteAllText(mcpFile, Templates.McpJson(Config.SqlclPath), Utf8NoBom);
            options.Add("--mcp-config");
            options.Add(PsQuote(mcpFile));
        }
        if (panel != null)
        {
            options.Add("--plugin-dir");
            options.Add(PsQuote(System.IO.Path.Combine(panel.Value.Dir, "mod")));
        }
        else if (Config.PanelMode == "si")
        {
            Log.Info("Sesión sin panel: " + Panel.LastProblem);
        }

        if (Config.ExtraArgs.Length > 0) options.Add(Config.ExtraArgs);
        string opts = options.Count > 0 ? " " + string.Join(" ", options) : "";

        var ps = new StringBuilder();
        ps.AppendLine("try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}");
        ps.AppendLine($"$Host.UI.RawUI.WindowTitle = {PsQuote(title)}");
        ps.AppendLine("$env:CLAUDE_CODE_DISABLE_TERMINAL_TITLE = '1'  # que Claude no cambie el título: el plug-in busca la ventana por él");
        ps.AppendLine($"$env:CLAUDE_PLSQL_PROYECTO = '{ProjectId(ws)}'  # BBDD conectadas en esta carpeta (lo leen el hook y el mod)");
        ps.AppendLine($"Set-Location -LiteralPath {PsQuote(ws.Root)}");
        ps.AppendLine($"$prompt = Get-Content -LiteralPath {PsQuote(promptFile)} -Raw -Encoding UTF8");
        ps.AppendLine($"Remove-Item -LiteralPath {PsQuote(promptFile)}, {PsQuote(script)} -ErrorAction SilentlyContinue");
        ps.AppendLine($"if (-not (Get-Command {claude} -ErrorAction SilentlyContinue)) {{");
        ps.AppendLine($"  Write-Host 'No se encuentra el comando de Claude Code ({Config.ClaudeCommand.Replace("'", "''")}).' -ForegroundColor Red");
        ps.AppendLine("  Write-Host 'Instala Claude Code o pon la ruta completa en ComandoClaude (plug-in > Configurar).' -ForegroundColor Red");
        ps.AppendLine("  return");
        ps.AppendLine("}");
        if (continueConversation)
        {
            // Retoma la última conversación de esta carpeta; si no hay ninguna y claude termina con error enseguida,
            // abre una nueva con el mismo mensaje.
            ps.AppendLine("$inicio = Get-Date");
            ps.AppendLine($"& {claude} --continue{opts} $prompt");
            ps.AppendLine("if ($LASTEXITCODE -ne 0 -and ((Get-Date) - $inicio).TotalSeconds -lt 20) {");
            ps.AppendLine($"  & {claude}{opts} $prompt");
            ps.AppendLine("}");
        }
        else
        {
            ps.AppendLine($"& {claude}{opts} $prompt");
        }
        File.WriteAllText(script, ps.ToString(), new UTF8Encoding(true)); // con BOM para Windows PowerShell 5.1

        string psArgs = $"-NoLogo -NoExit -ExecutionPolicy Bypass -File {Quote(script)}";
        string wt = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        bool useWt = Config.Terminal == "wt" || (Config.Terminal == "auto" && File.Exists(wt));

        if (hidden)
        {
            // Sin ventana: el anfitrión corre oculto y la sesión se ve y se usa en el panel, que se abre en ella
            try
            {
                string id = Panel.NewId();
                Process.Start(new ProcessStartInfo(panel!.Value.Node,
                    Panel.HostArguments(panel.Value.Dir, title, ws.Root, $"{Config.Shell} {psArgs}", id))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = ws.Root,
                })?.Dispose();
                Panel.Show(panel.Value, id);
                Ide.Status(status + " (en el panel de sesiones)");
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e);
                Win32.Message($"No se pudo abrir la sesión en el panel (Node.js):\n\n{e.Message}", Win32.MB_ICONERROR);
                return false;
            }
        }

        try
        {
            // Lo que se ejecuta en la ventana: PowerShell con el lanzador o, con el panel, el anfitrión que lo envuelve
            string exe = Config.Shell, args = psArgs;
            if (panel != null)
            {
                exe = panel.Value.Node;
                args = Panel.HostArguments(panel.Value.Dir, title, ws.Root, $"{Config.Shell} {psArgs}");
            }
            ProcessStartInfo psi = useWt
                ? new ProcessStartInfo(File.Exists(wt) ? wt : "wt.exe", $"-d {Quote(ws.Root)} --title \"{title}\" {Panel.Arg(exe)} {args}")
                : new ProcessStartInfo(exe, args);
            psi.UseShellExecute = true;
            psi.WorkingDirectory = ws.Root;
            Process.Start(psi);
            Ide.Status(panel != null ? status + " (también en el panel de sesiones)" : Config.PanelMode == "si" ? $"{status}. Sin panel de sesiones: {Panel.LastProblem}" : status);
            if (Config.SidePanel)
            {
                _placeTitle = title;
                _placeUntil = DateTime.UtcNow.AddSeconds(30);
            }
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No se pudo abrir la terminal ({(useWt ? "Windows Terminal" : panel != null ? "Node.js" : Config.Shell)}):\n\n{e.Message}\n\n" +
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

    private static string Where(Workspace ws) =>
        ws.IsTopic ? $" (tema {ws.Label})" : ws.IsProject ? $" (expediente {ws.Number})" : "";

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
