# Changelog

## 0.1.0

- First release: `GaiaDeskEmbed.Start` / `Stop` / `Dispose`, `SetMaskedRects`, `SetPaused`,
  `RequestConsent`, `Version`, `BuildInfo`, over `libgaiadesk_embed` 0.1.0.
- Events as records (`StateEvent`, `CodeEvent`, `JoinedEvent`, `LeftEvent`, `ControlEvent`,
  `ActionEvent`, `ErrorEvent`, `EndedEvent`, `UnknownEvent`), delivered on a
  `SynchronizationContext` when given.
- `EmbedRect.FromScreenRect` for masking without a WinForms or WPF reference.
- `GAIADESK_EMBED_LIB_DIR` to load the native library from a directory of your choice.
- WinForms example (`examples/HelpDeskDemo.WinForms`).
