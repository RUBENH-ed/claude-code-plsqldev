using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Ayuda del plug-in: una página (ayuda/ayuda.html, incrustada en la DLL) que explica qué hace cada botón, para quien
/// lo instala sin que nadie se lo cuente. Se abre con el botón «Ayuda» y, sola, la primera vez después de instalar o
/// de una versión con la ayuda cambiada.
/// </summary>
internal static class Help
{
    /// <summary>Cambia cuando la ayuda trae algo nuevo que conviene que todos vean una vez.</summary>
    private const string Version = "1.9";

    private static string FilePath => Path.Combine(Config.Dir, "ayuda.html");
    private static string SeenPath => Path.Combine(Config.Dir, "ayuda-vista.txt");

    private static bool _pending;

    /// <summary>Al activarse el plug-in: si esta ayuda aún no se ha visto, se abrirá en cuanto el IDE esté listo.</summary>
    public static void CheckFirstRun()
    {
        try
        {
            _pending = !File.Exists(SeenPath) || File.ReadAllText(SeenPath).Trim() != Version;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    /// <summary>Desde el temporizador: abre la ayuda pendiente una sola vez.</summary>
    public static void OpenIfPending()
    {
        if (!_pending) return;
        _pending = false;
        try
        {
            Directory.CreateDirectory(Config.Dir);
            File.WriteAllText(SeenPath, Version, new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        Open();
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

            string? edge = EdgePath();
            if (edge != null)
            {
                string url = new Uri(FilePath).AbsoluteUri; // file:///C:/Users/.../ayuda.html, con lo que haga falta escapado
                Process.Start(new ProcessStartInfo(edge, $"--app={Panel.Arg(url)}") { UseShellExecute = false })?.Dispose();
            }
            else
            {
                Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true })?.Dispose();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            Win32.Message($"No he podido abrir la ayuda ({e.Message}). Está en {FilePath}.", Win32.MB_ICONWARNING);
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
