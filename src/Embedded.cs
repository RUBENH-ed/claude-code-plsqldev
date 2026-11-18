using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ClaudeCodePlsql;

/// <summary>
/// Panel de Claude dentro de PL/SQL Developer (prototipo). El interface de plug-ins no permite paneles acoplados, así
/// que se hace con Windows: una ventana propia, hija de la ventana principal del IDE, a la derecha de la zona de
/// editores (la zona MDI), que se estrecha para dejarle sitio. Dentro va el panel de sesiones, en WebView2, el
/// componente de Edge que el propio PL/SQL Developer trae (WebView2Loader.dll, junto a plsqldev.exe).
///
/// - La zona MDI se subclasifica: cada vez que el IDE la coloca (al cambiar de tamaño, al mover sus paneles...), se le
///   quita el ancho del panel y el panel se pone en ese hueco. Así no hace falta saber cómo reparte el IDE su ventana.
/// - El borde izquierdo del panel (Splitter px) se arrastra para cambiar el ancho.
/// - Nada de esto está en la documentación de plug-ins: si una versión de PL/SQL Developer cambia cómo coloca sus
///   zonas, puede dejar de funcionar. Todo se deshace al desactivar el plug-in.
/// </summary>
internal static unsafe partial class Embedded
{
    private const int Splitter = 5, MinWidth = 260, MinEditors = 300;
    private const string ClassName = "ClaudeCodePlsqlPanelIntegrado";

    private static nint _host, _mdi, _parent, _hook, _env, _controller, _webview;
    private static nint _envHandler, _controllerHandler;
    private static bool _created, _visible, _dragging, _navigated;
    private static int _width;             // ancho pedido del panel (px)
    private static int _applied;           // lo que se le está quitando ahora a la zona MDI
    private static int _fx, _fy, _fw, _fh; // rectángulo completo de la zona MDI (sin quitarle el panel)
    private static volatile string? _url;  // enlace de entrada al panel (abrir.js --imprimir), de un hilo aparte
    private static volatile string? _urlError;

    public static bool Visible => _visible;

    /// <summary>Botón de la cinta: enseña u oculta el panel.</summary>
    public static void Toggle()
    {
        if (!_created && !Create()) return;
        if (_visible) Hide(); else Show();
    }

    /// <summary>Desde el temporizador: navega en cuanto están listos WebView2 y el enlace de entrada.</summary>
    public static void Tick()
    {
        if (!_created || _navigated) return;
        if (_urlError != null)
        {
            string e = _urlError;
            _urlError = null;
            _navigated = true;
            Win32.Message($"No he podido abrir el panel de sesiones dentro de PL/SQL Developer:\n\n{e}", Win32.MB_ICONWARNING);
            return;
        }
        if (_webview == 0 || _url == null) return;
        _navigated = true;
        string url = _url;
        fixed (char* u = url) Check(((delegate* unmanaged[Stdcall]<nint, char*, int>)Slot(_webview, 5))(_webview, u), "Navigate"); // ICoreWebView2::Navigate
    }

    /// <summary>Al desactivar el plug-in (o cerrar el IDE): se deja todo como estaba.</summary>
    public static void Destroy()
    {
        try
        {
            if (_visible) Hide();
            if (_controller != 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, int>)Slot(_controller, 24))(_controller); // ICoreWebView2Controller::Close
                Release(_controller);
                _controller = 0;
            }
            if (_webview != 0) { Release(_webview); _webview = 0; }
            if (_env != 0) { Release(_env); _env = 0; }
            if (_mdi != 0) RemoveWindowSubclass(_mdi, (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint>)&MdiProc, 1);
            if (_hook != 0) UnhookWindowsHookEx(_hook);
            if (_host != 0 && IsWindow(_host)) DestroyWindow(_host);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        _host = _mdi = _parent = _hook = 0;
        _created = _visible = _navigated = false;
        _url = null;
    }

    // =========================================================================================== ventana y sitio

    private static bool Create()
    {
        var panel = Panel.Available();
        if (panel == null)
        {
            Win32.Message($"El panel de sesiones no está disponible: {Panel.LastProblem}", Win32.MB_ICONWARNING);
            return false;
        }

        nint main = Ide.MainWindow;
        _mdi = main == 0 ? 0 : FindChildByClass(main, "MDIClient");
        if (_mdi == 0)
        {
            Win32.Message("No encuentro la zona de editores de PL/SQL Developer: el panel integrado no funciona con esta versión.",
                Win32.MB_ICONWARNING);
            return false;
        }
        _parent = GetParent(_mdi);

        nint instance = GetModuleHandleW(0);
        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint>)&HostProc,
                hInstance = instance,
                hCursor = LoadCursorW(0, IDC_ARROW),
                hbrBackground = COLOR_3DSHADOW + 1, // el borde que se arrastra se ve con el color de las sombras del tema
                lpszClassName = (nint)cls,
            };
            RegisterClassExW(&wc); // si ya está registrada (segunda vez) falla sin más
            _host = CreateWindowExW(0, cls, null, WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS, 0, 0, 10, 10,
                _parent, 0, instance, 0);
        }
        if (_host == 0)
        {
            Win32.Message($"No he podido crear la ventana del panel (error {Marshal.GetLastPInvokeError()}).", Win32.MB_ICONWARNING);
            return false;
        }

        SetWindowSubclass(_mdi, (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint>)&MdiProc, 1, 0);
        _hook = SetWindowsHookExW(WH_GETMESSAGE, (nint)(delegate* unmanaged[Stdcall]<int, nuint, nint, nint>)&GetMsgProc, 0,
            GetCurrentThreadId());
        _created = true;

        // El enlace de entrada (de un solo uso) se pide en otro hilo: arrancar el servidor puede tardar unos segundos
        var p = panel.Value;
        _url = null;
        _urlError = null;
        _navigated = false;
        Task.Run(() =>
        {
            string? url = Panel.EntryUrl(p, out string error);
            if (url != null) _url = url; else _urlError = error;
        });

        if (StartWebView()) return true;
        Destroy();
        return false;
    }

    private static void Show()
    {
        RECT r = MdiRectInParent();
        if (_width == 0) _width = Math.Max(MinWidth, (r.Right - r.Left) * Config.EmbeddedPercent / 100);
        _visible = true;
        Relayout(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        if (_controller != 0) ((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(_controller, 4))(_controller, 1); // put_IsVisible(TRUE)
    }

    private static void Hide()
    {
        _visible = false;
        ShowWindow(_host, SW_HIDE);
        if (_controller != 0) ((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(_controller, 4))(_controller, 0);
        // La zona MDI recupera su ancho: el gancho ya no le quita nada
        SetWindowPos(_mdi, 0, _fx, _fy, _fw, _fh, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    /// <summary>Recoloca la zona MDI con su rectángulo completo; el gancho le quita el panel y coloca el panel.</summary>
    private static void Relayout(int x, int y, int w, int h)
    {
        _fx = x; _fy = y; _fw = w; _fh = h;
        SetWindowPos(_mdi, 0, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static RECT MdiRectInParent()
    {
        RECT r;
        GetWindowRect(_mdi, &r);
        MapWindowPoints(0, _parent, (POINT*)&r, 2);
        r.Right += _applied; // lo que le quita ahora el panel no es suyo
        return r;
    }

    /// <summary>Gancho de la zona MDI: cada vez que el IDE la coloca, le quita el ancho del panel y pone ahí el panel.</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint MdiProc(nint hwnd, uint msg, nuint wParam, nint lParam, nuint id, nuint data)
    {
        try
        {
            if (msg == WM_WINDOWPOSCHANGING && lParam != 0)
            {
                var wp = (WINDOWPOS*)lParam;
                if ((wp->flags & (SWP_NOSIZE | SWP_NOMOVE)) != (SWP_NOSIZE | SWP_NOMOVE))
                {
                    RECT cur = MdiRectInParent();
                    int x = (wp->flags & SWP_NOMOVE) != 0 ? cur.Left : wp->x;
                    int y = (wp->flags & SWP_NOMOVE) != 0 ? cur.Top : wp->y;
                    int w = (wp->flags & SWP_NOSIZE) != 0 ? cur.Right - cur.Left : wp->cx;
                    int h = (wp->flags & SWP_NOSIZE) != 0 ? cur.Bottom - cur.Top : wp->cy;
                    _fx = x; _fy = y; _fw = w; _fh = h;

                    int panel = _visible ? Math.Clamp(_width, MinWidth, Math.Max(MinWidth, w - MinEditors)) : 0;
                    if (panel > 0 && w - panel < MinEditors) panel = 0; // no cabe: se esconde hasta que haya sitio
                    _applied = panel;
                    wp->x = x; wp->y = y; wp->cx = w - panel; wp->cy = h;
                    wp->flags &= ~(SWP_NOSIZE | SWP_NOMOVE);
                    if (panel > 0)
                        SetWindowPos(_host, 0, x + w - panel, y, panel, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    else if (_host != 0)
                        ShowWindow(_host, SW_HIDE);
                }
            }
            else if (msg == WM_NCDESTROY)
            {
                RemoveWindowSubclass(hwnd, (nint)(delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint>)&MdiProc, 1);
                _mdi = 0;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>Ventana del panel: el borde izquierdo se arrastra; el resto lo ocupa WebView2.</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint HostProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_SIZE:
                    ResizeWebView();
                    break;
                case WM_MOVE:
                    if (_controller != 0) ((delegate* unmanaged[Stdcall]<nint, int>)Slot(_controller, 23))(_controller); // NotifyParentWindowPositionChanged
                    break;
                case WM_SETCURSOR:
                    if ((short)(lParam & 0xFFFF) == HTCLIENT)
                    {
                        POINT p;
                        GetCursorPos(&p);
                        MapWindowPoints(0, hwnd, &p, 1);
                        if (p.X < Splitter) { SetCursor(LoadCursorW(0, IDC_SIZEWE)); return 1; }
                    }
                    break;
                case WM_LBUTTONDOWN:
                    if ((short)(lParam & 0xFFFF) < Splitter) { _dragging = true; SetCapture(hwnd); return 0; }
                    break;
                case WM_MOUSEMOVE:
                    if (_dragging)
                    {
                        POINT p;
                        GetCursorPos(&p);
                        MapWindowPoints(0, _parent, &p, 1);
                        _width = Math.Max(MinWidth, _fx + _fw - p.X);
                        Relayout(_fx, _fy, _fw, _fh);
                        return 0;
                    }
                    break;
                case WM_LBUTTONUP:
                    if (_dragging) { _dragging = false; ReleaseCapture(); return 0; }
                    break;
                case WM_CAPTURECHANGED:
                    _dragging = false;
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Las teclas que vayan a una ventana del panel se entregan directamente, sin pasar por el bucle de mensajes del IDE,
    /// que las tomaría como atajos suyos (F8, Ctrl+C...). Con WebView2 las ventanas que reciben el teclado suelen ser
    /// de otro proceso y no pasan por aquí; esto cubre las que sean del IDE.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint GetMsgProc(int code, nuint wParam, nint lParam)
    {
        try
        {
            if (code >= 0 && wParam == PM_REMOVE && lParam != 0 && _host != 0 && _visible)
            {
                var m = (MSG*)lParam;
                if (m->message >= WM_KEYFIRST && m->message <= WM_KEYLAST && (m->hwnd == _host || IsChild(_host, m->hwnd)))
                {
                    TranslateMessage(m);
                    DispatchMessageW(m);
                    m->message = WM_NULL;
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return CallNextHookEx(0, code, wParam, lParam);
    }

    // =========================================================================================== WebView2

    private static bool StartWebView()
    {
        string exeDir = AppContext.BaseDirectory;
        try
        {
            exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? exeDir;
        }
        catch
        {
            // se usa la carpeta de la DLL
        }
        nint loader = LoadLibraryW(Path.Combine(exeDir, "WebView2Loader.dll"));
        if (loader == 0) loader = LoadLibraryW("WebView2Loader.dll");
        nint create = loader == 0 ? 0 : GetProcAddress(loader, "CreateCoreWebView2EnvironmentWithOptions");
        if (create == 0)
        {
            Win32.Message("No encuentro WebView2Loader.dll junto a plsqldev.exe: esta versión de PL/SQL Developer no trae WebView2.",
                Win32.MB_ICONWARNING);
            return false;
        }

        _envHandler = NewHandler((nint)(delegate* unmanaged[Stdcall]<nint, int, nint, int>)&OnEnvironment);
        _controllerHandler = NewHandler((nint)(delegate* unmanaged[Stdcall]<nint, int, nint, int>)&OnController);
        // Datos propios (caché, cookie del panel) en %APPDATA%\ClaudeCodePlsql, no junto a plsqldev.exe
        string data = Path.Combine(Config.Dir, "webview2");
        Directory.CreateDirectory(data);
        int hr;
        fixed (char* d = data)
            hr = ((delegate* unmanaged[Stdcall]<char*, char*, nint, nint, int>)create)(null, d, 0, _envHandler);
        if (hr < 0)
        {
            Win32.Message($"No he podido arrancar WebView2 (0x{hr:X8}). ¿Está instalado el runtime de WebView2 / Edge?", Win32.MB_ICONWARNING);
            return false;
        }
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnEnvironment(nint self, int hr, nint env)
    {
        try
        {
            if (hr < 0 || env == 0) { Log.Info($"Panel integrado: WebView2 no arranca (0x{hr:X8})"); return 0; }
            if (_host == 0) return 0; // se ha desactivado mientras tanto
            AddRef(env);
            _env = env;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)Slot(env, 3))(env, _host, _controllerHandler), "CreateCoreWebView2Controller");
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int OnController(nint self, int hr, nint controller)
    {
        try
        {
            if (hr < 0 || controller == 0) { Log.Info($"Panel integrado: WebView2 no crea la vista (0x{hr:X8})"); return 0; }
            if (_host == 0) return 0;
            AddRef(controller);
            _controller = controller;

            nint webview;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(controller, 25))(controller, &webview), "get_CoreWebView2");
            _webview = webview;

            nint settings;
            if (((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(webview, 3))(webview, &settings) >= 0 && settings != 0) // get_Settings
            {
                ((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(settings, 10))(settings, 0); // put_IsStatusBarEnabled(FALSE)
                Release(settings);
            }

            ResizeWebView();
            ((delegate* unmanaged[Stdcall]<nint, int, int>)Slot(controller, 4))(controller, _visible ? 1 : 0); // put_IsVisible
            Tick();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        return 0;
    }

    private static void ResizeWebView()
    {
        if (_controller == 0 || _host == 0) return;
        RECT r;
        GetClientRect(_host, &r);
        r.Left = Math.Min(Splitter, r.Right);
        ((delegate* unmanaged[Stdcall]<nint, RECT, int>)Slot(_controller, 6))(_controller, r); // put_Bounds
    }

    // Objetos COM mínimos para los avisos de WebView2: IUnknown + Invoke. Viven mientras viva el IDE.
    private static nint NewHandler(nint invoke)
    {
        var vtbl = (nint*)NativeMemory.Alloc((nuint)(4 * sizeof(nint)));
        vtbl[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&QueryInterface;
        vtbl[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRefHandler;
        vtbl[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRefHandler;
        vtbl[3] = invoke;
        var obj = (nint*)NativeMemory.Alloc((nuint)sizeof(nint));
        obj[0] = (nint)vtbl;
        return (nint)obj;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(nint self, Guid* iid, nint* ppv)
    {
        *ppv = self; // solo se le pide su propio interface o IUnknown
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint AddRefHandler(nint self) => 1;

    // Dirección del método número <slot> de la tabla de un objeto COM (0-2 son los de IUnknown)
    private static nint Slot(nint obj, int slot) => (*(nint**)obj)[slot];

    private static void AddRef(nint obj) => ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(obj, 1))(obj);
    private static void Release(nint obj) => ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(obj, 2))(obj);

    private static void Check(int hr, string what)
    {
        if (hr < 0) Log.Info($"Panel integrado: {what} ha fallado (0x{hr:X8})");
    }

    private static nint FindChildByClass(nint parent, string cls)
    {
        _findClass = cls;
        _found = 0;
        EnumChildWindows(parent, (nint)(delegate* unmanaged[Stdcall]<nint, nint, int>)&EnumChild, 0);
        return _found;
    }

    private static string _findClass = "";
    private static nint _found;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int EnumChild(nint hwnd, nint lParam)
    {
        char* buf = stackalloc char[64];
        int n = GetClassNameW(hwnd, buf, 64);
        if (new string(buf, 0, n) == _findClass) { _found = hwnd; return 0; }
        return 1;
    }

    // =========================================================================================== Windows

    private const uint WS_CHILD = 0x40000000, WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    private const int SW_HIDE = 0, WH_GETMESSAGE = 3, HTCLIENT = 1, COLOR_3DSHADOW = 16;
    private const uint WM_NULL = 0, WM_MOVE = 0x3, WM_SIZE = 0x5, WM_SETCURSOR = 0x20, WM_WINDOWPOSCHANGING = 0x46,
        WM_NCDESTROY = 0x82, WM_KEYFIRST = 0x100, WM_KEYLAST = 0x109, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201,
        WM_LBUTTONUP = 0x202, WM_CAPTURECHANGED = 0x215;
    private const nuint PM_REMOVE = 1;
    private static readonly nint IDC_ARROW = 32512, IDC_SIZEWE = 32644;

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS { public nint hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public POINT pt; public uint lPrivate; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
    }

    [LibraryImport("user32.dll")] private static partial ushort RegisterClassExW(WNDCLASSEXW* wc);
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint CreateWindowExW(uint exStyle, char* cls, char* name, uint style, int x, int y, int w, int h,
        nint parent, nint menu, nint instance, nint param);
    [LibraryImport("user32.dll")] private static partial nint DefWindowProcW(nint hwnd, uint msg, nuint wParam, nint lParam);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool DestroyWindow(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsWindow(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsChild(nint parent, nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ShowWindow(nint hwnd, int cmd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetWindowRect(nint hwnd, RECT* r);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetClientRect(nint hwnd, RECT* r);
    [LibraryImport("user32.dll")] private static partial int MapWindowPoints(nint from, nint to, POINT* points, uint count);
    [LibraryImport("user32.dll")] private static partial nint GetParent(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetCursorPos(POINT* p);
    [LibraryImport("user32.dll")] private static partial nint SetCursor(nint cursor);
    [LibraryImport("user32.dll")] private static partial nint LoadCursorW(nint instance, nint name);
    [LibraryImport("user32.dll")] private static partial nint SetCapture(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ReleaseCapture();
    [LibraryImport("user32.dll")] private static partial int GetClassNameW(nint hwnd, char* name, int max);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool EnumChildWindows(nint parent, nint proc, nint lParam);
    [LibraryImport("user32.dll")] private static partial nint SetWindowsHookExW(int id, nint proc, nint module, uint threadId);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool UnhookWindowsHookEx(nint hook);
    [LibraryImport("user32.dll")] private static partial nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool TranslateMessage(MSG* msg);
    [LibraryImport("user32.dll")] private static partial nint DispatchMessageW(MSG* msg);
    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowSubclass(nint hwnd, nint proc, nuint id, nuint data);
    [LibraryImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveWindowSubclass(nint hwnd, nint proc, nuint id);
    [LibraryImport("comctl32.dll")] private static partial nint DefSubclassProc(nint hwnd, uint msg, nuint wParam, nint lParam);
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)] private static partial nint LoadLibraryW(string file);
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf8)] private static partial nint GetProcAddress(nint module, string name);
    [LibraryImport("kernel32.dll")] private static partial nint GetModuleHandleW(nint name);
    [LibraryImport("kernel32.dll")] private static partial uint GetCurrentThreadId();
}
