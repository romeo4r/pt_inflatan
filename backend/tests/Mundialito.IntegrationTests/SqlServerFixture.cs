using Testcontainers.MsSql;

namespace Mundialito.IntegrationTests;

/// <summary>
/// One disposable SQL Server 2022 container for the whole test run (shared by the collection).
/// The API boots against it with migrations and seed enabled, exactly as in Docker Compose.
/// Requires Docker; the image is the same one used by the compose file.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public IntegrationApiFactory Factory { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new IntegrationApiFactory(_container.GetConnectionString());
        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
