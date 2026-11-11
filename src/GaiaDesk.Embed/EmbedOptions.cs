using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GaiaDesk.Embed;

/// <summary>What is shared.</summary>
public enum CaptureMode
{
    /// <summary>One window of this app (the default).</summary>
    Window,
    /// <summary>Every window of this app.</summary>
    App,
    /// <summary>The whole display this app's window is on (opt-in).</summary>
    Screen,
}

/// <summary>How a support session starts. Only <see cref="EmbedToken"/> is required.</summary>
public sealed record EmbedOptions
{
    /// <summary><c>gdemb_…</c>, from your backend (<c>POST /v1/support/sessions</c>).</summary>
    public required string EmbedToken { get; init; }

    /// <summary>The GaiaDesk server origin; default <c>https://gaiadesk.net</c>.</summary>
    public string? Server { get; init; }

    /// <summary>Your company's name, shown on the consent dialog and the sharing indicator.</summary>
    public string? Company { get; init; }

    /// <summary>What is shared; default <see cref="CaptureMode.Window"/>.</summary>
    public CaptureMode Capture { get; init; } = CaptureMode.Window;

    /// <summary>
    /// A window of THIS app to share (an HWND on Windows, e.g. <c>Form.Handle</c>
    /// or <c>new WindowInteropHelper(window).Handle</c>); default: the app's frontmost.
    /// </summary>
    public nint? WindowId { get; init; }

    /// <summary>Allow guided input (the session must also be "cobrowse").</summary>
    public bool Guided { get; init; }

    /// <summary>Frames per second, 1 to 30; default (null) is the library's 15.</summary>
    public int? Fps { get; init; }

    /// <summary>
    /// Regions painted out from the very first frame (see <see cref="EmbedRect"/>).
    /// Pass the ones you already know here: an agent can join straight away, and
    /// only start-time masks are guaranteed to cover the first frame.
    /// <see cref="GaiaDeskEmbed.SetMaskedRects"/> replaces them later.
    /// </summary>
    public IReadOnlyList<EmbedRect>? Masks { get; init; }

    /// <summary>
    /// Your brand colour, <c>"#RRGGBB"</c>: the indicator's stripe and Stop
    /// button; default the library's own. Needs libgaiadesk_embed 0.2.0.
    /// </summary>
    public string? BrandColor { get; init; }

    /// <summary>
    /// Your logo, a PNG (at most 256 KiB) as base64, shown small on the
    /// indicator. Or set <see cref="LogoPng"/>, not both. Needs libgaiadesk_embed 0.2.0.
    /// </summary>
    public string? LogoPngBase64 { get; init; }

    /// <summary>Your logo as PNG bytes (e.g. <c>File.ReadAllBytes("logo.png")</c>); the alternative to <see cref="LogoPngBase64"/>.</summary>
    public byte[]? LogoPng { get; init; }

    internal static string CaptureName(CaptureMode c) => c switch
    {
        CaptureMode.Window => "window",
        CaptureMode.App => "app",
        CaptureMode.Screen => "screen",
        _ => throw new ArgumentOutOfRangeException(nameof(c), c, "capture is Window, App or Screen"),
    };

    /// <summary>
    /// The library's config JSON. Keys are exactly the ones it accepts (it
    /// refuses unknown keys); optional ones are left out when null.
    /// </summary>
    internal string ToConfigJson(bool consentGranted)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("embed_token", EmbedToken ?? string.Empty);
            w.WriteBoolean("consent_granted", consentGranted);
            if (Server is not null)
            {
                w.WriteString("server", Server);
            }
            if (Company is not null)
            {
                w.WriteString("company", Company);
            }
            w.WriteString("capture", CaptureName(Capture));
            if (WindowId is { } id)
            {
                // An HWND is a pointer-sized handle; the library reads it as an unsigned integer.
                w.WriteNumber("window_id", unchecked((ulong)(long)id));
            }
            w.WriteBoolean("guided", Guided);
            if (Fps is { } fps)
            {
                w.WriteNumber("fps", fps);
            }
            if (Masks is { Count: > 0 } masks)
            {
                w.WriteStartArray("masks");
                foreach (var r in masks)
                {
                    w.WriteStartObject();
                    w.WriteNumber("x", r.X);
                    w.WriteNumber("y", r.Y);
                    w.WriteNumber("w", r.Width);
                    w.WriteNumber("h", r.Height);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            WriteBrand(w, BrandColor, LogoPngBase64, LogoPng);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static readonly Regex s_color = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>brand_color</c> and <c>logo_png_base64</c>, when set. The library
    /// refuses a bad colour too; checking here names the property first.
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException"><c>bad_config</c>: a colour that is not <c>#RRGGBB</c>, or both logo forms.</exception>
    internal static void WriteBrand(Utf8JsonWriter w, string? color, string? logoBase64, byte[]? logo)
    {
        if (color is not null)
        {
            if (!s_color.IsMatch(color))
            {
                throw new GaiaDeskEmbedException("bad_config", $"BrandColor must be \"#RRGGBB\" (not \"{color}\")");
            }
            w.WriteString("brand_color", color);
        }
        if (logoBase64 is not null && logo is not null)
        {
            throw new GaiaDeskEmbedException("bad_config", "set LogoPngBase64 or LogoPng, not both");
        }
        if (logo is { Length: > 0 })
        {
            w.WriteString("logo_png_base64", Convert.ToBase64String(logo));
        }
        else if (!string.IsNullOrEmpty(logoBase64))
        {
            w.WriteString("logo_png_base64", logoBase64);
        }
    }
}
