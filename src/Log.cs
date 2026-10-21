using System;
using System.IO;

namespace ClaudeCodePlsql;

/// <summary>Registro de errores en %APPDATA%\ClaudeCodePlsql\plugin.log (útil si algo falla dentro del IDE).</summary>
internal static class Log
{
    private static string FilePath => Path.Combine(Config.Dir, "plugin.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(Exception e) => Write("ERROR", e.ToString());

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Config.Dir);
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > 1_000_000) info.Delete(); // no dejar que crezca sin límite
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");
        }
        catch
        {
            // el registro nunca debe tumbar el IDE
        }
    }
}
