using System.Diagnostics;
using System.Text.Json;
using GaiaDesk.Embed;

namespace HelpDeskDemo;

internal sealed class MainForm : Form
{
    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private readonly TextBox _token = new() { PlaceholderText = "gdemb_… (from your backend)" };
    private readonly Button _getHelp = new() { Text = "Get help" };
    private readonly Button _stop = new() { Text = "Stop", Enabled = false };
    private readonly Button _buy = new() { Text = "Buy" };
    private readonly Label _clicks = new() { Text = "Buy clicked 0 times", AutoSize = true };
    private readonly Label _status = new() { Text = "Press Get help to share this window with Acme support.", AutoSize = false };
    private readonly Panel _card = new() { BackColor = Color.Red };
    private readonly Panel _blue = new() { BackColor = Color.RoyalBlue };
    private GaiaDeskEmbed? _help;
    private int _bought;

    public MainForm()
    {
        Text = "Acme Desk";
        ClientSize = new Size(640, 400);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.SeaGreen;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(200, 200);

        _card.SetBounds(40, 40, 200, 120);
        _card.Controls.Add(new Label { Text = "Card 4242 4242 4242 4242", ForeColor = Color.White, AutoSize = true, Location = new Point(10, 50) });
        _blue.SetBounds(400, 40, 200, 120);
        _buy.SetBounds(260, 180, 120, 40);
        _token.SetBounds(40, 240, 420, 24);
        _token.Text = Env("GAIADESK_EMBED_TOKEN") ?? string.Empty;
        _getHelp.SetBounds(470, 238, 130, 30);
        _stop.SetBounds(470, 274, 130, 30);
        _clicks.Location = new Point(40, 280);
        _status.SetBounds(40, 320, 560, 60);
        _status.ForeColor = Color.White;
        Controls.AddRange(new Control[] { _card, _blue, _buy, _token, _getHelp, _stop, _clicks, _status });

        _getHelp.Click += (_, _) => GetHelp();
        _stop.Click += (_, _) => _help?.Stop();
        _buy.Click += (_, _) =>
        {
            _bought++;
            _clicks.Text = $"Buy clicked {_bought} times";
            Emit(new() { ["type"] = "demo", ["bought"] = _bought });
        };
        // Window-relative rects only change with the layout.
        Resize += (_, _) => UpdateMask();
        Shown += (_, _) => OnShownFirst();
        FormClosing += (_, _) => _help?.Dispose();
    }

    private void OnShownFirst()
    {
        string build;
        try
        {
            build = GaiaDeskEmbed.BuildInfo;
        }
        catch (DllNotFoundException)
        {
            build = "";
            _status.Text = "gaiadesk_embed.dll was not found: run scripts/fetch-binary.ps1 <version> first.";
        }
        // Where things are, in the window's GetWindowRect space (tests sample the picture there).
        Emit(new()
        {
            ["type"] = "demo",
            ["window"] = (long)Handle,
            ["build"] = build,
            ["pid"] = Environment.ProcessId,
            ["frame"] = new[] { Width, Height },
            ["card"] = Box(_card),
            ["blue"] = Box(_blue),
            ["buy"] = Box(_buy),
        });
        if (Env("GAIADESK_DEMO_AUTOSTART") == "1")
        {
            var t = new System.Windows.Forms.Timer { Interval = 500 };
            t.Tick += (_, _) =>
            {
                t.Dispose();
                GetHelp();
            };
            t.Start();
        }
    }

    private EmbedRect RectOf(Control c)
    {
        var s = c.RectangleToScreen(c.ClientRectangle);
        return EmbedRect.FromScreenRect(Handle, (s.X, s.Y, s.Width, s.Height));
    }

    private double[] Box(Control c)
    {
        var r = RectOf(c);
        return new[] { r.X, r.Y, r.Width, r.Height };
    }

    private void UpdateMask()
    {
        try
        {
            _help?.SetMaskedRects(new[] { RectOf(_card) });
        }
        catch (GaiaDeskEmbedException e)
        {
            Emit(new() { ["type"] = "demo", ["mask_error"] = e.Code });
        }
    }

    private void GetHelp()
    {
        if (_help is not null)
        {
            return;
        }
        var guided = Env("GAIADESK_DEMO_GUIDED") == "1";
        bool agreed;
        if (Env("GAIADESK_DEMO_TEST_CONSENT") == "1")
        {
            agreed = true; // automated tests only: never ship this
        }
        else
        {
            bool? answer;
            try
            {
                answer = GaiaDeskEmbed.RequestConsent("Acme", CaptureMode.Window, guided);
            }
            catch (DllNotFoundException)
            {
                _status.Text = "gaiadesk_embed.dll was not found: run scripts/fetch-binary.ps1 <version> first.";
                return;
            }
            // null: no native dialog here, so ask with our own.
            agreed = answer ?? MessageBox.Show(this,
                "Share this window with Acme support?\n\nThey will see this window" + (guided ? " and may click and type in it while it is in front" : "") + ". You can stop at any time.",
                "Acme support", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        if (!agreed)
        {
            _status.Text = "Not shared.";
            Emit(new() { ["type"] = "demo", ["consent"] = false });
            return;
        }

        var token = (Env("GAIADESK_EMBED_TOKEN") ?? _token.Text).Trim();
        var options = new EmbedOptions
        {
            EmbedToken = token,
            Server = Env("GAIADESK_SERVER"),
            Company = "Acme",
            WindowId = Handle,
            Guided = guided,
            // The card's mask goes in the start options: it covers the very first frame.
            Masks = new[] { RectOf(_card) },
        };
        try
        {
            _help = GaiaDeskEmbed.Start(options, consentGranted: true, OnEvent, SynchronizationContext.Current);
            _getHelp.Enabled = false;
            _stop.Enabled = true;
        }
        catch (GaiaDeskEmbedException e)
        {
            _status.Text = $"Could not start: {e.Message}";
            Emit(new() { ["type"] = "demo", ["start_error"] = e.Code });
        }
    }

    // On the UI thread (we passed SynchronizationContext.Current).
    private void OnEvent(EmbedEvent e)
    {
        switch (e)
        {
            case CodeEvent c:
                _status.Text = $"Support code {c.Code} — read it to the Acme agent.";
                Emit(new() { ["type"] = "code", ["code"] = c.Code, ["session_id"] = c.SessionId, ["mode"] = c.Mode, ["expires_at"] = c.ExpiresAt?.ToUnixTimeSeconds() });
                break;
            case JoinedEvent j:
                _status.Text = $"{j.Agent ?? "An agent"} is viewing this window.";
                Emit(new() { ["type"] = "joined", ["agent"] = j.Agent });
                break;
            case LeftEvent:
                Emit(new() { ["type"] = "left" });
                break;
            case StateEvent s:
                Emit(new() { ["type"] = "state", ["state"] = s.State });
                break;
            case ControlEvent c:
                Emit(new() { ["type"] = "control", ["allowed"] = c.Allowed });
                break;
            case ActionEvent a:
                Emit(new() { ["type"] = "action", ["action"] = a.Action, ["applied"] = a.Applied, ["reason"] = a.Reason });
                break;
            case ErrorEvent err:
                Emit(new() { ["type"] = "error", ["code"] = err.Code, ["message"] = err.Message });
                break;
            case EndedEvent end:
                _status.Text = $"Sharing ended ({end.Reason}).";
                _help?.Dispose();
                _help = null;
                _getHelp.Enabled = true;
                _stop.Enabled = false;
                Emit(new() { ["type"] = "ended", ["reason"] = end.Reason });
                if (Env("GAIADESK_DEMO_AUTOSTART") == "1")
                {
                    var t = new System.Windows.Forms.Timer { Interval = 300 };
                    t.Tick += (_, _) => Close();
                    t.Start();
                }
                break;
            case UnknownEvent u:
                Console.Out.WriteLine(u.Json);
                Console.Out.Flush();
                break;
        }
    }

    /// <summary>stdin commands (tests): "stop", "pause", "resume".</summary>
    internal void Command(string cmd)
    {
        if (cmd.Contains("pause", StringComparison.Ordinal))
        {
            _help?.SetPaused(true);
        }
        if (cmd.Contains("resume", StringComparison.Ordinal))
        {
            _help?.SetPaused(false);
        }
        if (cmd.Contains("stop", StringComparison.Ordinal))
        {
            _help?.Stop();
        }
    }

    private static void Emit(Dictionary<string, object?> obj)
    {
        var line = JsonSerializer.Serialize(new SortedDictionary<string, object?>(obj, StringComparer.Ordinal));
        Console.Out.WriteLine(line);
        Console.Out.Flush();
        Debug.WriteLine(line);
    }
}
