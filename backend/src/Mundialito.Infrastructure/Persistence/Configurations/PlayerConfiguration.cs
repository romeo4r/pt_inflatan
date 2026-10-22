using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");
        builder.ConfigureAuditable();

        builder.Property(p => p.FirstName)
            .HasMaxLength(Player.NameMaxLength)
            .IsRequired();

        builder.Property(p => p.LastName)
            .HasMaxLength(Player.NameMaxLength)
            .IsRequired();

        builder.Ignore(p => p.FullName);

        builder.Property(p => p.JerseyNumber)
            .IsRequired();

        builder.Property(p => p.Position)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.BirthDate);

        builder.Property(p => p.IsActive)
            .IsRequired();

        // A roster belongs to its team: deleting the team removes its players.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TeamId, p.JerseyNumber }).IsUnique();
    }
}
