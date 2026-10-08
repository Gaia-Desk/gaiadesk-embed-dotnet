# GaiaDesk.Embed

A **"Get help" button for your Windows desktop app**. Your customer clicks it and agrees; your
app's window is shared with **your** support team, who see it in the GaiaDesk console and, if you
allow it, guide it: click and type **inside your app only**, while it is in front.

- An idiomatic .NET 8 API (`GaiaDeskEmbed.Start(...)`, events as records, `IDisposable`) over the
  native `libgaiadesk_embed` library. Works with WinForms, WPF and anything else with a window.
- The library's own consent dialog, or yours: it **refuses to start without consent**.
- A visible **"Sharing with Acme support · Stop"** indicator for as long as anything is shared.
- Rects you mark (card numbers, passwords) are **painted out of every frame** before it leaves
  the machine.
- Only your app's window is captured unless you ask for the screen. Guided input is off unless
  you allow it **and** your backend made a `cobrowse` session; it lands only in your app's own
  windows, and the customer can pause it or stop the whole session at any time.

```sh
dotnet add package GaiaDesk.Embed
```

The package carries `runtimes/win-x64/native/gaiadesk_embed.dll`. The managed API also loads
`libgaiadesk_embed.dylib` (macOS) and `libgaiadesk_embed.so` (Linux) when you put them on the
library path, or in the directory named by `GAIADESK_EMBED_LIB_DIR`.

## Quick start

Your backend creates a support session with a secret API key (scope `support`) and hands the
app its `embed_token`:

```sh
curl -s -X POST https://api.gaiadesk.net/v1/support/sessions \
  -H "Authorization: Bearer $GAIADESK_API_KEY" -H "Content-Type: application/json" \
  -d '{"mode": "cobrowse", "customer": {"name": "Ada", "plan": "pro"}}'
# → {"id": "ss_…", "join_code": "123456789", "embed_token": "gdemb_…", …}
```

Never ship the secret key in the app: the embed token is all the app needs.

### WinForms

```csharp
using GaiaDesk.Embed;

GaiaDeskEmbed? help;

void GetHelp_Click(object? sender, EventArgs e)
{
    // The library's dialog; null means "no native dialog here": ask with your own.
    var agreed = GaiaDeskEmbed.RequestConsent("Acme")
        ?? MessageBox.Show(this, "Share this window with Acme support?", "Acme",
               MessageBoxButtons.YesNo) == DialogResult.Yes;
    if (!agreed) return;

    try
    {
        help = GaiaDeskEmbed.Start(
            new EmbedOptions { EmbedToken = embedToken, Company = "Acme", WindowId = Handle },
            consentGranted: true,
            OnHelpEvent,
            SynchronizationContext.Current); // events arrive on the UI thread
    }
    catch (GaiaDeskEmbedException ex)
    {
        statusLabel.Text = $"Could not start: {ex.Message} ({ex.Code})";
        return;
    }

    // Mask the card number panel.
    var s = cardPanel.RectangleToScreen(cardPanel.ClientRectangle);
    help.SetMaskedRects(new[] { EmbedRect.FromScreenRect(Handle, (s.X, s.Y, s.Width, s.Height)) });
}

void OnHelpEvent(EmbedEvent e)
{
    switch (e)
    {
        case CodeEvent c: statusLabel.Text = $"Support code {c.Code}"; break;
        case JoinedEvent j: statusLabel.Text = $"{j.Agent ?? "An agent"} is viewing this window"; break;
        case EndedEvent end: help?.Dispose(); help = null; break;
    }
}
```

Set `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` so WinForms coordinates are
physical pixels, the same units as `GetWindowRect` (which mask rects are relative to).

### WPF

```csharp
using System.Windows.Interop;
using GaiaDesk.Embed;

var hwnd = new WindowInteropHelper(this).Handle;
var agreed = GaiaDeskEmbed.RequestConsent("Acme")
    ?? MessageBox.Show(this, "Share this window with Acme support?", "Acme",
           MessageBoxButton.YesNo) == MessageBoxResult.Yes;
if (!agreed) return;

help = GaiaDeskEmbed.Start(
    new EmbedOptions { EmbedToken = embedToken, Company = "Acme", WindowId = hwnd },
    consentGranted: true,
    e => { if (e is CodeEvent c) Status.Text = $"Support code {c.Code}"; },
    SynchronizationContext.Current); // the Dispatcher's context

// PointToScreen returns physical pixels, as GetWindowRect does.
var tl = CardBox.PointToScreen(new Point(0, 0));
var br = CardBox.PointToScreen(new Point(CardBox.ActualWidth, CardBox.ActualHeight));
help.SetMaskedRects(new[] { EmbedRect.FromScreenRect(hwnd, tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y) });
```

Declare per-monitor DPI awareness in your `app.manifest` so those are true physical pixels.

## Consent

`Start` takes `consentGranted`: your user's answer to a consent dialog. The library refuses to
start without it (`GaiaDeskEmbedException` with `Code == "consent_required"`), before it looks
at anything else. Use the library's own dialog, `GaiaDeskEmbed.RequestConsent(company, capture,
guided)` (modal; call it on your UI thread): `true` agreed, `false` declined, `null` when there is
no native dialog here or it was called on the wrong thread, in which case show your own and pass
its answer. Never pass `true` without asking.

## Options

| `EmbedOptions` | |
|---|---|
| `EmbedToken` | Required. `gdemb_…`, from your backend. |
| `Server` | Default `https://gaiadesk.net` (`https://` only; `http://` for localhost). |
| `Company` | Your name, on the consent dialog and the indicator. |
| `Capture` | `CaptureMode.Window` (default: one window of this app), `App` (all its windows), `Screen` (opt-in: the whole display). |
| `WindowId` | The window to share: an HWND of **this** app (`Form.Handle`, `WindowInteropHelper.Handle`). Default: the app's frontmost. Another app's window is refused. |
| `Guided` | Allow guided input (default `false`). |
| `Fps` | 1 to 30 (default 15). |

Refusals throw `GaiaDeskEmbedException`; `Code` is stable: `consent_required`, `bad_token`,
`bad_config`, `bad_server`, …

## Masking

`SetMaskedRects` takes rects relative to the top-left of the shared window **as `GetWindowRect`
reports it** (so including the title bar and borders), in its units. `EmbedRect.FromScreenRect
(windowHandle, screenRect)` converts a screen rect for you (Windows only; no WinForms or WPF
reference needed). Each call replaces the earlier set; an empty list clears it. Call it again
after your layout changes.

## Guided mode

Guided input needs **both** sides to agree: the app passes `Guided = true`, and your backend
created the session with `"mode": "cobrowse"`. Then:

- input lands only in your app's own windows, and only while one of them is in front; anything
  else is refused and reported as an `ActionEvent` with `Applied == false` and a `Reason`
  (`not_focused`, …);
- the customer can pause it from the indicator, and your app can with `SetPaused(true)` /
  `SetPaused(false)`; a `ControlEvent` says whether input is allowed right now;
- tell your customer guided input is on: pass the same `guided` to `RequestConsent` so the dialog
  says so.

## Events

`onEvent` gets one `EmbedEvent` at a time: on your `SynchronizationContext` when you pass one,
otherwise on the library's own threads (do not block there; hop to your UI thread before touching
UI). `EndedEvent` is always the last event, exactly once.

| Event | Fields | |
|---|---|---|
| `StateEvent` | `State` | `starting`, `waiting`, `connected`, `ended` |
| `CodeEvent` | `Code`, `SessionId`, `Mode`, `ExpiresAt` | The support code to read to your agent; `Mode` is `view` or `cobrowse` |
| `JoinedEvent` | `Agent` | An agent joined (their email, when known) |
| `LeftEvent` | | The agent left |
| `ControlEvent` | `Allowed` | Whether guided input is allowed now |
| `ActionEvent` | `Action`, `Applied`, `Reason` | A guided action, and why it was not applied |
| `ErrorEvent` | `Code`, `Message` | A problem during the session |
| `EndedEvent` | `Reason` | `stopped`, `app_stopped`, `disconnected`, `expired`, `error` |
| `UnknownEvent` | `Type`, `Json` | An event type newer than this wrapper |

`Stop()` (or `Dispose()`) ends the session; it is safe to call more than once and from an event
handler. `GaiaDeskEmbed.Version` and `GaiaDeskEmbed.BuildInfo` describe the loaded native binary.

## Example

[`examples/HelpDeskDemo.WinForms`](examples/HelpDeskDemo.WinForms) is an "Acme Desk" app with a
Get help button, a masked red "card number" panel and a Buy button that counts clicks (try guided
mode on it). Windows only:

```powershell
./scripts/fetch-binary.ps1 0.1.0
$env:GAIADESK_EMBED_TOKEN = "gdemb_…"
dotnet run --project examples/HelpDeskDemo.WinForms
```

`GAIADESK_SERVER` picks the server, `GAIADESK_DEMO_GUIDED=1` allows guided input,
`GAIADESK_DEMO_AUTOSTART=1` presses Get help at launch; every event is printed to stdout as one
JSON line, and stdin `stop` / `pause` / `resume` drive the session (for automated tests).

## The native library

`gaiadesk_embed.dll` is closed source and ships from this repository's GitHub releases.
`scripts/fetch-binary.ps1 <version>` downloads
`https://github.com/Gaia-Desk/gaiadesk-embed-dotnet/releases/download/v<version>/gaiadesk_embed-win-x64.zip`
and puts the DLL in `runtimes/win-x64/native/`, where `dotnet pack` picks it up and project
references copy it beside your app.

## Building

```sh
dotnet build GaiaDesk.Embed.sln
dotnet test GaiaDesk.Embed.sln
```

Tests that need the native library skip when it cannot be loaded; point `GAIADESK_EMBED_LIB_DIR`
at a directory holding it to run them (they only exercise refusals and never start a session).
The WinForms example is not in the solution, so the library and tests build anywhere:
`dotnet build examples/HelpDeskDemo.WinForms` on Windows.

## Licence

The .NET wrapper (this repository's source) is MIT ([LICENSE](LICENSE)). The native library
binary is licensed under the GaiaDesk Native Binary Licence ([LICENSE-BINARY](LICENSE-BINARY)).
