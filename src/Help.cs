using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Ayuda y novedades del plug-in.
/// - La ayuda (ayuda/ayuda.html, incrustada en la DLL) explica qué hace cada botón, para quien lo instala sin que nadie
///   se lo cuente. Se abre con el botón «Ayuda» y, sola, la primera vez después de instalar o de una versión con la
///   ayuda cambiada.
/// - Las novedades (novedades.html, que prepara actualizar.ps1 a partir de CAMBIOS.md) se abren solas una vez, la
///   primera vez que arranca PL/SQL Developer después de la actualización, ya con la versión nueva cargada. Si toca
///   también la ayuda, primero la ayuda y, cuando se cierra, las novedades. Siempre están en «Acerca de».
/// </summary>
internal static class Help
{
    /// <summary>Cambia cuando la ayuda trae algo nuevo que conviene que todos vean una vez.</summary>
    private const string Version = "1.9";

    /// <summary>Parte del título de la ayuda (su &lt;title&gt;), que es el de su ventana de Edge.</summary>
    private const string HelpTitle = "PL/SQL Developer: ayuda";

    private static string FilePath => Path.Combine(Config.Dir, "ayuda.html");
    private static string SeenPath => Path.Combine(Config.Dir, "ayuda-vista.txt");
    private static string NewsPath => Path.Combine(Config.Dir, "novedades.html");
    private static string NewsPendingPath => Path.Combine(Config.Dir, "novedades-pendientes.txt");

    private static bool _helpPending, _newsPending, _helpSeen;
    private static DateTime _helpOpenedUtc;

    /// <summary>
    /// Al activarse el plug-in, antes de lanzar la actualización: lo que haya que enseñar se abrirá en cuanto el IDE
    /// esté listo. Las novedades pendientes son las de una actualización anterior, nunca las de la que empieza ahora.
    /// </summary>
    public static void CheckFirstRun()
    {
        try
        {
            _helpPending = !File.Exists(SeenPath) || File.ReadAllText(SeenPath).Trim() != Version;
            _newsPending = File.Exists(NewsPendingPath);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Desde el temporizador: primero la ayuda; las novedades, cuando se cierre la ayuda.</summary>
    public static void OpenIfPending()
    {
        if (_helpPending)
        {
            _helpPending = false;
            try
            {
                Directory.CreateDirectory(Config.Dir);
                File.WriteAllText(SeenPath, Version, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            _helpOpenedUtc = DateTime.UtcNow;
            _helpSeen = false;
            Open();
            return;
        }

        if (!_newsPending) return;
        if (_helpOpenedUtc != default)
        {
            // Se espera a que la ventana de la ayuda aparezca y se cierre; si no llega a verse (sin Edge, en una pestaña
            // del navegador...), las novedades salen a los 20 segundos, ya por detrás de la ayuda.
            bool open = Win32.FindTopWindow(HelpTitle) != 0;
            if (open) _helpSeen = true;
            if (open || (!_helpSeen && DateTime.UtcNow - _helpOpenedUtc < TimeSpan.FromSeconds(20))) return;
        }

        _newsPending = false;
        try
        {
            File.Delete(NewsPendingPath);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        if (File.Exists(NewsPath)) Show(NewsPath);
    }

    /// <summary>Botón «Ayuda»: deja la página en %APPDATA%\ClaudeCodePlsql y la abre en una ventana propia de Edge.</summary>
    public static void Open()
    {
        try
        {
            using Stream? s = typeof(Help).Assembly.GetManifestResourceStream("ayuda.html");
            if (s == null)
            {
                Win32.Message("Esta versión del plug-in no trae la ayuda. Mira el README del repositorio.", Win32.MB_ICONWARNING);
                return;
            }
            using var reader = new StreamReader(s, Encoding.UTF8);
            string html = reader.ReadToEnd();
            Directory.CreateDirectory(Config.Dir);
            if (!File.Exists(FilePath) || File.ReadAllText(FilePath, Encoding.UTF8) != html)
                File.WriteAllText(FilePath, html, new UTF8Encoding(false));
            Show(FilePath);
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No he podido abrir la ayuda ({e.Message}). Está en {FilePath}.", Win32.MB_ICONWARNING);
        }
    }

    /// <summary>
    /// «Acerca de» en la configuración de plug-ins: la versión y, si se quiere, las novedades. Devuelve "" porque el
    /// diálogo lo enseña el propio plug-in (así lo permite la función About de PL/SQL Developer).
    /// </summary>
    public static void About(string text)
    {
        bool news = File.Exists(NewsPath);
        string question = news
            ? "\r\n\r\n¿Quieres ver las novedades de cada versión?"
            : "\r\n\r\nLas novedades de cada versión estarán aquí después de la próxima actualización.";
        if (Win32.Message(text + question, news ? Win32.MB_YESNO | Win32.MB_ICONINFORMATION : Win32.MB_ICONINFORMATION) == Win32.IDYES)
            Show(NewsPath);
    }

    /// <summary>Abre una página local en una ventana propia de Edge (o en el navegador, si no hay Edge).</summary>
    private static void Show(string file)
    {
        try
        {
            string? edge = EdgePath();
            if (edge != null)
            {
                string url = new Uri(file).AbsoluteUri; // file:///C:/Users/.../ayuda.html, con lo que haga falta escapado
                Process.Start(new ProcessStartInfo(edge, $"--app={Panel.Arg(url)}") { UseShellExecute = false })?.Dispose();
            }
            else
            {
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })?.Dispose();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No he podido abrir {file} ({e.Message}).", Win32.MB_ICONWARNING);
        }
    }

    private static string? EdgePath()
    {
        foreach (Environment.SpecialFolder f in new[]
                 {
                     Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.LocalApplicationData,
                 })
        {
            string candidate = Path.Combine(Environment.GetFolderPath(f), "Microsoft", "Edge", "Application", "msedge.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
