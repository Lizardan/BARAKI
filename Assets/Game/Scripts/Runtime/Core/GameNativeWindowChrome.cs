using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Standalone Windows chrome helpers for the Bootstrap launcher window.
    /// Unity has no borderless-windowed <see cref="FullScreenMode"/>; strip OS frame via Win32.
    /// </summary>
    public static class GameNativeWindowChrome
    {
        const int GwlStyle = -16;
        const uint WsCaption = 0x00C00000;
        const uint WsThickFrame = 0x00040000;
        const uint WsMinimizeBox = 0x00020000;
        const uint WsMaximizeBox = 0x00010000;
        const uint WsSysMenu = 0x00080000;
        const uint WsPopup = 0x80000000;
        const uint WsVisible = 0x10000000;
        const uint WsClipSiblings = 0x04000000;
        const uint WsClipChildren = 0x02000000;

        const uint SwpNoMove = 0x0002;
        const uint SwpNoSize = 0x0001;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoOwnerZOrder = 0x0200;
        const uint SwpFrameChanged = 0x0020;
        const uint SwpShowWindow = 0x0040;

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

            var style = unchecked((uint)GetWindowLongPtr(hwnd, GwlStyle).ToInt64());
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
            style |= WsPopup | WsVisible | WsClipSiblings | WsClipChildren;
            SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(unchecked((long)style)));

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
        static IntPtr ResolveMainWindowHandle()
        {
            var hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero)
            {
                return hwnd;
            }

            var product = Application.productName;
            if (!string.IsNullOrEmpty(product))
            {
                hwnd = FindWindow(null, product);
                if (hwnd != IntPtr.Zero)
                {
                    return hwnd;
                }
            }

            return IntPtr.Zero;
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

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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
