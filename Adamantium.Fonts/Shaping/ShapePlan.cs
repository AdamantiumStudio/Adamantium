using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Shaping;

internal sealed class ShapePlan
{
    public const uint GlobalBit = 1u << 31;

    private const int MaxBitsPerFeature = 8;
    private const int GlobalBitShift = 31;

    private static readonly string[] CommonFeatures = ["abvm", "blwm", "ccmp", "locl", "mark", "mkmk", "rlig"];
    private static readonly string[] HorizontalFeatures = ["calt", "clig", "curs", "dist", "kern", "liga", "rclt"];

    private readonly Dictionary<string, FeatureMap> _features = new();

    private ShapePlan()
    {
    }

    public uint GlobalMask { get; private set; } = GlobalBit;

    public PlannedLookup[][] GsubStages { get; private set; } = [];

    public PlannedLookup[] GposLookups { get; private set; } = [];

    public bool ApplyGpos { get; private set; }

    public bool ApplyKernTable { get; private set; }

    public uint KernMask { get; private set; }

    public uint FracMask { get; private set; }

    public uint NumrMask { get; private set; }

    public uint DnomMask { get; private set; }

    public FeatureMap GetFeature(string tag) => _features.TryGetValue(tag, out var map) ? map : null;

    public static ShapePlan Create(OpenTypeLayout layout, string scriptTag, string languageTag,
        IReadOnlyList<FontFeature> userFeatures)
    {
        var plan = new ShapePlan();
        var gsub = Select(layout.Gsub, scriptTag, languageTag);
        var gpos = Select(layout.Gpos, scriptTag, languageTag);
        var requests = Merge(CollectRequests(userFeatures));

        var requiredGsubStage = 0;
        var requiredGposStage = 0;
        var nextBit = 1;
        var maps = new List<FeatureMap>();
        foreach (var request in requests)
        {
            var usesGlobalBit = (request.Flags & FeatureFlags.Global) != 0 && request.MaxValue == 1;
            var bitsNeeded = usesGlobalBit ? 0 : Math.Min(MaxBitsPerFeature, BitStorage(request.MaxValue));
            if (request.MaxValue == 0 || nextBit + bitsNeeded >= GlobalBitShift)
            {
                continue;
            }

            if (gsub.Required?.Name == request.Tag)
            {
                requiredGsubStage = request.GsubStage;
            }

            if (gpos.Required?.Name == request.Tag)
            {
                requiredGposStage = request.GposStage;
            }

            var gsubIndex = FindFeature(layout.Gsub, gsub.LangSys, request.Tag);
            var gposIndex = FindFeature(layout.Gpos, gpos.LangSys, request.Tag);
            if (gsubIndex < 0 && gposIndex < 0 && (request.Flags & FeatureFlags.HasFallback) == 0)
            {
                continue;
            }

            var map = new FeatureMap
            {
                Tag = request.Tag,
                GsubIndex = gsubIndex,
                GposIndex = gposIndex,
                GsubStage = request.GsubStage,
                GposStage = request.GposStage,
                AutoZwnj = (request.Flags & FeatureFlags.ManualZwnj) == 0,
                AutoZwj = (request.Flags & FeatureFlags.ManualZwj) == 0,
            };

            if (usesGlobalBit)
            {
                map.Shift = GlobalBitShift;
                map.Mask = GlobalBit;
            }
            else
            {
                map.Shift = nextBit;
                map.Mask = (uint)((1L << (nextBit + bitsNeeded)) - (1L << nextBit));
                nextBit += bitsNeeded;
                plan.GlobalMask |= (request.DefaultValue << map.Shift) & map.Mask;
            }

            maps.Add(map);
            plan._features[map.Tag] = map;
        }

        plan.GsubStages = CollectLookups(layout.Gsub, gsub, requiredGsubStage, maps, true, 2);
        plan.GposLookups = CollectLookups(layout.Gpos, gpos, requiredGposStage, maps, false, 1)[0];

        var kern = plan.GetFeature("kern");
        plan.ApplyGpos = layout.Gpos?.LookupList?.Length > 0;
        plan.ApplyKernTable = layout.HasKernTable && (kern == null || kern.GposIndex < 0);
        plan.KernMask = kern?.Mask ?? 0;
        plan.FracMask = plan.GetFeature("frac")?.Mask ?? 0;
        plan.NumrMask = plan.GetFeature("numr")?.Mask ?? 0;
        plan.DnomMask = plan.GetFeature("dnom")?.Mask ?? 0;
        return plan;
    }

    private static List<FeatureRequest> CollectRequests(IReadOnlyList<FontFeature> userFeatures)
    {
        var requests = new List<FeatureRequest>();
        var gsubStage = 0;

        void Add(string tag, FeatureFlags flags, uint value = 1)
        {
            requests.Add(new FeatureRequest
            {
                Tag = tag,
                Flags = flags,
                MaxValue = value,
                DefaultValue = (flags & FeatureFlags.Global) != 0 ? value : 0,
                GsubStage = gsubStage,
                Order = requests.Count,
            });
        }

        Add("rvrn", FeatureFlags.Global);
        gsubStage++;
        Add("ltra", FeatureFlags.Global);
        Add("ltrm", FeatureFlags.Global);
        Add("frac", FeatureFlags.None);
        Add("numr", FeatureFlags.None);
        Add("dnom", FeatureFlags.None);
        foreach (var tag in CommonFeatures)
        {
            Add(tag, tag is "mark" or "mkmk" ? FeatureFlags.Global | FeatureFlags.ManualJoiners : FeatureFlags.Global);
        }

        foreach (var tag in HorizontalFeatures)
        {
            Add(tag, tag == "kern" ? FeatureFlags.Global | FeatureFlags.HasFallback : FeatureFlags.Global);
        }

        foreach (var feature in userFeatures)
        {
            Add(feature.Tag, feature.IsGlobal ? FeatureFlags.Global : FeatureFlags.None, feature.Value);
        }

        return requests;
    }

    private static List<FeatureRequest> Merge(List<FeatureRequest> requests)
    {
        var sorted = requests.OrderBy(r => r.Tag, StringComparer.Ordinal).ThenBy(r => r.Order).ToList();
        var merged = new List<FeatureRequest>();
        foreach (var request in sorted)
        {
            var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
            if (last == null || last.Tag != request.Tag)
            {
                merged.Add(request);
                continue;
            }

            if ((request.Flags & FeatureFlags.Global) != 0)
            {
                last.Flags |= FeatureFlags.Global;
                last.MaxValue = request.MaxValue;
                last.DefaultValue = request.DefaultValue;
            }
            else
            {
                last.Flags &= ~FeatureFlags.Global;
                last.MaxValue = Math.Max(last.MaxValue, request.MaxValue);
            }

            last.Flags |= request.Flags & FeatureFlags.HasFallback;
            last.GsubStage = Math.Min(last.GsubStage, request.GsubStage);
            last.GposStage = Math.Min(last.GposStage, request.GposStage);
        }

        return merged;
    }

    private static PlannedLookup[][] CollectLookups(IFontLayout table, (LangSysTable LangSys, FeatureTable Required) selection,
        int requiredStage, List<FeatureMap> maps, bool gsub, int stageCount)
    {
        var stages = new PlannedLookup[stageCount][];
        for (var stage = 0; stage < stageCount; stage++)
        {
            var lookups = new List<PlannedLookup>();
            if (table != null)
            {
                if (selection.Required != null && requiredStage == stage)
                {
                    AddLookups(table, selection.Required, GlobalBit, true, true, lookups);
                }

                foreach (var map in maps)
                {
                    var index = gsub ? map.GsubIndex : map.GposIndex;
                    var mapStage = gsub ? map.GsubStage : map.GposStage;
                    if (index >= 0 && mapStage == stage)
                    {
                        AddLookups(table, table.FeatureList[index], map.Mask, map.AutoZwnj, map.AutoZwj, lookups);
                    }
                }
            }

            stages[stage] = MergeLookups(lookups);
        }

        return stages;
    }

    private static void AddLookups(IFontLayout table, FeatureTable feature, uint mask, bool autoZwnj, bool autoZwj,
        List<PlannedLookup> lookups)
    {
        foreach (var index in feature.LookupListIndices)
        {
            if (index >= table.LookupList.Length)
            {
                continue;
            }

            lookups.Add(new PlannedLookup
            {
                Index = index,
                Table = table.LookupList[index],
                Mask = mask,
                AutoZwnj = autoZwnj,
                AutoZwj = autoZwj,
            });
        }
    }

    private static PlannedLookup[] MergeLookups(List<PlannedLookup> lookups)
    {
        var merged = new List<PlannedLookup>();
        foreach (var lookup in lookups.OrderBy(l => l.Index))
        {
            if (merged.Count > 0 && merged[merged.Count - 1].Index == lookup.Index)
            {
                var last = merged[merged.Count - 1];
                last.Mask |= lookup.Mask;
                last.AutoZwnj &= lookup.AutoZwnj;
                last.AutoZwj &= lookup.AutoZwj;
                merged[merged.Count - 1] = last;
                continue;
            }

            merged.Add(lookup);
        }

        return merged.ToArray();
    }

    private static (LangSysTable LangSys, FeatureTable Required) Select(IFontLayout table, string scriptTag,
        string languageTag)
    {
        if (table?.ScriptList == null)
        {
            return (null, null);
        }

        var script = FindScript(table, scriptTag) ?? FindScript(table, "DFLT") ?? FindScript(table, "dflt")
            ?? FindScript(table, "latn");
        if (script == null)
        {
            return (null, null);
        }

        LangSysTable langSys = null;
        if (languageTag != null)
        {
            langSys = script.LangSysTables?.FirstOrDefault(l => l.Name == languageTag);
        }

        langSys ??= script.DefaultLang;
        var required = langSys is { HasRequireFeature: true } && langSys.RequiredFeatureIndex < table.FeatureList.Length
            ? table.FeatureList[langSys.RequiredFeatureIndex]
            : null;
        return (langSys, required);
    }

    private static ScriptTable FindScript(IFontLayout table, string tag)
    {
        return table.ScriptList.FirstOrDefault(s => s.Name == tag);
    }

    private static int FindFeature(IFontLayout table, LangSysTable langSys, string tag)
    {
        if (table == null || langSys?.FeatureIndices == null)
        {
            return -1;
        }

        foreach (var index in langSys.FeatureIndices)
        {
            if (index < table.FeatureList.Length && table.FeatureList[index].Name == tag)
            {
                return index;
            }
        }

        return -1;
    }

    private static int BitStorage(uint value)
    {
        var bits = 0;
        while (value != 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }
}
