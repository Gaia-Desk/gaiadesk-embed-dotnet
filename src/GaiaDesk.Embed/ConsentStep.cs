using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GaiaDesk.Embed;

// The consent step (libgaiadesk_embed 0.4.0, GD-31): the library's own dialog
// with a live preview of what support will see, per-session choices,
// localized wording, and a consent receipt the library makes and stores.

/// <summary>The consent dialog's light or dark look.</summary>
public enum ConsentAppearance
{
    /// <summary>Follow the system.</summary>
    Auto,
    /// <summary>Always light.</summary>
    Light,
    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>The consent dialog's buttons.</summary>
public enum ConsentButtonStyle
{
    /// <summary>Solid accent.</summary>
    Filled,
    /// <summary>Accent outline.</summary>
    Outline,
    /// <summary>Solid, fully rounded.</summary>
    Pill,
}

/// <summary>What the consent step decided.</summary>
public enum ConsentOutcome
{
    /// <summary>The customer agreed.</summary>
    Accepted,
    /// <summary>The customer said no.</summary>
    Declined,
    /// <summary>The dialog declined itself after <see cref="ConsentStepOptions.TimeoutSeconds"/>.</summary>
    Timeout,
    /// <summary>No dialog here: ask with your own UI, then <see cref="GaiaDeskEmbed.RecordConsent"/>.</summary>
    Unavailable,
}

/// <summary>The consent dialog's look. Every property optional.</summary>
public sealed record ConsentTheme
{
    /// <summary>Light, dark or the system's (default).</summary>
    [JsonPropertyName("appearance")] public ConsentAppearance? Appearance { get; init; }
    /// <summary><c>"#RRGGBB"</c>.</summary>
    [JsonPropertyName("background")] public string? Background { get; init; }
    /// <summary><c>"#RRGGBB"</c>.</summary>
    [JsonPropertyName("surface")] public string? Surface { get; init; }
    /// <summary><c>"#RRGGBB"</c>.</summary>
    [JsonPropertyName("text")] public string? Text { get; init; }
    /// <summary><c>"#RRGGBB"</c>.</summary>
    [JsonPropertyName("accent")] public string? Accent { get; init; }
    /// <summary>Corner radius in pixels, 0 to 24.</summary>
    [JsonPropertyName("radius")] public int? Radius { get; init; }
    /// <summary>A font family name; falls back to the system UI font.</summary>
    [JsonPropertyName("font_family")] public string? FontFamily { get; init; }
    /// <summary>Filled (default), outline or pill buttons.</summary>
    [JsonPropertyName("button_style")] public ConsentButtonStyle? ButtonStyle { get; init; }
}

/// <summary>What the customer may choose in the dialog.</summary>
public sealed record ConsentOffers
{
    /// <summary>"Allow support to click and type" (only when the app asks for guided).</summary>
    [JsonPropertyName("offer_guided")] public bool? OfferGuided { get; init; }
    /// <summary>"Also share app logs".</summary>
    [JsonPropertyName("offer_logs")] public bool? OfferLogs { get; init; }
    /// <summary>One window or the whole screen (only where both are allowed).</summary>
    [JsonPropertyName("offer_capture")] public bool? OfferCapture { get; init; }
}

/// <summary>
/// The consent step's options (<see cref="GaiaDeskEmbed.Consent"/>, <see cref="GaiaDeskEmbed.Preview"/>).
/// Every property optional; unset ones are left out and take the library's default.
/// </summary>
public sealed record ConsentStepOptions
{
    /// <summary><c>{company}</c> in the wording.</summary>
    [JsonPropertyName("company")] public string? Company { get; init; }
    /// <summary>What is shared by default.</summary>
    [JsonPropertyName("capture")] public CaptureMode? Capture { get; init; }
    /// <summary>The app wants click-and-type (the customer may still say no).</summary>
    [JsonPropertyName("guided")] public bool? Guided { get; init; }
    /// <summary>Say private fields stay hidden (pass <see cref="Masks"/> too).</summary>
    [JsonPropertyName("masked")] public bool? Masked { get; init; }
    /// <summary><c>"#RRGGBB"</c>: the dialog's accent.</summary>
    [JsonPropertyName("brand_color")] public string? BrandColor { get; init; }
    /// <summary>Your logo as PNG bytes (at most 256 KiB).</summary>
    [JsonPropertyName("logo_png_base64")] public byte[]? LogoPng { get; init; }
    /// <summary>The calling window (an HWND): the dialog is owned by it.</summary>
    [JsonPropertyName("parent_window")] public nint? ParentWindow { get; init; }
    /// <summary>The dialog's look.</summary>
    [JsonPropertyName("theme")] public ConsentTheme? Theme { get; init; }
    /// <summary><c>"auto"</c> (the system's languages) or a BCP-47 tag.</summary>
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    /// <summary>Per-key overrides of the default wording (<c>title</c>, <c>accept</c>, …; placeholders <c>{company}</c> <c>{team}</c> <c>{minutes}</c> <c>{count}</c>).</summary>
    [JsonPropertyName("wording")] public IReadOnlyDictionary<string, string>? Wording { get; init; }
    /// <summary>Your id for your wording; default <c>gaiadesk-&lt;version&gt;-&lt;locale&gt;</c>.</summary>
    [JsonPropertyName("wording_version")] public string? WordingVersion { get; init; }
    /// <summary>What the customer may choose.</summary>
    [JsonPropertyName("choices")] public ConsentOffers? Choices { get; init; }
    /// <summary>https only; shown as a link.</summary>
    [JsonPropertyName("privacy_url")] public string? PrivacyUrl { get; init; }
    /// <summary>https only; shown as a link.</summary>
    [JsonPropertyName("terms_url")] public string? TermsUrl { get; init; }
    /// <summary>0 never; else the dialog declines itself (<see cref="ConsentOutcome.Timeout"/>).</summary>
    [JsonPropertyName("timeout_seconds")] public int? TimeoutSeconds { get; init; }
    /// <summary>The live preview (default on, when capture is possible).</summary>
    [JsonPropertyName("preview")] public bool? Preview { get; init; }
    /// <summary>With <see cref="EmbedToken"/> (and <see cref="SessionId"/>): the library posts the receipt itself and shows who is waiting.</summary>
    [JsonPropertyName("server")] public string? Server { get; init; }
    /// <summary>The session's <c>gdemb_…</c>.</summary>
    [JsonPropertyName("embed_token")] public string? EmbedToken { get; init; }
    /// <summary>The support session's <c>ss_…</c>.</summary>
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }
    /// <summary>Into the receipt; default the executable's version.</summary>
    [JsonPropertyName("app_version")] public string? AppVersion { get; init; }
    /// <summary>The preview's window (an HWND), as <see cref="EmbedOptions.WindowId"/>.</summary>
    [JsonPropertyName("window_id")] public nint? WindowId { get; init; }
    /// <summary>The preview's masks, as <see cref="EmbedOptions.Masks"/>.</summary>
    [JsonPropertyName("masks")] public IReadOnlyList<EmbedRect>? Masks { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, ConsentJson.Default.ConsentStepOptions);

    /// <summary>Only what <c>gd_embed_preview</c> reads.</summary>
    internal string ToPreviewJson() =>
        new ConsentStepOptions { Capture = Capture, WindowId = WindowId, ParentWindow = ParentWindow, Masks = Masks }.ToJson();
}

/// <summary>What the customer chose (and the session then runs with).</summary>
public sealed record ConsentChoices
{
    /// <summary>Support may click and type.</summary>
    [JsonPropertyName("guided")] public bool Guided { get; init; }
    /// <summary>App logs are shared too.</summary>
    [JsonPropertyName("share_logs")] public bool ShareLogs { get; init; }
    /// <summary>What is shared.</summary>
    [JsonPropertyName("capture")] public CaptureMode Capture { get; init; }
}

/// <summary>
/// The library's record of the answer: store it, let the library post it to the session
/// (<see cref="ConsentStepOptions.Server"/> + <see cref="ConsentStepOptions.EmbedToken"/>), or pass it to Start.
/// </summary>
public sealed record ConsentReceipt
{
    /// <summary><c>cr_…</c>.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary><c>accepted</c>, <c>declined</c> or <c>timeout</c>.</summary>
    [JsonPropertyName("decision")] public string Decision { get; init; } = "";
    /// <summary>RFC 3339, UTC, milliseconds.</summary>
    [JsonPropertyName("at")] public string At { get; init; } = "";
    /// <summary><c>native_dialog</c> or <c>embedded</c> (your own UI, or the web component).</summary>
    [JsonPropertyName("surface")] public string Surface { get; init; } = "";
    /// <summary>The locale shown.</summary>
    [JsonPropertyName("locale")] public string Locale { get; init; } = "";
    /// <summary>The wording's version.</summary>
    [JsonPropertyName("wording_version")] public string WordingVersion { get; init; } = "";
    /// <summary>SHA-256 of the strings actually shown.</summary>
    [JsonPropertyName("wording_sha256")] public string WordingSha256 { get; init; } = "";
    /// <summary>What was chosen.</summary>
    [JsonPropertyName("choices")] public ConsentChoices Choices { get; init; } = new();
    /// <summary>The login name (never a full name or path).</summary>
    [JsonPropertyName("os_user")] public string? OsUser { get; init; }
    /// <summary><c>windows</c>, <c>macos</c>, <c>linux</c>.</summary>
    [JsonPropertyName("os")] public string Os { get; init; } = "";
    /// <summary>The app's name.</summary>
    [JsonPropertyName("app_name")] public string? AppName { get; init; }
    /// <summary>The app's version.</summary>
    [JsonPropertyName("app_version")] public string? AppVersion { get; init; }
    /// <summary><c>libgaiadesk_embed/&lt;version&gt;</c>.</summary>
    [JsonPropertyName("sdk")] public string Sdk { get; init; } = "";
}

/// <summary>The consent step's answer.</summary>
public sealed record ConsentDecision
{
    /// <summary>Accepted, declined, timeout or unavailable.</summary>
    [JsonPropertyName("decision")] public ConsentOutcome Decision { get; init; }
    /// <summary>Null when <see cref="ConsentOutcome.Unavailable"/>.</summary>
    [JsonPropertyName("choices")] public ConsentChoices? Choices { get; init; }
    /// <summary>Accepted only: for <see cref="EmbedOptions.ConsentToken"/> (Start with a decision sets it).</summary>
    [JsonPropertyName("consent_token")] public string? ConsentToken { get; init; }
    /// <summary>Every outcome but <see cref="ConsentOutcome.Unavailable"/>.</summary>
    [JsonPropertyName("receipt")] public ConsentReceipt? Receipt { get; init; }

    internal static ConsentDecision Parse(string json) =>
        JsonSerializer.Deserialize(json, ConsentJson.Default.ConsentDecision)
        ?? throw new GaiaDeskEmbedException("unknown", "the library returned no decision");
}

/// <summary>Your own (or an embedded) consent UI's answer, for <see cref="GaiaDeskEmbed.RecordConsent"/>.</summary>
public sealed record ConsentAnswer
{
    /// <summary>Accepted, declined or timeout.</summary>
    [JsonPropertyName("decision")] public required ConsentOutcome Decision { get; init; }
    /// <summary>What the customer chose.</summary>
    [JsonPropertyName("choices")] public required ConsentChoices Choices { get; init; }
    /// <summary>The strings your UI showed, by key (<c>title</c>, <c>accept</c>, …): hashed into the receipt.</summary>
    [JsonPropertyName("wording")] public required IReadOnlyDictionary<string, string> Wording { get; init; }
    /// <summary>Your id for that wording.</summary>
    [JsonPropertyName("wording_version")] public string? WordingVersion { get; init; }
    /// <summary>The locale shown.</summary>
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    /// <summary>Your company's name.</summary>
    [JsonPropertyName("company")] public string? Company { get; init; }
    /// <summary>Must equal <c>Choices.Capture</c> when set.</summary>
    [JsonPropertyName("capture")] public CaptureMode? Capture { get; init; }
    /// <summary>Into the receipt.</summary>
    [JsonPropertyName("app_version")] public string? AppVersion { get; init; }
    /// <summary>With <see cref="EmbedToken"/> (and <see cref="SessionId"/>): the library posts the receipt too.</summary>
    [JsonPropertyName("server")] public string? Server { get; init; }
    /// <summary>The session's <c>gdemb_…</c>.</summary>
    [JsonPropertyName("embed_token")] public string? EmbedToken { get; init; }
    /// <summary>The support session's <c>ss_…</c>.</summary>
    [JsonPropertyName("session_id")] public string? SessionId { get; init; }

    internal string ToJson() => JsonSerializer.Serialize(this, ConsentJson.Default.ConsentAnswer);
}

/// <summary>One frame of exactly what support would see (masks filled black), at most 480×300.</summary>
public sealed record ConsentPreview(byte[] Png, int Width, int Height, int MaskedCount);

internal sealed record PreviewWire
{
    [JsonPropertyName("png_base64")] public string PngBase64 { get; init; } = "";
    [JsonPropertyName("width")] public int Width { get; init; }
    [JsonPropertyName("height")] public int Height { get; init; }
    [JsonPropertyName("masked_count")] public int MaskedCount { get; init; }
}

/// <summary>Enums as the library's lower-case names.</summary>
internal sealed class LowerEnum<T> : JsonStringEnumConverter<T> where T : struct, Enum
{
    public LowerEnum() : base(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) { }
}

/// <summary>An HWND / NSWindow* as the unsigned integer the library reads.</summary>
internal sealed class HandleConverter : JsonConverter<nint>
{
    public override nint Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => (nint)(long)r.GetUInt64();
    public override void Write(Utf8JsonWriter w, nint v, JsonSerializerOptions o) => w.WriteNumberValue(unchecked((ulong)(long)v));
}

/// <summary>A mask as <c>{"x","y","w","h"}</c>.</summary>
internal sealed class RectConverter : JsonConverter<EmbedRect>
{
    public override EmbedRect Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => throw new NotSupportedException();
    public override void Write(Utf8JsonWriter w, EmbedRect v, JsonSerializerOptions o)
    {
        w.WriteStartObject();
        w.WriteNumber("x", v.X);
        w.WriteNumber("y", v.Y);
        w.WriteNumber("w", v.Width);
        w.WriteNumber("h", v.Height);
        w.WriteEndObject();
    }
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = new[] { typeof(LowerEnum<CaptureMode>), typeof(LowerEnum<ConsentAppearance>), typeof(LowerEnum<ConsentButtonStyle>), typeof(LowerEnum<ConsentOutcome>), typeof(HandleConverter), typeof(RectConverter) })]
[JsonSerializable(typeof(ConsentStepOptions))]
[JsonSerializable(typeof(ConsentDecision))]
[JsonSerializable(typeof(ConsentAnswer))]
[JsonSerializable(typeof(ConsentReceipt))]
[JsonSerializable(typeof(ConsentChoices))]
[JsonSerializable(typeof(PreviewWire))]
internal sealed partial class ConsentJson : JsonSerializerContext
{
}
