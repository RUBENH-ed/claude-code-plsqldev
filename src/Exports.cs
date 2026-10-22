using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Funciones que PL/SQL Developer busca en la DLL (Plug-In interface). Convención cdecl.
/// Ninguna excepción puede escapar de aquí: tumbaría el IDE.
/// </summary>
public static unsafe class Exports
{
    private const string Description = "Claude Code para PL/SQL Developer";

    private const string AboutText =
        "Claude Code para PL/SQL Developer 1.2 (EDISA)\r\n\r\n" +
        "Pasa el codigo de la ventana activa a Claude Code, abre una sesion en la terminal " +
        "y carga en el editor los cambios que haga Claude.\r\n\r\n" +
        "Si el fichero esta en una carpeta de expediente (1234567 - Titulo), Claude se abre en ella.\r\n" +
        "Una ventana de Claude por carpeta, como panel a la derecha, que retoma la conversacion del proyecto.\r\n\r\n" +
        "Configuracion: %APPDATA%\\ClaudeCodePlsql\\ClaudeCode.ini";

    // Las cadenas que se devuelven al IDE deben seguir vivas después de la llamada: se reservan una vez y no se liberan.
    private static readonly Dictionary<string, nint> HostStrings = new();

    private static nint HostString(string s)
    {
        if (!HostStrings.TryGetValue(s, out nint p))
        {
            byte[] bytes = Encoding.ASCII.GetBytes(s); // los textos de menú son ASCII: válidos en modo ANSI y UTF-8
            p = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            ((byte*)p)[bytes.Length] = 0;
            HostStrings[s] = p;
        }
        return p;
    }

    private static bool UseClassicMenu()
    {
        if (Config.MenuStyle == "clasico") return true;
        if (Config.MenuStyle == "cinta") return false;
        int v = Ide.Version;
        return v > 0 && v < 1200; // la cinta (ribbon) llegó en PL/SQL Developer 12
    }

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error(e);
            try
            {
                Win32.Message($"Se ha producido un error en el plug-in de Claude Code:\n\n{e.Message}\n\nDetalle en {Config.Dir}\\plugin.log",
                    Win32.MB_ICONERROR);
            }
            catch
            {
                // nada más que hacer
            }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "IdentifyPlugIn", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static nint IdentifyPlugIn(int id)
    {
        try
        {
            Ide.PluginId = id;
            Config.Load();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return HostString(Description);
    }

    [UnmanagedCallersOnly(EntryPoint = "RegisterCallback", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void RegisterCallback(int index, nint addr)
    {
        try
        {
            Ide.Register(index, addr);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "CreateMenuItem", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static nint CreateMenuItem(int index)
    {
        try
        {
            if (UseClassicMenu())
            {
                const string m = "Tools / Claude Code / ";
                return HostString(index switch
                {
                    Plugin.MenuOpen => m + "&Abrir en Claude Code",
                    Plugin.MenuAskSelection => m + "&Preguntar por lo seleccionado",
                    Plugin.MenuPull => m + "&Traer cambios de Claude",
                    Plugin.MenuCopyRef => m + "&Copiar referencia para Claude",
                    Plugin.MenuFolder => m + "Abrir carpeta de &trabajo",
                    Plugin.MenuConfig => m + "Con&figurar...",
                    Plugin.MenuConnect => m + "Conectar a &BBDD",
                    Plugin.MenuDisconnect => m + "&Desconectar BBDD",
                    Plugin.MenuSetDev => m + "Fijar como BD de d&esarrollo",
                    _ => "",
                });
            }

            return HostString(index switch
            {
                1 => "TAB=Claude Code",
                2 => "GROUP=Claude Code",
                Plugin.MenuOpen => "LARGEITEM=Abrir en Claude Code", // grande: las otras tres quedan juntas a su derecha
                Plugin.MenuAskSelection => "ITEM=Preguntar por lo seleccionado",
                Plugin.MenuPull => "ITEM=Traer cambios de Claude",
                Plugin.MenuCopyRef => "ITEM=Copiar referencia para Claude",
                7 => "GROUP=Ajustes",
                Plugin.MenuFolder => "ITEM=Abrir carpeta de trabajo",
                Plugin.MenuConfig => "ITEM=Configurar",
                10 => "GROUP=Base de datos",
                Plugin.MenuConnect => "ITEM=Conectar a BBDD",
                Plugin.MenuDisconnect => "ITEM=Desconectar BBDD",
                Plugin.MenuSetDev => "ITEM=Fijar como BD de desarrollo",
                _ => "",
            });
        }
        catch (Exception e)
        {
            Log.Error(e);
            return HostString("");
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "OnMenuClick", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void OnMenuClick(int index) => Guard(() => Plugin.MenuClick(index));

    [UnmanagedCallersOnly(EntryPoint = "OnActivate", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void OnActivate() => Guard(Plugin.Activate);

    [UnmanagedCallersOnly(EntryPoint = "OnDeactivate", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void OnDeactivate() => Guard(Plugin.Deactivate);

    [UnmanagedCallersOnly(EntryPoint = "OnDestroy", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void OnDestroy() => Guard(Plugin.Deactivate);

    /// <summary>0 = comportamiento normal al cerrar la ventana.</summary>
    [UnmanagedCallersOnly(EntryPoint = "OnWindowClose", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static int OnWindowClose(int windowType, int changed)
    {
        Guard(Plugin.WindowClosing);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "About", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static nint About() => HostString(AboutText);

    [UnmanagedCallersOnly(EntryPoint = "Configure", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Configure() => Guard(Plugin.OpenConfig);
}
