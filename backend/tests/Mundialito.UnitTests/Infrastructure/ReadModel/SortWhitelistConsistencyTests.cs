using System.Reflection;
using Mundialito.Application.DomainEvents.Queries.GetDomainEvents;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Application.Players.Queries.GetPlayers;
using Mundialito.Application.Teams.Queries.GetTeams;
using Mundialito.Infrastructure.ReadModel;

namespace Mundialito.UnitTests.Infrastructure.ReadModel;

/// <summary>
/// The validator (Application) and the column map (Infrastructure) are two halves of one
/// whitelist. This test fails if someone adds a sort key to one side and forgets the other.
/// </summary>
public class SortWhitelistConsistencyTests
{
    public static TheoryData<Type, string[]> Cases => new()
    {
        { typeof(TeamReadStore), GetTeamsQuery.SortableFields },
        { typeof(PlayerReadStore), GetPlayersQuery.SortableFields },
        { typeof(MatchReadStore), GetMatchesQuery.SortableFields },
        { typeof(DomainEventReadStore), GetDomainEventsQuery.SortableFields },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EverySortableFieldShouldHaveAColumn(Type readStoreType, string[] sortableFields)
    {
        var map = (IReadOnlyDictionary<string, string>)readStoreType
            .GetField("SortColumns", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        map.Keys.ShouldBe(sortableFields, ignoreOrder: true);
    }
}
