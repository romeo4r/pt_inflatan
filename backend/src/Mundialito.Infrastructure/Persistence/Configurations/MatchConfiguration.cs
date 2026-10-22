using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches", table =>
            table.HasCheckConstraint("CK_Matches_DifferentTeams", "[HomeTeamId] <> [AwayTeamId]"));

        builder.ConfigureAuditable();

        builder.Property(m => m.MatchDay).IsRequired();
        builder.Property(m => m.ScheduledAtUtc).IsRequired();
        builder.Property(m => m.Venue).HasMaxLength(Match.VenueMaxLength);
        builder.Property(m => m.Status).HasConversion<int>().IsRequired();
        builder.Property(m => m.HomeScore);
        builder.Property(m => m.AwayScore);
        builder.Property(m => m.FinishedAtUtc);

        // A team with fixtures cannot be deleted; the application answers 409 before reaching here.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Goals are children of the aggregate, persisted through the private backing field.
        builder.HasMany(m => m.Goals)
            .WithOne()
            .HasForeignKey(g => g.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Goals)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        builder.HasIndex(m => m.ScheduledAtUtc);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.HomeTeamId);
        builder.HasIndex(m => m.AwayTeamId);
    }
}
