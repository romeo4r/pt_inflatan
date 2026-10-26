using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Seeding;

namespace Mundialito.Infrastructure;

/// <summary>What the host wants done at startup. Both default to the safest local behaviour.</summary>
public sealed record DatabaseInitializationOptions(bool ApplyMigrations = false, bool Seed = true);

/// <summary>
/// Startup hook: waits for the database (containers start out of order), optionally applies
/// pending migrations, then seeds when empty. Called once by the host before serving traffic.
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

        await WaitForDatabaseAsync(context, logger, cancellationToken);

        if (options.ApplyMigrations)
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                LogApplyingMigrations(logger, pending.Count);
                await context.Database.MigrateAsync(cancellationToken);
            }
        }

        if (options.Seed)
        {
            var seeder = provider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
    }

    private static async Task WaitForDatabaseAsync(ApplicationDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (await context.Database.CanConnectAsync(cancellationToken))
                {
                    return;
                }
            }
            catch (Exception exception) when (attempt < MaxConnectionAttempts)
            {
                LogWaiting(logger, attempt, MaxConnectionAttempts, exception.GetType().Name);
            }

            if (attempt >= MaxConnectionAttempts)
            {
                throw new InvalidOperationException($"Database was not reachable after {MaxConnectionAttempts} attempts.");
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not reachable (attempt {Attempt}/{MaxAttempts}: {Reason}); retrying")]
    private static partial void LogWaiting(ILogger logger, int attempt, int maxAttempts, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} pending migration(s)")]
    private static partial void LogApplyingMigrations(ILogger logger, int count);
}
