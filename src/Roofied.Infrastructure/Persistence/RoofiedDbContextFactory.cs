using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Roofied.Application.Abstractions;

namespace Roofied.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef` can build the context without running the web host.
/// Uses the ROOFIED_DESIGN_CONNECTION environment variable when set, otherwise a LocalDB default.
/// This connection is ONLY used by tooling, never at runtime.
/// </summary>
public sealed class RoofiedDbContextFactory : IDesignTimeDbContextFactory<RoofiedDbContext>
{
    public RoofiedDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ROOFIED_DESIGN_CONNECTION")
            ?? "Server=(localdb)\\mssqllocaldb;Database=Roofied;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<RoofiedDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(RoofiedDbContextFactory).Assembly.FullName))
            .Options;

        return new RoofiedDbContext(options, new SystemClock());
    }
}
