using System.Reflection;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;

namespace Mundialito.UnitTests.Architecture;

/// <summary>
/// Enforces the CQRS rule from the specification: queries are served by Dapper read stores and
/// never touch EF Core, repositories or the Unit of Work. Checked by inspecting handler
/// constructor dependencies, so a violation fails the build's test run.
/// </summary>
public class CqrsSeparationTests
{
    private static readonly Type[] WriteSideDependencies =
    [
        typeof(IUnitOfWork),
        typeof(ITeamRepository),
        typeof(IPlayerRepository),
        typeof(IMatchRepository),
    ];

    [Fact]
    public void QueryHandlers_ShouldNotDependOnWriteSide()
    {
        var queryHandlers = global::Mundialito.Application.AssemblyReference.Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsQueryHandler))
            .ToArray();

        queryHandlers.ShouldNotBeEmpty();

        var violations = queryHandlers
            .SelectMany(handler => handler
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType)
                .Where(IsWriteSideDependency)
                .Select(type => $"{handler.Name} depends on {type.Name}"))
            .ToArray();

        violations.ShouldBeEmpty("query handlers must read through a read store only");
    }

    private static bool IsWriteSideDependency(Type type) =>
        WriteSideDependencies.Contains(type)
        || (type.Namespace is not null && type.Namespace.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

    private static bool IsQueryHandler(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryHandler<,>);
}
