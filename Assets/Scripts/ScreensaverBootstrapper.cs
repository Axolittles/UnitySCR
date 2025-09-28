#if UNITY_STANDALONE_WIN
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScreensaverBootstrap : MonoBehaviour
{
    // ---------- Win32 interop ----------
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern bool ShowCursor(bool bShow);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll", SetLastError = true)] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", SetLastError = true)] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    // GWL indexes
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;

    // Base styles
    const int WS_CHILD = 0x40000000;
    const int WS_POPUP = unchecked((int)0x80000000);
    const int WS_VISIBLE = 0x10000000;

    // Windowed chrome (clear these)
    const int WS_OVERLAPPED = 0x00000000;
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    const int WS_MINIMIZEBOX = 0x00020000;
    const int WS_MAXIMIZEBOX = 0x00010000;
    const int WS_SYSMENU = 0x00080000;
    const int WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    // Extended styles
    const int WS_EX_DLGMODALFRAME = 0x00000001;
    const int WS_EX_WINDOWEDGE = 0x00000100;
    const int WS_EX_CLIENTEDGE = 0x00000200;
    const int WS_EX_APPWINDOW = 0x00040000;
    const int WS_EX_TOOLWINDOW = 0x00000080; // hide from Alt-Tab

    // SetWindowPos flags
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SWP_SHOWWINDOW = 0x0040;

    // Virtual screen metrics (for spanning)
    const int SM_XVIRTUALSCREEN = 76;
    const int SM_YVIRTUALSCREEN = 77;
    const int SM_CXVIRTUALSCREEN = 78;
    const int SM_CYVIRTUALSCREEN = 79;

    // DWM attributes
    const int DWMWA_NCRENDERING_ENABLED = 1;
    const int DWMWA_NCRENDERING_POLICY = 2; // 0=UseWindowStyle,1=Disabled,2=Enabled
    const int DWMNCRP_DISABLED = 1;

    // Z-order targets
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    enum SaverMode { Fullscreen, Preview, Config }

    // ---------- State ----------
    SaverMode mode = SaverMode.Fullscreen;
    IntPtr previewParent = IntPtr.Zero;
    IntPtr unityHwnd = IntPtr.Zero;

    // expected rect for enforcement (windowed borderless)
    struct IRect { public int x, y, w, h; }
    IRect expectedRect;

    Vector3 lastMouse;
    float idleGrace = 2f;
    float idleTimer = 0f;
    bool cursorHidden = false;

    // stabilization loop
    float enforceUntilTime = 0f;   // enforce every frame until this time
    float nextPeriodicEnforce = 0f;

    // Scene indices
    [SerializeField] int saverSceneBuildIndex = 1;
    [SerializeField] int settingsSceneBuildIndex = 2;

    // Span across all monitors when running /s
    [SerializeField] bool spanAllDisplays = true;

    // ---------- Lifecycle ----------
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        ParseArgs();

        // Always start windowed; we’ll convert to true borderless popup ourselves
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
    }

    void Start()
    {
        if (mode == SaverMode.Config)
        {
            Screen.SetResolution(1024, 768, FullScreenMode.Windowed);
            if (!SceneManager.GetSceneByBuildIndex(settingsSceneBuildIndex).isLoaded)
                SceneManager.LoadScene(settingsSceneBuildIndex, LoadSceneMode.Additive);
            return;
        }

        if (mode == SaverMode.Preview)
        {
            if (previewParent == IntPtr.Zero) { Application.Quit(); return; }
            StartCoroutine(SetupPreview());
            return;
        }

        // Normal saver scene
        if (!SceneManager.GetSceneByBuildIndex(saverSceneBuildIndex).isLoaded)
            SceneManager.LoadScene(saverSceneBuildIndex, LoadSceneMode.Additive);

        // Compute target rect (virtual desktop or primary)
        if (spanAllDisplays)
        {
            expectedRect.x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            expectedRect.y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            expectedRect.w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            expectedRect.h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        }
        else
        {
            expectedRect.x = 0;
            expectedRect.y = 0;
            expectedRect.w = Display.main.systemWidth;
            expectedRect.h = Display.main.systemHeight;
        }

        // Match Unity render size (windowed) to target immediately
        Screen.SetResolution(expectedRect.w, expectedRect.h, FullScreenMode.Windowed);

        // Now enter borderless & enforce for ~2 seconds every frame, then periodically
        StartCoroutine(EnterAndStabilizeBorderless());
    }

    System.Collections.IEnumerator EnterAndStabilizeBorderless()
    {
        // Wait one frame for HWND
        yield return null;

        unityHwnd = FindOwnWindow();
        if (unityHwnd == IntPtr.Zero || !IsWindow(unityHwnd))
            yield break;

        // First application of style + position
        ApplyBorderlessTopmost();
        PositionToExpected();

        // Enforce hard for 2 seconds (handles Unity/OS flips on startup)
        enforceUntilTime = Time.unscaledTime + 2.0f;
        nextPeriodicEnforce = Time.unscaledTime + 1.0f;
    }

    void LateUpdate()
    {
        if (mode != SaverMode.Fullscreen || unityHwnd == IntPtr.Zero || !IsWindow(unityHwnd))
            return;

        // During stabilization window: enforce every frame
        if (Time.unscaledTime <= enforceUntilTime)
        {
            ApplyBorderlessTopmost();
            PositionToExpected();
            return;
        }

        // Periodic re-assert (once per second) to fight DPI/optimizations/Focus flips
        if (Time.unscaledTime >= nextPeriodicEnforce)
        {
            ApplyBorderlessTopmost();
            PositionToExpected();
            nextPeriodicEnforce = Time.unscaledTime + 1.0f;
        }
    }

    void ApplyBorderlessTopmost()
    {
        // Strip all normal chrome; force popup+visible
        int style = GetWindowLong(unityHwnd, GWL_STYLE);
        int desiredStyle = (style & ~(WS_OVERLAPPEDWINDOW | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX))
                         | (WS_POPUP | WS_VISIBLE);
        if (style != desiredStyle)
            SetWindowLong(unityHwnd, GWL_STYLE, desiredStyle);

        // Strip extended edges/shadows; hide from Alt-Tab
        int ex = GetWindowLong(unityHwnd, GWL_EXSTYLE);
        int desiredEx = (ex & ~(WS_EX_DLGMODALFRAME | WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_APPWINDOW))
                      | WS_EX_TOOLWINDOW;
        if (ex != desiredEx)
            SetWindowLong(unityHwnd, GWL_EXSTYLE, desiredEx);

        // Disable DWM non-client rendering/shadow (Windows 10/11)
        try
        {
            int disable = 0;
            DwmSetWindowAttribute(unityHwnd, DWMWA_NCRENDERING_ENABLED, ref disable, sizeof(int));
            int policy = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(unityHwnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
        }
        catch { }

        // Re-assert TOPMOST (don’t activate to avoid input focus weirdness)
        SetWindowPos(unityHwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    void PositionToExpected()
    {
        SetWindowPos(unityHwnd, HWND_TOPMOST, expectedRect.x, expectedRect.y, expectedRect.w, expectedRect.h,
            SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (!cursorHidden && mode == SaverMode.Fullscreen)
        {
            ShowCursor(false);
            cursorHidden = true;
        }

        if (mode == SaverMode.Fullscreen)
        {
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer >= idleGrace)
            {
                if (Input.mousePosition != lastMouse) QuitSaver();
                if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) QuitSaver();
            }
            lastMouse = Input.mousePosition;
        }
#endif
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // When focus flips, re-assert once immediately
        if (mode == SaverMode.Fullscreen && unityHwnd != IntPtr.Zero && IsWindow(unityHwnd))
        {
            ApplyBorderlessTopmost();
            PositionToExpected();
        }
    }

    void OnApplicationQuit()
    {
        if (cursorHidden) ShowCursor(true);
    }

    // ---------- Preview embedding ----------
    System.Collections.IEnumerator SetupPreview()
    {
        for (int i = 0; i < 5 && unityHwnd == IntPtr.Zero; i++)
        {
            yield return null;
            unityHwnd = FindOwnWindow();
        }

        if (unityHwnd == IntPtr.Zero || !IsWindow(previewParent))
        {
            Application.Quit();
            yield break;
        }

        int style = GetWindowLong(unityHwnd, GWL_STYLE);
        style |= WS_CHILD;
        style &= ~(WS_OVERLAPPEDWINDOW | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        SetWindowLong(unityHwnd, GWL_STYLE, style);
        SetParent(unityHwnd, previewParent);

        if (GetClientRect(previewParent, out var rc))
        {
            int w = Math.Max(1, rc.right - rc.left);
            int h = Math.Max(1, rc.bottom - rc.top);
            MoveWindow(unityHwnd, 0, 0, w, h, true);
        }

        if (!SceneManager.GetSceneByBuildIndex(saverSceneBuildIndex).isLoaded)
            SceneManager.LoadScene(saverSceneBuildIndex, LoadSceneMode.Additive);
    }

    // ---------- Arg parsing ----------
    void ParseArgs()
    {
        var args = Environment.GetCommandLineArgs();

        if (args.Length <= 1)
        {
            mode = SaverMode.Config; // double-click in Explorer → treat as configure
            return;
        }

        for (int i = 1; i < args.Length; ++i)
        {
            string raw = args[i]?.Trim() ?? "";
            string a = raw.ToLowerInvariant();

            string sw = a;
            string val = null;
            int sep = a.IndexOfAny(new[] { ':', ',' });
            if (sep >= 0) { sw = a.Substring(0, sep); val = a.Substring(sep + 1).Trim(); }

            bool IsSwitch(string s, string key) =>
                s == key || s == ("/" + key) || s == ("-" + key);

            if (IsSwitch(sw, "s"))
            {
                mode = SaverMode.Fullscreen;
            }
            else if (IsSwitch(sw, "c"))
            {
                mode = SaverMode.Config;
                if (string.IsNullOrEmpty(val) && i + 1 < args.Length && !args[i + 1].StartsWith("/", StringComparison.Ordinal))
                    val = args[++i];
            }
            else if (IsSwitch(sw, "p"))
            {
                mode = SaverMode.Preview;
                if (string.IsNullOrEmpty(val) && i + 1 < args.Length) val = args[++i];

                if (!string.IsNullOrEmpty(val))
                {
                    val = val.Trim();
                    if (val.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (long.TryParse(val.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out long hwndHex))
                            previewParent = new IntPtr(hwndHex);
                    }
                    else if (long.TryParse(val, out long hwndDec))
                    {
                        previewParent = new IntPtr(hwndDec);
                    }
                }
            }
            else if (a == "/configure" || a == "-configure")
            {
                mode = SaverMode.Config;
            }
        }
    }

    // ---------- Helpers ----------
    IntPtr FindOwnWindow()
    {
        var candidates = new[] { GetActiveWindow(), GetForegroundWindow() };
        uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        foreach (var h in candidates)
        {
            if (h != IntPtr.Zero && IsWindow(h))
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == myPid) return h;
            }
        }
        return candidates.FirstOrDefault(h => h != IntPtr.Zero);
    }

    void QuitSaver()
    {
        if (cursorHidden) ShowCursor(true);
        Application.Quit();
    }
}
#else
using UnityEngine;

public class ScreensaverBootstrap : MonoBehaviour
{
    Vector3 lastMouse;
    float idleGrace = 2f;
    float idleTimer = 0f;

    void Awake()
    {
        Application.runInBackground = true;
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.fullScreen = true;
    }

    void Update()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleGrace)
        {
            if (Input.mousePosition != lastMouse) Application.Quit();
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                Application.Quit();
        }
        lastMouse = Input.mousePosition;
    }
}
#endif
