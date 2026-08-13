using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Standalone Windows chrome helpers for the Bootstrap launcher window.
    /// Unity has no borderless-windowed <see cref="FullScreenMode"/>; strip OS frame via Win32.
    /// Keeps <see cref="WsMinimizeBox"/> so the taskbar icon can minimize/restore.
    /// </summary>
    public static class GameNativeWindowChrome
    {
        const int GwlStyle = -16;
        const int GwlExStyle = -20;

        public const uint WsCaption = 0x00C00000;
        public const uint WsThickFrame = 0x00040000;
        public const uint WsMinimizeBox = 0x00020000;
        public const uint WsMaximizeBox = 0x00010000;
        public const uint WsSysMenu = 0x00080000;
        public const uint WsPopup = 0x80000000;
        public const uint WsVisible = 0x10000000;
        public const uint WsClipSiblings = 0x04000000;
        public const uint WsClipChildren = 0x02000000;
        public const uint WsExAppWindow = 0x00040000;
        public const uint WsExToolWindow = 0x00000080;

        const uint SwpNoMove = 0x0002;
        const uint SwpNoSize = 0x0001;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoOwnerZOrder = 0x0200;
        const uint SwpFrameChanged = 0x0020;
        const uint SwpShowWindow = 0x0040;

        /// <summary>Win32 class of the standalone player window.</summary>
        public const string UnityWindowClassName = "UnityWndClass";

        /// <summary>
        /// Strips caption / resize / maximize chrome but keeps minimize + sysmenu
        /// so Explorer can toggle the window from the taskbar button.
        /// </summary>
        public static uint ToBorderlessStyle(uint currentStyle)
        {
            currentStyle &= ~(WsCaption | WsThickFrame | WsMaximizeBox);
            currentStyle |= WsPopup | WsVisible | WsClipSiblings | WsClipChildren
                            | WsMinimizeBox | WsSysMenu;
            return currentStyle;
        }

        /// <summary>Adds the bits Explorer needs to minimize an already-focused window.</summary>
        public static uint EnsureTaskbarMinimizeStyle(uint currentStyle) =>
            currentStyle | WsMinimizeBox | WsSysMenu;

        /// <summary>Forces a taskbar button; tool windows are hidden from the taskbar.</summary>
        public static uint ToTaskbarAppExStyle(uint currentExStyle)
        {
            currentExStyle &= ~WsExToolWindow;
            currentExStyle |= WsExAppWindow;
            return currentExStyle;
        }

        /// <summary>
        /// Removes title bar / resize border from the player window (Windows standalone only).
        /// Keeps the given client size.
        /// </summary>
        public static bool TryApplyBorderless(int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = ResolveMainWindowHandle();
            if (hwnd == IntPtr.Zero || width <= 0 || height <= 0)
            {
                return false;
            }

            ApplyStyle(
                hwnd,
                ToBorderlessStyle(ReadStyle(hwnd, GwlStyle)),
                ToTaskbarAppExStyle(ReadStyle(hwnd, GwlExStyle)));

            return SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0,
                0,
                width,
                height,
                SwpNoMove | SwpNoZOrder | SwpNoOwnerZOrder | SwpFrameChanged | SwpShowWindow);
#else
            return false;
#endif
        }

        /// <summary>
        /// Restores taskbar minimize/restore after Unity changes fullscreen styles.
        /// </summary>
        public static bool TryEnableTaskbarMinimize()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = ResolveMainWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            ApplyStyle(
                hwnd,
                EnsureTaskbarMinimizeStyle(ReadStyle(hwnd, GwlStyle)),
                ToTaskbarAppExStyle(ReadStyle(hwnd, GwlExStyle)));

            return SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoOwnerZOrder | SwpFrameChanged);
#else
            return false;
#endif
        }

        /// <summary>Screen-space cursor (Win32 top-left origin), for drag deltas matching window moves.</summary>
        public static bool TryGetCursorScreenPosition(out Vector2Int position)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (GetCursorPos(out var point))
            {
                position = new Vector2Int(point.X, point.Y);
                return true;
            }
#endif
            position = default;
            return false;
        }

        /// <summary>Moves the player window by a screen-pixel delta without resizing.</summary>
        public static bool TryMoveBy(Vector2Int delta)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (delta.x == 0 && delta.y == 0)
            {
                return true;
            }

            var hwnd = ResolveMainWindowHandle();
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
            {
                return false;
            }

            return SetWindowPos(
                hwnd,
                IntPtr.Zero,
                rect.Left + delta.x,
                rect.Top + delta.y,
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoOwnerZOrder);
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        static readonly EnumWindowsProc s_enumWindowsProc = TryMatchCurrentProcessUnityWindow;
        static IntPtr s_enumWindowsMatch;

        static IntPtr ResolveMainWindowHandle()
        {
            var hwnd = GetActiveWindow();
            if (IsCurrentProcessUnityWindow(hwnd))
            {
                return hwnd;
            }

            hwnd = FindCurrentProcessUnityWindow();
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }

            var product = Application.productName;
            if (string.IsNullOrEmpty(product))
            {
                return IntPtr.Zero;
            }

            hwnd = FindWindow(null, product);
            return IsCurrentProcessUnityWindow(hwnd) ? hwnd : IntPtr.Zero;
        }

        static IntPtr FindCurrentProcessUnityWindow()
        {
            s_enumWindowsMatch = IntPtr.Zero;
            EnumWindows(s_enumWindowsProc, IntPtr.Zero);
            return s_enumWindowsMatch;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        static bool TryMatchCurrentProcessUnityWindow(IntPtr hWnd, IntPtr lParam)
        {
            if (!IsCurrentProcessUnityWindow(hWnd))
            {
                return true;
            }

            s_enumWindowsMatch = hWnd;
            return false;
        }

        static bool IsCurrentProcessUnityWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != GetCurrentProcessId())
            {
                return false;
            }

            var className = new System.Text.StringBuilder(64);
            if (GetClassName(hwnd, className, className.Capacity) <= 0)
            {
                return false;
            }

            return className.ToString() == UnityWindowClassName;
        }

        static uint ReadStyle(IntPtr hwnd, int index) =>
            unchecked((uint)GetWindowLongPtr(hwnd, index).ToInt64());

        static void ApplyStyle(IntPtr hwnd, uint style, uint exStyle)
        {
            SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(unchecked((long)style)));
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(unchecked((long)exStyle)));
        }

        static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
            IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));

        static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        [StructLayout(LayoutKind.Sequential)]
        struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
#endif
    }
}
