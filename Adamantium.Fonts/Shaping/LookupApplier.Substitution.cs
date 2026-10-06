using System.Globalization;
using Adamantium.Fonts.Tables.GSUB;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Shaping;

internal sealed partial class LookupApplier
{
    private bool ApplySubstitution(ILookupSubTable subtable)
    {
        var glyph = (ushort)_buffer.Info[_index].Glyph;
        switch (subtable)
        {
            case SingleSubstitutionSubTableFormat1 single when single.Coverage.FindPosition(glyph) >= 0:
                ReplaceGlyph((ushort)(glyph + single.DeltaGlyphId));
                _index++;
                return true;
            case SingleSubstitutionSubTableFormat2 single:
                var singleIndex = single.Coverage.FindPosition(glyph);
                if (singleIndex < 0)
                {
                    return false;
                }

                ReplaceGlyph(single.SubstituteGlyphIds[singleIndex]);
                _index++;
                return true;
            case MultipleSubstitutionSubTable multiple:
                return ApplyMultiple(multiple, glyph);
            case AlternateSubstitutionSubTable alternate:
                return ApplyAlternate(alternate, glyph);
            case LigatureSubstitutionSubTable ligature:
                return ApplyLigature(ligature, glyph);
            case ContextualSubstitutionSubTableFormat1 context:
                return ApplyGlyphContext(context.Coverage, context.SequenceRuleSets, glyph);
            case ContextualSubstitutionSubTableFormat2 context:
                return ApplyClassContext(context.Coverage, context.ClassDef, context.ClassSequenceRuleSets, glyph);
            case ContextualSubstitutionSubTableFormat3 context:
                return ApplyCoverageContext(context.CoverageTables, context.LookupRecords);
            case ChainedContextsSubstitutionSubTableFormat1 chain:
                return ApplyGlyphChain(chain.Coverage, chain.ChainedSeqRuleSets, glyph);
            case ChainedContextsSubstitutionSubTableFormat2 chain:
                return ApplyClassChain(chain.Coverage, chain.BacktrackClassDef, chain.InputClassDef,
                    chain.LookaheadClassDef, chain.ChainedSeqRuleSets, glyph);
            case ChainedContextsSubstitutionSubTableFormat3 chain:
                return ApplyCoverageChain(chain.BacktrackCoverages, chain.InputCoverages, chain.LookaheadCoverages,
                    chain.LookupRecords);
            case ReverseChainingContextualSubstitutionFormat1 reverse:
                return ApplyReverseChain(reverse, glyph);
            default:
                return false;
        }
    }

    private void ReplaceGlyph(uint glyph, ushort classGuess = 0, bool ligature = false)
    {
        SetGlyph(ref _buffer.Info[_index], glyph, classGuess, ligature, false);
    }

    private void SetGlyph(ref GlyphInfo info, uint glyph, ushort classGuess, bool ligature, bool component)
    {
        var props = (ushort)(info.Props | GlyphProps.Substituted);
        if (ligature)
        {
            props |= GlyphProps.Ligated;
            props &= unchecked((ushort)~GlyphProps.Multiplied);
        }

        if (component)
        {
            props |= GlyphProps.Multiplied;
        }

        if (_layout.HasGlyphClasses)
        {
            props = (ushort)((props & GlyphProps.Preserve) | _layout.GetGlyphProps(glyph));
        }
        else if (classGuess != 0)
        {
            props = (ushort)((props & GlyphProps.Preserve) | classGuess);
        }

        info.Props = props;
        info.Glyph = glyph;
    }

    private bool ApplyMultiple(MultipleSubstitutionSubTable multiple, ushort glyph)
    {
        var coverageIndex = multiple.Coverage.FindPosition(glyph);
        if (coverageIndex < 0)
        {
            return false;
        }

        var substitutes = multiple.SequenceTables[coverageIndex].SubstituteGlyphIDs;
        if (substitutes.Length == 1)
        {
            ReplaceGlyph(substitutes[0]);
            _index++;
            return true;
        }

        if (substitutes.Length == 0)
        {
            DeleteGlyph();
            return true;
        }

        var current = _buffer.Info[_index];
        var classGuess = current.IsLigature ? GlyphProps.BaseGlyph : (ushort)0;
        var ligId = current.LigId;
        for (var i = 0; i < substitutes.Length; i++)
        {
            if (ligId == 0)
            {
                current.SetLigPropsForMark(0, i);
            }

            SetGlyph(ref current, substitutes[i], classGuess, false, true);
            if (i == 0)
            {
                _buffer.Info[_index] = current;
            }
            else
            {
                _buffer.Insert(_index + i, current);
            }
        }

        _index += substitutes.Length;
        return true;
    }

    private void DeleteGlyph()
    {
        var info = _buffer.Info;
        var cluster = info[_index].Cluster;
        var survives = (_index + 1 < _buffer.Length && cluster == info[_index + 1].Cluster)
                       || (_index > 0 && cluster == info[_index - 1].Cluster);
        if (!survives)
        {
            if (_index > 0)
            {
                if (cluster < info[_index - 1].Cluster)
                {
                    var oldCluster = info[_index - 1].Cluster;
                    for (var i = _index - 1; i >= 0 && info[i].Cluster == oldCluster; i--)
                    {
                        info[i].Cluster = cluster;
                    }
                }
            }
            else if (_index + 1 < _buffer.Length)
            {
                _buffer.MergeClusters(_index, _index + 2);
            }
        }

        _buffer.RemoveAt(_index);
    }

    private bool ApplyAlternate(AlternateSubstitutionSubTable alternate, ushort glyph)
    {
        var coverageIndex = alternate.Coverage.FindPosition(glyph);
        if (coverageIndex < 0)
        {
            return false;
        }

        var alternates = alternate.AlternateSetTables[coverageIndex].AlternateGlyphIDs;
        var shift = 0;
        while (((_lookupMask >> shift) & 1) == 0)
        {
            shift++;
        }

        var alternateIndex = (_buffer.Info[_index].Mask & _lookupMask) >> shift;
        if (alternateIndex == 0 || alternateIndex > alternates.Length)
        {
            return false;
        }

        ReplaceGlyph(alternates[alternateIndex - 1]);
        _index++;
        return true;
    }

    private bool ApplyLigature(LigatureSubstitutionSubTable table, ushort glyph)
    {
        var coverageIndex = table.Coverage.FindPosition(glyph);
        if (coverageIndex < 0)
        {
            return false;
        }

        var positions = PositionsForLevel();
        foreach (var ligature in table.LigatureSetTables[coverageIndex].Ligatures)
        {
            var components = ligature.ComponentGlypIDs;
            if (components.Length == 0)
            {
                ReplaceGlyph(ligature.LigatureGlyphID);
                _index++;
                return true;
            }

            if (MatchInput(components.Length + 1, (i, info) => info.Glyph == components[i - 1], positions,
                    out var end, out var totalComponents))
            {
                Ligate(positions, components.Length + 1, end, totalComponents, ligature.LigatureGlyphID);
                return true;
            }
        }

        return false;
    }

    private void Ligate(int[] positions, int count, int matchEnd, int totalComponents, uint ligatureGlyph)
    {
        _buffer.MergeClusters(_index, matchEnd);
        var info = _buffer.Info;
        var isBaseLigature = info[positions[0]].IsBaseGlyph;
        var isMarkLigature = info[positions[0]].IsMark;
        for (var i = 1; i < count; i++)
        {
            if (!info[positions[i]].IsMark)
            {
                isBaseLigature = false;
                isMarkLigature = false;
                break;
            }
        }

        var isLigature = !isBaseLigature && !isMarkLigature;
        var classGuess = isLigature ? GlyphProps.Ligature : (ushort)0;
        var ligId = isLigature ? _buffer.AllocateLigId() : 0;
        ref var first = ref info[_index];
        var lastLigId = first.LigId;
        var lastComponents = first.LigNumComps;
        var componentsSoFar = lastComponents;
        if (isLigature)
        {
            first.SetLigPropsForLigature(ligId, totalComponents);
            if (first.Category == UnicodeCategory.NonSpacingMark)
            {
                first.Category = UnicodeCategory.OtherLetter;
            }
        }

        ReplaceGlyph(ligatureGlyph, classGuess, true);

        var position = _index + 1;
        for (var i = 1; i < count; i++)
        {
            var target = positions[i] - (i - 1);
            for (; position < target; position++)
            {
                if (isLigature)
                {
                    var thisComponent = info[position].LigComp;
                    if (thisComponent == 0)
                    {
                        thisComponent = lastComponents;
                    }

                    var newComponent = componentsSoFar - lastComponents + System.Math.Min(thisComponent, lastComponents);
                    info[position].SetLigPropsForMark(ligId, newComponent);
                }
            }

            lastLigId = info[target].LigId;
            lastComponents = info[target].LigNumComps;
            componentsSoFar += lastComponents;
            _buffer.RemoveAt(target);
        }

        if (!isMarkLigature && lastLigId != 0)
        {
            for (var i = position; i < _buffer.Length; i++)
            {
                if (info[i].LigId != lastLigId)
                {
                    break;
                }

                var thisComponent = info[i].LigComp;
                if (thisComponent == 0)
                {
                    break;
                }

                var newComponent = componentsSoFar - lastComponents + System.Math.Min(thisComponent, lastComponents);
                info[i].SetLigPropsForMark(ligId, newComponent);
            }
        }

        _index = position;
    }

    private bool ApplyReverseChain(ReverseChainingContextualSubstitutionFormat1 reverse, ushort glyph)
    {
        var coverageIndex = reverse.Coverage.FindPosition(glyph);
        if (coverageIndex < 0 || _nestingLeft != MaxNestingLevel)
        {
            return false;
        }

        var backtrack = reverse.BacktrackCoverages;
        var lookahead = reverse.LookaheadCoverages;
        if (!MatchBacktrack(backtrack.Length, (i, info) => backtrack[i].FindPosition((ushort)info.Glyph) >= 0)
            || !MatchLookahead(lookahead.Length, (i, info) => lookahead[i].FindPosition((ushort)info.Glyph) >= 0,
                _index + 1))
        {
            return false;
        }

        ReplaceGlyph(reverse.SubstituteGlyphIDs[coverageIndex]);
        return true;
    }
}
