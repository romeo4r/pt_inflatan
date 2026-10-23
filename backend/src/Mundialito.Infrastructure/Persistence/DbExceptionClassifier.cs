using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// Recognizes provider-specific error codes so the Unit of Work can turn them into business
/// results. SQL Server is the production provider; SQLite codes are recognized because the
/// persistence tests run on it.
/// </summary>
internal static class DbExceptionClassifier
{
    private const int SqlServerUniqueIndexViolation = 2601;
    private const int SqlServerUniqueConstraintViolation = 2627;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is DbException inner && IsUniqueViolation(inner);

    public static bool IsUniqueViolation(DbException exception) => exception switch
    {
        SqlException sql => sql.Number is SqlServerUniqueIndexViolation or SqlServerUniqueConstraintViolation,
        _ when exception.GetType().Name == "SqliteException" =>
            ExtendedErrorCode(exception) is SqliteConstraintPrimaryKey or SqliteConstraintUnique,
        _ => false,
    };

    // Read by reflection to avoid referencing the SQLite provider from production code.
    private static int? ExtendedErrorCode(DbException exception) =>
        exception.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(exception) as int?;
}
