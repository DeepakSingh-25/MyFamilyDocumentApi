using Documents.Core.Entities;
using Documents.Core.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Documents.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
    {
        public void Configure(EntityTypeBuilder<UserEntity> builder)
        {
            builder.ToTable("Users");
            builder.Property(u => u.FullName).HasMaxLength(DomainRules.FullNameMax).IsRequired();
            builder.Property(u => u.Email).HasMaxLength(DomainRules.EmailMax).IsRequired();
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            builder.Property(u => u.PhotoKey).HasMaxLength(260);
            builder.Property(u => u.PhotoType).HasMaxLength(10).IsUnicode(false);
        }
    }

    public class SetupStateConfiguration : IEntityTypeConfiguration<SetupStateEntity>
    {
        public void Configure(EntityTypeBuilder<SetupStateEntity> builder)
        {
            builder.ToTable("SetupState", t => t.HasCheckConstraint("CK_SetupState_Singleton", "[Id] = 1"));
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.HasOne(s => s.ClosedByUser).WithMany().HasForeignKey(s => s.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RevokedTokenConfiguration : IEntityTypeConfiguration<RevokedTokenEntity>
    {
        public void Configure(EntityTypeBuilder<RevokedTokenEntity> builder)
        {
            builder.ToTable("RevokedTokens");
            builder.HasKey(t => t.Jti);
            builder.Property(t => t.Jti).HasMaxLength(32).IsFixedLength().IsUnicode(false);
            builder.HasIndex(t => t.ExpiresOn);
            builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetTokenEntity>
    {
        public void Configure(EntityTypeBuilder<PasswordResetTokenEntity> builder)
        {
            builder.ToTable("PasswordResetTokens");
            builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
            builder.HasIndex(t => t.TokenHash);
            builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
