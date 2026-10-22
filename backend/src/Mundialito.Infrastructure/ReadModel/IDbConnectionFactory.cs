using System.Data.Common;

namespace Mundialito.Infrastructure.ReadModel;

/// <summary>Opens connections for the Dapper read side. Each query owns and disposes its connection.</summary>
internal interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
