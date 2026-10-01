using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Skills;

namespace ChambaIA.Api.Endpoints;

public static class CatalogEndpoints
{
    public sealed record SkillOptionDto(string Key, string Name);

    /// <summary>Static reference data the app needs for pickers. One source of truth: the same catalogs the matcher uses.</summary>
    public static RouteGroupBuilder MapCatalog(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/catalog").WithTags("Catalog").RequireAuthorization();

        group.MapGet("/districts", () => Results.Ok(LimaDistricts.All.Select(d => d.Name).Order(StringComparer.Create(new System.Globalization.CultureInfo("es-PE"), true)).ToList()));

        group.MapGet("/skills", () => Results.Ok(SkillCatalog.All.Select(s => new SkillOptionDto(s.Key, s.Name)).OrderBy(s => s.Name).ToList()));

        return api;
    }
}
