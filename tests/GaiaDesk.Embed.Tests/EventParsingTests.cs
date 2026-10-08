using Xunit;

namespace GaiaDesk.Embed.Tests;

public class EventParsingTests
{
    [Theory]
    [InlineData("starting")]
    [InlineData("waiting")]
    [InlineData("connected")]
    [InlineData("ended")]
    public void State(string state)
    {
        var e = Assert.IsType<StateEvent>(EmbedEvent.Parse($"{{\"type\":\"state\",\"state\":\"{state}\"}}"));
        Assert.Equal(state, e.State);
        Assert.Equal("state", e.Type);
    }

    [Fact]
    public void Code()
    {
        var e = Assert.IsType<CodeEvent>(EmbedEvent.Parse("{\"type\":\"code\",\"code\":\"123456789\",\"session_id\":\"ss_abc\",\"mode\":\"cobrowse\",\"expires_at\":1790000000}"));
        Assert.Equal(new CodeEvent("123456789", "ss_abc", "cobrowse", DateTimeOffset.FromUnixTimeSeconds(1790000000)), e);
        Assert.Equal("code", e.Type);
    }

    [Fact]
    public void Code_without_expiry()
    {
        var e = Assert.IsType<CodeEvent>(EmbedEvent.Parse("{\"type\":\"code\",\"code\":\"1\",\"session_id\":\"ss_x\",\"mode\":\"view\"}"));
        Assert.Null(e.ExpiresAt);
    }

    [Fact]
    public void Joined_with_and_without_agent()
    {
        Assert.Equal(new JoinedEvent("agent@company.com"), EmbedEvent.Parse("{\"type\":\"joined\",\"agent\":\"agent@company.com\"}"));
        Assert.Equal(new JoinedEvent(null), EmbedEvent.Parse("{\"type\":\"joined\",\"agent\":null}"));
    }

    [Fact]
    public void Left()
    {
        var e = Assert.IsType<LeftEvent>(EmbedEvent.Parse("{\"type\":\"left\"}"));
        Assert.Equal("left", e.Type);
    }

    [Fact]
    public void Control()
    {
        Assert.Equal(new ControlEvent(true), EmbedEvent.Parse("{\"type\":\"control\",\"allowed\":true}"));
        Assert.Equal(new ControlEvent(false), EmbedEvent.Parse("{\"type\":\"control\",\"allowed\":false}"));
    }

    [Fact]
    public void Action()
    {
        Assert.Equal(new ActionEvent("click", false, "not_focused"), EmbedEvent.Parse("{\"type\":\"action\",\"action\":\"click\",\"applied\":false,\"reason\":\"not_focused\"}"));
        Assert.Equal(new ActionEvent("type", true, null), EmbedEvent.Parse("{\"type\":\"action\",\"action\":\"type\",\"applied\":true,\"reason\":null}"));
    }

    [Fact]
    public void Error()
    {
        Assert.Equal(new ErrorEvent("capture_failed", "no window"), EmbedEvent.Parse("{\"type\":\"error\",\"code\":\"capture_failed\",\"message\":\"no window\"}"));
    }

    [Theory]
    [InlineData("stopped")]
    [InlineData("app_stopped")]
    [InlineData("disconnected")]
    [InlineData("expired")]
    [InlineData("error")]
    public void Ended(string reason)
    {
        var e = Assert.IsType<EndedEvent>(EmbedEvent.Parse($"{{\"reason\":\"{reason}\",\"type\":\"ended\"}}"));
        Assert.Equal(reason, e.Reason);
        Assert.Equal("ended", e.Type);
    }

    [Fact]
    public void Unknown_type_is_kept_raw()
    {
        const string json = "{\"type\":\"future\",\"x\":1}";
        var e = Assert.IsType<UnknownEvent>(EmbedEvent.Parse(json));
        Assert.Equal("future", e.Type);
        Assert.Equal(json, e.Json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"no_type\":1}")]
    public void Not_an_event(string json)
    {
        Assert.Null(EmbedEvent.Parse(json));
    }
}
