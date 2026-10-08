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

/// <summary>One support session. Start it with <see cref="Start"/>; end it with <see cref="Stop"/> or <see cref="Dispose"/>.</summary>
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
    /// answer to a consent dialog (<see cref="RequestConsent"/>, or your own):
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

    /// <summary>The native library's version ("0.1.0").</summary>
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
