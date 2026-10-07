using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class Command
    {
        public OperatorsType Operator;
        public List<CommandOperand> Operands;
        public List<CommandOperand> BlendedOperands;
        public bool IsBlendPresent { get; set; }

        public void ApplyBlend(VariationRegionList regionList, float[] variationPoint)
        {
            BlendedOperands = Operands;
            
            if (!IsBlendPresent ||
                regionList == null ||
                variationPoint == null ||
                variationPoint.Length < regionList.AxisCount)
            {
                return;
            }

            foreach (var blendedOperand in BlendedOperands)
            {
                var blendData = blendedOperand.BlendData;
                if (blendData == null || blendData.Data.Count == 0)
                {
                    continue;
                }

                double netAdjustment = 0;

                for (var r = 0; r < blendData.Data.Count; ++r)
                {
                    var region = regionList.VariationRegions[blendData.RegionIndices[r]];
                    netAdjustment += region.GetScalar(variationPoint) * blendData.Data[r];
                }

                blendedOperand.Value += netAdjustment;
            }
        }
        
        internal bool IsNewOutline()
        {
            switch (Operator)
            {
                case OperatorsType.rmoveto:
                case OperatorsType.hmoveto:
                case OperatorsType.vmoveto:
                    return true;
                default:
                    return false;
            }
        }
        
        public override string ToString()
        {
            return $"IsBlendPresent: {IsBlendPresent}; {Operator} {string.Join(" , ", Operands)}";
        }
    }
}
