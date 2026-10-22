using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Events;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.ReadStores;
using Mundialito.Application.Abstractions.Time;
using Mundialito.Infrastructure.Events;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.Repositories;
using Mundialito.Infrastructure.ReadModel;
using Mundialito.Infrastructure.Time;

namespace Mundialito.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core write side (context, repositories, Unit of Work), the Dapper read side
    /// (connection factory, read stores), the domain event dispatcher and the system clock.
    /// Both sides share the same connection string; they differ in how they talk to the database.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Write side: EF Core.
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(AssemblyReference.Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();

        // Read side: Dapper.
        services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddScoped<ITeamReadStore, TeamReadStore>();
        services.AddScoped<IPlayerReadStore, PlayerReadStore>();
        services.AddScoped<IMatchReadStore, MatchReadStore>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<IClock, SystemClock>();

        // Exposed through /health so orchestrators wait for the database before routing traffic.
        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

        return services;
    }
}
