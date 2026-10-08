using System.Text.Json;
using Xunit;

namespace GaiaDesk.Embed.Tests;

public class ConfigJsonTests
{
    private const string Token = "gdemb_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string[] Keys(JsonElement o) => o.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public void Defaults_are_window_capture_not_guided_and_optional_keys_omitted()
    {
        var o = Parse(new EmbedOptions { EmbedToken = Token }.ToConfigJson(consentGranted: true));
        Assert.Equal(new[] { "embed_token", "consent_granted", "capture", "guided" }, Keys(o));
        Assert.Equal(Token, o.GetProperty("embed_token").GetString());
        Assert.Equal(JsonValueKind.True, o.GetProperty("consent_granted").ValueKind);
        Assert.Equal("window", o.GetProperty("capture").GetString());
        Assert.Equal(JsonValueKind.False, o.GetProperty("guided").ValueKind);
    }

    [Fact]
    public void Consent_flag_is_a_json_boolean_false_when_not_granted()
    {
        var o = Parse(new EmbedOptions { EmbedToken = Token }.ToConfigJson(consentGranted: false));
        Assert.Equal(JsonValueKind.False, o.GetProperty("consent_granted").ValueKind);
    }

    [Fact]
    public void Every_option_uses_the_library_snake_case_keys()
    {
        var opts = new EmbedOptions
        {
            EmbedToken = Token,
            Server = "https://support.example.com",
            Company = "Acme \"Desk\"",
            Capture = CaptureMode.Screen,
            WindowId = (nint)0x1234,
            Guided = true,
            Fps = 20,
            Masks = new[] { new EmbedRect(10, 20, 100, 30) },
        };
        var o = Parse(opts.ToConfigJson(true));
        Assert.Equal(new[] { "embed_token", "consent_granted", "server", "company", "capture", "window_id", "guided", "fps", "masks" }, Keys(o));
        var m = o.GetProperty("masks")[0];
        Assert.Equal((10.0, 20.0, 100.0, 30.0), (m.GetProperty("x").GetDouble(), m.GetProperty("y").GetDouble(), m.GetProperty("w").GetDouble(), m.GetProperty("h").GetDouble()));
        Assert.Equal("https://support.example.com", o.GetProperty("server").GetString());
        Assert.Equal("Acme \"Desk\"", o.GetProperty("company").GetString());
        Assert.Equal("screen", o.GetProperty("capture").GetString());
        Assert.Equal(0x1234UL, o.GetProperty("window_id").GetUInt64());
        Assert.True(o.GetProperty("guided").GetBoolean());
        Assert.Equal(20, o.GetProperty("fps").GetInt32());
    }

    [Theory]
    [InlineData(CaptureMode.Window, "window")]
    [InlineData(CaptureMode.App, "app")]
    [InlineData(CaptureMode.Screen, "screen")]
    public void Capture_names(CaptureMode mode, string name)
    {
        var o = Parse(new EmbedOptions { EmbedToken = Token, Capture = mode }.ToConfigJson(true));
        Assert.Equal(name, o.GetProperty("capture").GetString());
    }

    [Fact]
    public void Window_id_is_written_as_an_unsigned_integer()
    {
        var o = Parse(new EmbedOptions { EmbedToken = Token, WindowId = (nint)0x7FFF_FFFF }.ToConfigJson(true));
        Assert.Equal(JsonValueKind.Number, o.GetProperty("window_id").ValueKind);
        Assert.Equal(0x7FFF_FFFFUL, o.GetProperty("window_id").GetUInt64());
    }

    [Fact]
    public void Screen_rect_becomes_window_relative()
    {
        var r = EmbedRect.FromScreenRect(100, 50, 140, 130, 200, 120);
        Assert.Equal(new EmbedRect(40, 80, 200, 120), r);
    }

    [Fact]
    public void Last_error_json_becomes_an_exception()
    {
        var e = GaiaDeskEmbedException.FromLastError("{\"code\":\"bad_token\",\"message\":\"nope\"}");
        Assert.Equal("bad_token", e.Code);
        Assert.Equal("nope", e.Message);
        Assert.Equal("unknown", GaiaDeskEmbedException.FromLastError("").Code);
        Assert.Equal("unknown", GaiaDeskEmbedException.FromLastError("not json").Code);
    }
}
