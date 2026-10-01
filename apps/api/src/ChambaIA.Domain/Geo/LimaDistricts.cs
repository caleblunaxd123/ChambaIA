using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Geo;

public sealed record District(string Name, double Latitude, double Longitude, string[] Aliases);

/// <summary>
/// Lima districts with approximate centroids. Coordinates are rounded to ~100 m and only meant for the
/// rough commute heuristic, not for routing.
/// </summary>
public static class LimaDistricts
{
    public static IReadOnlyList<District> All { get; } =
    [
        new("Los Olivos", -11.9696, -77.0704, []),
        new("Comas", -11.9456, -77.0586, []),
        new("Independencia", -11.9947, -77.0540, []),
        new("San Martín de Porres", -12.0043, -77.0740, ["smp"]),
        new("Carabayllo", -11.8870, -77.0360, []),
        new("Puente Piedra", -11.8650, -77.0750, []),
        new("Rímac", -12.0290, -77.0440, []),
        new("Cercado de Lima", -12.0464, -77.0428, ["lima", "centro de lima", "lima cercado"]),
        new("Breña", -12.0570, -77.0500, []),
        new("Lince", -12.0834, -77.0369, []),
        new("Jesús María", -12.0702, -77.0518, []),
        new("Pueblo Libre", -12.0715, -77.0644, []),
        new("San Miguel", -12.0773, -77.0884, []),
        new("Magdalena del Mar", -12.0894, -77.0722, ["magdalena"]),
        new("San Isidro", -12.0977, -77.0365, []),
        new("Miraflores", -12.1219, -77.0297, []),
        new("Surquillo", -12.1110, -77.0170, []),
        new("Barranco", -12.1490, -77.0210, []),
        new("Chorrillos", -12.1678, -77.0150, []),
        new("San Borja", -12.1089, -76.9979, []),
        new("Santiago de Surco", -12.1450, -76.9930, ["surco"]),
        new("La Molina", -12.0870, -76.9490, []),
        new("San Luis", -12.0768, -76.9967, []),
        new("La Victoria", -12.0650, -77.0150, []),
        new("El Agustino", -12.0440, -77.0020, []),
        new("Santa Anita", -12.0430, -76.9740, []),
        new("Ate", -12.0258, -76.9186, []),
        new("San Juan de Lurigancho", -11.9930, -77.0090, ["sjl"]),
        new("San Juan de Miraflores", -12.1550, -76.9700, ["sjm"]),
        new("Villa El Salvador", -12.2140, -76.9340, ["ves"]),
        new("Callao", -12.0566, -77.1181, [])
    ];

    private static readonly Dictionary<string, District> Lookup = All
        .SelectMany(d => d.Aliases.Append(d.Name).Select(a => (Key: TextNormalizer.Normalize(a), District: d)))
        .GroupBy(x => x.Key)
        .ToDictionary(g => g.Key, g => g.First().District);

    /// <summary>Resolves free text ("san miguel", "SMP") to the canonical district.</summary>
    public static District? Find(string? name) =>
        Lookup.TryGetValue(TextNormalizer.Normalize(name), out var d) ? d : null;

    /// <summary>Canonical display name, or the trimmed input when unknown.</summary>
    public static string Canonical(string name) => Find(name)?.Name ?? name.Trim();
}
