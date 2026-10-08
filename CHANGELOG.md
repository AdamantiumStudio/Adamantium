# Changelog

Notable changes to the Adamantium Engine packages, in the form of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
All packages share one version.

## Unreleased

### Added

- What a panel of OpenType features and a glyph panel need from a font: `Feature.Name` (a stylistic set's or a
  character variant's own label from the 'name' table, otherwise the registered name), `Tooltip`, `SampleText`,
  `ParameterLabels`, `Characters` and `ValueCount` (how many alternates the feature offers); `IFont.GetName` for any
  string of the 'name' table; `IFont.GetGlyphAlternates` - every glyph a single or alternate substitution turns a glyph
  into, with the feature and value that ask for it; `IFont.GetGlyphText` - the characters a glyph stands for, through
  substitutions and ligatures. Checked against HarfBuzz.
- `TextShaper.DefaultFeatures`: the features shaping applies without being asked (`ccmp`, `locl`, `calt`, `liga`,
  `kern`…), from the same list the shaper plans with, so a settings panel can show a font's `calt` as on and its
  `ss01` as off before anything is asked for.
- `FontCollection`: fonts grouped into families from their headers alone (`name`, `OS/2`, `head`, `fvar`; collection
  files included), found by typographic, older or full name, with each face's weight, slant and width (`FontFace`).
  `Match` picks a face as CSS Fonts 4 and the browsers do: the width first, then the slant, then the weight; a variable
  font covers its range. `FontCollection.System` indexes the operating system's fonts once and keeps the index on
  disk, reading again only files changed since. `FontWeight` (1-1000, `SemiBold`, `650`), `FontStyle` and
  `FontStretch` (`Condensed`, `75%`). `Typeface.LoadSystemFont` finds a font through it instead of parsing every file's
  names.
- `TextAttributes.Font`: a face per range, such as a bold word in a regular line; each line takes the largest ascent
  and descent of its fonts. `GlyphWordData.Font`, `TextLayout.GetGlyphs`.
- `TypeParser` parses a type that has its own public static `Parse(string)` without a registered parser.
- Font fallback: a character the text's font lacks is drawn from another font, in the weight, slant and width of the
  text's (`FontFallback`, `TextLayout.Fallback`; the operating system's fallback families unless set, none with null).
  Han, kana and Hangul take the family of the text's language first (Japanese, Korean, Traditional or Simplified
  Chinese), emoji the emoji family; a grapheme (with its marks, joiners, variation selectors and emoji modifiers)
  stays in one font. A family's character map is read without loading the font (`FontFace.HasCharacter`), so only
  the font chosen is loaded. A character no font has draws the font's missing-glyph box.
- `IFont.Weight`, `Style` and `Stretch` from 'OS/2'; `IFont.GetAdvanceWidth` and `GetLeftSideBearing` from the font's own
  'hmtx'; `IFont.TryGetGlyphIndex` is public.
- `GrowingTextureArray`: a 2D texture array that grows as its layers are taken, the way a list does: `Count`,
  `Capacity`, `MaxCapacity` (the device's limit), `TryAdd`, `EnsureCapacity`; a full array is replaced by one twice as
  deep with the layers in use copied on the GPU, and the old texture is retired once no frame in flight reads it.
  `ITexture.ReadbackToImage` takes the layer to read.
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
- Variable fonts: `IFont.Axes` (`FontAxis`: tag and range, from 'fvar') and `IFont.GetInstance`, the font at the axis
  values given (`FontVariation`, `wght=650`), with its own outlines and advances: TrueType outlines moved by 'gvar'
  (untouched points interpolated, composite offsets and phantom points varied), CFF2 outlines blended, advances from
  'HVAR' or the phantom points, coordinates mapped through 'avar'. An instance has its own `Typeface`, so its glyphs
  take their own places in the shared atlas; the same values give the same instance. Advances match HarfBuzz exactly
  and outline extents to a unit on Source Sans 3 VF (TrueType and CFF2), Bahnschrift, Segoe UI Variable and Sitka.
  `FontCollection.Load(face, weight, stretch)` sets a variable face's 'wght' and 'wdth' (`FontVariation.For`), and
  fallback fonts take the weight and width of the text's.
- Fonts load without waiting: `TypefaceStore.TryGetTypeface` and `LoadInBackground` (parsed on a worker, which raises
  `TypefaceStore.Loaded`; `LoadedCount`), `FontCollection.TryLoad`, `FontFace.TryHasCharacter`, and
  `FontFallback.FontFor(codepoint, like, language, out pending)`. With `TextLayout.LoadFontsInBackground` a character
  whose fallback font is still loading takes the room of the missing glyph and draws nothing; `HasPendingFonts` says
  to lay the text out again when the font arrives.
- `TextureAtlasGenerator.GenerateTextureForGlyphs(glyphs, ready)` hands each glyph over as soon as it is rasterized.
- Color glyphs from 'COLR' version 0 and 'CPAL': a color glyph is drawn as its layers, bottom first, each an ordinary
  glyph of its font in a color of the font's first palette, or in the text's color for the foreground entry
  (`IFont.GetColorLayers`, `ColorLayer`). The layers take places in the shared atlas and draw in the same batch as the
  text around them, so Segoe UI Emoji's emoji come out in color at any size.
- 'COLR' version 1 paint graphs read as the steps that draw them (`IFont.GetColorPaint`, `ColorPaintOperation`,
  `ColorPaintOperationKind`, `ColorCompositeMode`, `ColorFill`, `ColorStop`): clips by glyph outlines, groups
  composited with any of the format's 28 modes, and fills with a color or a linear, radial or sweep gradient,
  transforms folded in. Checked step for step against HarfBuzz on Google's COLRv1 test fonts.
- Color glyphs from 'COLR' version 1 drawn in real time: a color glyph is one quad whose pixel shader runs the glyph's
  paint program - nested clips intersect, groups composite exactly (Porter-Duff, blend and HSL modes), linear, radial
  (two-point conical) and sweep gradients with pad, repeat and reflect, interpolated premultiplied - each outline from
  the atlas's distance field, transformed. The atlas keeps one program per glyph in font units, so one serves every size
  (`FontItem.Paint`; FontEffect's `PaintProgramsAddress` and `PaintStopsAddress`). Segoe UI Emoji's emoji come out
  with their gradients, shading and translucent groups, as Edge draws them; the 28 modes of Google's test font too.
- Color glyphs from PNG images, 'CBDT'/'CBLC' (Noto Color Emoji) and 'sbix': `IFont.ColorBitmapSizes`,
  `IFont.GetColorBitmap` (`ColorBitmap`: the PNG and where it lies, in pixels of its size), the size chosen as HarfBuzz
  chooses it and checked against HarfBuzz's PNGs and extents; a font with images and no outlines loads. Such a glyph is
  one quad of its paint program, whose one step samples a color atlas of its own: premultiplied cells sized to the
  image by powers of two (Noto's 136×128 takes 256×128), each with its own mips, read trilinear at the level the size
  asks for and kept inside the cell. Images are decoded on a worker and land as outline glyphs do. Vector color
  ('COLR') wins over images where a font has both.
- 'COLR' version 1 at a variable font's axis values: every variable paint takes its deltas (the variation store, through
  the delta-set index map) - solid colors' alpha, gradients' points, radii and angles, color stops' offsets and alpha,
  and every transform. The clip list is read (`IFont.TryGetColorClipBox`, its boxes varying too) and a color glyph is
  drawn within its box. Checked against HarfBuzz on Google's variable test font, an axis of each kind set.
- Color glyphs from SVG documents ('SVG ', plain or gzipped): a glyph's drawing becomes the steps a 'COLR' version 1
  glyph is drawn with, so it draws in real time at any size - each shape (path with its arcs, rect, circle, ellipse,
  polygon) an outline clipping its fill, a solid color or a linear or radial gradient (bounding-box or user units, its
  transform, pad, reflect or repeat, stops' opacity, one gradient taking what it lacks from another), groups with their
  transforms and their opacity fading the group as a whole, `use`, `clip-path`, `fill-rule="evenodd"`, `currentColor`
  as the text's color and `var(--colorN)` from the text's palette. Strokes, masks, patterns, filters and style sheets
  are not drawn. Google's samples drawn from their SVG documents match the same drawings
  drawn from their 'COLR' version 1 graph.
- `SvgPathData` in `Adamantium.Mathematics.Svg`: `Walk` reads SVG path data into an `ISvgPathSink` as absolute steps
  (relative points, `H`/`V`, smooth curves' reflected controls and repeated commands worked out; arc flags written
  without separators included), `ArcToCubics` turns an arc into cubic curves, and `ReadNumbers` reads SVG's numbers;
  `Colors.TryGetNamed`.
- Color palettes: `TextAttributes.ColorPalette` draws a range's color glyphs in another of its font's 'CPAL' palettes
  (`IFont.ColorPalettes`, `ColorPalette` with the backgrounds version 1 says it suits, `ColorPaletteUsage`;
  `IFont.GetColorPaint` and `GetColorLayers` with a palette); a palette the font lacks draws in its first. Checked
  against HarfBuzz's paint in the second and third palettes of Google's test font.
- Emoji sequences: a variation sequence the font maps ('cmap' format 14) shapes to its own glyph, its selector dropped,
  as HarfBuzz does (`IFont.TryGetGlyphIndex(codepoint, variationSelector, out glyph)`), and a default-ignorable
  character the font's substitutions have replaced is no longer hidden or zeroed: a keycap, a joined flag, a skin tone
  and the ideographic variants of Noto Sans CJK shape as HarfBuzz shapes them. A grapheme takes one font, the one its
  first character picks; one drawn as an emoji (`EmojiPresentation.IsEmoji`: VS16 after an emoji character, a flag, a
  skin tone, a joined sequence, or an emoji by default) takes the emoji family unless the text's font has the glyph in
  color, keeping the text's font when no family has it in color; one drawn as text (VS15, or text by default, as a bare
  heart) does not prefer it (`FontFallback.FontFor(..., emoji)`). Segoe UI Emoji's sequences come out as Edge draws
  them.
- Synthesized bold and italic for a face a family lacks (`FontSynthesis`, `TextAttributes.Synthesis`): a bold moves
  the glyph's edge out in the distance field, by a 48th of the size per side at 9 pixels down to a 64th at 36, and
  advances it further by twice that; an italic slants the glyph's quad about its baseline by a quarter of its height
  (about 14 degrees), and the text takes the room its letters lean into. Both draw from the same atlas glyphs, in the
  same batch as the text around them (`FontItem.Synthesis`). `FontSynthesisRules.Needed` says what to synthesize: a
  weight of 600 or more on a lighter face, an italic or oblique style on an upright one, only as allowed.

### Fixed

- A render device's per-frame constants are rewound when that device begins its own frame, not when the main device's
  frame finishes. A device recording beside the window's loop - an off-screen render - had its pool rewound mid-frame,
  and its later draws overwrote the constants of its earlier ones.
- `TransitionImageLayout` moves every mip level, not only the first: a texture made with mip levels left the rest
  undefined while it said they were in its layout, and the validation layer reported the first barrier that relied on
  it.
- `IFont.GetInstance` gave the font itself for axis values when its outlines do not vary ('glyf' without 'gvar'), so
  what else varies - advances ('HVAR'), color glyphs ('COLR') - stayed at the defaults. Such a font now has its
  instances, their outlines read as they are.
- A 'COLR' version 1 outline drawn through a transform that squeezes one axis (Google's samples draw a vertical bar
  from a horizontal one scaled by 1/8 and 8) lost its antialiasing along that axis and came out half a pixel narrow: the
  field's distance became pixels by both axes' average. It becomes pixels along the outline's normal.
- A palette PNG (color type 3) did not decode: the palette was read by its byte count as if by its entries and ran
  past the chunk. It reads entry by entry, and an index past the palette is an error rather than a stray read.
- `GrowingTextureArray` copied only level 0 when it grew; an array with mips keeps every level.
- A glyph's distance field drew a faint line past an acute corner, along the extension of one of its edges, out to the
  edge of its cell, and dots in narrow gaps (the swirls of Segoe UI Emoji's ice cream; 37 glyphs of Segoe UI, 12 of
  Source Sans 3, 30 of Cascadia Code): neighboring texels' channels, interpolated, put the contour where it is not.
  Where they do, a texel past one from the outline takes the true distance in every channel, as does every texel past
  three; corners keep their channels, sharp for a bold that moves the contour out.
- A point on the line of a corner's edge, beyond the corner, could take the distance to the glyph's outline with the
  wrong sign, drawing a dot well outside the glyph (Cascadia Code's geometric shapes, such as ◆): the two edges at the corner
  were equally near but for the last digits, and only one was kept.
- `FeatureInfo.DefaultFeatureState` said Off for features shaping applies to all text unasked (`liga`, `kern`,
  `calt`, `locl`, `mark`…) and On for `size`, which shaping never applies. It comes from the shaper's own list now
  (`TextShaper.DefaultFeatures`), and `FeatureInfo.Create` no longer takes a default state.
- A text block drew no more than 4096 glyphs and dropped the rest without a word; with a quad per layer of a color
  glyph, some 65 emoji filled it. `TextLayout` has no cap now: its glyphs and the direct path's vertex buffer
  (`EnsureVertexBuffer`) grow to what the block holds.
- A line holding nothing but text of another font (a fallback emoji, a run in a smaller font) took that font's height
  and baseline: a line of emoji sat 4 pixels higher than its neighbours. A line starts from the text's own font, as a
  CSS line box starts from its strut, and only grows for taller text.
- The fonts of a collection file after the first one skipped every table they share with it, their character maps
  included, so NSimSun of simsun.ttc had no characters at all. Only outline data (`glyf`, `loca`, `CFF`) is shared now;
  each font reads its own character map, metrics, names and layout tables, and its advances and bearings come from
  its own 'hmtx' instead of the glyphs the fonts share.
- A `cmap` of format 13 (a range of characters to one glyph, as last-resort fonts map) failed the font's loading, and
  its lookup found only the first character of a range.
- Letters drawn from overlapping contours lost pieces: in Cascadia Code and Mono `n h m u a r 3 4 5` were cut and `w`
  vanished. Variable fonts and the static fonts made from them draw a stem and an arch as separate contours, and the
  overlaps were removed by flipping an "inside" flag at each crossing. A contour piece is now kept where the glyph is
  filled on one side of it and empty on the other, by the nonzero rule; an edge two contours share is kept once.
- One glyph that could not be rasterized dropped every glyph of its batch, so all the text asking for letters in that
  frame drew blank. `TextureAtlasGenerator` now leaves that glyph out, as one without an outline, and names it in
  `Typeface.ErrorMessages`; the rest of the batch lands.
- A font's names came from whichever record of its `name` table came last, often a translation: Segoe UI Bold was
  "Segoe UI Gras". The English (United States) Windows record is taken first, then any Windows one, then Unicode, then
  Mac Roman, which is read as single-byte text instead of UTF-16. `TypefaceStore` is safe to use from several threads.
- Thin slivers of other letters at the edges of glyphs, mostly at fractional sizes. Atlas cells lie edge to edge and a
  glyph's quad reached the very edge of its cell, so the texture filter mixed in the neighboring cell's field. The quad
  now stops half a texel inside its cell.
- Text in a second font drew the first font's letters. `FontAtlasStore` kept one atlas per set of rasterization
  parameters, whatever the font, and glyphs are found in it by index, so a glyph of the second font got the picture the
  first font has at that index. Every font now shares one atlas (32 MB of video memory each, so one per font would not
  scale) and a glyph is found in it by its typeface and index (`GlyphTextureData.Key`, `Typeface.Id`): `FontAtlas` is
  made without a font, `RequestAsync`, `GetGlyphData` and `GetUVCoordinatesForGlyph` take the font with the glyph,
  `FontAtlasStore.GetOrCreateFrom` takes only the parameters, and each glyph is rasterized at its own font's em.
  `IFont.Typeface` is public.
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
- Glyphs drawn inside out in the distance field: a contour running against its format's direction, as a mirrored
  component of a composite glyph does, fills the same under the winding rule but read as a hole, so ☃ from Segoe UI
  Emoji grew wedges of fill and 😀 lost its mouth. Merged outline segments now all fill on the side their format
  fills on.
- A composite glyph whose components are placed by matching points was marked invalid and drawn empty; its components
  are placed on the points now.
- A CFF glyph's bounds took only its on-curve points and dropped the fraction, so a curve bulging past them was cut off
  at the edge of its atlas cell; they now take the control points too and are rounded outward.
- 32-bit deltas of an item variation store (long words) were read as 16-bit ones.

### Changed

- Glyphs reach the atlas one by one as they are rasterized, the heaviest started first and spread over the workers, so
  a few complex glyphs no longer hold back the rest of a batch: the sandbox's text page went from 8.6 s of glyph
  generation to 1.1 s. The distance field of a glyph looks only at the outline segments near each texel (a grid of
  them) instead of all of them, with the same result; ☃ from Segoe UI Emoji took 4.8 s and takes 0.3 s.
- `TypefaceStore` parses each file once without holding one lock for all of them: files parse in parallel, and a font
  already loaded is found while another parses.
- The font atlas grows instead of filling up: it starts with two layers (8 MB of video memory instead of 32) and doubles
  as glyphs need room, up to the device's limit, where it used to overwrite its last layer past some 1800 glyphs.
  `FontAtlas.LayerCount` and `LayerCapacity`; `AtlasLayerCount` is gone, `InitialLayerCount` takes its place.
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
