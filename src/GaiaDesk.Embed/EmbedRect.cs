using System;
using System.Runtime.InteropServices;

namespace GaiaDesk.Embed;

/// <summary>
/// A masked region, relative to the top-left of the shared window as
/// <c>GetWindowRect</c> reports it (so including the title bar and borders),
/// in the same units.
/// </summary>
public readonly record struct EmbedRect(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// Convert a rect in screen coordinates (for WinForms,
    /// <c>control.RectangleToScreen(control.ClientRectangle)</c>; for WPF,
    /// <c>element.PointToScreen</c> of its corners) to the mask space of
    /// <paramref name="windowHandle"/>: relative to its <c>GetWindowRect</c>
    /// top-left. Windows only.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Not on Windows.</exception>
    /// <exception cref="InvalidOperationException">The window handle is not valid.</exception>
    public static EmbedRect FromScreenRect(nint windowHandle, double screenX, double screenY, double width, double height)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("EmbedRect.FromScreenRect uses GetWindowRect (Windows only)");
        }
        if (!GetWindowRect(windowHandle, out var wr))
        {
            throw new InvalidOperationException($"GetWindowRect failed (error {Marshal.GetLastPInvokeError()})");
        }
        return FromScreenRect(wr.Left, wr.Top, screenX, screenY, width, height);
    }

    /// <summary>
    /// The same, for a rect given as any <c>(X, Y, Width, Height)</c> tuple
    /// (e.g. from a <c>System.Drawing.Rectangle</c>).
    /// </summary>
    public static EmbedRect FromScreenRect(nint windowHandle, (double X, double Y, double Width, double Height) screenRect) =>
        FromScreenRect(windowHandle, screenRect.X, screenRect.Y, screenRect.Width, screenRect.Height);

    /// <summary>Pure core of <see cref="FromScreenRect(nint, double, double, double, double)"/>: offset by the window's top-left.</summary>
    internal static EmbedRect FromScreenRect(double windowLeft, double windowTop, double screenX, double screenY, double width, double height) =>
        new(screenX - windowLeft, screenY - windowTop, width, height);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out Win32Rect rect);
}
