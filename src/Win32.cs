using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ClaudeCodePlsql;

/// <summary>Las pocas funciones de Windows que necesita el plug-in (mensajes, temporizador y portapapeles).</summary>
internal static unsafe partial class Win32
{
    public const uint MB_OK = 0x0, MB_YESNOCANCEL = 0x3, MB_YESNO = 0x4;
    public const uint MB_ICONERROR = 0x10, MB_ICONQUESTION = 0x20, MB_ICONWARNING = 0x30, MB_ICONINFORMATION = 0x40;
    public const int IDCANCEL = 2, IDYES = 6, IDNO = 7;

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);

    [LibraryImport("user32.dll")]
    public static partial nuint SetTimer(nint hWnd, nuint id, uint elapse, nint timerProc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hWnd, nuint id);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, uint* processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint format, nint hMem);

    [LibraryImport("user32.dll")]
    private static partial nint GetClipboardData(uint format);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint hMem);

    /// <summary>
    /// Mensaje con botones. Se muestra desde un hilo propio y el del IDE sigue atendiendo sus mensajes mientras tanto:
    /// PL/SQL Developer engancha (con un hook de su hilo) los cuadros de diálogo que se abren en él para pintarlos con
    /// su tema, y a veces el texto del mensaje se quedaba en blanco (solo el icono y los botones). Desde otro hilo
    /// el cuadro es el de Windows tal cual. La ventana del IDE queda deshabilitada, como con un mensaje normal.
    /// </summary>
    public static int Message(string text, uint flags)
    {
        nint owner = Ide.MainWindow;
        int result = IDCANCEL;
        using var done = new ManualResetEvent(false);
        var thread = new Thread(() =>
        {
            try
            {
                result = MessageBoxW(owner, text, "Claude Code", flags | MB_SETFOREGROUND);
            }
            catch
            {
                // nunca dejar escapar una excepción del hilo
            }
            finally
            {
                try { done.Set(); } catch { } // si el IDE se está cerrando, el evento puede estar ya liberado
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        PumpUntil(done.SafeWaitHandle.DangerousGetHandle());
        return result;
    }

    private const uint MB_SETFOREGROUND = 0x10000;
    private const uint QS_ALLINPUT = 0x04FF;
    private const uint PM_REMOVE = 0x0001;
    private const uint WAIT_OBJECT_0 = 0;
    private const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public int x, y; }

    [LibraryImport("user32.dll")]
    private static partial uint MsgWaitForMultipleObjects(uint count, nint* handles, int waitAll, uint milliseconds, uint wakeMask);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(MSG* msg, nint hWnd, uint min, uint max, uint remove);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(MSG* msg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static partial nint DispatchMessage(MSG* msg);

    [LibraryImport("user32.dll")]
    private static partial void PostQuitMessage(int code);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnableWindow(nint hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    /// <summary>
    /// Espera a que se señale el objeto sin bloquear el hilo del IDE: mientras tanto le pasa sus mensajes, como hace un
    /// cuadro de diálogo modal (así PL/SQL Developer se repinta y Windows no lo da por colgado).
    /// </summary>
    /// <returns>True si el objeto se ha señalado; false si se acaba el tiempo o hay un error.</returns>
    public static bool PumpUntil(nint handle, int timeoutMs = -1)
    {
        MSG msg;
        long until = timeoutMs < 0 ? long.MaxValue : Environment.TickCount64 + timeoutMs;
        while (true)
        {
            long left = until == long.MaxValue ? 0xFFFFFFFF : Math.Max(0, until - Environment.TickCount64);
            uint r = MsgWaitForMultipleObjects(1, &handle, 0, (uint)left, QS_ALLINPUT);
            if (r == WAIT_OBJECT_0) return true;
            if (r != WAIT_OBJECT_0 + 1) return false; // se acabó el tiempo o error: no esperar más
            while (PeekMessage(&msg, 0, 0, 0, PM_REMOVE))
            {
                if (msg.message == WM_QUIT)
                {
                    PostQuitMessage((int)msg.wParam); // que lo vea el bucle principal del IDE
                    return false;
                }
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
    }

    /// <summary>True si la ventana en primer plano pertenece a PL/SQL Developer (este proceso).</summary>
    public static bool IsOurProcessInForeground()
    {
        nint fg = GetForegroundWindow();
        if (fg == 0) return false;
        uint pid = 0;
        GetWindowThreadProcessId(fg, &pid);
        return pid == (uint)Environment.ProcessId;
    }

    public static bool SetClipboardText(string text)
    {
        bool opened = false;
        for (int i = 0; i < 10 && !(opened = OpenClipboard(Ide.MainWindow)); i++)
            Thread.Sleep(30); // otro programa puede tener el portapapeles abierto un instante
        if (!opened) return false;

        try
        {
            EmptyClipboard();
            nuint bytes = (nuint)((text.Length + 1) * sizeof(char));
            nint h = GlobalAlloc(GMEM_MOVEABLE, bytes);
            if (h == 0) return false;

            char* dst = (char*)GlobalLock(h);
            if (dst == null) { GlobalFree(h); return false; }
            fixed (char* src = text)
            {
                Buffer.MemoryCopy(src, dst, (long)bytes, text.Length * sizeof(char));
            }
            dst[text.Length] = '\0';
            GlobalUnlock(h);

            if (SetClipboardData(CF_UNICODETEXT, h) == 0)
            {
                GlobalFree(h);
                return false;
            }
            return true; // a partir de aquí la memoria es del sistema
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Texto del portapapeles, o "" si no hay texto o no se puede abrir.</summary>
    public static string GetClipboardText()
    {
        bool opened = false;
        for (int i = 0; i < 10 && !(opened = OpenClipboard(Ide.MainWindow)); i++)
            Thread.Sleep(30);
        if (!opened) return "";

        try
        {
            nint h = GetClipboardData(CF_UNICODETEXT);
            if (h == 0) return "";
            char* src = (char*)GlobalLock(h);
            if (src == null) return "";
            try
            {
                return new string(src); // el texto del portapapeles termina en '\0'
            }
            finally
            {
                GlobalUnlock(h);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    // ---------------------------------------------------------------- ventana de Claude junto a PL/SQL Developer

    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(nint proc, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLengthW(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextW(nint hWnd, char* text, int max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsZoomed(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hWnd, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, MONITORINFO* info);

    private static string _findText = "";
    private static nint _found;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int EnumProc(nint hWnd, nint lParam)
    {
        try
        {
            if (!IsWindowVisible(hWnd)) return 1;
            uint pid = 0;
            GetWindowThreadProcessId(hWnd, &pid);
            if (pid == (uint)Environment.ProcessId) return 1; // las ventanas de PL/SQL Developer no cuentan
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0 || len > 1000) return 1;
            char* buf = stackalloc char[len + 1];
            int n = GetWindowTextW(hWnd, buf, len + 1);
            if (new string(buf, 0, n).Contains(_findText, StringComparison.OrdinalIgnoreCase))
            {
                _found = hWnd;
                return 0;
            }
        }
        catch
        {
            // nunca dejar escapar una excepción a Windows
        }
        return 1;
    }

    private static List<string>? _titles;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int EnumTitlesProc(nint hWnd, nint lParam)
    {
        try
        {
            if (!IsWindowVisible(hWnd)) return 1;
            uint pid = 0;
            GetWindowThreadProcessId(hWnd, &pid);
            if (pid == (uint)Environment.ProcessId) return 1;
            int len = GetWindowTextLengthW(hWnd);
            if (len <= 0 || len > 1000) return 1;
            char* buf = stackalloc char[len + 1];
            int n = GetWindowTextW(hWnd, buf, len + 1);
            string title = new(buf, 0, n);
            if (title.Contains(_findText, StringComparison.OrdinalIgnoreCase)) _titles?.Add(title);
        }
        catch
        {
            // nunca dejar escapar una excepción a Windows
        }
        return 1;
    }

    /// <summary>Títulos de las ventanas visibles de otros programas que contienen el texto.</summary>
    public static List<string> FindTopWindowTitles(string titlePart)
    {
        _findText = titlePart;
        _titles = new List<string>();
        EnumWindows((nint)(delegate* unmanaged[Stdcall]<nint, nint, int>)&EnumTitlesProc, 0);
        var list = _titles;
        _titles = null;
        return list;
    }

    /// <summary>Ventana visible de otro programa cuyo título contiene el texto, o 0.</summary>
    public static nint FindTopWindow(string titlePart)
    {
        _findText = titlePart;
        _found = 0;
        EnumWindows((nint)(delegate* unmanaged[Stdcall]<nint, nint, int>)&EnumProc, 0);
        return _found;
    }

    /// <summary>Restaura la ventana si está minimizada y la pone delante.</summary>
    public static bool BringToFront(nint hWnd)
    {
        if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
        return SetForegroundWindow(hWnd);
    }

    /// <summary>
    /// Coloca la ventana de Claude como panel a la derecha de la pantalla de PL/SQL Developer (el porcentaje del
    /// ancho indicado) y PL/SQL Developer en el resto.
    /// </summary>
    public static void PlaceSide(nint claude, int percent)
    {
        nint ide = Ide.MainWindow;
        if (claude == 0 || ide == 0) return;

        MONITORINFO mi = default;
        mi.cbSize = sizeof(MONITORINFO);
        if (!GetMonitorInfo(MonitorFromWindow(ide, MONITOR_DEFAULTTONEAREST), &mi)) return;

        RECT w = mi.rcWork;
        int width = w.Right - w.Left, height = w.Bottom - w.Top;
        int side = width * Math.Clamp(percent, 20, 60) / 100;

        if (IsZoomed(ide)) ShowWindow(ide, SW_RESTORE);
        if (IsZoomed(claude) || IsIconic(claude)) ShowWindow(claude, SW_RESTORE);
        SetWindowPos(ide, 0, w.Left, w.Top, width - side, height, SWP_NOZORDER | SWP_NOACTIVATE);
        SetWindowPos(claude, 0, w.Right - side, w.Top, side, height, SWP_NOZORDER);
    }
}
