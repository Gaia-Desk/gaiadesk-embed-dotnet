using System.Text.Json;
using Xunit;

namespace GaiaDesk.Embed.Tests;

/// <summary>
/// Against the real native library, when it loads. Only refusals: these
/// never start a session (that would open real capture).
/// </summary>
public class NativeLibraryTests
{
    private const string Token = "gdemb_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static GaiaDeskEmbedException Refused(EmbedOptions options, bool consent)
    {
        var events = new List<EmbedEvent>();
        var ex = Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Start(options, consent, events.Add));
        Assert.Empty(events);
        Assert.False(string.IsNullOrEmpty(ex.Message));
        return ex;
    }

    [NativeFact]
    public void Version_is_not_empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(GaiaDeskEmbed.Version));
    }

    [NativeFact]
    public void Build_info_is_json_with_the_version()
    {
        var info = GaiaDeskEmbed.BuildInfo;
        Assert.False(string.IsNullOrWhiteSpace(info));
        using var doc = JsonDocument.Parse(info);
        Assert.Equal(GaiaDeskEmbed.Version, doc.RootElement.GetProperty("version").GetString());
    }

    [NativeFact]
    public void Start_without_consent_is_refused()
    {
        Assert.Equal("consent_required", Refused(new EmbedOptions { EmbedToken = Token }, consent: false).Code);
    }

    [NativeFact]
    public void Consent_is_checked_before_anything_else()
    {
        Assert.Equal("consent_required", Refused(new EmbedOptions { EmbedToken = "nope", Fps = 99 }, consent: false).Code);
    }

    [NativeTheory]
    [InlineData("")]
    [InlineData("gdk_live_x")]
    [InlineData("gdemb_0123")]
    [InlineData("gdemb_0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Bad_token_is_refused(string token)
    {
        Assert.Equal("bad_token", Refused(new EmbedOptions { EmbedToken = token }, consent: true).Code);
    }

    [NativeFact]
    public void Out_of_range_fps_is_refused()
    {
        Assert.Equal("bad_config", Refused(new EmbedOptions { EmbedToken = Token, Fps = 60 }, consent: true).Code);
    }

    [NativeFact]
    public void Non_https_server_is_refused()
    {
        Assert.Equal("bad_server", Refused(new EmbedOptions { EmbedToken = Token, Server = "http://example.com" }, consent: true).Code);
    }

    [NativeFact]
    public void Window_id_zero_is_refused()
    {
        Assert.Equal("bad_config", Refused(new EmbedOptions { EmbedToken = Token, WindowId = 0 }, consent: true).Code);
    }
}
