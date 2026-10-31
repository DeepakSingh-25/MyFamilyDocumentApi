using System.Net.Http.Headers;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Documents.Api.Tests
{
    /// <summary>
    /// Real API pipeline + real SQL Server, lekin ALAG database <c>Documents_Tests</c> (spec 007 §B.6).
    /// Server/login user-secrets ke DefaultConnection se aata hai; sirf database ka naam badalta hai.
    /// Asli <c>Documents</c> database ko ye kabhi touch nahi karta (InitializeAsync guard).
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string TestDatabase = "Documents_Tests";

        private readonly string connectionString = BuildConnectionString();

        /// <summary>Test files yahan; har run ke baad delete.</summary>
        public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "family-docs-tests", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Storage:RootPath"] = StorageRoot,
            }));
        }

        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(StorageRoot);
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Guard: galti se bhi asli database par migrate / delete na ho
            var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            if (actual != TestDatabase)
                throw new InvalidOperationException($"Tests must run on '{TestDatabase}', not '{actual}'.");

            await db.Database.MigrateAsync();
            // Pichhle run ka data saaf (system categories / relationships reference data hain, rehne do; users ke apne hatao)
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Documents; DELETE FROM FamilyMembers; DELETE FROM DocumentCategories WHERE UserId IS NOT NULL; " +
                "DELETE FROM Relationships WHERE UserId IS NOT NULL; DELETE FROM RevokedTokens; DELETE FROM PasswordResetTokens; DELETE FROM Users;");
        }

        public new async Task DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
        }

        /// <summary>Naya user (sign-up one-time hai, isliye seedha DB mein) + uske token wala client.</summary>
        public async Task<HttpClient> CreateUserClientAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new UserEntity
            {
                FullName = "Test User",
                Email = $"test-{Guid.NewGuid():N}@example.test",
                PasswordHash = "not-used",
                CreatedOn = DateTime.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateToken(user).Token;
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>Raw SQL — restore runbook (docs/migrations/004) jaisa, kyunki app mein restore nahi hai.</summary>
        public async Task<int> ExecuteSqlAsync(FormattableString sql)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Database.ExecuteSqlInterpolatedAsync(sql);
        }

        /// <summary>Raw SQL (query filter ke bina): member row ka DeletedOn. Row na ho to Exists = false.</summary>
        public async Task<(bool Exists, DateTime? DeletedOn)> GetMemberRowAsync(Guid id)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.Database
                .SqlQuery<DateTime?>($"SELECT DeletedOn AS Value FROM FamilyMembers WHERE Id = {id}")
                .ToListAsync();
            return (rows.Count == 1, rows.FirstOrDefault());
        }

        /// <summary>Raw SQL (query filter ke bina): member ke document rows.</summary>
        public async Task<int> CountDocumentRowsAsync(Guid memberId)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Documents WHERE MemberId = {memberId}")
                .SingleAsync();
        }

        /// <summary>Raw SQL (query filter ke bina): document row ka DeletedOn aur StorageKey. Row na ho to Exists = false.</summary>
        public async Task<(bool Exists, DateTime? DeletedOn, string? StorageKey)> GetDocumentRowAsync(Guid id)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.Database
                .SqlQuery<DateTime?>($"SELECT DeletedOn AS Value FROM Documents WHERE Id = {id}")
                .ToListAsync();
            var keys = await db.Database
                .SqlQuery<string>($"SELECT StorageKey AS Value FROM Documents WHERE Id = {id}")
                .ToListAsync();
            return (deleted.Count == 1, deleted.FirstOrDefault(), keys.FirstOrDefault());
        }

        /// <summary>StorageKey wali file disk par hai ya nahi.</summary>
        public bool StoredFileExists(string storageKey) => File.Exists(Path.Combine(StorageRoot, storageKey));

        public int CountStoredFiles() =>
            Directory.Exists(StorageRoot) ? Directory.EnumerateFiles(StorageRoot, "*", SearchOption.AllDirectories).Count() : 0;

        private static string BuildConnectionString()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .Build();
            var baseConnection = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing (user-secrets of Documents.Api).");
            return new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = TestDatabase }.ConnectionString;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class ApiCollection : ICollectionFixture<ApiFactory>
    {
        public const string Name = "api";
    }
}
