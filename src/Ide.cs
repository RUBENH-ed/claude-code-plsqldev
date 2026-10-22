using System;
using System.Text;

namespace ClaudeCodePlsql;

/// <summary>
/// Acceso a las funciones "callback" que PL/SQL Developer entrega al plug-in mediante RegisterCallback.
/// Los índices y firmas salen de la documentación oficial del Plug-In interface (PlugInDoc\plugindoc.pdf).
/// Todas las funciones usan la convención cdecl y cadenas char* (ANSI o UTF-8 según CHARMODE).
/// </summary>
internal static unsafe class Ide
{
    public static int PluginId;

    /// <summary>Codificación de las cadenas que viajan por el interface. Se pasa a UTF-8 en OnActivate si el IDE lo admite.</summary>
    public static Encoding Enc = AnsiEncoding();

    private static readonly Encoding Cp1252 = AnsiEncoding();

    // Índices documentados de RegisterCallback
    private const int CB_SYS_Version = 1;
    private const int CB_IDE_Connected = 11;
    private const int CB_IDE_GetConnectionInfo = 12;
    private const int CB_IDE_GetWindowType = 14;
    private const int CB_IDE_GetWindowHandle = 16;
    private const int CB_IDE_GetChildHandle = 18;
    private const int CB_IDE_Filename = 23;
    private const int CB_IDE_GetText = 30;
    private const int CB_IDE_GetSelectedText = 31;
    private const int CB_IDE_SetText = 34;
    private const int CB_IDE_SetStatusMessage = 35;
    private const int CB_IDE_CreatePopupItem = 69;
    private const int CB_IDE_GetWindowObject = 110;
    private const int CB_IDE_GetCursorY = 142;
    private const int CB_IDE_SetCursor = 143;
    private const int CB_IDE_TabInfo = 148;
    private const int CB_IDE_TabIndex = 149;
    private const int CB_IDE_WindowHasEditor = 153;
    private const int CB_IDE_PlugInSetting = 219;
    private const int CB_IDE_GetConnectionInfoEx = 240;
    private const int CB_IDE_GetWindowConnection = 245;

    private static nint _sysVersion, _connected, _getConnectionInfo, _getWindowType, _getWindowHandle,
        _getChildHandle, _filename, _getText, _getSelectedText, _setText, _setStatusMessage,
        _createPopupItem, _getWindowObject, _getCursorY, _setCursor, _tabInfo, _tabIndex,
        _windowHasEditor, _plugInSetting, _getConnectionInfoEx, _getWindowConnection;

    public static void Register(int index, nint addr)
    {
        switch (index)
        {
            case CB_SYS_Version: _sysVersion = addr; break;
            case CB_IDE_Connected: _connected = addr; break;
            case CB_IDE_GetConnectionInfo: _getConnectionInfo = addr; break;
            case CB_IDE_GetWindowType: _getWindowType = addr; break;
            case CB_IDE_GetWindowHandle: _getWindowHandle = addr; break;
            case CB_IDE_GetChildHandle: _getChildHandle = addr; break;
            case CB_IDE_Filename: _filename = addr; break;
            case CB_IDE_GetText: _getText = addr; break;
            case CB_IDE_GetSelectedText: _getSelectedText = addr; break;
            case CB_IDE_SetText: _setText = addr; break;
            case CB_IDE_SetStatusMessage: _setStatusMessage = addr; break;
            case CB_IDE_CreatePopupItem: _createPopupItem = addr; break;
            case CB_IDE_GetWindowObject: _getWindowObject = addr; break;
            case CB_IDE_GetCursorY: _getCursorY = addr; break;
            case CB_IDE_SetCursor: _setCursor = addr; break;
            case CB_IDE_TabInfo: _tabInfo = addr; break;
            case CB_IDE_TabIndex: _tabIndex = addr; break;
            case CB_IDE_WindowHasEditor: _windowHasEditor = addr; break;
            case CB_IDE_PlugInSetting: _plugInSetting = addr; break;
            case CB_IDE_GetConnectionInfoEx: _getConnectionInfoEx = addr; break;
            case CB_IDE_GetWindowConnection: _getWindowConnection = addr; break;
        }
    }

    // ---------------------------------------------------------------- utilidades de cadenas

    private static Encoding AnsiEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    private static string S(byte* p)
    {
        if (p == null) return "";
        int n = 0;
        while (p[n] != 0) n++;
        return Enc.GetString(p, n).TrimStart('\uFEFF');
    }

    private static byte[] H(string s)
    {
        byte[] b = Enc.GetBytes(s ?? "");
        Array.Resize(ref b, b.Length + 1); // terminador NUL
        return b;
    }

    // ---------------------------------------------------------------- funciones

    /// <summary>Versión del IDE (por ejemplo 1600 para la 16.0). 0 si aún no se conoce.</summary>
    public static int Version => _sysVersion == 0 ? 0 : ((delegate* unmanaged[Cdecl]<int>)_sysVersion)();

    /// <summary>Pide al IDE que pase las cadenas en UTF-8 (setting CHARMODE). Devuelve true si lo acepta.</summary>
    public static bool TrySetUtf8()
    {
        if (_plugInSetting == 0) return false;
        fixed (byte* name = Encoding.ASCII.GetBytes("CHARMODE\0"))
        fixed (byte* value = Encoding.ASCII.GetBytes("UTF8\0"))
        {
            return ((delegate* unmanaged[Cdecl]<int, byte*, byte*, int>)_plugInSetting)(PluginId, name, value) != 0;
        }
    }

    public static bool Connected => _connected != 0 && ((delegate* unmanaged[Cdecl]<int>)_connected)() != 0;

    /// <summary>Usuario y base de datos de la ventana activa (nunca se lee la contraseña).</summary>
    public static (string User, string Database) Connection()
    {
        byte* u = null, p = null, d = null, a = null;

        if (_getWindowConnection != 0 && _getConnectionInfoEx != 0)
        {
            int ix = ((delegate* unmanaged[Cdecl]<int>)_getWindowConnection)();
            if (ix >= 0 &&
                ((delegate* unmanaged[Cdecl]<int, byte**, byte**, byte**, byte**, int>)_getConnectionInfoEx)(ix, &u, &p, &d, &a) != 0)
            {
                string user = S(u);
                if (user.Length > 0) return (user, S(d));
            }
        }

        if (_getConnectionInfo != 0)
        {
            u = p = d = null;
            ((delegate* unmanaged[Cdecl]<byte**, byte**, byte**, void>)_getConnectionInfo)(&u, &p, &d);
            return (S(u), S(d));
        }

        return ("", "");
    }

    /// <summary>1 = SQL, 2 = Test, 3 = Programa, 4 = Comandos, 5 = Plan, 6 = Informe, 0 = otra.</summary>
    public static int WindowType => _getWindowType == 0 ? 0 : ((delegate* unmanaged[Cdecl]<int>)_getWindowType)();

    public static nint MainWindow => _getWindowHandle == 0 ? 0 : ((delegate* unmanaged[Cdecl]<int>)_getWindowHandle)();

    /// <summary>Identificador de la ventana hija activa (se usa como clave para vincularla con su fichero).</summary>
    public static int ChildWindow => _getChildHandle == 0 ? 0 : ((delegate* unmanaged[Cdecl]<int>)_getChildHandle)();

    public static string Filename => _filename == 0 ? "" : S(((delegate* unmanaged[Cdecl]<byte*>)_filename)());

    public static bool HasCodeEditor =>
        _windowHasEditor == 0 || ((delegate* unmanaged[Cdecl]<int, int>)_windowHasEditor)(1) != 0;

    public static string GetText() => _getText == 0 ? "" : S(((delegate* unmanaged[Cdecl]<byte*>)_getText)());

    public static string GetSelectedText() =>
        _getSelectedText == 0 ? "" : S(((delegate* unmanaged[Cdecl]<byte*>)_getSelectedText)());

    public static bool SetText(string text)
    {
        if (_setText == 0) return false;
        fixed (byte* p = H(text))
        {
            return ((delegate* unmanaged[Cdecl]<byte*, int>)_setText)(p) != 0;
        }
    }

    /// <summary>
    /// Pone el texto en el editor y lo relee para comprobar que ha llegado igual. Si no (el IDE ha interpretado los
    /// bytes con otra codificación y las tildes salen como «Ã³»), lo repite en UTF-8 con BOM y después en
    /// Windows-1252. Devuelve si el IDE ha aceptado el texto y la codificación con la que ha llegado bien, o null si
    /// con ninguna (entonces deja la primera, la de siempre).
    /// </summary>
    public static (bool Set, string? Encoding) SetTextVerified(string text, Func<string, string> normalize)
    {
        if (_setText == 0) return (false, null);
        string expected = normalize(text);
        byte[] utf8Bom = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text), 0];
        byte[] ansi = [.. Cp1252.GetBytes(text), 0];
        (string Name, byte[] Bytes)[] attempts = Enc.CodePage == Cp1252.CodePage
            ? new[] { (Enc.WebName, H(text)) }
            : new[] { (Enc.WebName, H(text)), ("utf-8 con BOM", utf8Bom), (Cp1252.WebName, ansi) };

        foreach ((string name, byte[] bytes) in attempts)
        {
            if (!SetBytes(bytes)) return (false, null);
            if (normalize(GetText()) == expected) return (true, name);
        }
        SetBytes(attempts[0].Bytes);
        return (true, null);
    }

    private static bool SetBytes(byte[] bytes)
    {
        fixed (byte* p = bytes)
        {
            return ((delegate* unmanaged[Cdecl]<byte*, int>)_setText)(p) != 0;
        }
    }

    public static bool Status(string text)
    {
        if (_setStatusMessage == 0) return false;
        fixed (byte* p = H(text))
        {
            return ((delegate* unmanaged[Cdecl]<byte*, int>)_setStatusMessage)(p) != 0;
        }
    }

    /// <summary>Objeto abierto en una ventana de programa (tipo, propietario, nombre). Null si no aplica.</summary>
    public static (string Type, string Owner, string Name)? WindowObject()
    {
        if (_getWindowObject == 0) return null;
        byte* t = null, o = null, n = null, s = null;
        if (((delegate* unmanaged[Cdecl]<byte**, byte**, byte**, byte**, int>)_getWindowObject)(&t, &o, &n, &s) == 0)
            return null;
        string name = S(n);
        return name.Length == 0 ? null : (S(t), S(o), name);
    }

    /// <summary>Nombre de la pestaña activa de la ventana (por ejemplo especificación / cuerpo).</summary>
    public static string CurrentTab()
    {
        if (_tabIndex == 0 || _tabInfo == 0) return "";
        int ix = ((delegate* unmanaged[Cdecl]<int, int>)_tabIndex)(-1);
        return ix < 0 ? "" : S(((delegate* unmanaged[Cdecl]<int, byte*>)_tabInfo)(ix));
    }

    public static int CursorLine => _getCursorY == 0 ? 0 : ((delegate* unmanaged[Cdecl]<int>)_getCursorY)();

    public static void SetCursor(int x, int y)
    {
        if (_setCursor != 0) ((delegate* unmanaged[Cdecl]<int, int, void>)_setCursor)(x, y);
    }

    /// <summary>Añade una opción al menú contextual (clic derecho) de un tipo de ventana.</summary>
    public static void AddPopup(int index, string name, string objectType)
    {
        if (_createPopupItem == 0) return;
        fixed (byte* n = H(name))
        fixed (byte* t = H(objectType))
        {
            ((delegate* unmanaged[Cdecl]<int, int, byte*, byte*, void*>)_createPopupItem)(PluginId, index, n, t);
        }
    }
}
