namespace Adamantium.Fonts.Tables.CFF
{
    internal class VariationStore
    {
        public VariationStore(VariationRegionList regionList, ItemVariationDataSubtable[] variationData)
        {
            VariationRegionList = regionList;
            ItemVariationData = variationData;
        }
        
        public VariationRegionList VariationRegionList { get; }
        
        public ItemVariationDataSubtable[] ItemVariationData { get; }

        public float GetDelta(int outer, int inner, float[] coordinates)
        {
            if (outer >= ItemVariationData.Length || inner >= ItemVariationData[outer].ItemCount)
            {
                return 0;
            }

            var data = ItemVariationData[outer];
            var deltas = data.DeltaSets[inner].Deltas;
            var delta = 0f;
            for (var r = 0; r < deltas.Length; r++)
            {
                if (deltas[r] != 0)
                {
                    delta += VariationRegionList.VariationRegions[data.RegionIndices[r]].GetScalar(coordinates) * deltas[r];
                }
            }

            return delta;
        }
    }
}