using FluentValidation;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Application.Players.Queries.GetPlayers;
using Mundialito.Application.Teams.Queries.GetTeams;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;

namespace Mundialito.UnitTests.Application.Queries;

public class QueryValidatorTests
{
    private static IValidator<T> ValidatorFor<T>() =>
        (IValidator<T>)Activator.CreateInstance(
            global::Mundialito.Application.AssemblyReference.Assembly.GetTypes()
                .Single(t => typeof(IValidator<T>).IsAssignableFrom(t) && !t.IsAbstract))!;

    [Fact]
    public void GetTeams_WithValidSortKeys_ShouldPass()
    {
        var validator = ValidatorFor<GetTeamsQuery>();

        foreach (var field in GetTeamsQuery.SortableFields)
        {
            validator.Validate(new GetTeamsQuery { SortBy = field }).IsValid.ShouldBeTrue(field);
        }
    }

    [Fact]
    public void GetTeams_WithSearchTooLong_ShouldFail()
    {
        var result = ValidatorFor<GetTeamsQuery>().Validate(new GetTeamsQuery { Search = new string('a', 101) });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(GetTeamsQuery.Search));
    }

    [Fact]
    public void GetPlayers_WithUndefinedPosition_ShouldFail()
    {
        var result = ValidatorFor<GetPlayersQuery>().Validate(new GetPlayersQuery { Position = (PlayerPosition)42 });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GetPlayers_WithValidFilters_ShouldPass()
    {
        var query = new GetPlayersQuery { TeamId = Guid.CreateVersion7(), Position = PlayerPosition.Forward, SortBy = "teamName" };

        ValidatorFor<GetPlayersQuery>().Validate(query).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void GetMatches_WithInvertedDateRange_ShouldFail()
    {
        var from = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetMatchesQuery { FromUtc = from, ToUtc = from.AddDays(-1) };

        var result = ValidatorFor<GetMatchesQuery>().Validate(query);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe(nameof(GetMatchesQuery.ToUtc));
    }

    [Fact]
    public void GetMatches_WithValidFilters_ShouldPass()
    {
        var from = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetMatchesQuery
        {
            FromUtc = from,
            ToUtc = from.AddMonths(1),
            Status = MatchStatus.Scheduled,
            MatchDay = 3,
            SortBy = "homeTeam",
        };

        ValidatorFor<GetMatchesQuery>().Validate(query).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void GetMatches_WithInvalidMatchDayOrStatus_ShouldFail(int value)
    {
        var validator = ValidatorFor<GetMatchesQuery>();

        validator.Validate(new GetMatchesQuery { MatchDay = value }).IsValid.ShouldBe(value == 99);
        validator.Validate(new GetMatchesQuery { Status = (MatchStatus)value }).IsValid.ShouldBeFalse();
    }
}
