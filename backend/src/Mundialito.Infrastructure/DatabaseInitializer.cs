using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Seeding;

namespace Mundialito.Infrastructure;

/// <summary>What the host wants done at startup. Both default to the safest local behaviour.</summary>
public sealed record DatabaseInitializationOptions(bool ApplyMigrations = false, bool Seed = true);

/// <summary>
/// Startup hook: waits for the database server (containers start out of order), optionally
/// applies pending migrations, then seeds when empty. Called once by the host before serving traffic.
/// </summary>
public static partial class DatabaseInitializer
{
    private const int MaxConnectionAttempts = 15;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        DatabaseInitializationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is { ApplyMigrations: false, Seed: false })
        {
            // Nothing to do: do not even touch the database (integration tests run without one).
            return;
        }

        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<ApplicationDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var databaseExists = await WaitForServerAsync(context, logger, cancellationToken);

        if (options.ApplyMigrations)
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                LogApplyingMigrations(logger, pending.Count);
                await context.Database.MigrateAsync(cancellationToken);
            }
        }
        else if (!databaseExists)
        {
            throw new InvalidOperationException(
                "The database does not exist. Apply the migrations (dotnet ef database update) or enable Database:ApplyMigrationsOnStartup.");
        }

        if (options.Seed)
        {
            var seeder = provider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Retries until the server answers. Asking whether the database exists (a query against the
    /// server, not the database) separates "server not up yet" from "database not created yet",
    /// which is the normal state of a fresh container before the first migration runs.
    /// </summary>
    private static async Task<bool> WaitForServerAsync(ApplicationDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await creator.ExistsAsync(cancellationToken);
            }
            catch (Exception exception) when (attempt < MaxConnectionAttempts)
            {
                LogWaiting(logger, attempt, MaxConnectionAttempts, exception.GetType().Name);
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database server not reachable (attempt {Attempt}/{MaxAttempts}: {Reason}); retrying")]
    private static partial void LogWaiting(ILogger logger, int attempt, int maxAttempts, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migration(s)")]
    private static partial void LogApplyingMigrations(ILogger logger, int count);
}
