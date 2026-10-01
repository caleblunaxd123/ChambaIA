using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChambaIA.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef`. Never opens a connection to generate migrations; override with ConnectionStrings__Postgres to apply them.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5442;Database=chambaia;Username=chambaia;Password=chambaia_dev";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
