namespace Adamantium.Fonts.Tables.CFF
{
    internal class RegionAxisCoordinates
    {
        public float StartCoord { get; set; }
        
        public float PeakCoord { get; set; }
        
        public float EndCoord { get; set; }

        public float GetScalar(float coordinate)
        {
            return GetScalar(StartCoord, PeakCoord, EndCoord, coordinate);
        }

        public static float GetScalar(float start, float peak, float end, float coordinate)
        {
            if (start > peak || peak > end || peak == 0 || (start < 0 && end > 0))
            {
                return 1;
            }

            if (coordinate < start || coordinate > end)
            {
                return 0;
            }

            if (coordinate == peak)
            {
                return 1;
            }

            return coordinate < peak
                ? (coordinate - start) / (peak - start)
                : (end - coordinate) / (end - peak);
        }

        public override string ToString()
        {
            return $"Start: {StartCoord} Peak: {PeakCoord} End {EndCoord}";
        }
    }
}