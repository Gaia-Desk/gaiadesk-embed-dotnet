using System;
using System.Text.Json;

namespace GaiaDesk.Embed;

/// <summary>
/// One event from a session. Match on the derived type
/// (<c>switch (e) { case CodeEvent c: … }</c>) or read <see cref="Type"/>.
/// <see cref="EndedEvent"/> is always the last event, exactly once.
/// </summary>
public abstract record EmbedEvent
{
    /// <summary>The event's <c>type</c> as the library sent it ("state", "code", …).</summary>
    public abstract string Type { get; }

    /// <summary>
    /// Parse one event's JSON. Returns an <see cref="UnknownEvent"/> for a
    /// type this version does not know, and null for JSON that is not an event.
    /// </summary>
    public static EmbedEvent? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object || Str(o, "type") is not { } type)
            {
                return null;
            }
            return type switch
            {
                "state" => new StateEvent(Str(o, "state") ?? string.Empty) { Choices = Choices(o) },
                "code" => new CodeEvent(Str(o, "code") ?? string.Empty, Str(o, "session_id") ?? string.Empty, Str(o, "mode") ?? "view", Time(o, "expires_at")),
                "joined" => new JoinedEvent(Str(o, "agent")),
                "left" => new LeftEvent(),
                "control" => new ControlEvent(Bool(o, "allowed")),
                "action" => new ActionEvent(Str(o, "action") ?? string.Empty, Bool(o, "applied"), Str(o, "reason")),
                "error" => new ErrorEvent(Str(o, "code") ?? string.Empty, Str(o, "message") ?? string.Empty),
                "indicator_moved" => new IndicatorMovedEvent(Num(o, "x"), Num(o, "y")),
                "ended" => new EndedEvent(Str(o, "reason") ?? string.Empty),
                "consent_shown" => new ConsentShownEvent(Str(o, "locale") ?? string.Empty, Str(o, "wording_version") ?? string.Empty),
                "consent_accepted" => Choices(o) is { } ch ? new ConsentAcceptedEvent(ch) : new UnknownEvent(type, json),
                "consent_declined" => new ConsentDeclinedEvent(Str(o, "reason") ?? "declined"),
                _ => new UnknownEvent(type, json),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ConsentChoices? Choices(JsonElement o) =>
        o.TryGetProperty("choices", out var v) && v.ValueKind == JsonValueKind.Object
            ? v.Deserialize(ConsentJson.Default.ConsentChoices)
            : null;

    private static string? Str(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static double Num(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static DateTimeOffset? Time(JsonElement o, string key) =>
        o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s)
            : null;
}

/// <summary>The session's state: "starting", "waiting", "connected" or "ended".</summary>
public sealed record StateEvent(string State) : EmbedEvent
{
    /// <summary>
    /// On "waiting", when the consent step's choices went with the join: what the session runs
    /// with. Needs libgaiadesk_embed 0.4.0.
    /// </summary>
    public ConsentChoices? Choices { get; init; }

    /// <inheritdoc />
    public override string Type => "state";
}

/// <summary>The support code to read to your agent, the session id, its mode ("view" | "cobrowse") and expiry.</summary>
public sealed record CodeEvent(string Code, string SessionId, string Mode, DateTimeOffset? ExpiresAt) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "code";
}

/// <summary>A support agent joined (their email, when known).</summary>
public sealed record JoinedEvent(string? Agent) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "joined";
}

/// <summary>The support agent left.</summary>
public sealed record LeftEvent : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "left";
}

/// <summary>Whether guided input is currently allowed.</summary>
public sealed record ControlEvent(bool Allowed) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "control";
}

/// <summary>A guided action from the agent: whether it was applied, and why not.</summary>
public sealed record ActionEvent(string Action, bool Applied, string? Reason) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "action";
}

/// <summary>A problem during the session (the session may go on, or end next).</summary>
public sealed record ErrorEvent(string Code, string Message) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "error";
}

/// <summary>
/// The customer dragged the indicator to (<see cref="X"/>, <see cref="Y"/>), in screen
/// coordinates. Keep it and pass it back as <see cref="EmbedOptions.IndicatorOrigin"/>
/// next time. Needs libgaiadesk_embed 0.3.0.
/// </summary>
public sealed record IndicatorMovedEvent(double X, double Y) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "indicator_moved";
}

/// <summary>
/// The session ended: "stopped", "app_stopped", "customer_cancelled" (Cancel on the
/// indicator, or Stop, before any agent was admitted; libgaiadesk_embed 0.3.0),
/// "customer_declined" / "consent_timeout" (the consent receipt said no, or the dialog timed
/// out; 0.4.0), "disconnected", "expired" or "error". Always last.
/// </summary>
public sealed record EndedEvent(string Reason) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "ended";
}

/// <summary>The consent step's dialog is up, in this locale and wording version (0.4.0).</summary>
public sealed record ConsentShownEvent(string Locale, string WordingVersion) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "consent_shown";
}

/// <summary>The customer agreed in the consent step's dialog (0.4.0).</summary>
public sealed record ConsentAcceptedEvent(ConsentChoices Choices) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "consent_accepted";
}

/// <summary>The customer said no in the consent step's dialog: "declined" or "timeout" (0.4.0).</summary>
public sealed record ConsentDeclinedEvent(string Reason) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => "consent_declined";
}

/// <summary>An event type this version of the wrapper does not know; <see cref="Json"/> is the raw event.</summary>
public sealed record UnknownEvent(string RawType, string Json) : EmbedEvent
{
    /// <inheritdoc />
    public override string Type => RawType;
}
