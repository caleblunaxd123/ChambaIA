namespace ChambaIA.Domain.Geo;

/// <summary>
/// Rough public-transport travel time between two points in Lima. It is a heuristic (straight-line distance,
/// road factor, average city speed, fixed wait) so results are shown as "aprox." and never as promises.
/// Replace with a routing provider if precision becomes a product requirement.
/// </summary>
public static class CommuteEstimator
{
    private const double RoadFactor = 1.35;
    private const double AverageSpeedKmh = 20;
    private const double FixedMinutes = 8;

    public static int EstimateMinutes(double lat1, double lon1, double lat2, double lon2)
    {
        var km = HaversineKm(lat1, lon1, lat2, lon2) * RoadFactor;
        return (int)Math.Round(FixedMinutes + km / AverageSpeedKmh * 60);
    }

    public static int? EstimateMinutes(District from, double? toLat, double? toLon) =>
        toLat is null || toLon is null ? null : EstimateMinutes(from.Latitude, from.Longitude, toLat.Value, toLon.Value);

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
