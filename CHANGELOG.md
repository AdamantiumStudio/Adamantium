# Changelog

Notable changes to the Adamantium Engine packages, in the form of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
All packages share one version.

## Unreleased

### Added

- `Win32Interop.LoadImage` and `GetModuleHandle`, with `LoadImageType` and `LoadImageFlags`: an icon loaded from a
  module's resources, such as the application's own.
- `Win32Interop.EnumDisplayMonitors`, `GetMonitorInfo` and `GetWindowPlacement`, with `MONITORINFOEX`,
  `WINDOWPLACEMENT` and `MonitorEnumProc`: the monitors and their work areas, and a window's restored rectangle.
- `TextShaper.Shape` in `Adamantium.Fonts.Shaping`: text to positioned glyphs with the font's OpenType substitutions and
  positioning applied the way HarfBuzz applies them - lookups of every enabled feature in the font's order, lookup
  flags and mark filtering sets, ligatures across marks, mark attachment to bases, ligatures and other marks, cursive
  attachment, contextual and chained rules of every format. Features are set per range of text (`FontFeature`,
  parsed from `liga=0`, `ss01`, `kern[3:5]=0`), the script and language pick the font's language system, combining
  marks are reordered and composed when the font has the precomposed glyph, and each glyph keeps the UTF-16 offset of
  the text it came from.
- `TextLayout.GetCaretStops` and `CaretStop`: the caret position before each character and after the last, with the
  character's width; the characters of one glyph, such as a ligature, share its width. `GlyphWordData` carries its
  `PenX`, `Advance` and positioning offsets; `FontAtlas.RequestAsync` takes glyphs as well as text.
- `AttributedText` and `TextAttributes`: text with features, language, color, background and lines
  (`TextDecorations`: underline, strikethrough, squiggle) over ranges, laid out by `TextLayout.ProcessText`. Each glyph
  takes the color of its text on both text paths, a glyph without one the element's (`FontItem.HasOwnColor`).
  `GetAdornments` gives the backgrounds and lines to draw, `GetRangeRects` the rectangles of a range line by line (for a
  selection), and `GetLine` / `LineCount` the visual lines with their top and baseline. The fonts' underline and
  strikeout metrics are read (`IFont.UnderlinePosition`, `UnderlineThickness`, `StrikeoutPosition`, `StrikeoutSize`).
- `TextShaper.Shape` without a script splits the text where its script changes and shapes each part with its own, so
  Latin and Cyrillic in one line each get their own forms.
- `TextBoundaries` in `Adamantium.Fonts.Text`: grapheme and word boundaries by Unicode Standard Annex #29, emoji
  sequences, flags and Indic conjuncts included; it passes all of Unicode's `GraphemeBreakTest` and `WordBreakTest`.
  `TextLayout` uses them: `NextCaretStop` / `PreviousCaretStop` step over a whole grapheme, `HitTest` gives the grapheme
  under a point and where a click there puts the caret (`TextHit`), and `GetWordAt`, `NextWordStop` and
  `PreviousWordStop` give words for a double click and Ctrl+arrows.
- `TextBoundaries.LineBreaks`: where a line may or must end, by Unicode Standard Annex #14; it passes all of Unicode's
  `LineBreakTest`.
- Typed font features: `FontFeature.Ligatures`, `Capitals`, `Numerals`, `Position` and `Kerning` (`FontFeature.Ligatures.Off`,
  `FontFeature.Numerals.Tabular`), `StylisticSet(n)`, `CharacterVariant(n, value)`, `StylisticAlternates` and `Swash`.
  `FontFeature.TryParseList` checks a list such as `liga=0, ss01, cv05=2` against the OpenType feature registry: a
  lowercase tag the registry does not have is an error naming the tag most likely meant, and a private feature needs an
  uppercase letter in its tag. `ParseList` throws with the same message.
- `TextAttributes.FontSize`: a size per range of text. Each line is as tall as its largest text and its glyphs share its
  baseline; `GetLine`, `HitTest`, `GetRangeRects` and `GetAdornments` follow each line's own height.
  `GlyphWordData.FontSize` is the size a glyph is set at.
- `TextLayout.TabSize`: a tab moves the pen to the next tab stop, a multiple of that many spaces from the line's start
  (4 by default), and draws nothing. Before, it was laid out as the font's glyph for it, usually the `.notdef` box.

### Fixed

- Thin slivers of other letters at the edges of glyphs, mostly at fractional sizes. Atlas cells lie edge to edge and a
  glyph's quad reached the very edge of its cell, so the texture filter mixed in the neighboring cell's field. The quad
  now stops half a texel inside its cell.
- Text in a second font drew the first font's letters. `FontAtlasStore` kept one atlas per set of rasterization
  parameters, whatever the font, and glyphs are found in it by index, so a glyph of the second font got the picture the
  first font has at that index. Each font now has its own atlas: `FontAtlasStore.GetOrCreateFrom` and the `FontAtlas`
  constructor take the `IFont`, which the atlas rasterizes instead of the typeface's first font.
- A swapchain rebuild that failed - out of device memory, say - left the presenter without surfaces, and the next frame
  drew into them and crashed the process. `GraphicsPresenter.IsReady` now says whether the surfaces exist,
  `GraphicsDevice.BeginDraw` skips the frame while they do not, and the failed rebuild reports `OutOfDate`, so it is
  retried on the next frame. A retry no longer destroys the swapchain objects a second time.
- `ErrorOutOfDeviceMemory` when several applications, or an application and its designer previews, ran at once on a GPU
  without Resizable BAR. The device-local host-visible window (about 214 MB on such cards) is shared by every process;
  when it is full, buffers that want it now take host-visible system memory instead of failing.
- Letters of one height no longer jump a pixel apart within a line. `TextLayout` rounded each glyph's top and bottom to
  a whole pixel on their own, so a round letter, a fraction of a pixel above a flat one, could land a whole pixel above
  it. Only the line's baseline is rounded now; every glyph stands exactly where the font draws it.
- CFF delta arrays (`BlueValues`, `OtherBlues`, `StemSnapH` and the rest) were decoded backwards: each value was taken
  as the difference from the previous one instead of their sum.
- Every base glyph of a mark-to-base subtable got the anchors of the first one, so a mark sat where it belonged on one
  letter only. The GDEF mark glyph sets were read from the wrong offset.
- `Font.GetGlyphByIndex` returned the glyph at that position among the glyphs `cmap` maps, not the glyph with that
  index, so any glyph reached through a substitution - a ligature, a small capital - came back as another one.

### Changed

- Lines are as tall as the font says: ascent plus descent plus line gap (`IFont.LineAscent`, `LineDescent`, `LineGap`),
  the baseline half the gap and the ascent below the line's top, taken as HarfBuzz and the browsers take them (the
  typographic metrics when the font sets USE_TYPO_METRICS, the horizontal header's otherwise). Before, the baseline
  stood almost at the bottom of the line and descenders hung below it, so a background or a selection stopped at the
  baseline; and a font with no line gap was laid out with a gap as tall as the font - Source Sans and Cascadia Code at
  more than twice their size. Segoe UI's lines are now 1.33 of its size apart, as on Windows, instead of 1.13.
- `TextLayout` with `TextWrapping.WrapByWords` wraps where Unicode allows a line to end, not only at spaces: after a
  hyphen, between ideographs, never before a closing parenthesis or a comma, never at a no-break space.
- `TextLayout` shapes its text with `TextShaper`: the font's ligatures, contextual alternates, mark positioning and
  kerning apply, and a character the font lacks draws its `.notdef` box instead of nothing. A glyph is no longer one
  character: `GetTextData` lists glyphs with the UTF-16 offset of their first character, and caret positions come from
  `GetCaretStops`.
- `IFont.FeatureCatalog` replaces `FeatureService`: the features a font offers per script (`FontScript`) and language
  system (`FontLanguage`), each with its table and parameters, as the font lists them. Before, the features of every
  script were merged by name and the first script's won, so a Serbian or Bulgarian `locl` was lost behind the Latin one.
  The catalog is read-only: which features apply belongs to the text being shaped (`ShapingOptions`), not to the font.
- Memory blocks in a small heap are a 64th of it, at least 4 MB, instead of an 8th: a process takes about half as much of
  the shared window as before (a designer preview 40 MB instead of 80).
- Running out of memory in every type a buffer allows reports the size, the memory type and the heap.
- Every new memory block is logged at the Debug level with its type, heap and the heap's total in blocks.

### Removed

- `IFont.Baseline`: a made-up metric (`UnitsPerEm - Ascender + LineGap + CapsHeight`); the baseline of a line is
  `LineGap / 2 + LineAscent` below its top.
- The feature application `TextShaper` replaced: `GlyphLayoutContainer`, `IGlyphSubstitutions`, `IGlyphPositioning`,
  `GlyphLayoutData`, `GlyphPosition`, `Glyph.Layout`, `IFont.NotDefLayoutData`, `Feature.Apply`, `Feature.IsEnabled`,
  `FeatureService.EnableFeature` / `ApplyFeature`, and the `SubstituteGlyphs` / `PositionGlyph` methods of lookup
  subtables. They applied one feature at a time with the enabled state kept in the font, and had no implementation for
  contextual subtables.
