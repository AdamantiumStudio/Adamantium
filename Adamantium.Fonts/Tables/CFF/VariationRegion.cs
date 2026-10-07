namespace Adamantium.Fonts.Tables.CFF
{
    internal class VariationRegion
    {
        public RegionAxisCoordinates[] RegionAxes { get; set; }

        public float GetScalar(float[] coordinates)
        {
            var scalar = 1f;
            for (var axis = 0; axis < RegionAxes.Length && scalar != 0; axis++)
            {
                scalar *= RegionAxes[axis].GetScalar(axis < coordinates.Length ? coordinates[axis] : 0);
            }

            return scalar;
        }
    }
}