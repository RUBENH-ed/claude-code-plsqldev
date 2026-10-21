using System;
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

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint hMem);

    public static int Message(string text, uint flags) =>
        MessageBoxW(Ide.MainWindow, text, "Claude Code", flags);

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
}
