using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Repositories
{
    public class UserRepository(AppDbContext db) : IUserRepository
    {
        public Task<bool> IsSetupClosedAsync(CancellationToken ct) =>
            db.SetupState.AnyAsync(ct);

        public async Task<bool> CreateFirstUserAsync(UserEntity user, DateTime closedOn, CancellationToken ct)
        {
            if (await IsSetupClosedAsync(ct)) return false;

            db.Users.Add(user);
            db.SetupState.Add(new SetupStateEntity { ClosedOn = closedOn, ClosedByUser = user });
            try
            {
                // Ek SaveChanges = ek transaction: user aur SetupState saath mein, ya dono nahi
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Doosri request ne SetupState row pehle daal di (ya email pehle se hai)
                db.ChangeTracker.Clear();
                return false;
            }
        }

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
            db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

        public Task<UserEntity?> GetByEmailAsync(string normalizedEmail, CancellationToken ct) =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        public Task<UserEntity?> GetByIdAsync(Guid userId, CancellationToken ct) =>
            db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        public async Task<(int? PasswordVersion, bool Revoked)> GetSessionStateAsync(Guid userId, string jti, CancellationToken ct)
        {
            var state = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.PasswordVersion, Revoked = db.RevokedTokens.Any(t => t.Jti == jti && t.UserId == u.Id) })
                .FirstOrDefaultAsync(ct);
            return state is null ? (null, false) : (state.PasswordVersion, state.Revoked);
        }

        public async Task RevokeTokenAsync(Guid userId, string jti, DateTime expiresOn, DateTime now, CancellationToken ct)
        {
            if (!await db.RevokedTokens.AnyAsync(t => t.Jti == jti, ct))
            {
                db.RevokedTokens.Add(new RevokedTokenEntity { Jti = jti, UserId = userId, ExpiresOn = expiresOn });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
                {
                    // Isi token ka doosra logout saath-saath aa gaya — already revoked, theek hai
                    db.ChangeTracker.Clear();
                }
            }

            // Cleanup: expire ho chuke tokens ki rows bekaar hain. Ye fail ho (jaise deadlock) to bhi logout ho chuka hai.
            try
            {
                await db.RevokedTokens.Where(t => t.ExpiresOn < now).ExecuteDeleteAsync(ct);
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                // Agle logout par phir koshish hogi
            }
        }

        public Task UpdatePasswordAsync(Guid userId, string passwordHash, DateTime now, CancellationToken ct) =>
            UpdatePasswordIfVersionAsync(userId, null, passwordHash, now, ct);

        public async Task<bool> UpdatePasswordIfVersionAsync(Guid userId, int? expectedVersion, string passwordHash, DateTime now, CancellationToken ct) =>
            await db.Users
                .Where(u => u.Id == userId && (expectedVersion == null || u.PasswordVersion == expectedVersion))
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PasswordHash, passwordHash)
                .SetProperty(u => u.PasswordVersion, u => u.PasswordVersion + 1)
                .SetProperty(u => u.PasswordChangedOn, now), ct) > 0;

        public async Task<bool> UpdateFullNameAsync(Guid userId, string fullName, CancellationToken ct) =>
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, fullName), ct) > 0;

        public async Task<bool> UpdatePhotoIfCurrentAsync(Guid userId, string? expectedKey, string? photoKey, string? photoType, CancellationToken ct) =>
            // EF nullable parameter ke liye "= @key OR (both NULL)" banata hai
            await db.Users.Where(u => u.Id == userId && u.PhotoKey == expectedKey)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PhotoKey, photoKey)
                    .SetProperty(u => u.PhotoType, photoType), ct) > 0;

        public async Task AddResetTokenAsync(Guid userId, string tokenHash, DateTime expiresOn, DateTime now, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Purane unused links band
            await db.PasswordResetTokens
                .Where(t => t.UserId == userId && t.UsedOn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedOn, now), ct);
            db.PasswordResetTokens.Add(new PasswordResetTokenEntity { UserId = userId, TokenHash = tokenHash, ExpiresOn = expiresOn });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task<bool> ResetPasswordWithTokenAsync(string tokenHash, string passwordHash, DateTime now, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var userId = await db.PasswordResetTokens
                .Where(t => t.TokenHash == tokenHash && t.UsedOn == null && t.ExpiresOn > now)
                .Select(t => (Guid?)t.UserId)
                .FirstOrDefaultAsync(ct);
            if (userId is null) return false;

            // Conditional update — do saath-saath requests mein sirf ek hi token use kar paati hai
            var consumed = await db.PasswordResetTokens
                .Where(t => t.TokenHash == tokenHash && t.UsedOn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedOn, now), ct);
            if (consumed == 0) return false;

            await UpdatePasswordAsync(userId.Value, passwordHash, now, ct);
            await tx.CommitAsync(ct);
            return true;
        }
    }
}
