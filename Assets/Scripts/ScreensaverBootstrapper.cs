#if UNITY_STANDALONE_WIN
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class ScreensaverBootstrap : MonoBehaviour
{
    // --- Win32 interop ---
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

    const int GWL_STYLE = -16;
    const int WS_CHILD = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    enum SaverMode { Fullscreen, Preview, Config }

    SaverMode mode = SaverMode.Fullscreen;
    IntPtr previewParent = IntPtr.Zero;
    IntPtr unityHwnd = IntPtr.Zero;
    Vector3 lastMouse;
    float idleGrace = 2f; // seconds before we start watching input
    float idleTimer = 0f;
    bool cursorHidden = false;

    void Awake()
    {
        Application.runInBackground = true; // don’t pause
        ParseArgs();
        if (mode == SaverMode.Fullscreen)
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow; // spans multi-monitor “virtual desktop”
            Screen.fullScreen = true;
        }
        // For config mode, you might show a simple menu/Canvas then quit.
    }

    void Start()
    {
        // Grab Unity window handle on/after first frame
        unityHwnd = FindOwnWindow();
        if (mode == SaverMode.Preview && previewParent != IntPtr.Zero && unityHwnd != IntPtr.Zero)
        {
            // Make Unity a child of the given host window and fit it
            int style = GetWindowLong(unityHwnd, GWL_STYLE);
            SetWindowLong(unityHwnd, GWL_STYLE, style | WS_CHILD);
            SetParent(unityHwnd, previewParent);
            if (GetClientRect(previewParent, out var rc))
            {
                MoveWindow(unityHwnd, 0, 0, Math.Max(1, rc.right - rc.left), Math.Max(1, rc.bottom - rc.top), true);
            }
        }
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (!cursorHidden && mode != SaverMode.Config)
        {
            ShowCursor(false);
            cursorHidden = true;
        }  
#endif

        // Exit policy: after a brief grace period, any input closes the saver
        if (mode != SaverMode.Config)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleGrace)
            {
                // Mouse moved?
                if (Input.mousePosition != lastMouse) QuitSaver();
                // Any key or click?
                if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) QuitSaver();
            }
            lastMouse = Input.mousePosition;
        }
    }

    void OnApplicationQuit()
    {
        if (cursorHidden) ShowCursor(true);
    }

    void ParseArgs()
    {
        var args = Environment.GetCommandLineArgs();
        // Examples: ["YourSaver.scr", "/s"] or [".scr", "/p", "123456"]
        for (int i = 1; i < args.Length; ++i)
        {
            string a = args[i].Trim().ToLowerInvariant();
            if (a == "/s")
            {
                mode = SaverMode.Fullscreen;
            }
            else if (a == "/c")
            {
                // Optional hwnd after /c (not always present)
                mode = SaverMode.Config;
            }
            else if (a == "/p" && i + 1 < args.Length)
            {
                if (long.TryParse(args[i + 1], out long hwndVal))
                {
                    previewParent = new IntPtr(hwndVal);
                    mode = SaverMode.Preview;
                    i++;
                }
            }
        }
    }

    IntPtr FindOwnWindow()
    {
        // Try the active window, then foreground; verify it belongs to this process.
        var candidates = new[] { GetActiveWindow(), GetForegroundWindow() };
        uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        foreach (var h in candidates)
        {
            if (h != IntPtr.Zero && IsWindow(h))
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == myPid)
                    return h;
            }
        }
        // Fallback: Unity’s class name is usually "UnityWndClass", but enumerating by class is overkill here.
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
    float idleGrace = 2f; // seconds before we start watching input
    float idleTimer = 0f;
    bool cursorHidden = false;

    void Awake()
    {
        Application.runInBackground = true; // don’t pause
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow; // spans multi-monitor “virtual desktop”
        Screen.fullScreen = true;
    }

    void Update()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleGrace)
        {
            if (Input.mousePosition != lastMouse) QuitSaver();
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                QuitSaver();
        }
        lastMouse = Input.mousePosition;
    }

    void QuitSaver()
    {
        Application.Quit();
    }
}
#endif
