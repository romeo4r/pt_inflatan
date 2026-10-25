using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Infrastructure.Persistence.DomainEvents;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class DomainEventLogConfiguration : IEntityTypeConfiguration<DomainEventLogRecord>
{
    public void Configure(EntityTypeBuilder<DomainEventLogRecord> builder)
    {
        builder.ToTable("DomainEventLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EventType).HasMaxLength(DomainEventLogRecord.EventTypeMaxLength).IsRequired();
        builder.Property(e => e.AggregateId).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(DomainEventLogRecord.IdentifierMaxLength);
        builder.Property(e => e.TraceId).HasMaxLength(DomainEventLogRecord.IdentifierMaxLength);
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.RecordedAtUtc).IsRequired();

        // The audit endpoint filters by these and sorts by time.
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => e.AggregateId);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.CorrelationId);
    }
}
