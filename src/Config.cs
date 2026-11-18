using System;
using System.IO;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Configuración del plug-in en %APPDATA%\ClaudeCodePlsql\ClaudeCode.ini.
/// Se relee antes de cada acción, así que los cambios se aplican sin reiniciar (salvo EstiloMenu).
/// </summary>
internal static class Config
{
    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeCodePlsql");

    public static string FilePath => Path.Combine(Dir, "ClaudeCode.ini");

    public static string Workspace = DefaultWorkspace();
    public static string Terminal = "auto";        // auto | wt | consola
    public static string Shell = "powershell.exe"; // powershell.exe | pwsh.exe
    public static string ClaudeCommand = "claude";
    public static string ExtraArgs = "";
    public static string Sync = "preguntar";       // preguntar | automatico | no
    public static string MenuStyle = "auto";       // auto | cinta | clasico
    public static string SqlclPath = "";
    public static string ClientsRoot = "";          // CarpetaClientes: carpeta con una subcarpeta por cliente (opcional)
    public static bool ProjectMode = true;         // ModoExpedientes: abrir Claude en la carpeta del expediente
    public static bool OnlyCp1252 = true;          // SoloWindows1252: no cargar texto que no quepa en Windows-1252
    public static bool ContinueConversation = true; // RetomarConversacion: abrir Claude con --continue
    public static bool DetailedMessages = true;    // MensajesDetallados: explicar en el mensaje lo que se pasa a Claude
    public static bool SidePanel = true;           // PanelLateral: una ventana de Claude por carpeta, a la derecha del IDE
    public static int PanelPercent = 35;           // AnchoPanel: % del ancho de la pantalla para Claude (20-60)
    public static string PanelMode = "auto";       // PanelSesiones: auto (si hay Node.js y panel) | si | no
    public static string NodePath = "";            // RutaNode: node.exe (vacío = el del PATH o el de Program Files)
    public static string PanelPath = "";           // RutaPanel: carpeta del panel (vacío = la que instala actualizar.ps1)
    public static bool EmbeddedAtStart = false;    // PanelIntegrado: abrir el panel dentro del IDE al arrancar
    public static int EmbeddedPercent = 35;        // AnchoPanelIntegrado: % de la zona de editores para el panel (20-70)

    /// <summary>Último expediente usado (estado.ini). Se ofrece para las ventanas que no son de un fichero de expediente.</summary>
    public static string LastProject = "";

    private static string StatePath => Path.Combine(Dir, "estado.ini");

    private static string DefaultWorkspace() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ClaudePLSQL");

    public static void Load()
    {
        try
        {
            EnsureFile();
            foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] is ';' or '#' or '[') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line[..eq].Trim().ToLowerInvariant();
                string value = Environment.ExpandEnvironmentVariables(line[(eq + 1)..].Trim());

                switch (key)
                {
                    case "carpetatrabajo": if (value.Length > 0) Workspace = value.TrimEnd('\\', '/'); break;
                    case "terminal": Terminal = Choice(value, "auto", "auto", "wt", "consola"); break;
                    case "shell": if (value.Length > 0) Shell = value; break;
                    case "comandoclaude": if (value.Length > 0) ClaudeCommand = value; break;
                    case "argumentosextra": ExtraArgs = value; break;
                    case "sincronizar": Sync = Choice(value, "preguntar", "preguntar", "automatico", "no"); break;
                    case "estilomenu": MenuStyle = Choice(value, "auto", "auto", "cinta", "clasico"); break;
                    case "rutasqlcl": SqlclPath = value.Trim('"'); break;
                    case "carpetaclientes": ClientsRoot = value.Trim('"').TrimEnd('\\', '/'); break;
                    case "modoexpedientes": ProjectMode = YesNo(value, true); break;
                    case "solowindows1252": OnlyCp1252 = YesNo(value, true); break;
                    case "retomarconversacion": ContinueConversation = YesNo(value, true); break;
                    case "mensajesdetallados": DetailedMessages = YesNo(value, true); break;
                    case "panellateral": SidePanel = YesNo(value, true); break;
                    case "anchopanel": if (int.TryParse(value.TrimEnd('%'), out int pct)) PanelPercent = Math.Clamp(pct, 20, 60); break;
                    case "panelsesiones": PanelMode = Choice(value.Replace("í", "i"), "auto", "auto", "si", "no"); break;
                    case "rutanode": NodePath = value.Trim('"'); break;
                    case "rutapanel": PanelPath = value.Trim('"').TrimEnd('\\', '/'); break;
                    case "panelintegrado": EmbeddedAtStart = YesNo(value, false); break;
                    case "anchopanelintegrado": if (int.TryParse(value.TrimEnd('%'), out int ep)) EmbeddedPercent = Math.Clamp(ep, 20, 70); break;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        LoadState();
    }

    private static bool YesNo(string value, bool fallback)
    {
        string v = value.ToLowerInvariant().Replace("í", "i");
        if (v is "si" or "s" or "true" or "1") return true;
        if (v is "no" or "n" or "false" or "0") return false;
        return fallback;
    }

    private static void LoadState()
    {
        try
        {
            LastProject = "";
            if (!File.Exists(StatePath)) return;
            foreach (string raw in File.ReadAllLines(StatePath, Encoding.UTF8))
            {
                string line = raw.Trim();
                const string key = "UltimoExpediente=";
                if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    LastProject = line[key.Length..].Trim();
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public static void SaveLastProject(string root)
    {
        try
        {
            if (root.Equals(LastProject, StringComparison.OrdinalIgnoreCase)) return;
            LastProject = root;
            Directory.CreateDirectory(Dir);
            File.WriteAllText(StatePath,
                "; Estado del plug-in de Claude Code (lo escribe el plug-in; no hace falta tocarlo)\r\n" +
                "UltimoExpediente=" + root + "\r\n",
                new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    private static string Choice(string value, string fallback, params string[] allowed)
    {
        string v = value.ToLowerInvariant().Replace("á", "a").Replace("ó", "o");
        return Array.IndexOf(allowed, v) >= 0 ? v : fallback;
    }

    public static void EnsureFile()
    {
        if (File.Exists(FilePath)) return;
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, Templates.DefaultConfig.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(true));
    }
}
