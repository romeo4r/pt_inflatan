using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Infrastructure.Persistence.Idempotency;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyRecord> builder)
    {
        builder.ToTable("IdempotencyKeys");

        // The primary key is what makes the reservation atomic across concurrent requests.
        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key).HasMaxLength(IdempotencyKeyRecord.KeyMaxLength);

        builder.Property(k => k.RequestHash).HasMaxLength(IdempotencyKeyRecord.HashMaxLength).IsRequired();
        builder.Property(k => k.Method).HasMaxLength(IdempotencyKeyRecord.MethodMaxLength).IsRequired();
        builder.Property(k => k.Path).HasMaxLength(IdempotencyKeyRecord.PathMaxLength).IsRequired();
        builder.Property(k => k.ResponseStatusCode);
        builder.Property(k => k.ResponseContentType).HasMaxLength(IdempotencyKeyRecord.ContentTypeMaxLength);
        builder.Property(k => k.ResponseBody);
        builder.Property(k => k.ResponseLocation).HasMaxLength(IdempotencyKeyRecord.PathMaxLength);
        builder.Property(k => k.CreatedAtUtc).IsRequired();
        builder.Property(k => k.ExpiresAtUtc).IsRequired();

        builder.Ignore(k => k.IsCompleted);

        builder.HasIndex(k => k.ExpiresAtUtc);
    }
}
