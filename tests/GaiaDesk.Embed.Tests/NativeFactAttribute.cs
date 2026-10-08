using GaiaDesk.Embed;
using Xunit;

namespace GaiaDesk.Embed.Tests;

internal static class Native
{
    internal const string SkipReason = "native libgaiadesk_embed not loadable (set GAIADESK_EMBED_LIB_DIR)";

    internal static bool Available { get; } = GaiaDeskEmbed.IsNativeLibraryAvailable;
}

/// <summary>
/// A fact that needs the native library: skipped (not failed) when it cannot
/// be loaded. Point GAIADESK_EMBED_LIB_DIR at a directory holding
/// gaiadesk_embed.dll / libgaiadesk_embed.dylib / libgaiadesk_embed.so.
/// </summary>
public sealed class NativeFactAttribute : FactAttribute
{
    public NativeFactAttribute()
    {
        if (!Native.Available)
        {
            Skip = Native.SkipReason;
        }
    }
}

/// <summary>The <see cref="NativeFactAttribute"/> of theories.</summary>
public sealed class NativeTheoryAttribute : TheoryAttribute
{
    public NativeTheoryAttribute()
    {
        if (!Native.Available)
        {
            Skip = Native.SkipReason;
        }
    }
}
