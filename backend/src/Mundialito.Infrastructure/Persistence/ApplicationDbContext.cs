using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mundialito.Domain.Common;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence.DomainEvents;
using Mundialito.Infrastructure.Persistence.Idempotency;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the write side. Only repositories and the Unit of Work use it;
/// queries go through Dapper. SaveChanges is intentionally not exposed outside the Unit of Work.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<IdempotencyKeyRecord> IdempotencyKeys => Set<IdempotencyKeyRecord>();

    public DbSet<DomainEventLogRecord> DomainEventLogs => Set<DomainEventLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);

        if (Database.ProviderName == SqliteProviderName)
        {
            ConfigureSqliteConcurrencyTokens(modelBuilder);
        }
    }

    /// <summary>
    /// SQLite (used by the persistence tests) has no rowversion. The column stays a plain,
    /// nullable concurrency token so the same model and the same Unit of Work code run there.
    /// </summary>
    private static void ConfigureSqliteConcurrencyTokens(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(AuditableEntity.RowVersion));
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.IsNullable = true;
            rowVersion.IsConcurrencyToken = true;
        }
    }
}
