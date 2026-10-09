using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class DeviceTable
    {
        public UInt16 StartSize { get; set; }
        
        public UInt16 EndSize { get; set; }
        
        public DeltaFormatValues DeltaFormat { get; set; }
        
        public UInt16[] DeltaValues { get; set; }

        public int VariationIndex =>
            DeltaFormat == DeltaFormatValues.VariationIndex ? (StartSize << 16) | EndSize : -1;
    }
}