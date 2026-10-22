using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mundialito.Infrastructure.Persistence;

namespace Mundialito.UnitTests.Infrastructure;

/// <summary>
/// Creates a real relational database in memory (SQLite) so Unit of Work tests exercise actual
/// transactions, constraints and SaveChanges instead of the non-relational in-memory provider.
/// The connection must stay open for the lifetime of the database.
/// </summary>
internal sealed class SqliteContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public SqliteContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
