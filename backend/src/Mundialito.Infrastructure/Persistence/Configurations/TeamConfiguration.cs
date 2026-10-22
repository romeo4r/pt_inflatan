using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.ConfigureAuditable();

        builder.Property(t => t.Name)
            .HasMaxLength(Team.NameMaxLength)
            .IsRequired();

        builder.Property(t => t.Code)
            .HasMaxLength(Team.CodeMaxLength)
            .IsRequired();

        builder.Property(t => t.Coach)
            .HasMaxLength(Team.CoachMaxLength);

        builder.Property(t => t.IsActive)
            .IsRequired();

        // Uniqueness is enforced in the database as the last line of defense; the application
        // checks first so it can answer 409 Conflict with a proper error instead of an exception.
        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => t.Code).IsUnique();
    }
}
