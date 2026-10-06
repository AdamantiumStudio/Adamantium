using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    // Single Adjustment Positioning: Format 1
    // uint16	posFormat	Format identifier: format = 1
    // Offset16	coverageOffset	Offset to Coverage table, from beginning of SinglePos subtable.
    // uint16	valueFormat	Defines the types of data in the ValueRecord.
    // ValueRecord	valueRecord	Defines positioning value(s) — applied to all glyphs in the Coverage table.

    // Single Adjustment Positioning: Format 2
    // uint16	posFormat	Format identifier: format = 2
    // Offset16	coverageOffset	Offset to Coverage table, from beginning of SinglePos subtable.
    // uint16	valueFormat	Defines the types of data in the ValueRecords.
    // uint16	valueCount	Number of ValueRecords — must equal glyphCount in the Coverage table.
    // ValueRecord	valueRecords[valueCount]	Array of ValueRecords — positioning values applied to glyphs.

    internal class SingleAdjustmentPositioningSubTable : GPOSLookupSubTable
    {
        public SingleAdjustmentPositioningSubTable(CoverageTable coverage, ValueRecord record)
        {
            Format = 1;
            Coverage = coverage;
            ValueRecords = new[] {record};
        }

        public SingleAdjustmentPositioningSubTable(CoverageTable coverage, ValueRecord[] records)
        {
            Format = 2;
            Coverage = coverage;
            ValueRecords = records;
        }

        public override GPOSLookupType Type => GPOSLookupType.SingleAdjustment;

        public uint Format { get; }

        public CoverageTable Coverage { get; }

        public ValueRecord[] ValueRecords { get; }
    }
}
