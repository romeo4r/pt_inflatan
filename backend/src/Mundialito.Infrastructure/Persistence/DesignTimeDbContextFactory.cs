using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling. Adding a migration needs the model but no database,
/// so a placeholder is enough; applying one needs a real server, which is supplied either through
/// the ConnectionStrings__Database environment variable or the <c>--connection</c> CLI option.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string Placeholder = "Server=localhost;Database=Mundialito;Integrated Security=true;TrustServerCertificate=true";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? Placeholder;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
