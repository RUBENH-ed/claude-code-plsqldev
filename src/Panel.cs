using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Panel de sesiones: una página local (127.0.0.1) con todas las sesiones de Claude abiertas desde el plug-in, su
/// terminal en directo y lo que está haciendo cada una. Lo sirve Node.js con los ficheros de la carpeta panel del
/// repositorio, que actualizar.ps1 copia a %APPDATA%\ClaudeCodePlsql\panel-app\&lt;versión&gt; (así una actualización
/// nunca choca con un panel en marcha).
///
/// Con el panel, cada sesión de Claude se abre dentro de su "anfitrión" (anfitrion.js), que la comparte con el panel;
/// en la ventana de Windows Terminal se ve y se usa igual que antes. Si falta Node.js o algo del panel, el plug-in
/// abre Claude como siempre.
/// </summary>
internal static class Panel
{
    private static string? _checkedFor; // node|carpeta que ya pasó la comprobación en esta sesión del IDE
    private static string? _failedFor;  // node|carpeta que ya la suspendió (no se repite hasta reiniciar el IDE)
    private static string _failure = "";
    private static string _lastProblem = "";

    /// <summary>Carpeta del panel instalada (la de RutaPanel o la que dejó actualizar.ps1), o null.</summary>
    public static string? AppDir()
    {
        if (Config.PanelPath.Length > 0)
            return File.Exists(Path.Combine(Config.PanelPath, "anfitrion.js")) ? Config.PanelPath : null;

        string pointer = Path.Combine(Config.Dir, "panel-app", "actual.txt");
        try
        {
            if (!File.Exists(pointer)) return null;
            string dir = File.ReadAllText(pointer, Encoding.UTF8).Trim();
            return dir.Length > 0 && File.Exists(Path.Combine(dir, "anfitrion.js")) ? dir : null;
        }
        catch (Exception e)
        {
            Log.Error(e);
            return null;
        }
    }

    /// <summary>node.exe: el de RutaNode, el del PATH o el de la instalación habitual de Node.js.</summary>
    public static string? NodePath()
    {
        if (Config.NodePath.Length > 0) return File.Exists(Config.NodePath) ? Config.NodePath : null;

        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                string d = dir.Trim().Trim('"');
                if (d.Length == 0) continue;
                string candidate = Path.Combine(Environment.ExpandEnvironmentVariables(d), "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // entrada del PATH mal formada
            }
        }
        foreach (Environment.SpecialFolder f in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.LocalApplicationData })
        {
            string candidate = Path.Combine(Environment.GetFolderPath(f), f == Environment.SpecialFolder.ProgramFiles ? "nodejs" : @"Programs\nodejs", "node.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Node.js y la carpeta del panel, si el panel está activado y funciona en este equipo; si no, null (y
    /// <see cref="LastProblem"/> dice por qué). La primera vez en cada sesión del IDE lanza comprobar.js.
    /// </summary>
    public static (string Node, string Dir)? Available()
    {
        if (Config.PanelMode == "no") { _lastProblem = "el panel está desactivado (PanelSesiones=no)."; return null; }

        string? dir = AppDir();
        if (dir == null)
        {
            _lastProblem = Config.PanelPath.Length > 0
                ? $"no está el panel en {Config.PanelPath} (RutaPanel)."
                : "el panel aún no está instalado: se instala con la próxima actualización (al arrancar PL/SQL Developer) o con instalar.ps1.";
            return null;
        }
        string? node = NodePath();
        if (node == null)
        {
            _lastProblem = "no encuentro Node.js (node.exe). Instálalo con: winget install OpenJS.NodeJS.LTS, o pon su ruta en RutaNode.";
            return null;
        }

        string key = node + "|" + dir;
        if (_checkedFor == key) return (node, dir);
        if (_failedFor == key) { _lastProblem = _failure; return null; }

        var (exit, output, error) = Run(node, Arg(Path.Combine(dir, "comprobar.js")), dir, 8_000);
        if (exit != 0)
        {
            _lastProblem = _failure = $"la comprobación del panel ha fallado: {(error.Length > 0 ? error : output)}";
            _failedFor = key;
            Log.Info("Panel de sesiones no disponible: " + _lastProblem);
            return null;
        }
        Log.Info($"Panel de sesiones disponible ({output}) en {dir}.");
        _checkedFor = key;
        return (node, dir);
    }

    public static string LastProblem => _lastProblem;

    /// <summary>Botón "Panel de sesiones": arranca el servidor si hace falta y abre el panel en el navegador.</summary>
    public static void Open()
    {
        var available = Available();
        if (available == null)
        {
            Win32.Message($"No se puede abrir el panel de sesiones: {_lastProblem}", Win32.MB_ICONWARNING);
            return;
        }
        var (node, dir) = available.Value;
        // Sin esperar: abrir.js deja el servidor en marcha y abre la ventana del panel por su cuenta
        Process.Start(new ProcessStartInfo(node, $"{Arg(Path.Combine(dir, "abrir.js"))}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = dir,
        })?.Dispose();
        Ide.Status("Abriendo el panel de sesiones…");
    }

    /// <summary>
    /// Argumentos para lanzar <paramref name="command"/> dentro del anfitrión del panel:
    /// node anfitrion.js --titulo ... --carpeta ... -- command. Con <paramref name="id"/>, sin ventana: la sesión solo
    /// se ve en el panel (que la enseña por ese id).
    /// </summary>
    public static string HostArguments(string dir, string title, string folder, string command, string? id = null) =>
        $"{Arg(Path.Combine(dir, "anfitrion.js"))} --titulo {Arg(title)} --carpeta {Arg(folder)}" +
        (id != null ? $" --id {id} --sin-ventana" : "") + $" -- {command}";

    /// <summary>Identificador para una sesión nueva (el anfitrión lo usa y el panel la enseña por él).</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..20];

    /// <summary>
    /// Preferencia del panel «Ventanas de Windows Terminal: ocultar»: las sesiones se abren sin ventana y solo se ven en
    /// el panel. La guarda el propio panel en %APPDATA%\ClaudeCodePlsql\panel\preferencias.json.
    /// </summary>
    public static bool HideWindows() => Preference("ventanas") == "ocultar";

    /// <summary>
    /// Mensajes breves al pasar una ventana o una BD a Claude: lo que se eligió en Personalizar del panel (mensajes:
    /// breves | detallados) o, si no se ha elegido ahí, MensajesDetallados del ClaudeCode.ini.
    /// </summary>
    public static bool BriefMessages() => Preference("mensajes") switch
    {
        "breves" => true,
        "detallados" => false,
        _ => !Config.DetailedMessages,
    };

    /// <summary>Una preferencia de texto del panel (%APPDATA%\ClaudeCodePlsql\panel\preferencias.json), o "".</summary>
    private static string Preference(string name)
    {
        try
        {
            string file = Path.Combine(Config.Dir, "panel", "preferencias.json");
            if (!File.Exists(file)) return "";
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty(name, out var v) &&
                   v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() ?? "" : "";
        }
        catch (Exception e)
        {
            Log.Error(e);
            return "";
        }
    }

    /// <summary>
    /// Si en el panel hay una sesión abierta con ese título, deja <paramref name="text"/> en su cuadro de mensaje (sin
    /// enviarlo) y la enseña. True si la había.
    /// </summary>
    public static bool Reuse((string Node, string Dir) panel, string title, string text, string tempDir)
    {
        string? file = null;
        try
        {
            string args = $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --titulo {Arg(title)}";
            if (text.Length > 0)
            {
                Directory.CreateDirectory(tempDir);
                file = Path.Combine(tempDir, $"pegar-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
                File.WriteAllText(file, text, new UTF8Encoding(false));
                args += $" --pegar {Arg(file)}";
            }
            var (exit, _, error) = Run(panel.Node, args, panel.Dir, 15_000);
            if (exit != 0 && exit != 3) Log.Info($"No he podido buscar la sesión en el panel: {error}");
            return exit == 0;
        }
        finally
        {
            if (file != null) try { File.Delete(file); } catch { }
        }
    }

    /// <summary>Sesiones vivas del panel (node abrir.js --lista), o null si no se ha podido preguntar.</summary>
    public static System.Collections.Generic.List<Session>? ListSessions((string Node, string Dir) panel)
    {
        var (exit, output, error) = Run(panel.Node, $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --lista", panel.Dir, 10_000);
        if (exit != 0)
        {
            Log.Info($"No he podido pedir la lista de sesiones al panel: {error}");
            return null;
        }
        try
        {
            var list = new System.Collections.Generic.List<Session>();
            using var doc = System.Text.Json.JsonDocument.Parse(output);
            foreach (var s in doc.RootElement.EnumerateArray())
            {
                string P(string n) => s.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString()! : "";
                bool viva = s.TryGetProperty("viva", out var vv) && vv.ValueKind == System.Text.Json.JsonValueKind.True;
                bool oculta = s.TryGetProperty("sinVentana", out var sv) && sv.ValueKind == System.Text.Json.JsonValueKind.True;
                if (viva) list.Add(new Session(P("id"), P("titulo"), P("carpeta"), oculta, P("claudeId")));
            }
            return list;
        }
        catch (Exception e)
        {
            Log.Error(e);
            return null;
        }
    }

    /// <summary>Deja el texto escrito (sin enviarlo) en la sesión del panel con ese id y la enseña. True si estaba abierta.</summary>
    public static bool PasteTo((string Node, string Dir) panel, string id, string text, string tempDir)
    {
        string? file = null;
        try
        {
            string args = $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --id {Arg(id)}";
            if (text.Length > 0)
            {
                Directory.CreateDirectory(tempDir);
                file = Path.Combine(tempDir, $"pegar-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
                File.WriteAllText(file, text, new UTF8Encoding(false));
                args += $" --pegar {Arg(file)}";
            }
            var (exit, _, error) = Run(panel.Node, args, panel.Dir, 15_000);
            if (exit != 0 && exit != 3) Log.Info($"No he podido dejar el texto en la sesión {id} del panel: {error}");
            return exit == 0;
        }
        finally
        {
            if (file != null) try { File.Delete(file); } catch { }
        }
    }

    /// <summary>Cierra la sesión del panel con ese id y espera (como mucho 20 s) a que termine. True si ha terminado.</summary>
    public static bool Close((string Node, string Dir) panel, string id)
    {
        // Puede tardar unos segundos: el IDE queda deshabilitado pero sigue repintándose
        nint ide = Ide.MainWindow;
        if (ide != 0) Win32.EnableWindow(ide, false);
        try
        {
            var (exit, _, error) = Run(panel.Node, $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --cerrar {Arg(id)}", panel.Dir, 25_000, pump: true);
            if (exit != 0) Log.Info($"No he podido cerrar la sesión {id} del panel: {error}");
            return exit == 0;
        }
        finally
        {
            if (ide != 0) Win32.EnableWindow(ide, true);
        }
    }

    /// <summary>Enseña una sesión en el panel (en el que ya esté abierto o en uno nuevo), sin esperar.</summary>
    /// <summary>Enlace de entrada al panel (de un solo uso, 2 minutos), para el panel integrado en el IDE.</summary>
    public static string? EntryUrl((string Node, string Dir) panel, out string error)
    {
        var (exit, output, err) = Run(panel.Node, $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --imprimir", panel.Dir, 30_000);
        string url = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("http://127.0.0.1:")) ?? "";
        error = url.Length > 0 ? "" : (err.Length > 0 ? err : $"abrir.js ha terminado con el código {exit} sin dar el enlace.");
        return url.Length > 0 ? url : null;
    }

    public static void Show((string Node, string Dir) panel, string id)
    {
        try
        {
            Process.Start(new ProcessStartInfo(panel.Node, $"{Arg(Path.Combine(panel.Dir, "abrir.js"))} --sesion {id}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = panel.Dir,
            })?.Dispose();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Un argumento para la línea de comandos de Windows, con las comillas y barras que hagan falta.</summary>
    public static string Arg(string s)
    {
        if (s.Length > 0 && s.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return s;
        var sb = new StringBuilder("\"");
        int backslashes = 0;
        foreach (char c in s)
        {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"') sb.Append('\\', backslashes * 2 + 1).Append('"');
            else sb.Append('\\', backslashes).Append(c);
            backslashes = 0;
        }
        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }

    /// <param name="pump">Esperar atendiendo los mensajes del IDE (solo con su ventana deshabilitada).</param>
    private static (int Exit, string Output, string Error) Run(string exe, string args, string dir, int timeoutMs, bool pump = false)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using Process proc = Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEndAsync();
            var error = proc.StandardError.ReadToEndAsync();
            if (pump) Win32.PumpUntil(proc.Handle, timeoutMs);
            if (!proc.WaitForExit(pump ? 0 : timeoutMs))
            {
                try { proc.Kill(); } catch { }
                return (-1, "", $"{Path.GetFileName(exe)} no ha terminado en {timeoutMs / 1000} s.");
            }
            return (proc.ExitCode, output.Result.Trim(), error.Result.Trim());
        }
        catch (Exception e)
        {
            Log.Error(e);
            return (-1, "", e.Message);
        }
    }
}
