// GaiaDeskEmbed: share your app's window with your support team, from a
// "Get help" button. A thin .NET layer over libgaiadesk_embed's C API.
//
// What the library itself enforces (no wrapper can skip it): it refuses to
// start without consent; its own "Sharing with <company>" indicator is up for
// as long as anything is shared; only this app's window is captured unless
// you ask for the screen; the rects you mask are painted out of every frame;
// input is off unless you allow Guided AND the session is "cobrowse", and
// then lands only in this app's own windows while it is in front.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace GaiaDesk.Embed;

/// <summary>One support session. Start it with <see cref="Start(EmbedOptions, bool, Action{EmbedEvent}, SynchronizationContext?)"/>; end it with <see cref="Stop"/> or <see cref="Dispose"/>.</summary>
public sealed class GaiaDeskEmbed : IDisposable
{
    // One callback for every session, held by a static field so the GC never
    // collects the delegate the library calls (the classic P/Invoke trap).
    // Each session is found again through its own GCHandle in user_data.
    private static readonly NativeMethods.GdEventFn s_onEvent = OnNativeEvent;
    private static readonly IntPtr s_onEventPtr = Marshal.GetFunctionPointerForDelegate(s_onEvent);

    private readonly object _gate = new();
    private readonly Action<EmbedEvent> _onEvent;
    private readonly SynchronizationContext? _context;
    private IntPtr _handle;
    // Keeps this object (and so _onEvent) alive while the library may call
    // back; freed after the "ended" event, the library's last call.
    private GCHandle _self;
    private int _ended;

    private GaiaDeskEmbed(Action<EmbedEvent> onEvent, SynchronizationContext? context)
    {
        _onEvent = onEvent;
        _context = context;
    }

    /// <summary>
    /// Start sharing. <paramref name="consentGranted"/> must be your user's
    /// answer to a consent dialog (<see cref="RequestConsent(string, CaptureMode, bool)"/>, or your own):
    /// the library refuses without it.
    /// </summary>
    /// <param name="options">The session: embed token, window, capture, guided, …</param>
    /// <param name="consentGranted">Whether your user agreed to share.</param>
    /// <param name="onEvent">Called for every event; <see cref="EndedEvent"/> is the last.</param>
    /// <param name="context">
    /// Where to deliver events: pass <c>SynchronizationContext.Current</c> from
    /// your UI thread (WinForms, WPF) to get them there. Null delivers them on
    /// the library's own threads; do not block there.
    /// </param>
    /// <exception cref="GaiaDeskEmbedException">The library refused (no consent, bad token, bad config, …).</exception>
    /// <exception cref="DllNotFoundException">The native library could not be loaded.</exception>
    public static GaiaDeskEmbed Start(EmbedOptions options, bool consentGranted, Action<EmbedEvent> onEvent, SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(onEvent);
        var json = options.ToConfigJson(consentGranted);
        var embed = new GaiaDeskEmbed(onEvent, context);
        embed._self = GCHandle.Alloc(embed, GCHandleType.Normal);
        var cfg = Marshal.StringToCoTaskMemUTF8(json);
        try
        {
            var callbacks = new NativeMethods.GdEmbedCallbacks { OnEvent = s_onEventPtr, UserData = GCHandle.ToIntPtr(embed._self) };
            // Lock so a fast "ended" cannot race the handle being stored.
            lock (embed._gate)
            {
                var h = NativeMethods.Start(cfg, callbacks);
                if (h == IntPtr.Zero)
                {
                    // Thread-local: read on this thread, right after the call.
                    var err = NativeMethods.Utf8(NativeMethods.LastError());
                    // Refused: the library keeps no reference to user_data.
                    embed._self.Free();
                    GC.SuppressFinalize(embed);
                    throw GaiaDeskEmbedException.FromLastError(err);
                }
                embed._handle = h;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(cfg);
        }
        return embed;
    }

    /// <summary>
    /// End the session (and free the library's handle). Safe to call more
    /// than once and from an event handler. Waits for the session to wind
    /// down, except from inside an event handler on the library's thread.
    /// </summary>
    public void Stop()
    {
        IntPtr h;
        lock (_gate)
        {
            h = _handle;
            _handle = IntPtr.Zero;
        }
        if (h != IntPtr.Zero)
        {
            NativeMethods.Stop(h);
        }
    }

    /// <summary>Same as <see cref="Stop"/>.</summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>Only reachable once the session has ended and nobody called Stop: free the handle.</summary>
    ~GaiaDeskEmbed()
    {
        Stop();
    }

    /// <summary>Whether the "ended" event has arrived.</summary>
    public bool HasEnded => Volatile.Read(ref _ended) != 0;

    /// <summary>
    /// Mask these rects (card numbers, passwords, …): relative to the top-left
    /// of the shared window as <c>GetWindowRect</c> reports it (see
    /// <see cref="EmbedRect.FromScreenRect(nint, double, double, double, double)"/>).
    /// Replaces any earlier set; an empty list clears them. Call again after
    /// your layout changes. Does nothing once stopped.
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException">The library rejected the rects.</exception>
    public void SetMaskedRects(IEnumerable<EmbedRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        var list = rects.Select(r => new NativeMethods.GdRect { X = r.X, Y = r.Y, W = r.Width, H = r.Height }).ToArray();
        lock (_gate)
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }
            var rc = NativeMethods.SetMaskRects(_handle, list.Length == 0 ? null : list, (UIntPtr)list.Length);
            if (rc != 0)
            {
                throw GaiaDeskEmbedException.FromLastError(NativeMethods.Utf8(NativeMethods.LastError()));
            }
        }
    }

    /// <summary>Pause (or resume) guided input, as the indicator's own button does.</summary>
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_handle != IntPtr.Zero)
            {
                _ = NativeMethods.SetPaused(_handle, paused ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// The library's default consent dialog (modal; call it on your UI thread).
    /// Returns whether the user agreed, or null when there is no native dialog
    /// here or this is the wrong thread: then show your own and pass its answer.
    /// </summary>
    public static bool? RequestConsent(string company, CaptureMode capture = CaptureMode.Window, bool guided = false)
    {
        ArgumentNullException.ThrowIfNull(company);
        var c = Marshal.StringToCoTaskMemUTF8(company);
        var k = Marshal.StringToCoTaskMemUTF8(EmbedOptions.CaptureName(capture));
        try
        {
            return NativeMethods.ShowConsent(c, k, guided ? 1 : 0) switch
            {
                1 => true,
                0 => false,
                _ => null, // -1 no native dialog, -2 wrong thread
            };
        }
        finally
        {
            Marshal.FreeCoTaskMem(c);
            Marshal.FreeCoTaskMem(k);
        }
    }

    /// <summary>
    /// The consent dialog in your look: <see cref="RequestConsent(string, CaptureMode, bool)"/>
    /// plus your colour and logo (the library's <c>gd_embed_show_consent_ex</c>, 0.2.0).
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException">
    /// <c>bad_config</c>: a colour or logo that is refused (no dialog is shown; this is never a decline).
    /// </exception>
    public static bool? RequestConsent(ConsentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var o = Marshal.StringToCoTaskMemUTF8(options.ToJson());
        try
        {
            switch (NativeMethods.ShowConsentEx(o))
            {
                case 1:
                    return true;
                case 0:
                    // Thread-local: read on this thread, right after the call.
                    var err = NativeMethods.Utf8(NativeMethods.LastError());
                    if (err.Length > 0 && GaiaDeskEmbedException.FromLastError(err) is { Code: "bad_config" } refused)
                    {
                        throw refused;
                    }
                    return false;
                default:
                    return null; // -1 no native dialog, -2 wrong thread
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(o);
        }
    }

    /// <summary>
    /// Start after the consent step: the decision's token and receipt go with the join,
    /// <see cref="EmbedOptions.Capture"/> becomes what the customer chose and
    /// <see cref="EmbedOptions.Guided"/> stays on only if they allowed it. Anything but
    /// <see cref="ConsentOutcome.Accepted"/> is refused (<c>consent_required</c>).
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException">The library refused.</exception>
    public static GaiaDeskEmbed Start(EmbedOptions options, ConsentDecision consent, Action<EmbedEvent> onEvent, SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(consent);
        return Start(Applying(consent, options), consent.Decision == ConsentOutcome.Accepted, onEvent, context);
    }

    /// <summary><paramref name="options"/> as Start with a decision starts it. Pure.</summary>
    internal static EmbedOptions Applying(ConsentDecision consent, EmbedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var o = options with { ConsentToken = consent.ConsentToken, ConsentReceipt = consent.Receipt };
        return consent.Choices is { } ch ? o with { Capture = ch.Capture, Guided = o.Guided && ch.Guided } : o;
    }

    // The consent dialog's events arrive on the calling thread, before it returns.
    private static readonly NativeMethods.GdEventFn s_onConsentEvent = OnConsentEvent;
    private static readonly IntPtr s_onConsentEventPtr = Marshal.GetFunctionPointerForDelegate(s_onConsentEvent);

    private static void OnConsentEvent(IntPtr userData, IntPtr eventJson)
    {
        if (userData == IntPtr.Zero || eventJson == IntPtr.Zero || GCHandle.FromIntPtr(userData).Target is not Action<EmbedEvent> handler)
        {
            return;
        }
        if (EmbedEvent.Parse(NativeMethods.Utf8(eventJson)) is { } ev)
        {
            try
            {
                handler(ev);
            }
            catch (Exception ex)
            {
                // Never unwind into native code.
                var info = ExceptionDispatchInfo.Capture(ex);
                ThreadPool.QueueUserWorkItem(static i => ((ExceptionDispatchInfo)i!).Throw(), info);
            }
        }
    }

    /// <summary>
    /// The consent step (libgaiadesk_embed 0.4.0): the library's dialog with a live preview of
    /// what support will see, the customer's choices and localized wording. Modal; call it on
    /// your UI thread (macOS: the main thread). <paramref name="onEvent"/> gets
    /// <see cref="ConsentShownEvent"/> and <see cref="ConsentAcceptedEvent"/> /
    /// <see cref="ConsentDeclinedEvent"/> on this thread, before it returns.
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException"><c>bad_config</c> (refused options; no dialog), <c>wrong_thread</c> (macOS).</exception>
    public static ConsentDecision Consent(ConsentStepOptions? options = null, Action<EmbedEvent>? onEvent = null)
    {
        var json = Marshal.StringToCoTaskMemUTF8((options ?? new ConsentStepOptions()).ToJson());
        var handle = onEvent is null ? default : GCHandle.Alloc(onEvent);
        try
        {
            var raw = NativeMethods.Consent(json, onEvent is null ? IntPtr.Zero : s_onConsentEventPtr, onEvent is null ? IntPtr.Zero : GCHandle.ToIntPtr(handle));
            return DecisionOrThrow(raw);
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
            Marshal.FreeCoTaskMem(json);
        }
    }

    /// <summary>
    /// One frame of what support would see. Reads <see cref="ConsentStepOptions.Capture"/>,
    /// <see cref="ConsentStepOptions.WindowId"/>, <see cref="ConsentStepOptions.ParentWindow"/> and
    /// <see cref="ConsentStepOptions.Masks"/>.
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException"><c>capture_unavailable</c> (no permission, no window), <c>bad_config</c>.</exception>
    public static ConsentPreview Preview(ConsentStepOptions? options = null)
    {
        var json = Marshal.StringToCoTaskMemUTF8((options ?? new ConsentStepOptions()).ToPreviewJson());
        try
        {
            var raw = NativeMethods.Preview(json);
            if (raw == IntPtr.Zero)
            {
                throw GaiaDeskEmbedException.FromLastError(NativeMethods.Utf8(NativeMethods.LastError()));
            }
            var w = System.Text.Json.JsonSerializer.Deserialize(NativeMethods.Utf8(raw), ConsentJson.Default.PreviewWire)!;
            return new ConsentPreview(Convert.FromBase64String(w.PngBase64), w.Width, w.Height, w.MaskedCount);
        }
        finally
        {
            Marshal.FreeCoTaskMem(json);
        }
    }

    /// <summary>
    /// Your own (or an embedded) consent UI's answer, made a receipt by the library (surface
    /// <c>embedded</c>). An accepted answer carries a <see cref="ConsentDecision.ConsentToken"/> for Start.
    /// </summary>
    /// <exception cref="GaiaDeskEmbedException"><c>bad_config</c> and the like.</exception>
    public static ConsentDecision RecordConsent(ConsentAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var json = Marshal.StringToCoTaskMemUTF8(answer.ToJson());
        try
        {
            return DecisionOrThrow(NativeMethods.RecordConsentEx(json));
        }
        finally
        {
            Marshal.FreeCoTaskMem(json);
        }
    }

    private static ConsentDecision DecisionOrThrow(IntPtr raw)
    {
        if (raw == IntPtr.Zero)
        {
            // Thread-local: read on this thread, right after the call.
            throw GaiaDeskEmbedException.FromLastError(NativeMethods.Utf8(NativeMethods.LastError()));
        }
        return ConsentDecision.Parse(NativeMethods.Utf8(raw));
    }

    /// <summary>The native library's version ("0.4.0").</summary>
    public static string Version => NativeMethods.Utf8(NativeMethods.Version());

    /// <summary>What the loaded native binary is (JSON: version, codec, os, arch, test_source).</summary>
    public static string BuildInfo => NativeMethods.Utf8(NativeMethods.BuildInfo());

    /// <summary>Whether the native library can be loaded here.</summary>
    public static bool IsNativeLibraryAvailable
    {
        get
        {
            try
            {
                _ = NativeMethods.Version();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }

    // Called from the library's threads.
    private static void OnNativeEvent(IntPtr userData, IntPtr eventJson)
    {
        if (userData == IntPtr.Zero || eventJson == IntPtr.Zero)
        {
            return;
        }
        var gch = GCHandle.FromIntPtr(userData);
        if (gch.Target is not GaiaDeskEmbed self)
        {
            return;
        }
        var ev = EmbedEvent.Parse(NativeMethods.Utf8(eventJson));
        if (ev is EndedEvent)
        {
            if (Interlocked.Exchange(ref self._ended, 1) != 0)
            {
                return;
            }
            // The library's last call: let go of the session object (the
            // closure below still holds it until the event is delivered).
            gch.Free();
        }
        if (ev is not null)
        {
            self.Deliver(ev);
        }
    }

    private void Deliver(EmbedEvent ev)
    {
        if (_context is { } ctx)
        {
            ctx.Post(static s =>
            {
                var (self, e) = ((GaiaDeskEmbed, EmbedEvent))s!;
                self._onEvent(e);
            }, (this, ev));
            return;
        }
        try
        {
            _onEvent(ev);
        }
        catch (Exception ex)
        {
            // Never unwind into native code: rethrow on a pool thread, where
            // it is an ordinary unhandled exception.
            var info = ExceptionDispatchInfo.Capture(ex);
            ThreadPool.QueueUserWorkItem(static i => ((ExceptionDispatchInfo)i!).Throw(), info);
        }
    }
}
