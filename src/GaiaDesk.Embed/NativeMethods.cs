using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GaiaDesk.Embed;

/// <summary>The C ABI of libgaiadesk_embed (include/gaiadesk_embed.h).</summary>
internal static class NativeMethods
{
    /// <summary>
    /// Resolves as gaiadesk_embed.dll (Windows), libgaiadesk_embed.dylib
    /// (macOS) and libgaiadesk_embed.so (Linux).
    /// </summary>
    internal const string Lib = "gaiadesk_embed";

    /// <summary>A directory searched first for the native library.</summary>
    internal const string LibDirEnv = "GAIADESK_EMBED_LIB_DIR";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GdEventFn(IntPtr userData, IntPtr eventJson);

    [StructLayout(LayoutKind.Sequential)]
    internal struct GdEmbedCallbacks
    {
        public IntPtr OnEvent;
        public IntPtr UserData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GdRect
    {
        public double X;
        public double Y;
        public double W;
        public double H;
    }

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_start")]
    internal static extern IntPtr Start(IntPtr configJson, GdEmbedCallbacks callbacks);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_stop")]
    internal static extern void Stop(IntPtr handle);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_set_mask_rects")]
    internal static extern int SetMaskRects(IntPtr handle, [In] GdRect[]? rects, UIntPtr count);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_set_paused")]
    internal static extern int SetPaused(IntPtr handle, int paused);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_show_consent")]
    internal static extern int ShowConsent(IntPtr company, IntPtr capture, int guided);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_last_error")]
    internal static extern IntPtr LastError();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_version")]
    internal static extern IntPtr Version();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "gd_embed_build_info")]
    internal static extern IntPtr BuildInfo();

    /// <summary>The platform's file name for the library.</summary>
    internal static string FileName =>
        OperatingSystem.IsWindows() ? "gaiadesk_embed.dll"
        : OperatingSystem.IsMacOS() ? "libgaiadesk_embed.dylib"
        : "libgaiadesk_embed.so";

    /// <summary>Strings the library returns are UTF-8 and owned by it.</summary>
    internal static string Utf8(IntPtr p) => p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(p) ?? string.Empty;

    // A module initializer, so the resolver is in place before any P/Invoke
    // into this assembly, whichever entry point the app touches first.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void InstallResolver()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Lib)
        {
            return IntPtr.Zero;
        }
        var dir = Environment.GetEnvironmentVariable(LibDirEnv);
        if (!string.IsNullOrEmpty(dir))
        {
            var path = Path.Combine(dir, FileName);
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var h))
            {
                return h;
            }
        }
        // Fall back to the runtime's normal probing (deps.json runtimes/<rid>/native, app dir, PATH).
        return IntPtr.Zero;
    }
}
