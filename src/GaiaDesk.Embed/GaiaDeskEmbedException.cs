using System;
using System.Text.Json;

namespace GaiaDesk.Embed;

/// <summary>
/// A refusal from the library. <see cref="Code"/> is stable
/// (<c>consent_required</c>, <c>bad_token</c>, <c>bad_config</c>,
/// <c>bad_server</c>, …); <see cref="Exception.Message"/> is for people.
/// </summary>
public sealed class GaiaDeskEmbedException : Exception
{
    /// <summary>The stable refusal code.</summary>
    public string Code { get; }

    /// <summary>A refusal with this code and message.</summary>
    public GaiaDeskEmbedException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";

    /// <summary>Parse <c>gd_embed_last_error()</c>'s <c>{"code","message"}</c>.</summary>
    internal static GaiaDeskEmbedException FromLastError(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new GaiaDeskEmbedException("unknown", "the library refused without saying why");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var o = doc.RootElement;
            var code = o.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var msg = o.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return new GaiaDeskEmbedException(code ?? "unknown", msg ?? string.Empty);
        }
        catch (JsonException)
        {
            return new GaiaDeskEmbedException("unknown", raw);
        }
    }
}
