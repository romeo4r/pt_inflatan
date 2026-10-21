using System.Reflection;

namespace Mundialito.UnitTests.Architecture;

/// <summary>
/// Guards the Clean Architecture dependency rule:
/// Domain -> nothing, Application -> Domain, Infrastructure -> Application, Api -> Application + Infrastructure.
/// Note: reflection only lists assemblies that are actually used by compiled code, so the checks are
/// expressed as "must not reference" rather than "must reference".
/// </summary>
public class LayerDependencyTests
{
    private const string DomainName = "Mundialito.Domain";
    private const string ApplicationName = "Mundialito.Application";
    private const string InfrastructureName = "Mundialito.Infrastructure";
    private const string ApiName = "Mundialito.Api";

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        var references = GetReferencedProjectNames(Domain.AssemblyReference.Assembly);

        references.ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        var references = GetReferencedProjectNames(Application.AssemblyReference.Assembly);

        references.ShouldAllBe(name => name == DomainName);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        var references = GetReferencedProjectNames(Infrastructure.AssemblyReference.Assembly);

        references.ShouldNotContain(ApiName);
    }

    [Fact]
    public void Api_ShouldNotBeReferencedByAnyLayer()
    {
        var layers = new[]
        {
            Domain.AssemblyReference.Assembly,
            Application.AssemblyReference.Assembly,
            Infrastructure.AssemblyReference.Assembly,
        };

        foreach (var layer in layers)
        {
            GetReferencedProjectNames(layer).ShouldNotContain(ApiName);
        }
    }

    private static string[] GetReferencedProjectNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith("Mundialito.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
