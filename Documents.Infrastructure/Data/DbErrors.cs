using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Data
{
    internal static class DbErrors
    {
        /// <summary>2601 = unique index, 2627 = primary key / unique constraint.</summary>
        public static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
