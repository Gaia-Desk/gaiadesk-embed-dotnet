using System.IO;
using System.Text;
using System.Text.Json;

namespace GaiaDesk.Embed;

/// <summary>
/// The consent dialog's question, in your look
/// (<see cref="GaiaDeskEmbed.RequestConsent(ConsentOptions)"/>; libgaiadesk_embed 0.2.0).
/// </summary>
public sealed record ConsentOptions
{
    /// <summary>Your company's name on the dialog.</summary>
    public string? Company { get; init; }

    /// <summary>What will be shared; default <see cref="CaptureMode.Window"/>.</summary>
    public CaptureMode Capture { get; init; } = CaptureMode.Window;

    /// <summary>Say the agent may click and type.</summary>
    public bool Guided { get; init; }

    /// <summary><c>"#RRGGBB"</c>: the dialog's accent; default the platform's own.</summary>
    public string? BrandColor { get; init; }

    /// <summary>A PNG (at most 256 KiB) as base64, shown small on the dialog. Or <see cref="LogoPng"/>, not both.</summary>
    public string? LogoPngBase64 { get; init; }

    /// <summary>The logo as PNG bytes; the alternative to <see cref="LogoPngBase64"/>.</summary>
    public byte[]? LogoPng { get; init; }

    /// <summary>The JSON <c>gd_embed_show_consent_ex</c> takes; unset keys are left out.</summary>
    internal string ToJson()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("capture", EmbedOptions.CaptureName(Capture));
            w.WriteBoolean("guided", Guided);
            if (!string.IsNullOrEmpty(Company))
            {
                w.WriteString("company", Company);
            }
            EmbedOptions.WriteBrand(w, BrandColor, LogoPngBase64, LogoPng);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
