namespace Adamantium.Fonts.Shaping;

internal static class Positioner
{
    public static void Position(IFont font, GlyphBuffer buffer, ShapePlan plan, LookupApplier applier, bool rightToLeft)
    {
        buffer.ClearPlacements();
        var info = buffer.Info;
        var placements = buffer.Placements;
        for (var i = 0; i < buffer.Length; i++)
        {
            placements[i].XAdvance = font.GetAdvanceWidth(info[i].Glyph);
        }

        if (buffer.HasSpaceFallback)
        {
            FallbackSpaces(font, buffer);
        }

        if (plan.ApplyGpos)
        {
            applier.ApplyGpos(plan);
        }

        if (plan.ApplyKernTable)
        {
            KernTable(font, buffer, plan.KernMask);
        }

        var adjustOffsets = !plan.ApplyGpos;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (!info[i].IsMark)
            {
                continue;
            }

            if (adjustOffsets)
            {
                placements[i].XOffset -= placements[i].XAdvance;
                placements[i].YOffset -= placements[i].YAdvance;
            }

            placements[i].XAdvance = 0;
            placements[i].YAdvance = 0;
        }

        if (buffer.HasDefaultIgnorables)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (info[i].IsDefaultIgnorable)
                {
                    placements[i] = default;
                }
            }
        }

        for (var i = 0; i < buffer.Length; i++)
        {
            PropagateAttachment(placements, buffer.Length, i, 64, rightToLeft);
        }
    }

    private static void FallbackSpaces(IFont font, GlyphBuffer buffer)
    {
        var info = buffer.Info;
        var placements = buffer.Placements;
        int em = font.UnitsPerEm;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (info[i].Space == SpaceKind.NotSpace || info[i].IsLigated)
            {
                continue;
            }

            switch (info[i].Space)
            {
                case SpaceKind.Em:
                case SpaceKind.Em2:
                case SpaceKind.Em3:
                case SpaceKind.Em4:
                case SpaceKind.Em5:
                case SpaceKind.Em6:
                case SpaceKind.Em16:
                    var divisor = (int)info[i].Space;
                    placements[i].XAdvance = (em + divisor / 2) / divisor;
                    break;
                case SpaceKind.FourEm18:
                    placements[i].XAdvance = em * 4 / 18;
                    break;
                case SpaceKind.Figure:
                    for (var digit = '0'; digit <= '9'; digit++)
                    {
                        if (font.TryGetGlyphIndex(digit, out var glyph))
                        {
                            placements[i].XAdvance = font.GetAdvanceWidth(glyph);
                            break;
                        }
                    }

                    break;
                case SpaceKind.Punctuation:
                    if (font.TryGetGlyphIndex('.', out var period) || font.TryGetGlyphIndex(',', out period))
                    {
                        placements[i].XAdvance = font.GetAdvanceWidth(period);
                    }

                    break;
                case SpaceKind.Narrow:
                    placements[i].XAdvance /= 2;
                    break;
            }
        }
    }

    private static void KernTable(IFont font, GlyphBuffer buffer, uint kernMask)
    {
        var info = buffer.Info;
        var placements = buffer.Placements;
        var index = 0;
        while (index < buffer.Length)
        {
            if ((info[index].Mask & kernMask) == 0)
            {
                index++;
                continue;
            }

            var next = index + 1;
            while (next < buffer.Length && (info[next].IsMark || (info[next].IsDefaultIgnorable && !info[next].IsZwnj)))
            {
                next++;
            }

            if (next >= buffer.Length || (info[next].Mask & kernMask) == 0)
            {
                index++;
                continue;
            }

            int kern = font.GetKerningValue((ushort)info[index].Glyph, (ushort)info[next].Glyph);
            if (kern != 0)
            {
                var first = kern >> 1;
                var second = kern - first;
                placements[index].XAdvance += first;
                placements[next].XAdvance += second;
                placements[next].XOffset += second;
            }

            index = next;
        }
    }

    private static void PropagateAttachment(GlyphPlacement[] placements, int length, int i, int nesting, bool rightToLeft)
    {
        var chain = placements[i].AttachChain;
        var type = placements[i].AttachType;
        if (chain == 0)
        {
            return;
        }

        placements[i].AttachChain = 0;
        var j = i + chain;
        if (j < 0 || j >= length || nesting == 0)
        {
            return;
        }

        PropagateAttachment(placements, length, j, nesting - 1, rightToLeft);
        if ((type & GlyphPlacement.AttachCursive) != 0)
        {
            placements[i].YOffset += placements[j].YOffset;
            return;
        }

        placements[i].XOffset += placements[j].XOffset;
        placements[i].YOffset += placements[j].YOffset;
        if (rightToLeft)
        {
            for (var k = j + 1; k <= i; k++)
            {
                placements[i].XOffset += placements[k].XAdvance;
                placements[i].YOffset += placements[k].YAdvance;
            }

            return;
        }

        for (var k = j; k < i; k++)
        {
            placements[i].XOffset -= placements[k].XAdvance;
            placements[i].YOffset -= placements[k].YAdvance;
        }
    }
}
