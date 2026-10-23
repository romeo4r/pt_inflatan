using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Common;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal static class EntityConfigurationExtensions
{
    /// <summary>Key and non-persisted members shared by every entity.</summary>
    public static EntityTypeBuilder<TEntity> ConfigureEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Ignore(e => e.DomainEvents);

        return builder;
    }

    /// <summary>Audit columns and the optimistic concurrency token for auditable entities.</summary>
    public static EntityTypeBuilder<TEntity> ConfigureAuditable<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.ConfigureEntity();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.UpdatedAtUtc);

        // SQL Server rowversion: generated on insert and on every update, compared in the WHERE
        // clause of UPDATE/DELETE so a stale write affects 0 rows and raises a concurrency exception.
        builder.Property(e => e.RowVersion).IsRowVersion();

        return builder;
    }
}
