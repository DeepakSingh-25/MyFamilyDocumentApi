using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Documents.Infrastructure.Data
{
    internal static class DbErrors
    {
        /// <summary>2601/2627 (SQL Server) or 23505 (PostgreSQL unique_violation).</summary>
        public static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 }
            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
