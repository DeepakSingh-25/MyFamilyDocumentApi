using Documents.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Documents.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<UserEntity> Users => Set<UserEntity>();
        public DbSet<SetupStateEntity> SetupState => Set<SetupStateEntity>();
        public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();
        public DbSet<RevokedTokenEntity> RevokedTokens => Set<RevokedTokenEntity>();
        public DbSet<FamilyMemberEntity> FamilyMembers => Set<FamilyMemberEntity>();
        public DbSet<RelationshipEntity> Relationships => Set<RelationshipEntity>();
        public DbSet<DocumentCategoryEntity> DocumentCategories => Set<DocumentCategoryEntity>();
        public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Sab DateTime UTC mein save hote hain; padhte waqt Kind=Utc lagao taaki JSON mein "Z" aaye.
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
