# Third-party notices

Parts of this engine are derived from other people's work. All of it is under the MIT licence, which permits use,
modification and distribution — including inside a closed product — on one condition: the copyright notice and the
licence text travel with the code. The per-file headers in the sources are that notice and must not be removed; this
file carries the licence texts they refer to.

Listed here is code that was *taken in and edited*, not packages consumed as dependencies — those carry their own
licences with them.

**On modifications.** All of this has been changed, some of it heavily. That is what the licence allows, and it does not
transfer anything: the original notice stays with a derived file for as long as any of the original remains in it, and
the changes themselves belong to this project. Both statements are meant to be read together — the header says whose
work it started as, this file says it did not stay that way.

---

## FJCore — JPEG codec

**Where:** `Adamantium.Imaging/Jpeg/` (22 files: decoder, encoder, DCT, colour models, filters)

A pure C# JPEG codec, originally a Fluxcapacity Open Source project by Jeffrey Powers, later mirrored on GitHub after
Google Code shut down. Upstream is no longer maintained — the last code change was in 2016 — which changes nothing about
the grant: an MIT licence, once given, is not withdrawn by a project going quiet.

Our copy diverged from upstream long ago and keeps diverging: it was reworked to fit this engine's imaging types, and
most recently the inverse DCT was rebuilt for speed and a forced `GC.Collect()` taken out of the decode path. Those
changes are this project's; the notice below covers what they were made to.

```
Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Later maintainers of the same codebase state their copyright as
`(c) 2008-2009 Occipital Open Source, (c) 2010-2013 Brian Donahue, (c) 2012-2013 Anders Gustafsson, Cureos AB`,
under the same licence.

**What it supports**, so nobody expects more from it than it has: baseline (SOF0) and progressive (SOF2) JPEG. Not
JPEG 2000 — that is a different format built on wavelets, and there is no code for it here.

---

## Unicode Character Database

**Where:** `Adamantium.Fonts/Data/` — `Scripts.ucd`, `CombiningClass.ucd`, `Decompositions.ucd`,
`ExtendedPictographic.ucd`, `GraphemeBreak.ucd`, `WordBreak.ucd`, `IndicConjunctBreak.ucd`, `LineBreak.ucd` and
`EastAsianWidth.ucd`, embedded in `Adamantium.Fonts`; and, for the tests only, `GraphemeBreakTest.txt`,
`WordBreakTest.txt` and `LineBreakTest.txt` in `Tests/Adamantium.FontTests/Unicode/`.

Data, not code: the script, canonical combining class, canonical decomposition, Extended_Pictographic, grapheme, word
and line break, Indic_Conjunct_Break and East_Asian_Width properties of Unicode 16.0, cut down from the UCD files
(`Scripts.txt`, `DerivedCombiningClass.txt`, `UnicodeData.txt`, `DerivedNormalizationProps.txt`, `emoji-data.txt`,
`GraphemeBreakProperty.txt`, `WordBreakProperty.txt`, `DerivedCoreProperties.txt`, `LineBreak.txt`,
`EastAsianWidth.txt`) to the columns the text shaper and the text boundaries read, with adjacent ranges joined.

```
UNICODE LICENSE V3

COPYRIGHT AND PERMISSION NOTICE

Copyright © 1991-2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.
```

---

## ISO 639-3 code tables

**Where:** `Adamantium.Fonts/Data/Iso639.dat` — the two-letter ISO 639-1 codes with their three-letter ISO 639-3
codes, unchanged, taken from the code tables SIL International publishes as the ISO 639-3 Registration Authority
(https://iso639-3.sil.org).

---

## Google color font test glyphs

**Where:** `Tests/Adamantium.FontTests/ColorFonts/` — `test_glyphs-glyf_colr_1.ttf` and
`test_glyphs-glyf_colr_1_variable.ttf`, unchanged, from https://github.com/googlefonts/color-fonts, used only by the
tests. Copyright Google LLC, under the Apache License 2.0, whose text is `LICENSE.txt` beside them.

---

## SharpDX

**Where:** ten files still carry its notice, and only those ten are derived from it —

- `Adamantium.Imaging`: `ImageDescription.cs`, `PixelBuffer.cs`, `PixelBufferArray.cs`, and DDS handling
  (`DdsHelper.cs`, `DdsFlags.cs`, `FourCC.cs`, `HeaderDXT10.cs`)
- `Adamantium.Core`: `DataBuffer.cs`, `NamedObject.cs`
- `Adamantium.Mathematics`: `ViewportF.cs`

Everything else that once came from it has been rewritten and carries no notice.

**Its own ancestry.** SharpDX did not write its maths either: that part came in from SlimMath, itself a port of the
maths in SlimDX, under MIT/X11. So `ViewportF.cs` — and anything else here descended from that maths — traces back
further than the header says. The terms are the same all the way down, which is why one notice covers the chain; it is
recorded because provenance is worth knowing accurately, not because a second obligation exists.

Maths is also where this project keeps rewriting. As a file stops containing any of the original, its notice stops
applying and can go — but that is judged per file, on what is actually left in it, not on how much work has been done
around it.

```
Copyright (c) 2010-2014 SharpDX - Alexandre Mutel

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
