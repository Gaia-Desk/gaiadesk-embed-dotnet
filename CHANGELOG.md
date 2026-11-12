# Changelog

## 0.3.0

- `libgaiadesk_embed` 0.3.0: `EmbedOptions.ParentWindow` (the calling window's HWND; the indicator
  is placed clear of it), `EmbedOptions.IndicatorPosition` (a screen corner) and
  `EmbedOptions.IndicatorOrigin` (where the customer last dragged the indicator).
- `ConsentOptions.Masked` (the dialog says private fields stay hidden) and
  `ConsentOptions.ParentWindow` (the dialog is owned by your window, never behind it).
- New `IndicatorMovedEvent` (`X`, `Y`): keep it and pass it back as `IndicatorOrigin`.
- `EndedEvent.Reason` can be `customer_cancelled`: Cancel on the indicator, or `Stop`, before any
  agent was admitted.

## 0.2.0

- `libgaiadesk_embed` 0.2.0: white-label branding. `EmbedOptions.BrandColor` (`#RRGGBB`) and
  `EmbedOptions.LogoPng` / `LogoPngBase64` (a PNG, at most 256 KiB) put your colour and logo on
  the "Sharing with <company>" indicator; `GaiaDeskEmbed.RequestConsent(ConsentOptions)` (the
  library's `gd_embed_show_consent_ex`) shows the consent dialog in your look. A colour that is not
  `#RRGGBB` throws `GaiaDeskEmbedException` (`bad_config`) before the native call; a colour or
  logo the library refuses throws `bad_config` instead of reading as a decline.
- No customer-visible text names GaiaDesk any more (connect errors).

## 0.1.0

- First release: `GaiaDeskEmbed.Start` / `Stop` / `Dispose`, `SetMaskedRects`, `SetPaused`,
  `RequestConsent`, `Version`, `BuildInfo`, over `libgaiadesk_embed` 0.1.0.
- Events as records (`StateEvent`, `CodeEvent`, `JoinedEvent`, `LeftEvent`, `ControlEvent`,
  `ActionEvent`, `ErrorEvent`, `EndedEvent`, `UnknownEvent`), delivered on a
  `SynchronizationContext` when given.
- `EmbedRect.FromScreenRect` for masking without a WinForms or WPF reference.
- `GAIADESK_EMBED_LIB_DIR` to load the native library from a directory of your choice.
- WinForms example (`examples/HelpDeskDemo.WinForms`).
