using System.Text.Json;
using Xunit;

namespace GaiaDesk.Embed.Tests;

/// <summary>The consent step (libgaiadesk_embed 0.4.0). Nothing here shows a dialog.</summary>
public class ConsentTests
{
    private const string Token = "gdemb_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static ConsentAnswer Answer(ConsentOutcome decision, bool guided = false) => new()
    {
        Decision = decision,
        Choices = new ConsentChoices { Guided = guided, Capture = CaptureMode.Window },
        Wording = new Dictionary<string, string> { ["title"] = "Share with Acme support?", ["accept"] = "Share" },
        WordingVersion = "acme-1",
        Locale = "en",
        Company = "Acme",
        Capture = CaptureMode.Window,
        AppVersion = "2.4.1",
    };

    [Fact]
    public void Unset_options_are_left_out()
    {
        Assert.Equal("{}", new ConsentStepOptions().ToJson());
    }

    [Fact]
    public void Options_use_the_library_keys()
    {
        var o = Parse(new ConsentStepOptions
        {
            Company = "Acme",
            Capture = CaptureMode.Screen,
            Guided = true,
            Masked = true,
            BrandColor = "#112233",
            LogoPng = new byte[] { 0x89, 0x50 },
            ParentWindow = (nint)140230000,
            Theme = new ConsentTheme { Appearance = ConsentAppearance.Dark, Radius = 12, FontFamily = "Inter", ButtonStyle = ConsentButtonStyle.Pill },
            Locale = "fr",
            Wording = new Dictionary<string, string> { ["title"] = "Partager ?" },
            WordingVersion = "acme-2026-11",
            Choices = new ConsentOffers { OfferGuided = true, OfferLogs = false },
            PrivacyUrl = "https://acme.test/privacy",
            TermsUrl = "https://acme.test/terms",
            TimeoutSeconds = 60,
            Preview = false,
            Server = "https://gaiadesk.net",
            EmbedToken = Token,
            SessionId = "ss_1",
            AppVersion = "2.4.1",
            WindowId = (nint)42,
            Masks = new[] { new EmbedRect(1, 2, 3, 4) },
        }.ToJson());
        Assert.Equal(
            new[] { "company", "capture", "guided", "masked", "brand_color", "logo_png_base64", "parent_window", "theme", "locale", "wording", "wording_version", "choices", "privacy_url", "terms_url", "timeout_seconds", "preview", "server", "embed_token", "session_id", "app_version", "window_id", "masks" },
            o.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("screen", o.GetProperty("capture").GetString());
        Assert.Equal("iVA=", o.GetProperty("logo_png_base64").GetString());
        Assert.Equal(140230000UL, o.GetProperty("parent_window").GetUInt64());
        Assert.Equal("{\"appearance\":\"dark\",\"radius\":12,\"font_family\":\"Inter\",\"button_style\":\"pill\"}", o.GetProperty("theme").GetRawText());
        Assert.Equal("{\"offer_guided\":true,\"offer_logs\":false}", o.GetProperty("choices").GetRawText());
        Assert.Equal("{\"title\":\"Partager ?\"}", o.GetProperty("wording").GetRawText());
        Assert.Equal("[{\"x\":1,\"y\":2,\"w\":3,\"h\":4}]", o.GetProperty("masks").GetRawText());
    }

    [Fact]
    public void Preview_sends_only_what_it_reads()
    {
        var json = new ConsentStepOptions { Company = "Acme", Capture = CaptureMode.App, WindowId = (nint)7, Locale = "fr" }.ToPreviewJson();
        Assert.Equal("{\"capture\":\"app\",\"window_id\":7}", json);
    }

    [Fact]
    public void Decisions_parse()
    {
        Assert.Equal(new ConsentDecision { Decision = ConsentOutcome.Unavailable }, ConsentDecision.Parse("{\"decision\":\"unavailable\"}"));
        var d = ConsentDecision.Parse("{\"decision\":\"timeout\",\"choices\":{\"guided\":false,\"share_logs\":true,\"capture\":\"screen\"},\"receipt\":{\"id\":\"cr_x\",\"decision\":\"timeout\",\"os_user\":null,\"choices\":{\"guided\":false,\"share_logs\":true,\"capture\":\"screen\"}}}");
        Assert.Equal(ConsentOutcome.Timeout, d.Decision);
        Assert.Equal(new ConsentChoices { ShareLogs = true, Capture = CaptureMode.Screen }, d.Choices);
        Assert.Null(d.ConsentToken);
        Assert.Equal("cr_x", d.Receipt!.Id);
        Assert.Null(d.Receipt.OsUser);
    }

    [Fact]
    public void Start_with_a_decision_runs_with_the_customers_choices()
    {
        var receipt = new ConsentReceipt { Id = "cr_1", Decision = "accepted", Choices = new ConsentChoices { Capture = CaptureMode.Window } };
        var decision = new ConsentDecision { Decision = ConsentOutcome.Accepted, Choices = receipt.Choices, ConsentToken = "gdc_1", Receipt = receipt };
        var o = GaiaDeskEmbed.Applying(decision, new EmbedOptions { EmbedToken = Token, Capture = CaptureMode.Screen, Guided = true });
        Assert.Equal(CaptureMode.Window, o.Capture);
        Assert.False(o.Guided);
        var json = Parse(o.ToConfigJson(true));
        Assert.Equal("gdc_1", json.GetProperty("consent_token").GetString());
        Assert.Equal("cr_1", json.GetProperty("consent_receipt").GetProperty("id").GetString());
        Assert.Equal("window", json.GetProperty("consent_receipt").GetProperty("choices").GetProperty("capture").GetString());
        Assert.True(GaiaDeskEmbed.Applying(decision with { Choices = new ConsentChoices { Guided = true } }, new EmbedOptions { EmbedToken = Token, Guided = true }).Guided);
        Assert.DoesNotContain("consent_", new EmbedOptions { EmbedToken = Token }.ToConfigJson(true).Replace("consent_granted", ""));
    }

    [Fact]
    public void Consent_events_parse()
    {
        var ch = new ConsentChoices { Guided = true, ShareLogs = true, Capture = CaptureMode.Screen };
        Assert.Equal(new ConsentShownEvent("fr", "acme-1"), EmbedEvent.Parse("{\"type\":\"consent_shown\",\"locale\":\"fr\",\"wording_version\":\"acme-1\"}"));
        Assert.Equal(new ConsentAcceptedEvent(ch), EmbedEvent.Parse("{\"type\":\"consent_accepted\",\"choices\":{\"guided\":true,\"share_logs\":true,\"capture\":\"screen\"}}"));
        Assert.Equal(new ConsentDeclinedEvent("timeout"), EmbedEvent.Parse("{\"type\":\"consent_declined\",\"reason\":\"timeout\"}"));
        Assert.Equal("consent_declined", new ConsentDeclinedEvent("declined").Type);
        Assert.Equal(new EndedEvent("customer_declined"), EmbedEvent.Parse("{\"type\":\"ended\",\"reason\":\"customer_declined\"}"));
        Assert.Equal(new EndedEvent("consent_timeout"), EmbedEvent.Parse("{\"type\":\"ended\",\"reason\":\"consent_timeout\"}"));
    }

    [Fact]
    public void The_waiting_state_carries_the_sessions_choices()
    {
        var e = Assert.IsType<StateEvent>(EmbedEvent.Parse("{\"type\":\"state\",\"state\":\"waiting\",\"choices\":{\"guided\":false,\"share_logs\":true,\"capture\":\"window\"}}"));
        Assert.Equal("waiting", e.State);
        Assert.Equal(new ConsentChoices { ShareLogs = true, Capture = CaptureMode.Window }, e.Choices);
        Assert.Null(Assert.IsType<StateEvent>(EmbedEvent.Parse("{\"type\":\"state\",\"state\":\"waiting\"}")).Choices);
    }

    // ---- against the native library

    [NativeTheory]
    [InlineData("teal", null)]
    [InlineData(null, 99)]
    public void Bad_options_are_bad_config_and_show_no_dialog(string? color, int? radius)
    {
        var events = new List<EmbedEvent>();
        var o = new ConsentStepOptions { BrandColor = color, Theme = radius is { } r ? new ConsentTheme { Radius = r } : null };
        Assert.Equal("bad_config", Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Consent(o, events.Add)).Code);
        Assert.Empty(events);
    }

    [NativeFact]
    public void Consent_off_the_main_thread_is_wrong_thread_on_macos()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return; // elsewhere it would show the dialog
        }
        // xunit runs tests off the main thread.
        Assert.Equal("wrong_thread", Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Consent(new ConsentStepOptions { Company = "Acme" })).Code);
    }

    [NativeFact]
    public void Preview_refuses_bad_masks()
    {
        var o = new ConsentStepOptions { Masks = new[] { new EmbedRect(0, 0, -1, 1) } };
        Assert.Equal("bad_config", Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Preview(o)).Code);
    }

    [NativeFact]
    public void An_embedded_acceptance_becomes_a_receipt_and_a_token()
    {
        var d = GaiaDeskEmbed.RecordConsent(Answer(ConsentOutcome.Accepted, guided: true));
        Assert.Equal(ConsentOutcome.Accepted, d.Decision);
        Assert.Equal(new ConsentChoices { Guided = true, Capture = CaptureMode.Window }, d.Choices);
        Assert.StartsWith("gdc_", d.ConsentToken);
        var r = d.Receipt!;
        Assert.Matches("^cr_[0-9A-Z]{26}$", r.Id);
        Assert.Equal("accepted", r.Decision);
        Assert.Equal("embedded", r.Surface);
        Assert.Equal("acme-1", r.WordingVersion);
        Assert.Matches("^[0-9a-f]{64}$", r.WordingSha256);
        Assert.Equal("2.4.1", r.AppVersion);
        Assert.Equal($"libgaiadesk_embed/{GaiaDeskEmbed.Version}", r.Sdk);
    }

    [NativeFact]
    public void An_embedded_decline_has_a_receipt_but_no_token_and_does_not_start()
    {
        var d = GaiaDeskEmbed.RecordConsent(Answer(ConsentOutcome.Declined));
        Assert.Equal(ConsentOutcome.Declined, d.Decision);
        Assert.Null(d.ConsentToken);
        Assert.Equal("declined", d.Receipt!.Decision);
        var ex = Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Start(new EmbedOptions { EmbedToken = Token }, d, _ => { }));
        Assert.Equal("consent_required", ex.Code);
    }

    [NativeFact]
    public void An_answer_whose_capture_differs_from_the_choice_is_refused()
    {
        Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.RecordConsent(Answer(ConsentOutcome.Accepted) with { Capture = CaptureMode.Screen }));
    }

    [NativeFact]
    public void Start_reads_the_receipt()
    {
        var d = GaiaDeskEmbed.RecordConsent(Answer(ConsentOutcome.Accepted));
        var withReceipt = new EmbedOptions { EmbedToken = Token, ConsentToken = d.ConsentToken, ConsentReceipt = d.Receipt };
        // The customer chose view only: guided is refused; so is another capture.
        Assert.Equal("consent_required", Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Start(withReceipt with { Guided = true }, true, _ => { })).Code);
        Assert.Equal("consent_required", Assert.Throws<GaiaDeskEmbedException>(() => GaiaDeskEmbed.Start(withReceipt with { Capture = CaptureMode.Screen }, true, _ => { })).Code);
    }
}
